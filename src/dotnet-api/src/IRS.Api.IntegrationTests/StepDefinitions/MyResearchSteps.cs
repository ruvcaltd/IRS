using TechTalk.SpecFlow;
using IRS.Api.IntegrationTests.Support;
using System.Threading.Tasks;
using FluentAssertions;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Linq;

namespace IRS.Api.IntegrationTests.StepDefinitions;

[Binding]
public class MyResearchSteps
{
    private readonly ScenarioContextWrapper _context;
    public MyResearchSteps(ScenarioContextWrapper context) { _context = context; }

    [When(@"I list my research pages")]
    public async Task WhenIListMyResearchPages()
    {
        var response = await _context.HttpClient.GetAsync("/api/v1/research-pages/my");
        _context.LastResponse = response;
    }

    [Then(@"my research list should include the last created research page")]
    public async Task ThenMyResearchListShouldIncludeTheLastCreatedResearchPage()
    {
        var body = await _context.LastResponse!.Content.ReadFromJsonAsync<List<MyResearchItem>>();
        body.Should().NotBeNull();
        var lastId = _context.Get<int>("LastResearchPageId");
        body!.Any(x => x.id == lastId).Should().BeTrue();
    }
}

public record MyResearchItem(int id);
