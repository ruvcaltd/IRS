using TechTalk.SpecFlow;
using IRS.Api.IntegrationTests.Support;
using System.Threading.Tasks;
using System.Net.Http.Json;
using FluentAssertions;
using System.Linq;
using System.Text.Json.Serialization;

namespace IRS.Api.IntegrationTests.StepDefinitions;

[Binding]
public class ResearchPagesSteps
{
    private readonly ScenarioContextWrapper _context;

    public ResearchPagesSteps(ScenarioContextWrapper context)
    {
        _context = context;
    }

    [When(@"I create a research page for team ""(.*)"" with security:")]
    [Given(@"I create a research page for team ""(.*)"" with security:")]
    public async Task WhenICreateAResearchPageForTeamWithSecurity(string teamName, Table table)
    {
        var row = table.Rows.First();
        var figi = row["figi"]; var ticker = row["ticker"]; var name = row["name"]; var stype = row["security_type"];        

        // Retrieve team id from context (set by team creation step)
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        teamId.Should().BeGreaterThan(0);

        var req = new
        {
            team_id = teamId,
            figi,
            ticker,
            name,
            security_type = stype
        };

        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/research-pages", req);
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ResearchPageResponse>();
            body.Should().NotBeNull();
            _context.Set("LastResearchPageId", body!.Id);
            _context.Set("LastResearchPage", body);
        }
    }

    [Then(@"the research page should include (\d+) default sections")]
    public async Task ThenTheResearchPageShouldIncludeDefaultSections(int expected)
    {
        ResearchPageResponse? body = null;
        if (!_context.TryGet("LastResearchPage", out body))
        {
            body = await _context.LastResponse!.Content.ReadFromJsonAsync<ResearchPageResponse>();
        }
        body.Should().NotBeNull();
        body!.Sections.Should().HaveCount(expected);
    }

    [When(@"I get the created research page")]
    public async Task WhenIGetTheCreatedResearchPage()
    {
        var id = _context.Get<int>("LastResearchPageId");
        var response = await _context.HttpClient.GetAsync($"/api/v1/research-pages/{id}");
        _context.LastResponse = response;
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ResearchPageResponse>();
            _context.Set("LastResearchPage", body!);
        }
    }

    [When(@"I delete the research page")]
    public async Task WhenIDeleteTheResearchPage()
    {
        var id = _context.Get<int>("LastResearchPageId");
        var response = await IRS.Api.IntegrationTests.Helpers.HttpClientExtensions.DeleteWithAuthAsync(_context.HttpClient, $"/api/v1/research-pages/{id}", _context.AuthToken!);
        _context.LastResponse = response;
    }
}

public record ResearchPageResponse
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("sections")] public List<SectionDto> Sections { get; init; } = new();
}

public record SectionDto
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
}
