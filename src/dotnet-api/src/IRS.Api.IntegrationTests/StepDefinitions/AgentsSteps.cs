using System.Net.Http.Json;
using FluentAssertions;
using IRS.Api.IntegrationTests.Support;
using IRS.Infrastructure;
using IRS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechTalk.SpecFlow;

namespace IRS.Api.IntegrationTests.StepDefinitions;

[Binding]
public class AgentsSteps
{
    private readonly ScenarioContextWrapper _context;
    private readonly TestWebApplicationFactory _factory;

    public AgentsSteps(ScenarioContextWrapper context, TestWebApplicationFactory factory)
    {
        _context = context;
        _factory = factory;
    }

    [Given(@"an agent exists for team ""(.*)"" with name ""(.*)"" and visibility ""(.*)""")]
    public async Task GivenAnAgentExistsForTeamWithNameAndVisibility(string teamName, string agentName, string visibility)
    {
        var teamId = _context.Get<int>($"TeamId_{teamName}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

        var ownerUserId = await db.TeamMembers
            .Where(tm => tm.team_id == teamId && tm.status == "ACTIVE")
            .Select(tm => tm.user_id)
            .FirstAsync();

        var agent = new Agent
        {
            team_id = teamId,
            owner_user_id = ownerUserId,
            name = agentName,
            description = "BDD test agent",
            visibility = visibility,
            endpoint_url = "https://api.example.com/test",
            http_method = "GET",
            auth_type = "None",
            agent_instructions = "Test agent for BDD",
            version = "1.0",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
            is_deleted = false
        };

        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        _context.Set($"AgentId_{agentName}", agent.id);
    }

    [When(@"I list available agents for team ""(.*)""")]
    public async Task WhenIListAvailableAgentsForTeam(string teamName)
    {
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        var response = await _context.HttpClient.GetAsync($"/api/v1/teams/{teamId}/agents/available");
        _context.LastResponse = response;
    }
    [When(@"I create an agent for team ""(.*)"" via API with name ""(.*)"" and visibility ""(.*)""")]
    public async Task WhenICreateAnAgentForTeamViaApi(string teamName, string agentName, string visibility)
    {
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        var req = new
        {
            name = agentName,
            visibility = visibility,
            endpoint_url = "https://api.example.com/test",
            http_method = "GET",
            auth_type = "None",
            agent_instructions = "Test agent created via API"
        };

        var response = await _context.HttpClient.PostAsJsonAsync($"/api/v1/teams/{teamId}/agents", req);
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<AvailableAgentDto>();
            _context.Set($"AgentId_{agentName}", body!.id);
        }
    }

