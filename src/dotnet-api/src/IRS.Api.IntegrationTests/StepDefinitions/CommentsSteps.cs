using TechTalk.SpecFlow;
using IRS.Api.IntegrationTests.Support;
using System.Threading.Tasks;
using System.Net.Http.Json;
using FluentAssertions;
using System.Linq;

namespace IRS.Api.IntegrationTests.StepDefinitions;

[Binding]
public class CommentsSteps
{
    private readonly ScenarioContextWrapper _context;

    public CommentsSteps(ScenarioContextWrapper context)
    {
        _context = context;
    }

    [Given(@"I select the first section of the created research page")]
    public async Task GivenISelectTheFirstSectionOfTheCreatedResearchPage()
    {
        var pageId = _context.Get<int>("LastResearchPageId");
        var response = await _context.HttpClient.GetAsync($"/api/v1/research-pages/{pageId}");
        response.IsSuccessStatusCode.Should().BeTrue();

        var body = await response.Content.ReadFromJsonAsync<ResearchPageResponse>();
        body.Should().NotBeNull();
        body!.Sections.Should().NotBeEmpty();
        var sectionId = body.Sections.First().Id;
        _context.Set("SelectedSectionId", sectionId);
    }

    [When(@"I add a comment ""(.*)""")]
    public async Task WhenIAddAComment(string text)
    {
        var sectionId = _context.Get<int>("SelectedSectionId");
        var req = new { content = text };
        var response = await _context.HttpClient.PostAsJsonAsync($"/api/v1/sections/{sectionId}/comments", req);
        _context.LastResponse = response;
    }

    [When(@"I list comments for the selected section")]
    public async Task WhenIListCommentsForTheSelectedSection()
    {
        var sectionId = _context.Get<int>("SelectedSectionId");
        var response = await _context.HttpClient.GetAsync($"/api/v1/sections/{sectionId}/comments");
        _context.LastResponse = response;
    }

    [Then(@"the comments list should include ""(.*)""")]
    public async Task ThenTheCommentsListShouldInclude(string text)
    {
        var list = await _context.LastResponse!.Content.ReadFromJsonAsync<List<CommentsListItem>>();
        list.Should().NotBeNull();
        list!.Any(c => c.content == text).Should().BeTrue();
    }
}

public record CommentsListItem
{
    public int id { get; init; }
    public int section_id { get; init; }
    public string content { get; init; } = string.Empty;
}
