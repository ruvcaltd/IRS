# IRS Service - Clean Architecture

Investment Research System (IRS) backend service built with ASP.NET Core and Entity Framework Core 9.0, following Clean Architecture principles.

## 🏗️ Architecture

The solution follows Clean Architecture with the following layers:

```
Service/
├── src/
│   ├── IRS.Domain/           # Enterprise Business Rules
│   │   └── Entities/         # EF Core generated domain entities
│   ├── IRS.Application/      # Application Business Rules
│   │   ├── DTOs/            # Data Transfer Objects
│   │   ├── Interfaces/      # Use case interfaces
│   │   └── Services/        # Use case implementations
│   ├── IRS.Infrastructure/   # Frameworks & Drivers
│   │   └── Data/            # EF Core DbContext
│   └── IRS.Api/             # Interface Adapters
│       ├── Controllers/     # API Controllers
│       └── Program.cs       # Application entry point
└── scripts/
    └── scaffold-database.ps1 # Database scaffolding script
```

## 🚀 Getting Started

### Prerequisites

- .NET 9.0 SDK
- SQL Server (localhost)
- IRS Database deployed

### Database Connection

The service connects to:
- **Server**: localhost
- **Database**: IRS
- **Authentication**: Windows Authentication (Trusted_Connection)

### Build the Solution

```powershell
cd Service
dotnet build
```

### Run the API

```powershell
cd Service/src/IRS.Api
dotnet run
```

The API will start on:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

### Access Swagger UI

Navigate to: http://localhost:5000/swagger

## 📊 Database-First Approach

This project uses **database-first** development with Entity Framework Core. Models are generated from the existing database schema.

### Regenerate Models

When the database schema changes, run:

```powershell
cd Service/scripts
.\scaffold-database.ps1
```

This will:
1. Connect to the IRS database
2. Generate entity models in `IRS.Domain/Entities/`
3. Generate `IrsDbContext` in `IRS.Infrastructure/Data/`
4. Use database names and data annotations
5. Preserve navigation properties

### Custom Scaffold Options

```powershell
.\scaffold-database.ps1 -ConnectionString "Server=myserver;Database=IRS;..."
```

## 📦 Projects

### IRS.Domain
- Contains entity models generated from the database
- No dependencies (pure domain models)
- Namespace: `IRS.Infrastructure` (EF generated)

### IRS.Application
- Application business logic and use cases
- Depends on: `IRS.Domain`
- Future: DTOs, interfaces, validators

### IRS.Infrastructure
- Data access implementation
- EF Core DbContext
- Depends on: `IRS.Domain`
- External service integrations

### IRS.Api
- ASP.NET Core Web API
- Controllers and endpoints
- Depends on: `IRS.Application`, `IRS.Infrastructure`
- Swagger/OpenAPI documentation

## 🔌 API Endpoints

### Health Check
```
GET /api/health
```

Response:
```json
{
  "status": "healthy",
  "timestamp": "2026-01-22T12:05:37Z",
  "database": "connected"
}
```

### Core Research
- POST /api/v1/research-pages: Create a research page for a team and security; auto-generates default sections.
- GET /api/v1/research-pages/{id}: Retrieve a research page with sections and aggregated scores.
- POST /api/v1/sections/{sectionId}/comments: Add a comment to a section (team member only).
- GET /api/v1/sections/{sectionId}/comments: List comments for a section.
- GET /api/v1/securities/search?q={query}: Search local securities; optionally proxies OpenFIGI when configured.

Authorization: All endpoints require JWT auth; server validates caller is an ACTIVE member of the target team.

### Agents
- GET /api/v1/teams/{teamId}/agents/available: List agents available to the team (team-visible and user's private agents).
- POST /api/v1/research-pages/{pageId}/agents: Attach an agent to a research page.
- GET /api/v1/research-pages/{pageId}/agents: List agents attached to a research page.
- PUT /api/v1/page-agents/{pageAgentId}/enabled: Enable/disable a page agent.
- GET /api/v1/page-agents/{pageAgentId}/runs: List runs for a page agent.

## ⚙️ Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=IRS;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "your-super-secret-key-at-least-32-characters-long-change-this-in-production",
    "Issuer": "IRS.Api",
    "Audience": "IRS.Client",
    "ExpiryMinutes": 60
  }
}
```

### CORS Configuration

Configured to allow requests from Angular frontend:
- Origin: `http://localhost:4200`
- Credentials: Enabled
- All headers and methods allowed

## 📋 Database Entities

Generated from database schema:

- **Users** - User accounts and authentication
- **Teams** - Research teams
- **TeamMembers** - Team membership
- **TeamRoles** - Role assignments
- **TeamSecrets** - API keys and secrets
- **Roles** - User roles
- **Agents** - AI research agents
- **AgentRuns** - Agent execution history
- **ResearchPages** - Research documents
- **ResearchPageAgents** - Page-agent associations
- **Sections** - Document sections
- **Comments** - Research comments
- **Securities** - Financial securities

## 🛠️ Development

### Add a New Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using IRS.Infrastructure.Data;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MyController : ControllerBase
{
    private readonly IrsDbContext _context;

    public MyController(IrsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        // Implementation
    }
}
```

### Dependency Injection

DbContext is registered in `Program.cs`:

```csharp
builder.Services.AddDbContext<IrsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});
```

## 📝 Next Steps

1. ✅ Database schema deployed
2. ✅ Clean Architecture solution created
3. ✅ EF Core models scaffolded
4. ✅ Solution builds successfully
5. ✅ API running and database connected
6. 🔄 Implement authentication (JWT)
7. 🔄 Build team management endpoints
8. 🔄 Build research platform endpoints
9. 🔄 Build AI agent framework endpoints

## 🔐 Security Notes

- JWT secret key must be changed in production
- Consider using Azure Key Vault or similar for secrets
- Implement proper authentication middleware
- Add authorization policies based on roles

## 📚 References

- [PRD 01: Auth and Team Management](../PRDs/01-Auth-And-Team-Management.md)
- [PRD 02: Core Research Platform](../PRDs/02-Core-Research-Platform.md)
- [PRD 03: AI Agent Framework](../PRDs/03-AI-Agent-Framework.md)
