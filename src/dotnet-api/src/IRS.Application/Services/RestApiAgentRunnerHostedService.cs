using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IRS.Infrastructure.Data;
using IRS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using IRS.LLM.Services;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;

namespace IRS.Application.Services;

public class RestApiAgentRunnerHostedService : BackgroundService
{
    private readonly ILogger<RestApiAgentRunnerHostedService> _logger;
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEncryptionService _encryptionService;

    public RestApiAgentRunnerHostedService(
        ILogger<RestApiAgentRunnerHostedService> logger,
        IServiceProvider services,
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _services = services;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RestApiAgentRunnerHostedService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

                // Find next queued run
                var queued = await db.AgentRuns
                    .Where(r => r.status == "Queued")
                    .OrderBy(r => r.id)
                    .Include(r => r.research_page_agent)
                        .ThenInclude(rpa => rpa!.agent)
                    .Include(r => r.research_page_agent)
                        .ThenInclude(rpa => rpa!.research_page)
                            .ThenInclude(p => p.security_figiNavigation)
                    .Include(r => r.section_agent)
                        .ThenInclude(sa => sa!.agent)
                    .Include(r => r.section_agent)
                        .ThenInclude(sa => sa!.section)
                            .ThenInclude(s => s.research_page)
                                .ThenInclude(p => p.security_figiNavigation)
                    .FirstOrDefaultAsync(stoppingToken);

                if (queued != null)
                {
                    _logger.LogInformation("Processing AgentRun {RunId}", queued.id);
                    queued.status = "Running";
                    queued.started_at = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);

                    // Determine if this is a page agent or section agent run
                    Agent agent;
                    ResearchPage page;

                    if (queued.research_page_agent != null)
                    {
                        // Page-level agent run
                        agent = queued.research_page_agent.agent;
                        page = queued.research_page_agent.research_page;
                        _logger.LogInformation("Processing page agent run for agent {AgentId} on page {PageId}", agent.id, page.id);
                    }
                    else if (queued.section_agent != null)
                    {
                        // Section-level agent run
                        agent = queued.section_agent.agent;
                        page = queued.section_agent.section.research_page;
                        _logger.LogInformation("Processing section agent run for agent {AgentId} on section {SectionId}", agent.id, queued.section_agent.section.id);
                    }
                    else
                    {
                        _logger.LogError("AgentRun {RunId} has neither research_page_agent nor section_agent", queued.id);
                        queued.status = "Failed";
                        queued.error = "Invalid agent run configuration";
                        queued.completed_at = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    try
                    {
                        var result = await ExecuteRestApiAgentAsync(agent, page, stoppingToken);

                        queued.output = result.Output;
                        queued.error = result.Error;
                        queued.completed_at = DateTime.UtcNow;
                        queued.status = result.Success ? "Succeeded" : "Failed";

                        // Update last run status on the appropriate agent attachment
                        if (queued.research_page_agent != null)
                        {
                            queued.research_page_agent.last_run_status = queued.status;
                            queued.research_page_agent.last_run_at = queued.completed_at;
                        }
                        else if (queued.section_agent != null)
                        {
                            queued.section_agent.last_run_status = queued.status;
                            queued.section_agent.last_run_at = queued.completed_at;
                        }

                        await db.SaveChangesAsync(stoppingToken);

                        // After a successful run, trim old successful runs for same agent (keep last 3 successful)
                        if (result.Success)
                        {
                            // Page-agent runs
                            if (queued.research_page_agent != null)
                            {
                                var pageAgentId = queued.research_page_agent.id;
                                var successfulRuns = await db.AgentRuns
                                    .Where(ar => ar.research_page_agent_id == pageAgentId && ar.status == "Succeeded")
                                    .OrderByDescending(ar => ar.completed_at)
                                    .ToListAsync(stoppingToken);

                                if (successfulRuns.Count > 3)
                                {
                                    var toDelete = successfulRuns.Skip(3).ToList();
                                    db.AgentRuns.RemoveRange(toDelete);
                                    await db.SaveChangesAsync(stoppingToken);
                                }
                            }

                            // Section-agent runs
                            if (queued.section_agent != null)
                            {
                                var sectionAgentId = queued.section_agent.id;
                                var successfulRuns = await db.AgentRuns
                                    .Where(ar => ar.section_agent_id == sectionAgentId && ar.status == "Succeeded")
                                    .OrderByDescending(ar => ar.completed_at)
                                    .ToListAsync(stoppingToken);

                                if (successfulRuns.Count > 3)
                                {
                                    var toDelete = successfulRuns.Skip(3).ToList();
                                    db.AgentRuns.RemoveRange(toDelete);
                                    await db.SaveChangesAsync(stoppingToken);
                                }
                            }
                        }

                        // Call the method to parse and update section scores
                        if (queued.section_agent != null && result.Success)
                        {
                            await ParseAndUpdateSectionScores(result.Output, queued.section_agent.section.id, db, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "REST API agent execution failed for run {RunId}", queued.id);
                        queued.status = "Failed";
                        queued.error = ex.Message + "\n\n" + ex.StackTrace;
                        queued.completed_at = DateTime.UtcNow;

                        // Update last run status on the appropriate agent attachment
                        if (queued.research_page_agent != null)
                        {
                            queued.research_page_agent.last_run_status = "Failed";
                            queued.research_page_agent.last_run_at = queued.completed_at;
                        }
                        else if (queued.section_agent != null)
                        {
                            queued.section_agent.last_run_status = "Failed";
                            queued.section_agent.last_run_at = queued.completed_at;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                else
                {
                    // No queued runs, wait before checking again
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RestApiAgentRunnerHostedService loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("RestApiAgentRunnerHostedService stopped");
    }

    private async Task<AgentExecutionResult> ExecuteRestApiAgentAsync(
        Agent agent,
        ResearchPage page,
        CancellationToken cancellationToken)
    {
        var result = new AgentExecutionResult();
        var log = new StringBuilder();

        try
        {
            // Step 1: Fetch data from REST API endpoint
            // Replace placeholders in endpoint URL with actual values
            var processedUrl = agent.endpoint_url
                .Replace("{{ticker}}", page.security_figiNavigation?.ticker ?? "")
                .Replace("{{figi}}", page.security_figi ?? "")
                .Replace("{{name}}", page.security_figiNavigation?.name ?? "");

            var agentInstructions = agent.agent_instructions?.Replace("{{ticker}}", page.security_figiNavigation?.ticker ?? "")
                .Replace("{{figi}}", page.security_figi ?? "")
                .Replace("{{name}}", page.security_figiNavigation?.name ?? "");

            // Append additional hardcoded instructions
            agentInstructions += "\nAlways begin your response with a Fundamental Score and Conviction Score on the first line in this exact format:\n{FundamentalScore: X, ConvictionScore: Y}\nFundamental Score = -3 (terrible) to +3 (absolutely great)\nConviction Score = 0 (low confidence) to 5 (very high confidence)\nOn the very next line, state the investment decision: BUY, SELL, or HOLD — based on the scores above.\nEnsure both scores and the decision are always present and formatted exactly as shown.";

            log.AppendLine($"Calling REST endpoint: {processedUrl}");
            log.AppendLine($"HTTP Method: {agent.http_method}");

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var request = new HttpRequestMessage(
                new HttpMethod(agent.http_method ?? "GET"),
                processedUrl);

            // Add authentication if configured
            if (agent.auth_type == "ApiToken" && agent.api_token != null)
            {
                var token = _encryptionService.Decrypt(agent.api_token);
                request.Headers.Add("Authorization", $"Bearer {token}");
                log.AppendLine("Added Bearer token authentication");
            }
            else if (agent.auth_type == "BasicAuth" && agent.username != null && agent.password != null)
            {
                var password = _encryptionService.Decrypt(agent.password);
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{agent.username}:{password}"));
                request.Headers.Add("Authorization", $"Basic {credentials}");
                log.AppendLine($"Added Basic authentication for user: {agent.username}");
            }
            else if (agent.auth_type == "UsernamePassword" && agent.username != null && agent.password != null)
            {
                // First, obtain token from login endpoint
                if (string.IsNullOrEmpty(agent.login_endpoint_url))
                {
                    throw new InvalidOperationException("login_endpoint_url is required for UsernamePassword authentication");
                }

                log.AppendLine($"Authenticating with UsernamePassword at: {agent.login_endpoint_url}");

                var password = _encryptionService.Decrypt(agent.password);
                var loginRequest = new HttpRequestMessage(HttpMethod.Post, agent.login_endpoint_url);

                var loginPayload = new
                {
                    username = agent.username,
                    password = password
                };

                loginRequest.Content = new StringContent(
                    JsonSerializer.Serialize(loginPayload),
                    Encoding.UTF8,
                    "application/json");

                var loginResponse = await httpClient.SendAsync(loginRequest, cancellationToken);
                var loginResponseContent = await loginResponse.Content.ReadAsStringAsync(cancellationToken);

                log.AppendLine($"Login response status: {(int)loginResponse.StatusCode} {loginResponse.StatusCode}");

                if (!loginResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to authenticate: {loginResponseContent}");
                }

                // Parse token from response - supports common patterns
                string? token = null;
                try
                {
                    using var jsonDoc = JsonDocument.Parse(loginResponseContent);
                    var root = jsonDoc.RootElement;

                    // Try common token field names
                    if (root.TryGetProperty("token", out var tokenProp))
                        token = tokenProp.GetString();
                    else if (root.TryGetProperty("access_token", out tokenProp))
                        token = tokenProp.GetString();
                    else if (root.TryGetProperty("accessToken", out tokenProp))
                        token = tokenProp.GetString();
                    else if (root.TryGetProperty("auth_token", out tokenProp))
                        token = tokenProp.GetString();
                    else if (root.TryGetProperty("authToken", out tokenProp))
                        token = tokenProp.GetString();
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to parse login response as JSON: {ex.Message}");
                }

                if (string.IsNullOrEmpty(token))
                {
                    throw new InvalidOperationException("No token found in login response. Expected fields: token, access_token, accessToken, auth_token, or authToken");
                }

                request.Headers.Add("Authorization", $"Bearer {token}");
                log.AppendLine($"Successfully authenticated user: {agent.username}");
                log.AppendLine("Added Bearer token from login response");
            }

            // Add request body if needed (for POST/PUT/PATCH)
            if (!string.IsNullOrEmpty(agent.request_body_template) &&
                (agent.http_method == "POST" || agent.http_method == "PUT" || agent.http_method == "PATCH"))
            {
                // Replace placeholders in template with actual values
                var bodyContent = agent.request_body_template
                    .Replace("{{ticker}}", page.security_figiNavigation?.ticker ?? "")
                    .Replace("{{figi}}", page.security_figi ?? "")
                    .Replace("{{name}}", page.security_figiNavigation?.name ?? "");

                request.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json");
                log.AppendLine($"Request body: {bodyContent}");
            }

            // Execute the API call
            var response = await httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            log.AppendLine($"Response Status: {(int)response.StatusCode} {response.StatusCode}");
            log.AppendLine($"Response Body: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.Error = log.ToString();
                result.Output = responseContent;
                return result;
            }

            // Step 2: Process the response with agent instructions
            log.AppendLine("\n--- Agent Processing ---");
            log.AppendLine("Calling LLM for data processing...");

            try
            {
                // Create a single scope/LLM service instance and reuse it for URL summarization + final processing
                using var scope = _services.CreateScope();
                var llmService = scope.ServiceProvider.GetRequiredService<ILlmConversationService>();

                // Build responseContext: original API response + optional URL summaries
                var responseContext = responseContent ?? string.Empty;

                // Detect absolute URLs in the API response
                //var urlMatches = Regex.Matches(responseContent ?? string.Empty, @"https?://[^\s'""<>]+", RegexOptions.IgnoreCase);
                //var urls = urlMatches.Select(m => m.Value).Distinct().Take(5).ToList(); // limit to 5 URLs

                //if (urls.Count > 0)
                //{
                //    log.AppendLine($"Found {urls.Count} URL(s) in response - attempting to fetch & summarize (max 5).");

                //    foreach (var url in urls)
                //    {
                //        try
                //        {
                //            var webClient = _httpClientFactory.CreateClient();
                //            webClient.Timeout = TimeSpan.FromSeconds(10);

                //            var webResp = await webClient.GetAsync(url, cancellationToken);

                //            if (!webResp.IsSuccessStatusCode)
                //            {
                //                log.AppendLine($"Failed to fetch URL {url}: {(int)webResp.StatusCode} {webResp.StatusCode}");
                //                responseContext += $"\n\n[URL: {url}] Failed to fetch: {(int)webResp.StatusCode} {webResp.StatusCode}";
                //                continue;
                //            }

                //            var contentType = webResp.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
                //            string fetchedText;

                //            // If HTML (or unknown), try to extract visible/plain text; otherwise read raw
                //            if (contentType.Contains("html") || contentType.Contains("xml") || string.IsNullOrEmpty(contentType))
                //            {
                //                var html = await webResp.Content.ReadAsStringAsync(cancellationToken);
                //                fetchedText = HtmlToPlainText(html);
                //            }
                //            else
                //            {
                //                fetchedText = await webResp.Content.ReadAsStringAsync(cancellationToken);
                //            }

                //            // Trim to a safe size for LLM summarization
                //            const int MaxFetchedText = 20000;
                //            if (!string.IsNullOrEmpty(fetchedText) && fetchedText.Length > MaxFetchedText)
                //                fetchedText = fetchedText.Substring(0, MaxFetchedText) + "\n\n[Truncated]";

                //            // Ask LLM for a very compact plaintext summary (1-2 short sentences)
                //            var summaryInstruction = "Summarize the following webpage content in one short sentence or two (very compact). Output plaintext only.";
                //            var summary = await llmService.GetCompletionAsync(agent.id, summaryInstruction, fetchedText ?? string.Empty);

                //            if (string.IsNullOrWhiteSpace(summary))
                //                summary = "[No summary produced]";

                //            responseContext += $"\n\n[URL Summary] {url}\n{summary}";
                //            log.AppendLine($"Appended summary for {url}");
                //        }
                //        catch (Exception ex)
                //        {
                //            log.AppendLine($"Exception while fetching/summarizing {url}: {ex.Message}");
                //            responseContext += $"\n\n[URL: {url}] Error fetching or summarizing: {ex.Message}";
                //        }
                //    }
                //}

                // Final LLM processing uses the augmented responseContext
                var processedOutput = await llmService.GetCompletionAsync(
                    agent.id,
                    agentInstructions ?? "You are a helpful assistant.",
                    responseContext);

                log.AppendLine("LLM processing complete.");

                result.Success = true;
                result.Output = processedOutput;
                result.Error = null;
            }
            catch (Exception ex)
            {
                log.AppendLine($"\nLLM processing failed: {ex.Message}");
                _logger.LogError(ex, "LLM processing failed for agent {AgentId}", agent.id);

                result.Success = false;
                result.Error = log.ToString();
                result.Output = responseContent; // Keep raw data in output for debugging
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute REST API agent");
            result.Success = false;
            result.Error = log.ToString() + "\n\nException: " + ex.Message;
            result.Output = null;
            return result;
        }
    }

    private async Task ParseAndUpdateSectionScores(string response, int sectionId, IrsDbContext db, CancellationToken cancellationToken)
    {
        // Updated regular expression to extract multi-digit FundamentalScore and ConvictionScore
        var regex = new Regex(@"\{FundamentalScore:\s*(-?\d+),\s*ConvictionScore:\s*(\d+)\}");
        var match = regex.Match(response);

        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int fundamentalScore) &&
                int.TryParse(match.Groups[2].Value, out int convictionScore))
            {
                // Find the section by ID
                var section = await db.Sections
                    .Include(s => s.SectionAgents)
                    .ThenInclude(sa => sa.AgentRuns)
                    .FirstOrDefaultAsync(s => s.id == sectionId, cancellationToken);

                if (section != null)
                {
                    // Directly update the section scores with the extracted values
                    section.fundamental_score = fundamentalScore;
                    section.conviction_score = convictionScore;

                    // Calculate the average scores from the last runs of each agent in the section
                    var lastRuns = section.SectionAgents
                        .SelectMany(sa => sa.AgentRuns)
                        .Where(ar => ar.completed_at != null)
                        .GroupBy(ar => ar.section_agent_id)
                        .Select(g => g.OrderByDescending(ar => ar.completed_at).FirstOrDefault()?.section)
                        .Where(s => s != null);

                    var avgFundamentalScore = (decimal)lastRuns.Average(s => s!.fundamental_score).GetValueOrDefault();
                    var avgConvictionScore = (decimal)lastRuns.Average(s => s!.conviction_score).GetValueOrDefault();

                    // Update the section scores with the averages
                    section.fundamental_score = (int)Math.Round(avgFundamentalScore);
                    section.conviction_score = (int)Math.Round(avgConvictionScore);

                    // Save changes to the database
                    await db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Updated Section {SectionId} with extracted and averaged FundamentalScore: {FundamentalScore}, ConvictionScore: {ConvictionScore}", sectionId, avgFundamentalScore, avgConvictionScore);
                }
                else
                {
                    _logger.LogWarning("Section with ID {SectionId} not found", sectionId);
                }
            }
            else
            {
                _logger.LogWarning("Failed to parse scores from response: {Response}", response);
            }
        }
        else
        {
            _logger.LogWarning("No scores found in response: {Response}", response);
        }
    }

    internal static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script/style nodes and comments
        var removeNodes = doc.DocumentNode.Descendants()
            .Where(n => n.Name.Equals("script", StringComparison.OrdinalIgnoreCase)
                         || n.Name.Equals("style", StringComparison.OrdinalIgnoreCase)
                         || n.NodeType == HtmlNodeType.Comment)
            .ToList();

        foreach (var node in removeNodes)
            node.Remove();

        // Extract visible text nodes, decode entities and normalize whitespace
        var texts = doc.DocumentNode.Descendants()
            .Where(n => n.NodeType == HtmlNodeType.Text && !string.IsNullOrWhiteSpace(n.InnerText))
            .Select(n => WebUtility.HtmlDecode(n.InnerText))
            .Select(t => Regex.Replace(t, @"\s+", " ").Trim())
            .Where(t => !string.IsNullOrEmpty(t));

        var sb = new StringBuilder();
        foreach (var t in texts)
            sb.AppendLine(t);

        var result = Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();
        return result;
    }

    private class AgentExecutionResult
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
    }
}
