# IRS API Integration Tests

Comprehensive integration and BDD tests for the Investment Research System (IRS) API using SpecFlow, NUnit, and Respawn.

## Overview

This test project validates the IRS API endpoints through:
- **BDD Scenarios**: Feature files in Gherkin syntax describing business behavior
- **Step Definitions**: C# implementations of Gherkin steps
- **Database Isolation**: Respawn ensures clean state between tests
- **Web Testing**: WebApplicationFactory for in-memory API testing

## Project Structure

```
IRS.Api.IntegrationTests/
├── Features/                           # SpecFlow feature files (Gherkin scenarios)
│   ├── Authentication.feature
│   └── TeamManagement.feature (planned)
├── StepDefinitions/                   # C# step implementations
│   ├── AuthenticationSteps.cs
│   └── CommonSteps.cs
├── Support/                           # Test infrastructure
│   ├── DatabaseFixture.cs             # Respawn database reset
│   ├── TestWebApplicationFactory.cs   # In-memory test API
│   ├── ScenarioContextWrapper.cs      # Context sharing between steps
│   └── Hooks.cs                       # SpecFlow setup/teardown
├── Helpers/
│   ├── HttpClientExtensions.cs        # HTTP helper methods
│   ├── TestDataBuilder.cs             # Test data creation
├── appsettings.Test.json              # Test environment config
└── IRS.Api.IntegrationTests.csproj
```

## Quick Start

### Prerequisites

- SQL Server instance running (for test database)
- .NET 9.0 SDK
- Visual Studio 2022 or VS Code

### Setup

1. **Create test database** (optional - Respawn will auto-create if needed):
```bash
sqlcmd -S localhost -Q "CREATE DATABASE IRS_Test"
```

2. **Configure test connection** in `appsettings.Test.json`:
```json
"ConnectionStrings": {
  "TestConnection": "Server=localhost;Database=IRS_Test;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

3. **Run all tests**:
```bash
dotnet test
```

4. **Run specific feature**:
```bash
dotnet test --filter "FullyQualifiedName~Authentication"
```

5. **Run with detailed output**:
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Feature Files

### Authentication.feature

Tests user registration, login, and JWT token validation:

- ✅ Successful user registration
- ✅ User login with valid credentials
- ✅ User login with invalid credentials
- ✅ Accessing protected endpoints requires authentication
- ✅ Duplicate email registration is rejected

Run authentication tests:
```bash
dotnet test --filter "FullyQualifiedName~Authentication"
```

### Example Test Execution

```gherkin
Scenario: Successful user registration
    When I register a new user with email "test@example.com", password "Pass123!", and full name "John Doe"
    Then the response status code should be 201
    And the response should contain a user ID
    And the user should exist in the database
```

## Test Infrastructure

### DatabaseFixture

Manages database state using Respawn:
- Initializes Respawner on first use
- Resets database to clean state after each scenario
- Thread-safe singleton pattern

### TestWebApplicationFactory

Creates in-memory test instances of the IRS API:
- Uses test database connection
- Injects mocked services as needed
- Returns HttpClient for API calls

### ScenarioContextWrapper

Shares data between step definitions:
- Stores HTTP responses
- Maintains authentication tokens
- Generic data dictionary for complex scenarios

### Hooks

SpecFlow lifecycle management:
- `@BeforeTestRun`: Initialize database and factory
- `@BeforeScenario`: Create HttpClient and context
- `@AfterScenario`: Reset database
- `@AfterTestRun`: Cleanup resources

## Writing Tests

### Adding a New Scenario

1. **Add feature file entry** (Features/YourFeature.feature):
```gherkin
Scenario: Your test description
    Given initial condition
    When action occurs
    Then verify result
```

2. **Implement step definitions** (StepDefinitions/YourSteps.cs):
```csharp
[When(@"I perform an action")]
public async Task WhenIPerformAnAction()
{
    var response = await _context.HttpClient.GetAsync("/api/endpoint");
    _context.LastResponse = response;
}
```

3. **Run and verify**:
```bash
dotnet test --filter "FullyQualifiedName~YourScenario"
```

### Testing Authenticated Endpoints

```csharp
[Given(@"I am logged in as a user with email ""(.*)""")]
public async Task GivenIAmLoggedInAsAUserWithEmail(string email)
{
    // Create and login user
    await GivenAUserExistsWithEmailAndPassword(email, "Pass123!");
    await WhenILoginWithEmailAndPassword(email, "Pass123!");
    
    // Set authorization header for subsequent requests
    if (!string.IsNullOrEmpty(_context.AuthToken))
    {
        _context.HttpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _context.AuthToken);
    }
}
```

### Creating Test Data

Use `TestDataBuilder` for complex object creation:

```csharp
var builder = new TestDataBuilder(_serviceProvider);
var user = await builder.CreateUserAsync("test@example.com", "Test User");
var team = await builder.CreateTeamAsync("My Team");
await builder.AddTeamMemberAsync(user.id, team.id, teamRoleId: 1);
```

## Best Practices

1. **Test Isolation**: Each scenario is independent; Respawn ensures clean state
2. **Naming Conventions**: Use descriptive scenario names explaining business value
3. **Given-When-Then**: Keep steps atomic and focused
4. **Assertions**: Use FluentAssertions for readable assertions
5. **Error Scenarios**: Test both happy paths and error cases
6. **Performance**: Database reset runs asynchronously
7. **Documentation**: Feature files serve as living documentation

## Debugging Tests

### View Test Results

Tests appear as individual scenarios in Test Explorer (VS 2022):
- Expand test categories to see individual scenarios
- Right-click and "Debug Selected Tests"
- Use breakpoints in step definition methods

### SQL Server Database

Connect to test database to inspect state:
```sql
-- In SQL Server Management Studio
Server: localhost
Database: IRS_Test
```

### Console Output

Enable debug output in step definitions:
```csharp
Console.WriteLine($"Response: {await _context.LastResponse!.Content.ReadAsStringAsync()}");
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Run Integration Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run tests
      run: dotnet test --no-build --verbosity normal
```

## Troubleshooting

### Tests fail with "Database connection failed"

Ensure SQL Server is running and test database exists:
```bash
sqlcmd -S localhost -Q "SELECT @@VERSION"  # Verify SQL Server
sqlcmd -S localhost -Q "CREATE DATABASE IRS_Test"  # Create test DB
```

### Tests timeout

Increase timeout in test settings or check for infinite loops in test data setup.

### Authentication fails in tests

Verify JWT configuration in `appsettings.Test.json` matches production.

### SpecFlow not generating code

Rebuild project and check feature file extensions (.feature):
```bash
dotnet build --force
```

## Related Documentation

- [SpecFlow Documentation](https://docs.specflow.org/)
- [NUnit Documentation](https://docs.nunit.org/)
- [Respawn Documentation](https://github.com/jbogard/respawn)
- [FluentAssertions](https://fluentassertions.com/)

## Contributing

When adding new features to the API:

1. Add corresponding BDD scenarios
2. Implement step definitions
3. Test with: `dotnet test`
4. Ensure all scenarios pass before merging
