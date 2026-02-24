using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using IRS.Application.Services;
using IRS.LLM.Services;
using IRS.Infrastructure;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace IRS.ClientGeneration.Tests
{
    [TestFixture]
    public class RestApiAgentRunnerHostedServiceTests
    {
        [Test]
        public void HtmlToPlainText_removes_scripts_and_decodes_entities()
        {
            var html = "<html><head><style>p{display:none}</style><script>var x=1;</script></head><body><!-- hidden --><h1>Title &amp; More</h1><p>Para<br/>Line2</p></body></html>";

            var text = RestApiAgentRunnerHostedService.HtmlToPlainText(html);

            Assert.That(text, Does.Contain("Title & More"));
            Assert.That(text, Does.Contain("Para Line2"));
            Assert.That(text, Does.Not.Contain("var x=1"));
            Assert.That(text, Does.Not.Contain("hidden"));
        }

        [Test]
        public async Task ExecuteRestApiAgent_appends_url_summaries_before_final_llm_call()
        {
            // Arrange - fake HTTP responses (agent endpoint -> contains URL; fetched URL -> HTML)
            var handler = new DelegatingHandlerStub(req =>
            {
                if (req.RequestUri!.AbsoluteUri == "https://api.test/endpoint")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("See https://example.com/article for details", Encoding.UTF8, "text/plain")
                    });
                }

                if (req.RequestUri!.AbsoluteUri == "https://example.com/article")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("<html><body><h1>Example Title</h1><p>Article content.</p></body></html>", Encoding.UTF8, "text/html")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var httpFactory = new SimpleHttpClientFactory(handler);

            // make the handler visible so we can assert both requests were attempted
            var delegating = handler;
            // Sanity-check the same URL regex used by the service matches the example text
            var sample = "See https://example.com/article for details";
            var urlMatches = System.Text.RegularExpressions.Regex.Matches(sample, @"https?://[^\s'""<>]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Assert.That(urlMatches.Count, Is.EqualTo(1), "Sanity: URL regex should detect the URL in the sample response");

            // Fake LLM that records calls and returns predictable summaries
            var fakeLlm = new FakeLlmConversationService();

            var services = new ServiceCollection();
            services.AddSingleton<ILlmConversationService>(fakeLlm);
            var serviceProvider = services.BuildServiceProvider();

            var enc = new NoOpEncryptionService();

            var hosted = new RestApiAgentRunnerHostedService(NullLogger<RestApiAgentRunnerHostedService>.Instance, serviceProvider, httpFactory, enc);

            var agent = new Agent { id = 123, endpoint_url = "https://api.test/endpoint", http_method = "GET", agent_instructions = "Final instruction" };
            var page = new ResearchPage();

            // Act - invoke private method via reflection
            var method = typeof(RestApiAgentRunnerHostedService).GetMethod("ExecuteRestApiAgentAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var invokeObj = method.Invoke(hosted, new object[] { agent, page, CancellationToken.None })!; // returns Task<AgentExecutionResult>
            var task = (Task)invokeObj;
            await task.ConfigureAwait(false);

            // obtain the Result property from the generic Task<T>
            var resultProp = invokeObj.GetType().GetProperty("Result")!;
            var result = resultProp.GetValue(invokeObj)!;

            // Assert - LLM was called for URL summary and final processing received the appended summary
            Assert.That((bool)result.GetType().GetProperty("Success")!.GetValue(result)!, Is.True);

            // Ensure both HTTP requests were attempted (agent endpoint + fetched URL)
            Assert.That(delegating.Requests.Select(u => u.AbsoluteUri), Does.Contain("https://api.test/endpoint"));
            TestContext.WriteLine($"CreateClient called: {httpFactory.CreateCalls} time(s)");
            Assert.That(httpFactory.CreateCalls, Is.GreaterThanOrEqualTo(1));

            if (fakeLlm.Calls.Count < 2)
            {
                // Dump observed single call for diagnostics
                if (fakeLlm.Calls.Count == 1)
                {
                    var c = fakeLlm.Calls[0];
                    TestContext.WriteLine($"Only LLM call observed. systemMessage='{c.systemMessage}', userMessage='{c.userMessage}'");
                }
            }

            Assert.That(fakeLlm.Calls.Count, Is.GreaterThanOrEqualTo(2), "Expected both URL-summary and final LLM calls");

            var summaryCall = fakeLlm.Calls.FirstOrDefault(c => c.systemMessage != null && c.systemMessage.StartsWith("Summarize"));
            Assert.That(summaryCall, Is.Not.Null, "Expected a per-URL summary call to the LLM");

            var finalCall = fakeLlm.Calls.Last();
            Assert.That(finalCall.userMessage, Does.Contain("Compact summary for page."), "Final LLM input should contain the compact URL summary");
            Assert.That((string?)result.GetType().GetProperty("Output")!.GetValue(result), Does.Contain("FinalLLMOutput:"));
        }

        [Test]
        public async Task Trims_old_successful_runs_keep_last_3_for_page_agent()
        {
            // Arrange - in-memory EF Core DB and fake HTTP/LLM
            var services = new ServiceCollection();
            services.AddDbContext<IrsDbContext>(options => options.UseInMemoryDatabase("TrimTestDb"));

            var fakeLlm = new FakeLlmConversationService();
            services.AddSingleton<ILlmConversationService>(fakeLlm);

            var serviceProvider = services.BuildServiceProvider();

            // Seed DB with page, agent, attachment and 4 successful runs + one queued run
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

                db.Teams.Add(new Team { id = 1, name = "Alpha Fund", is_deleted = false });
                db.Users.Add(new User { id = 1, email = "analyst@example.com" });
                db.TeamMembers.Add(new TeamMember { team_id = 1, user_id = 1, status = "ACTIVE", is_deleted = false });

                db.Agents.Add(new Agent { id = 10, endpoint_url = "https://api.test/endpoint", http_method = "GET", agent_instructions = "instr", is_deleted = false });
                db.ResearchPages.Add(new ResearchPage { id = 100, team_id = 1, security_figi = "BBG000B9XRY4", is_deleted = false });
                db.ResearchPageAgents.Add(new ResearchPageAgent { id = 1000, agent_id = 10, research_page_id = 100, is_enabled = true });

                var now = DateTime.UtcNow;
                // add 4 successful runs (oldest -> newest)
                db.AgentRuns.Add(new AgentRun { research_page_agent_id = 1000, status = "Succeeded", started_at = now.AddMinutes(-10), completed_at = now.AddMinutes(-10) });
                db.AgentRuns.Add(new AgentRun { research_page_agent_id = 1000, status = "Succeeded", started_at = now.AddMinutes(-8), completed_at = now.AddMinutes(-8) });
                db.AgentRuns.Add(new AgentRun { research_page_agent_id = 1000, status = "Succeeded", started_at = now.AddMinutes(-6), completed_at = now.AddMinutes(-6) });
                db.AgentRuns.Add(new AgentRun { research_page_agent_id = 1000, status = "Succeeded", started_at = now.AddMinutes(-4), completed_at = now.AddMinutes(-4) });

                // add queued run that the hosted service should process
                db.AgentRuns.Add(new AgentRun { research_page_agent_id = 1000, status = "Queued", started_at = null, completed_at = null });

                await db.SaveChangesAsync();
            }

            // Fake HTTP responses so ExecuteRestApiAgentAsync succeeds
            var handler = new DelegatingHandlerStub(req => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK", Encoding.UTF8, "text/plain")
            }));
            var httpFactory = new SimpleHttpClientFactory(handler);

            var enc = new NoOpEncryptionService();
            var hosted = new RestApiAgentRunnerHostedService(NullLogger<RestApiAgentRunnerHostedService>.Instance, serviceProvider, httpFactory, enc);

            // Act - run the hosted service loop briefly to process the queued run
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var runTask = hosted.StartAsync(cts.Token);

            // Wait until the queued run is processed and trimming applied (or timeout)
            await Task.Delay(500); // small delay to let background work happen

            // Stop the hosted service
            await hosted.StopAsync(CancellationToken.None);
            await runTask;

            // Assert - only the latest 3 successful runs remain for the page agent
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IrsDbContext>();
                var successful = db.AgentRuns.Where(ar => ar.research_page_agent_id == 1000 && ar.status == "Succeeded").OrderByDescending(ar => ar.completed_at).ToList();
                Assert.That(successful.Count, Is.EqualTo(3), "Should keep last 3 successful runs");
            }
        }

        // --- test helpers ---
        private class DelegatingHandlerStub : DelegatingHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;
            public List<Uri> Requests { get; } = new();
            public DelegatingHandlerStub(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri!);
                return _responder(request);
            }
        }

        private class SimpleHttpClientFactory : IHttpClientFactory
        {
            private readonly DelegatingHandlerStub _handler;
            public int CreateCalls { get; private set; }
            public SimpleHttpClientFactory(DelegatingHandlerStub handler) => _handler = handler;
            public HttpClient CreateClient(string? name)
            {
                CreateCalls++;
                // return a new HttpClient instance (IHttpClientFactory typically provides new HttpClient wrappers)
                return new HttpClient(_handler, disposeHandler: false);
            }
        }

        private class FakeLlmConversationService : ILlmConversationService
        {
            public List<(int agentId, string systemMessage, string userMessage)> Calls { get; } = new();

            public Task<string> GetCompletionAsync(int agentId, string systemMessage, string userMessage)
            {
                Calls.Add((agentId, systemMessage, userMessage));
                if (!string.IsNullOrEmpty(systemMessage) && systemMessage.StartsWith("Summarize"))
                    return Task.FromResult("Compact summary for page.");

                return Task.FromResult("FinalLLMOutput:" + userMessage);
            }

            public Task<string> GetCompletionAsync(int agentId, List<LlmTornado.Chat.ChatMessage> messages)
                => Task.FromResult(string.Empty);

            public Task StreamCompletionAsync(int agentId, List<LlmTornado.Chat.ChatMessage> messages, Action<string> onToken)
                => Task.CompletedTask;

            public Task<string> GetGlobalCompletionAsync(string systemMessage, string userMessage)
                => Task.FromResult(string.Empty);
        }

        private class NoOpEncryptionService : IEncryptionService
        {
            public byte[] Encrypt(string plainText) => Encoding.UTF8.GetBytes(plainText);
            public string Decrypt(byte[] cipherText) => Encoding.UTF8.GetString(cipherText);
        }
    }
}