    [When(@"I update agent ""(.*)"" for team ""(.*)"" via API to name ""(.*)"" and endpoint ""(.*)""")]
    public async Task WhenIUpdateAgentForTeamViaApi(string agentName, string teamName, string newName, string newEndpoint)
    {
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        var agentId = _context.Get<int>($"AgentId_{agentName}");

        var req = new
        {
            name = newName,
            visibility = "Private",
            endpoint_url = newEndpoint,
            http_method = "GET",
            auth_type = "None",
            agent_instructions = "Updated via API"
        };

        var response = await _context.HttpClient.PutAsJsonAsync($"/api/v1/teams/{teamId}/agents/{agentId}", req);
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<AvailableAgentDto>();
            _context.Set($"AgentId_{newName}", body!.id);
        }
    }
    [Then(@"the available agents list should include ""(.*)""")]
    public async Task ThenTheAvailableAgentsListShouldInclude(string agentName)
    {
        var list = await _context.LastResponse!.Content.ReadFromJsonAsync<List<AvailableAgentDto>>();
        list.Should().NotBeNull();
        list!.Any(a => a.name == agentName).Should().BeTrue();
    }

    [When(@"I attach agent ""(.*)"" to the created research page")]
    public async Task WhenIAttachAgentToTheCreatedResearchPage(string agentName)
    {
        var agentId = _context.Get<int>($"AgentId_{agentName}");
        var pageId = _context.Get<int>("LastResearchPageId");

        var req = new { agent_id = agentId };
        var response = await _context.HttpClient.PostAsJsonAsync($"/api/v1/research-pages/{pageId}/agents", req);
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<PageAgentDto>();
            body.Should().NotBeNull();
            _context.Set("LastPageAgent", body!);
            _context.Set($"PageAgentId_{agentName}", body!.id);
        }
    }

    [When(@"I list agents for the created research page")]
    public async Task WhenIListAgentsForTheCreatedResearchPage()
    {
        var pageId = _context.Get<int>("LastResearchPageId");
        var response = await _context.HttpClient.GetAsync($"/api/v1/research-pages/{pageId}/agents");
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var list = await response.Content.ReadFromJsonAsync<List<PageAgentDto>>();
            if (list != null)
            {
                _context.Set("LastPageAgentList", list);
            }
        }
    }

    [Then(@"the page agents list should include ""(.*)""")]
    public void ThenThePageAgentsListShouldInclude(string agentName)
    {
        var list = _context.Get<List<PageAgentDto>>("LastPageAgentList");
        list.Any(a => a.name == agentName).Should().BeTrue();
    }

    [When(@"I disable the page agent for ""(.*)""")]
    public async Task WhenIDisableThePageAgentFor(string agentName)
    {
        int pageAgentId;
        if (!_context.TryGet($"PageAgentId_{agentName}", out pageAgentId))
        {
            var list = _context.Get<List<PageAgentDto>>("LastPageAgentList");
            pageAgentId = list.First(a => a.name == agentName).id;
        }

        var req = new { is_enabled = false };
        var response = await _context.HttpClient.PutAsJsonAsync($"/api/v1/page-agents/{pageAgentId}/enabled", req);
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<PageAgentDto>();
            if (body != null)
            {
                _context.Set("LastPageAgent", body);
            }
        }
    }

    [Then(@"the page agent should be disabled")]
    public void ThenThePageAgentShouldBeDisabled()
    {
        var agent = _context.Get<PageAgentDto>("LastPageAgent");
        agent.is_enabled.Should().BeFalse();
    }

    [Given(@"an agent run exists for the page agent on the first section")]
    [Then(@"an agent run exists for the page agent on the first section")]
    public async Task GivenAnAgentRunExistsForThePageAgentOnTheFirstSection()
    {
        var pageId = _context.Get<int>("LastResearchPageId");
        var pageAgent = _context.Get<PageAgentDto>("LastPageAgent");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

        var sectionId = await db.Sections
            .Where(s => s.research_page_id == pageId && !s.is_deleted)
            .OrderBy(s => s.id)
            .Select(s => s.id)
            .FirstAsync();

        var run = new AgentRun
        {
            research_page_agent_id = pageAgent.id,
            section_id = sectionId,
            status = "Succeeded",
            started_at = DateTime.UtcNow.AddMinutes(-5),
            completed_at = DateTime.UtcNow,
            output = "ok",
            error = null
        };

        db.AgentRuns.Add(run);
        await db.SaveChangesAsync();
    }

    [When(@"I list runs for the page agent")]
    public async Task WhenIListRunsForThePageAgent()
    {
        var pageAgent = _context.Get<PageAgentDto>("LastPageAgent");
        var response = await _context.HttpClient.GetAsync($"/api/v1/page-agents/{pageAgent.id}/runs");
        _context.LastResponse = response;
    }

    [When(@"I delete the page agent for ""(.*)""")]
    public async Task WhenIDeleteThePageAgentFor(string agentName)
    {
        if (!_context.TryGet($"PageAgentId_{agentName}", out int pageAgentId))
        {
            var list = _context.Get<List<PageAgentDto>>("LastPageAgentList");
            pageAgentId = list.First(a => a.name == agentName).id;
        }

        var response = await IRS.Api.IntegrationTests.Helpers.HttpClientExtensions.DeleteWithAuthAsync(_context.HttpClient, $"/api/v1/page-agents/{pageAgentId}", _context.AuthToken!);
        _context.LastResponse = response;
    }

    [Then(@"the page agents list should NOT include ""(.*)""")]
    public async Task ThenThePageAgentsListShouldNotInclude(string agentName)
    {
        var pageId = _context.Get<int>("LastResearchPageId");
        var response = await _context.HttpClient.GetAsync($"/api/v1/research-pages/{pageId}/agents");
        response.IsSuccessStatusCode.Should().BeTrue("Should be able to list agents for the page");

        var list = await response.Content.ReadFromJsonAsync<List<PageAgentDto>>();
        list!.Any(a => a.name == agentName).Should().BeFalse($"Page agents list should not include {agentName}");
    }

    [When(@"I attach agent ""(.*)"" to the first section of the created research page")]
    public async Task WhenIAttachAgentToTheFirstSection(string agentName)
    {
        var pageId = _context.Get<int>("LastResearchPageId");

        // find first section id
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IrsDbContext>();
        var sectionId = await db.Sections.Where(s => s.research_page_id == pageId && !s.is_deleted).OrderBy(s => s.id).Select(s => s.id).FirstAsync();

        var agentId = _context.Get<int>($"AgentId_{agentName}");
        var req = new { agent_id = agentId };
        var response = await _context.HttpClient.PostAsJsonAsync($"/api/v1/sections/{sectionId}/agents", req);
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<SectionAgentDto>();
            _context.Set("LastSectionAgent", body!);
            _context.Set($"SectionAgentId_{agentName}", body!.id);
        }
    }

    [Given(@"an agent run exists for the section agent on the first section")]
    public async Task GivenAnAgentRunExistsForTheSectionAgentOnTheFirstSection()
    {
        var pageId = _context.Get<int>("LastResearchPageId");
        var sectionAgent = _context.Get<SectionAgentDto>("LastSectionAgent");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

        var sectionId = await db.Sections
            .Where(s => s.research_page_id == pageId && !s.is_deleted)
            .OrderBy(s => s.id)
            .Select(s => s.id)
            .FirstAsync();

        var run = new AgentRun
        {
            section_agent_id = sectionAgent.id,
            section_id = sectionId,
            status = "Succeeded",
            started_at = DateTime.UtcNow.AddMinutes(-5),
            completed_at = DateTime.UtcNow,
            output = "ok",
            error = null
        };

        db.AgentRuns.Add(run);
        await db.SaveChangesAsync();
    }

    [When(@"I delete the section agent for ""(.*)""")]
    public async Task WhenIDeleteTheSectionAgentFor(string agentName)
    {
        if (!_context.TryGet($"SectionAgentId_{agentName}", out int sectionAgentId))
        {
            var sectionAgent = _context.Get<SectionAgentDto>("LastSectionAgent");
            sectionAgentId = sectionAgent.id;
        }

        var response = await IRS.Api.IntegrationTests.Helpers.HttpClientExtensions.DeleteWithAuthAsync(_context.HttpClient, $"/api/v1/section-agents/{sectionAgentId}", _context.AuthToken!);
        _context.LastResponse = response;
    }

    [When(@"I list runs for the section agent")]
    public async Task WhenIListRunsForTheSectionAgent()
    {
        var sectionAgent = _context.Get<SectionAgentDto>("LastSectionAgent");
        var response = await _context.HttpClient.GetAsync($"/api/v1/section-agents/{sectionAgent.id}/runs");
        _context.LastResponse = response;
    }
    [Then(@"the agent runs should include status ""(.*)""")]
    public async Task ThenTheAgentRunsShouldIncludeStatus(string status)
    {
        var runs = await _context.LastResponse!.Content.ReadFromJsonAsync<List<AgentRunDto>>();
        runs.Should().NotBeNull();
        runs!.Any(r => r.status == status).Should().BeTrue();
    }

    [When(@"I run the page agent now")]
    public async Task WhenIRunThePageAgentNow()
    {
        var pageAgent = _context.Get<PageAgentDto>("LastPageAgent");
        var response = await _context.HttpClient.PostAsJsonAsync($"/api/v1/page-agents/{pageAgent.id}/runs", new { });
        _context.LastResponse = response;
    }
}

public record AvailableAgentDto
{
    public int id { get; init; }
    public string name { get; init; } = string.Empty;
    public string visibility { get; init; } = string.Empty;
    public string endpoint_url { get; init; } = string.Empty;
    public string http_method { get; init; } = string.Empty;
    public string? description { get; init; }
}

public record PageAgentDto
{
    public int id { get; init; }
    public int agent_id { get; init; }
    public string name { get; init; } = string.Empty;
    public bool is_enabled { get; init; }
    public string? last_run_status { get; init; }
    public DateTime? last_run_at { get; init; }
}

public record SectionAgentDto
{
    public int id { get; init; }
    public int agent_id { get; init; }
    public string name { get; init; } = string.Empty;
    public bool is_enabled { get; init; }
    public string? last_run_status { get; init; }
    public DateTime? last_run_at { get; init; }
}

public record AgentRunDto
{
    public int id { get; init; }
    public int research_page_agent_id { get; init; }
    public int? section_id { get; init; }
    public string status { get; init; } = string.Empty;
    public DateTime? started_at { get; init; }
    public DateTime? completed_at { get; init; }
    public string? output { get; init; }
    public string? error { get; init; }
}