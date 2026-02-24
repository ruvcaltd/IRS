using TechTalk.SpecFlow;
using IRS.Api.IntegrationTests.Support;
using FluentAssertions;
using System.Threading.Tasks;

namespace IRS.Api.IntegrationTests.StepDefinitions;

[Binding]
public class CommonSteps
{
    private readonly ScenarioContextWrapper _context;
    private readonly DatabaseFixture _databaseFixture;

    public CommonSteps(ScenarioContextWrapper context, DatabaseFixture databaseFixture)
    {
        _context = context;
        _databaseFixture = databaseFixture;
    }

    [Given(@"the database is clean")]
    public async Task GivenTheDatabaseIsClean()
    {
        await _databaseFixture.ResetDatabaseAsync();
    }

    [Then(@"the response status code should be (.*)")]
    public void ThenTheResponseStatusCodeShouldBe(int expectedStatusCode)
    {
        _context.LastResponse.Should().NotBeNull("Response should not be null");
        ((int)_context.LastResponse!.StatusCode).Should().Be(expectedStatusCode, 
            $"Expected status code {expectedStatusCode}, but got {(int)_context.LastResponse.StatusCode}");
    }
}
