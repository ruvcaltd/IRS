using BoDi;
using Microsoft.Extensions.Configuration;
using TechTalk.SpecFlow;
using System.Threading.Tasks;

namespace IRS.Api.IntegrationTests.Support;

[Binding]
public class Hooks
{
    private readonly IObjectContainer _objectContainer;
    private static DatabaseFixture _databaseFixture = null!;
    private static TestWebApplicationFactory _factory = null!;

    public Hooks(IObjectContainer objectContainer)
    {
        _objectContainer = objectContainer;
    }

    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json")
            .Build();

        _databaseFixture = new DatabaseFixture(configuration);
        await _databaseFixture.InitializeAsync();

        _factory = new TestWebApplicationFactory(_databaseFixture.ConnectionString);
    }

    [BeforeScenario]
    public void BeforeScenario()
    {
        var httpClient = _factory.CreateClient();
        var scenarioContext = new ScenarioContextWrapper(httpClient);
        
        _objectContainer.RegisterInstanceAs(_databaseFixture);
        _objectContainer.RegisterInstanceAs(_factory);
        _objectContainer.RegisterInstanceAs(scenarioContext);
    }

    [AfterScenario]
    public async Task AfterScenario()
    {
        await _databaseFixture.ResetDatabaseAsync();
    }

    [AfterTestRun]
    public static void AfterTestRun()
    {
        _factory?.Dispose();
    }
}
