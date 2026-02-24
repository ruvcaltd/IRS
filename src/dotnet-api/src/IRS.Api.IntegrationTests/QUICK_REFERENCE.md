# Quick Reference Guide - IRS BDD Testing

## 📁 Project Structure

```
IRS.Api.IntegrationTests/
├── Features/
│   └── Authentication.feature (5 test scenarios)
├── StepDefinitions/
│   ├── AuthenticationSteps.cs
│   └── CommonSteps.cs
├── Support/
│   ├── DatabaseFixture.cs (Respawn)
│   ├── TestWebApplicationFactory.cs
│   ├── ScenarioContextWrapper.cs
│   └── Hooks.cs (SpecFlow lifecycle)
├── Helpers/
│   ├── HttpClientExtensions.cs
│   └── TestDataBuilder.cs
├── appsettings.Test.json
├── IRS.Api.IntegrationTests.csproj
├── README.md (detailed guide)
└── IMPLEMENTATION_SUMMARY.md
```

---

## 🚀 Quick Start

### 1. Create Test Databases
```bash
sqlcmd -S localhost -Q "CREATE DATABASE IRS_Test"
```

### 2. Run All Tests
```bash
cd c:\Work\IRS\Service
dotnet test src/IRS.Api.IntegrationTests/IRS.Api.IntegrationTests.csproj
```

### 3. Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~UserLoginWithValidCredentials"
```

---

## 📋 Test Scenarios

### Authentication.feature (5 Scenarios)

| # | Scenario | Status |
|---|----------|--------|
| 1 | Successful user registration | ✅ Ready |
| 2 | User login with valid credentials | ✅ Ready |
| 3 | User login with invalid credentials | ✅ Ready |
| 4 | Accessing protected endpoint without auth | ✅ Ready |
| 5 | Duplicate email registration rejected | ✅ Ready |

---

## 🔧 Key Classes

### DatabaseFixture
```csharp
// Usage: Reset database before each test
await _databaseFixture.ResetDatabaseAsync();
```

### ScenarioContextWrapper
```csharp
// Store data between steps
_context.Set("UserId", user.id);
var userId = _context.Get<int>("UserId");
```

### TestDataBuilder
```csharp
// Create test data
var user = await builder.CreateUserAsync("test@example.com");
var team = await builder.CreateTeamAsync("My Team");
```

---

## 🌐 API Endpoints

```
POST /api/v1/auth/register
POST /api/v1/auth/login
```

---

## 📝 Step Examples

### Registration Step
```gherkin
When I register a new user with email "test@example.com", 
     password "Pass123!", and full name "John Doe"
Then the response status code should be 201
```

### Login Step
```gherkin
Given a user exists with email "test@example.com" 
      and password "Pass123!"
When I login with email "test@example.com" 
     and password "Pass123!"
Then the response should contain a JWT token
```

---

## 🛠️ Common Commands

| Command | Purpose |
|---------|---------|
| `dotnet build` | Compile all projects |
| `dotnet test` | Run all tests |
| `dotnet test --filter "Authentication"` | Run auth tests |
| `dotnet run` | Start API server |

---

## 🔐 Authentication Flow

### Registration
```
Client → POST /api/v1/auth/register
         → Service validates & hashes password (BCrypt)
         → User created in database
         → JWT token generated
         → Response 201 with token
```

### Login
```
Client → POST /api/v1/auth/login
      → Service finds user by email
      → Service verifies password (BCrypt)
      → JWT token generated
      → Response 200 with token
```

---

## 📦 Dependencies

| Package | Version | Use |
|---------|---------|-----|
| SpecFlow | 3.9.74 | BDD framework |
| NUnit | 4.2.2 | Test framework |
| Respawn | 7.0.0 | Database cleanup |
| BCrypt.Net-Core | 1.6.0 | Password hashing |
| FluentAssertions | 7.0.0 | Assertions |

---

## ⚙️ Configuration

### appsettings.Test.json
```json
{
  "ConnectionStrings": {
    "TestConnection": "Server=localhost;Database=IRS_Test;..."
  },
  "Jwt": {
    "Key": "test-key-...",
    "Issuer": "IRS.Api.Test",
    "Audience": "IRS.Client.Test",
    "ExpiryMinutes": 60
  }
}
```

---

## 🎯 Next Steps

1. **Create databases**: `IRS` and `IRS_Test`
2. **Run tests**: `dotnet test`
3. **Add scenarios**: Expand Authentication.feature
4. **Create more features**: TeamManagement, ResearchPages, etc.
5. **CI/CD setup**: GitHub Actions workflow

---

## 📚 Documentation

- **[README.md](README.md)** - Comprehensive guide
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Full details
- **[Authentication.feature](Features/Authentication.feature)** - Test scenarios

---

## ✅ Verification Checklist

- [x] Test project created
- [x] SpecFlow configured
- [x] Respawn integrated
- [x] 5 test scenarios defined
- [x] Step definitions implemented
- [x] Database fixture working
- [x] JWT configuration ready
- [x] All projects building
- [x] Documentation complete

---

## 🆘 Troubleshooting

### Tests fail with "Database connection failed"
```bash
# Verify SQL Server running
sqlcmd -S localhost -Q "SELECT @@VERSION"

# Create test database
sqlcmd -S localhost -Q "CREATE DATABASE IRS_Test"
```

### JWT token invalid
- Check `appsettings.Test.json` JWT configuration
- Ensure key is at least 32 characters

### Configuration file not found
- Run: `dotnet clean && dotnet build`
- Check appsettings.Test.json copied to bin directory

---

## 📞 Support Resources

- SpecFlow: https://docs.specflow.org/
- NUnit: https://docs.nunit.org/
- Respawn: https://github.com/jbogard/respawn
- FluentAssertions: https://fluentassertions.com/

---

**Project Status: ✅ COMPLETE & READY FOR TESTING**
