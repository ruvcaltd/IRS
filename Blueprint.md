# Dockerized Multi-Service Web Application Blueprint

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Repository Folder Structure](#2-repository-folder-structure)
3. [Docker Compose Definition](#3-docker-compose-definition)
4. [Secrets Management](#4-secrets-management)
5. [SSDT Database Project Workflow](#5-ssdt-database-project-workflow)
6. [EF Core Database-First Scaffold (Docker Build Step)](#6-ef-core-database-first-scaffold-docker-build-step)
7. [ASP.NET Core Web API](#7-aspnet-core-web-api)
8. [Python Flask API](#8-python-flask-api)
9. [Angular UI with NSwag Auto-Generated Client](#9-angular-ui-with-nswag-auto-generated-client)
10. [Inter-Service Communication](#10-inter-service-communication)
11. [Schema Change Workflow (End-to-End)](#11-schema-change-workflow-end-to-end)
12. [Development Workflow](#12-development-workflow)
13. [CI/CD Considerations](#13-cicd-considerations)

---

## 1. Architecture Overview

The application is composed of **five Docker containers** running on a shared Docker bridge network (`app-network`). All inter-container communication uses Docker's internal DNS (service names as hostnames). The Angular UI communicates with both APIs directly (no reverse proxy); CORS is configured on both APIs.

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Docker Network: app-network                  │
│                                                                     │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐           │
│  │  angular-ui   │   │  dotnet-api  │   │  flask-api   │           │
│  │  (Nginx/ng)   │   │  (.NET 9)    │   │  (Gunicorn)  │           │
│  │  Port: 4203   │   │  Port: 5000  │   │  Port: 5001  │           │
│  └──────┬───────┘   └──────┬───────┘   └──────┬───────┘           │
│         │  HTTP             │  TCP 1433         │  TCP 1433         │
│         │                   │                   │                   │
│         │            ┌──────┴───────────────────┴──────┐           │
│         │            │         sqlserver               │           │
│         │            │   (SQL Server Express 2022)     │           │
│         │            │         Port: 1433              │           │
│         │            └──────────────┬─────────────────┘           │
│         │                           │                              │
│         │            ┌──────────────┴─────────────────┐           │
│         │            │       db-deploy                 │           │
│         │            │  (SSDT dacpac publisher)        │           │
│         │            │  Runs once, then exits          │           │
│         │            └────────────────────────────────┘           │
│                                                                     │
│  Communication Paths:                                               │
│  • Angular  →  .NET API   (HTTP, NSwag-generated client)           │
│  • Angular  →  Flask API  (HTTP, service wrapper or generated)     │
│  • .NET API →  Flask API  (HTTP, http://flask-api:5001)            │
│  • Flask API → .NET API   (HTTP, http://dotnet-api:5000)          │
│  • .NET API →  SQL Server (TCP, connection string)                 │
│  • Flask API → SQL Server (TCP, pyodbc connection string)          │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Database edition | SQL Server Express 2022 | Free for production (10 GB/db, 1 GB RAM, 4 cores) |
| Schema management | SSDT (.sqlproj) dacpac | Declarative, state-based schema with diff-deploy |
| ORM strategy | EF Core database-first | Schema owned by SSDT; entities auto-scaffolded |
| API client generation | NSwag (RxJS mode) | Angular-idiomatic `Observable` return types |
| Authentication | JWT Bearer tokens | .NET API issues tokens; Flask validates with shared secret |
| Reverse proxy | None | CORS configured on both APIs; simpler setup |
| EF scaffold trigger | Docker build step | Reproducible, tied to exact container DB state |

---

## 2. Repository Folder Structure

```
/
├── docker-compose.yml                 # Production-like compose definition
├── docker-compose.override.yml        # Dev overrides (ports, volumes, hot-reload)
├── .env.template                      # Template for required environment variables
├── .env                               # Actual secrets (GITIGNORED)
├── .gitignore
├── Blueprint.md                       # This document
├── README.md
│
├── src/
│   ├── angular-ui/                    # Angular 19+ standalone application
│   │   ├── Dockerfile
│   │   ├── Dockerfile.dev             # Dev Dockerfile (ng serve with hot-reload)
│   │   ├── angular.json
│   │   ├── package.json
│   │   ├── nswag.json                 # NSwag configuration for TypeScript client gen
│   │   ├── tsconfig.json
│   │   └── src/
│   │       ├── app/
│   │       │   ├── api-client/        # AUTO-GENERATED by NSwag — never hand-edit
│   │       │   │   └── api-client.ts
│   │       │   ├── core/
│   │       │   │   ├── interceptors/
│   │       │   │   │   └── auth.interceptor.ts
│   │       │   │   └── services/
│   │       │   ├── features/
│   │       │   └── shared/
│   │       ├── environments/
│   │       │   ├── environment.ts
│   │       │   └── environment.prod.ts
│   │       └── main.ts
│   │
│   ├── dotnet-api/                    # ASP.NET Core 9 Web API
│   │   ├── Dockerfile
│   │   ├── DotnetApi.sln
│   │   └── DotnetApi/
│   │       ├── DotnetApi.csproj
│   │       ├── Program.cs
│   │       ├── appsettings.json
│   │       ├── Controllers/
│   │       ├── Models/                # AUTO-GENERATED by EF scaffold — never hand-edit
│   │       │   ├── AppDbContext.cs
│   │       │   └── *.cs              # Entity classes
│   │       ├── DTOs/
│   │       ├── Services/
│   │       └── Auth/
│   │
│   ├── flask-api/                     # Python Flask API
│   │   ├── Dockerfile
│   │   ├── requirements.txt
│   │   ├── gunicorn.conf.py
│   │   ├── app/
│   │   │   ├── __init__.py           # Flask app factory
│   │   │   ├── config.py
│   │   │   ├── auth/
│   │   │   │   └── jwt_validator.py
│   │   │   ├── routes/
│   │   │   ├── models/
│   │   │   └── services/
│   │   └── tests/
│   │
│   └── database/
│       ├── MyDb.sqlproj               # SSDT database project (source of truth)
│       ├── MyDb.publish.xml           # Publish profile for dacpac deployment
│       ├── Tables/
│       ├── Views/
│       ├── StoredProcedures/
│       └── Scripts/
│           ├── PostDeployment/        # Seed data, reference data
│           └── PreDeployment/
│
└── tools/
    ├── db-deploy/
    │   ├── Dockerfile                 # Builds dacpac and runs SqlPackage publish
    │   └── deploy.sh                  # Entrypoint: waits for SQL, publishes dacpac
    ├── ef-scaffold/
    │   ├── Dockerfile                 # Scaffolds EF entities from live DB
    │   └── scaffold.ps1               # PowerShell wrapper for scaffold command
    └── nswag/
        └── generate.sh               # Script to regenerate NSwag TypeScript client
```

---

## 3. Docker Compose Definition

### `docker-compose.yml`

```yaml
version: "3.9"

services:
  # ──────────────────────────────────────────────
  # SQL Server Express 2022
  # ──────────────────────────────────────────────
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: sqlserver
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_PID: "Express"              # Free for production use
      MSSQL_SA_PASSWORD: "${SA_PASSWORD}"
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql    # Persist data across container restarts
    networks:
      - app-network
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$${MSSQL_SA_PASSWORD}" -C -Q "SELECT 1" || exit 1
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

  # ──────────────────────────────────────────────
  # Database Deployment (SSDT dacpac)
  # ──────────────────────────────────────────────
  db-deploy:
    build:
      context: ./tools/db-deploy
      dockerfile: Dockerfile
    container_name: db-deploy
    environment:
      SA_PASSWORD: "${SA_PASSWORD}"
      TARGET_SERVER: "sqlserver"
      TARGET_DATABASE: "${DB_NAME}"
    depends_on:
      sqlserver:
        condition: service_healthy
    networks:
      - app-network
    # Exits after deployment completes

  # ──────────────────────────────────────────────
  # ASP.NET Core Web API
  # ──────────────────────────────────────────────
  dotnet-api:
    build:
      context: ./src/dotnet-api
      dockerfile: Dockerfile
    container_name: dotnet-api
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__Default: "Server=sqlserver;Database=${DB_NAME};User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;"
      Jwt__Key: "${JWT_SECRET_KEY}"
      Jwt__Issuer: "${JWT_ISSUER}"
      Jwt__Audience: "${JWT_AUDIENCE}"
      FlaskApi__BaseUrl: "http://flask-api:5001"
    ports:
      - "5000:8080"
    depends_on:
      db-deploy:
        condition: service_completed_successfully
    networks:
      - app-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 15s
      timeout: 5s
      retries: 5

  # ──────────────────────────────────────────────
  # Python Flask API
  # ──────────────────────────────────────────────
  flask-api:
    build:
      context: ./src/flask-api
      dockerfile: Dockerfile
    container_name: flask-api
    environment:
      FLASK_ENV: "development"
      DB_CONNECTION_STRING: "Driver={ODBC Driver 18 for SQL Server};Server=sqlserver;Database=${DB_NAME};Uid=sa;Pwd=${SA_PASSWORD};TrustServerCertificate=yes;"
      JWT_SECRET_KEY: "${JWT_SECRET_KEY}"
      JWT_ISSUER: "${JWT_ISSUER}"
      JWT_AUDIENCE: "${JWT_AUDIENCE}"
      DOTNET_API_BASE_URL: "http://dotnet-api:8080"
    ports:
      - "5001:5001"
    depends_on:
      db-deploy:
        condition: service_completed_successfully
    networks:
      - app-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5001/health"]
      interval: 15s
      timeout: 5s
      retries: 5

  # ──────────────────────────────────────────────
  # Angular UI
  # ──────────────────────────────────────────────
  angular-ui:
    build:
      context: ./src/angular-ui
      dockerfile: Dockerfile
      args:
        API_BASE_URL: "${DOTNET_API_BASE_URL:-http://localhost:5000}"
        FLASK_API_BASE_URL: "${FLASK_API_BASE_URL:-http://localhost:5001}"
    container_name: angular-ui
    ports:
      - "4203:80"
    depends_on:
      - dotnet-api
      - flask-api
    networks:
      - app-network

volumes:
  sqlserver-data:
    driver: local

networks:
  app-network:
    driver: bridge
```

### `docker-compose.override.yml` (Development)

```yaml
version: "3.9"

services:
  dotnet-api:
    build:
      target: development
    volumes:
      - ./src/dotnet-api:/app
    entrypoint: ["dotnet", "watch", "run", "--project", "DotnetApi/DotnetApi.csproj"]

  flask-api:
    volumes:
      - ./src/flask-api:/app
    command: ["flask", "run", "--host=0.0.0.0", "--port=5001", "--reload"]

  angular-ui:
    build:
      context: ./src/angular-ui
      dockerfile: Dockerfile.dev
    volumes:
      - ./src/angular-ui:/app
      - /app/node_modules                # Prevent overwriting container node_modules
    ports:
      - "4203:4203"
    command: ["npx", "ng", "serve", "--host", "0.0.0.0", "--poll", "2000"]
```

### SQL Server Express Limitations

> **Important**: SQL Server Express 2022 is free for production but has hard limits:
> - **10 GB** maximum database size
> - **1 GB** maximum RAM usage
> - **4 CPU cores** maximum
>
> If you exceed these limits, consider upgrading to SQL Server Standard (`MSSQL_PID=Standard`) which requires a paid license key via `MSSQL_PID=<your-product-key>`.

---

## 4. Secrets Management

### Principle

**No secrets are ever baked into Docker images.** All sensitive values are passed at runtime via environment variables sourced from the `.env` file.

### `.env.template` (checked into source control)

```env
# ─── SQL Server ───────────────────────────────
SA_PASSWORD=                             # Strong password required (min 8 chars, mixed case, number, symbol)
DB_NAME=MyAppDb

# ─── JWT Authentication ──────────────────────
JWT_SECRET_KEY=                          # Min 32 characters, cryptographically random
JWT_ISSUER=MyApp
JWT_AUDIENCE=MyApp

# ─── API URLs (for Angular build args) ───────
DOTNET_API_BASE_URL=http://localhost:5000
FLASK_API_BASE_URL=http://localhost:5001
```

### `.env` (GITIGNORED — never committed)

```env
SA_PASSWORD=YourStr0ng!Passw0rd
DB_NAME=MyAppDb
JWT_SECRET_KEY=a-very-long-random-string-at-least-32-characters
JWT_ISSUER=MyApp
JWT_AUDIENCE=MyApp
DOTNET_API_BASE_URL=http://localhost:5000
FLASK_API_BASE_URL=http://localhost:5001
```

### `.gitignore` entry

```gitignore
.env
!.env.template
```

### How secrets flow

```
.env file
  │
  ├──► docker-compose.yml  (env_file or ${VAR} interpolation)
  │       │
  │       ├──► sqlserver        → MSSQL_SA_PASSWORD
  │       ├──► db-deploy        → SA_PASSWORD, TARGET_DATABASE
  │       ├──► dotnet-api       → ConnectionStrings__Default, Jwt__Key
  │       ├──► flask-api        → DB_CONNECTION_STRING, JWT_SECRET_KEY
  │       └──► angular-ui       → Build args only (API URLs, not secrets)
```

---

## 5. SSDT Database Project Workflow

### Overview

The **SSDT `.sqlproj`** is the **single source of truth** for the database schema. It uses a declarative, state-based model: you define the desired end-state of the schema, and `SqlPackage` computes and applies the delta (diff) to the target database.

### Structure

```
src/database/
├── MyDb.sqlproj                   # Project file
├── MyDb.publish.xml               # Publish profile (settings for dacpac deploy)
├── Tables/
│   ├── dbo.Users.sql
│   ├── dbo.Products.sql
│   └── dbo.Orders.sql
├── Views/
│   └── dbo.vw_OrderSummary.sql
├── StoredProcedures/
│   └── dbo.usp_GetUserOrders.sql
└── Scripts/
    ├── PreDeployment/
    │   └── PreDeploy.sql          # Runs before schema changes
    └── PostDeployment/
        └── PostDeploy.sql         # Seed data, reference data
```

### Building the dacpac

The SSDT project compiles into a `.dacpac` file (a portable package of the schema):

```bash
# Using dotnet build (cross-platform, requires Microsoft.Build.Sql SDK)
dotnet build src/database/MyDb.sqlproj -o artifacts/
# Output: artifacts/MyDb.dacpac
```

### `tools/db-deploy/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Install SqlPackage
RUN apt-get update && apt-get install -y unzip curl \
    && curl -L https://aka.ms/sqlpackage-linux -o sqlpackage.zip \
    && unzip sqlpackage.zip -d /opt/sqlpackage \
    && chmod +x /opt/sqlpackage/sqlpackage \
    && rm sqlpackage.zip

# Copy and build the SSDT project
WORKDIR /src
COPY ../../src/database/ ./database/
RUN dotnet build database/MyDb.sqlproj -o /artifacts/

# Deploy stage
FROM mcr.microsoft.com/mssql-tools:latest AS deploy
COPY --from=build /opt/sqlpackage /opt/sqlpackage
COPY --from=build /artifacts/MyDb.dacpac /deploy/MyDb.dacpac
COPY deploy.sh /deploy/deploy.sh
RUN chmod +x /deploy/deploy.sh

ENTRYPOINT ["/deploy/deploy.sh"]
```

### `tools/db-deploy/deploy.sh`

```bash
#!/bin/bash
set -e

echo "Waiting for SQL Server to be ready..."
until /opt/mssql-tools18/bin/sqlcmd -S "$TARGET_SERVER" -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" &>/dev/null; do
    echo "SQL Server not ready yet, retrying in 5s..."
    sleep 5
done

echo "SQL Server is ready. Publishing dacpac..."
/opt/sqlpackage/sqlpackage \
    /Action:Publish \
    /SourceFile:/deploy/MyDb.dacpac \
    /TargetServerName:"$TARGET_SERVER" \
    /TargetDatabaseName:"$TARGET_DATABASE" \
    /TargetUser:sa \
    /TargetPassword:"$SA_PASSWORD" \
    /TargetTrustServerCertificate:True \
    /p:BlockOnPossibleDataLoss=True

echo "Database deployment completed successfully."
```

### Dacpac Diff-Deploy Behaviour

- SqlPackage compares the dacpac (desired state) to the target database (current state)
- It generates and executes only the necessary `ALTER`, `CREATE`, or `DROP` statements
- `/p:BlockOnPossibleDataLoss=True` prevents destructive changes (column drops, type narrowing) unless explicitly handled
- Pre/post-deployment scripts run before/after the schema diff

---

## 6. EF Core Database-First Scaffold (Docker Build Step)

### Overview

After the `db-deploy` container publishes the dacpac, a separate container scaffolds (reverse-engineers) the database schema into EF Core entity classes and a `DbContext`. This ensures entities always match the actual deployed schema.

### `tools/ef-scaffold/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy the .NET API project (needed for scaffold target)
WORKDIR /src
COPY ../../src/dotnet-api/ ./dotnet-api/

WORKDIR /src/dotnet-api/DotnetApi

# Install required EF and SQL Server packages
RUN dotnet restore

COPY scaffold.ps1 /tools/scaffold.ps1

ENTRYPOINT ["pwsh", "/tools/scaffold.ps1"]
```

### `tools/ef-scaffold/scaffold.ps1`

```powershell
#!/usr/bin/env pwsh
param(
    [string]$Server = $env:DB_SERVER ?? "sqlserver",
    [string]$Database = $env:DB_NAME ?? "MyAppDb",
    [string]$User = "sa",
    [string]$Password = $env:SA_PASSWORD
)

$connectionString = "Server=$Server;Database=$Database;User Id=$User;Password=$Password;TrustServerCertificate=True;"

Write-Host "Scaffolding EF Core entities from: $Server/$Database"

dotnet ef dbcontext scaffold `
    $connectionString `
    Microsoft.EntityFrameworkCore.SqlServer `
    --output-dir Models `
    --context AppDbContext `
    --context-dir Models `
    --force `
    --no-onconfiguring `
    --verbose

Write-Host "Scaffold complete. Entities written to Models/"
```

### Docker Compose — One-Off Scaffold Command

```bash
# Run scaffold after db-deploy has completed
docker compose run --rm \
  -e SA_PASSWORD=$SA_PASSWORD \
  -e DB_NAME=MyAppDb \
  -v ./src/dotnet-api/DotnetApi/Models:/src/dotnet-api/DotnetApi/Models \
  ef-scaffold
```

Add the service to `docker-compose.yml` (or a separate `docker-compose.tools.yml`):

```yaml
  ef-scaffold:
    build:
      context: ./tools/ef-scaffold
      dockerfile: Dockerfile
    environment:
      SA_PASSWORD: "${SA_PASSWORD}"
      DB_NAME: "${DB_NAME}"
      DB_SERVER: "sqlserver"
    volumes:
      - ./src/dotnet-api/DotnetApi/Models:/src/dotnet-api/DotnetApi/Models
    depends_on:
      db-deploy:
        condition: service_completed_successfully
    networks:
      - app-network
    profiles:
      - tools    # Only runs when explicitly invoked
```

### Important Rules

- The `Models/` directory is **auto-generated** — never hand-edit files in it
- Generated classes are committed to source control so the API can build without a live DB
- `--force` overwrites all existing entity files
- `--no-onconfiguring` omits the connection string from the generated `DbContext` (it's injected via DI)

---

## 7. ASP.NET Core Web API

### Technology Stack

| Component | Choice |
|---|---|
| Framework | .NET 9 / ASP.NET Core |
| ORM | EF Core 9 (database-first, scaffolded) |
| Auth | JWT Bearer tokens (issues and validates) |
| API docs | Swashbuckle (Swagger / OpenAPI 3.0) |
| Health check | `/health` endpoint |

### Key Configuration — `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// ─── EF Core (connection string from environment variable) ───
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ─── JWT Authentication ───
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// ─── CORS (allow Angular dev server) ───
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4203")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─── Swagger / OpenAPI ───
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DotnetApi", Version = "v1" });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "DotnetApi.xml"));
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { /* ... */ });
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("Default")!);

var app = builder.Build();

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

### Dockerfile (Multi-Stage)

```dockerfile
# ── Build stage ──
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY DotnetApi.sln .
COPY DotnetApi/DotnetApi.csproj DotnetApi/
RUN dotnet restore
COPY . .
RUN dotnet publish DotnetApi/DotnetApi.csproj -c Release -o /app/publish

# ── Development stage (used by docker-compose.override) ──
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /app
COPY . .
RUN dotnet restore
EXPOSE 8080
# Entrypoint overridden by docker-compose.override.yml

# ── Production stage ──
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "DotnetApi.dll"]
```

### Swagger Requirements for NSwag

For NSwag to generate a high-quality TypeScript client, the .NET API **must**:

1. Enable **XML documentation** in `.csproj`: `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
2. Add **XML doc comments** on all controllers and action methods
3. Use **`[ProducesResponseType]`** attributes to declare all possible response types
4. Use **strongly-typed DTOs** (not anonymous objects) for request/response bodies
5. Ensure the Swagger endpoint is available at: `http://dotnet-api:8080/swagger/v1/swagger.json`

---

## 8. Python Flask API

### Technology Stack

| Component | Choice |
|---|---|
| Framework | Flask 3.x |
| WSGI server | Gunicorn |
| DB driver | pyodbc + ODBC Driver 18 for SQL Server |
| Auth | JWT validation (verifies tokens issued by .NET API) |
| API docs | flask-smorest (OpenAPI 3.0) |
| CORS | flask-cors |
| Health check | `/health` endpoint |

### App Factory — `app/__init__.py`

```python
from flask import Flask
from flask_cors import CORS
from flask_smorest import Api

def create_app():
    app = Flask(__name__)

    # Load config from environment variables
    app.config.from_object('app.config.Config')

    # CORS — allow Angular dev server
    CORS(app, origins=["http://localhost:4203"], supports_credentials=True)

    # OpenAPI / Swagger via flask-smorest
    api = Api(app)

    # Register blueprints (routes)
    from app.routes import items_bp, health_bp
    api.register_blueprint(items_bp)
    api.register_blueprint(health_bp)

    return app
```

### Configuration — `app/config.py`

```python
import os

class Config:
    DB_CONNECTION_STRING = os.environ.get('DB_CONNECTION_STRING')
    JWT_SECRET_KEY = os.environ.get('JWT_SECRET_KEY')
    JWT_ISSUER = os.environ.get('JWT_ISSUER', 'MyApp')
    JWT_AUDIENCE = os.environ.get('JWT_AUDIENCE', 'MyApp')
    DOTNET_API_BASE_URL = os.environ.get('DOTNET_API_BASE_URL', 'http://dotnet-api:8080')

    # flask-smorest settings
    API_TITLE = "Flask API"
    API_VERSION = "v1"
    OPENAPI_VERSION = "3.0.3"
    OPENAPI_URL_PREFIX = "/"
    OPENAPI_SWAGGER_UI_PATH = "/swagger"
    OPENAPI_SWAGGER_UI_URL = "https://cdn.jsdelivr.net/npm/swagger-ui-dist/"
```

### JWT Validation — `app/auth/jwt_validator.py`

```python
import jwt
from functools import wraps
from flask import request, jsonify, current_app

def require_auth(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        token = request.headers.get('Authorization', '').replace('Bearer ', '')
        if not token:
            return jsonify({'error': 'Missing token'}), 401
        try:
            payload = jwt.decode(
                token,
                current_app.config['JWT_SECRET_KEY'],
                algorithms=['HS256'],
                issuer=current_app.config['JWT_ISSUER'],
                audience=current_app.config['JWT_AUDIENCE']
            )
            request.user = payload
        except jwt.ExpiredSignatureError:
            return jsonify({'error': 'Token expired'}), 401
        except jwt.InvalidTokenError as e:
            return jsonify({'error': f'Invalid token: {str(e)}'}), 401
        return f(*args, **kwargs)
    return decorated
```

### Dockerfile

```dockerfile
FROM python:3.12-slim

# Install ODBC Driver 18 for SQL Server
RUN apt-get update && apt-get install -y \
    curl gnupg2 apt-transport-https \
    && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
    && curl https://packages.microsoft.com/config/debian/12/prod.list | tee /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y msodbcsql18 unixodbc-dev \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 5001

CMD ["gunicorn", "--config", "gunicorn.conf.py", "app:create_app()"]
```

### `gunicorn.conf.py`

```python
bind = "0.0.0.0:5001"
workers = 4
timeout = 120
accesslog = "-"
errorlog = "-"
```

---

## 9. Angular UI with NSwag Auto-Generated Client

### Technology Stack

| Component | Choice |
|---|---|
| Framework | Angular 19+ (standalone components) |
| State | Angular Signals |
| HTTP client | NSwag-generated TypeScript client (RxJS Observables) |
| Styling | SASS |
| Build | Angular CLI |

### Critical Rule: No Direct `HttpClient` Usage

> **All HTTP calls to the .NET API MUST go through the NSwag-generated client.**
> Using `HttpClient` directly in components or services is **prohibited**.
> The only place `HttpClient` is used directly is inside the auto-generated `api-client.ts` file (which NSwag creates) and in the `AuthInterceptor`.

### NSwag Configuration — `nswag.json`

```json
{
  "runtime": "Net90",
  "documentGenerator": {
    "fromDocument": {
      "url": "http://localhost:5000/swagger/v1/swagger.json"
    }
  },
  "codeGenerators": {
    "openApiToTypeScriptClient": {
      "className": "ApiClient",
      "moduleName": "",
      "namespace": "",
      "typeScriptVersion": 5.0,
      "template": "Angular",
      "promiseType": "Promise",
      "httpClass": "HttpClient",
      "withCredentials": false,
      "useSingletonProvider": true,
      "injectionTokenType": "InjectionToken",
      "rxJsVersion": 7.0,
      "dateTimeType": "Date",
      "nullValue": "Undefined",
      "generateClientClasses": true,
      "generateClientInterfaces": false,
      "generateOptionalParameters": true,
      "exportTypes": true,
      "wrapDtoExceptions": false,
      "clientBaseClass": null,
      "wrapResponses": false,
      "generateResponseClasses": true,
      "responseClass": "SwaggerResponse",
      "protectedMethods": [],
      "configurationClass": null,
      "useTransformOptionsMethod": false,
      "useTransformResultMethod": false,
      "generateDtoTypes": true,
      "operationGenerationMode": "MultipleClientsFromOperationId",
      "markOptionalProperties": true,
      "generateCloneMethod": false,
      "typeStyle": "Interface",
      "enumStyle": "Enum",
      "useLeafType": false,
      "classTypes": [],
      "extendedClasses": [],
      "extensionCode": null,
      "generateDefaultValues": true,
      "excludedTypeNames": [],
      "handleReferences": false,
      "generateConstructorInterface": true,
      "importRequiredTypes": true,
      "baseUrlTokenName": "API_BASE_URL",
      "output": "src/app/api-client/api-client.ts"
    }
  }
}
```

### Key NSwag Settings Explained

| Setting | Value | Purpose |
|---|---|---|
| `template` | `Angular` | Generates Angular-compatible service using `HttpClient` |
| `rxJsVersion` | `7.0` | Returns `Observable<T>` instead of `Promise<T>` |
| `operationGenerationMode` | `MultipleClientsFromOperationId` | Creates separate client class per controller |
| `typeStyle` | `Interface` | DTOs as TypeScript interfaces (immutable-friendly) |
| `baseUrlTokenName` | `API_BASE_URL` | Allows runtime injection of API base URL |

### NPM Script for Client Generation

In `package.json`:

```json
{
  "scripts": {
    "generate-api": "nswag run nswag.json /runtime:Net90",
    "prestart": "npm run generate-api"
  }
}
```

### Registering the Generated Client

In `app.config.ts`:

```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { API_BASE_URL } from './api-client/api-client';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { environment } from '../environments/environment';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
  ],
};
```

### Auth Interceptor — `core/interceptors/auth.interceptor.ts`

```typescript
import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('auth_token');

  if (token) {
    const clonedReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });
    return next(clonedReq);
  }

  return next(req);
};
```

### Example Usage in a Component

```typescript
import { Component, inject, signal } from '@angular/core';
import { ProductsClient, ProductDto } from '../../api-client/api-client';

@Component({
  selector: 'app-product-list',
  standalone: true,
  template: `
    @for (product of products(); track product.id) {
      <div>{{ product.name }} — {{ product.price | currency }}</div>
    }
  `,
})
export class ProductListComponent {
  private readonly productsClient = inject(ProductsClient);
  protected readonly products = signal<ProductDto[]>([]);

  constructor() {
    this.productsClient.getAll().subscribe({
      next: (data) => this.products.set(data),
      error: (err) => console.error('Failed to load products', err),
    });
  }
}
```

### Angular Conventions (from `.instructions.md`)

- **Standalone components** — no `NgModule`
- **Signals** for reactive state management
- **`inject()`** for dependency injection
- **`async` pipe** for observables in templates (or `.subscribe()` with signals)
- **Kebab-case file names**: `product-list.component.ts`
- **No `any` types** — use generated interfaces from NSwag
- **Single quotes**, 2-space indentation
- Lazy-loaded feature routes
- `NgOptimizedImage` for images
- Deferrable views for non-critical components

### Dockerfile (Production)

```dockerfile
# ── Build stage ──
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration=production

# ── Serve stage ──
FROM nginx:alpine
COPY --from=build /app/dist/angular-ui/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### Dockerfile.dev (Development with Hot-Reload)

```dockerfile
FROM node:22-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
EXPOSE 4203
CMD ["npx", "ng", "serve", "--host", "0.0.0.0", "--poll", "2000"]
```

---

## 10. Inter-Service Communication

### Communication Matrix

| From | To | URL (Dev) | URL (Docker Internal) | Protocol |
|---|---|---|---|---|
| Angular UI | .NET API | `http://localhost:5000` | N/A (browser-based) | HTTP/REST |
| Angular UI | Flask API | `http://localhost:5001` | N/A (browser-based) | HTTP/REST |
| .NET API | Flask API | N/A | `http://flask-api:5001` | HTTP/REST |
| Flask API | .NET API | N/A | `http://dotnet-api:8080` | HTTP/REST |
| .NET API | SQL Server | N/A | `sqlserver,1433` | TCP/TDS |
| Flask API | SQL Server | N/A | `sqlserver,1433` | TCP/ODBC |

### CORS Configuration

Since there is **no reverse proxy**, both APIs must configure CORS to allow the Angular origin.

**.NET API CORS** (in `Program.cs`):

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4203"    // Angular dev server
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

**Flask API CORS** (in `app/__init__.py`):

```python
CORS(app, origins=["http://localhost:4203"], supports_credentials=True)
```

### Inter-API Communication

When the .NET API needs to call the Flask API (or vice versa), they use Docker's internal DNS:

**.NET → Flask** (using `IHttpClientFactory`):

```csharp
// Registered in Program.cs
builder.Services.AddHttpClient("FlaskApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FlaskApi:BaseUrl"]!);
});

// Used in a service
public class FlaskApiService
{
    private readonly HttpClient _client;

    public FlaskApiService(IHttpClientFactory factory)
    {
        _client = factory.CreateClient("FlaskApi");
    }

    public async Task<string> GetPredictionAsync(int modelId)
    {
        var response = await _client.GetAsync($"/api/predictions/{modelId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

**Flask → .NET** (using `requests`):

```python
import requests
from flask import current_app

def get_user_from_dotnet(user_id: int) -> dict:
    base_url = current_app.config['DOTNET_API_BASE_URL']
    response = requests.get(f"{base_url}/api/users/{user_id}")
    response.raise_for_status()
    return response.json()
```

---

## 11. Schema Change Workflow (End-to-End)

This is the **critical workflow** that must be followed every time a database schema change is needed. It ensures consistency across the SSDT project, EF entities, Swagger spec, and Angular client.

### Workflow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SCHEMA CHANGE WORKFLOW                            │
│                                                                     │
│  Step 1          Step 2          Step 3          Step 4             │
│  ┌──────┐       ┌──────┐       ┌──────┐       ┌──────┐            │
│  │ Edit │──────►│Build │──────►│Deploy│──────►│Scaff-│            │
│  │ SSDT │       │dacpac│       │dacpac│       │old EF│            │
│  │.sql  │       │      │       │to SQL│       │models│            │
│  └──────┘       └──────┘       └──────┘       └──────┘            │
│                                                    │                │
│  Step 5          Step 6          Step 7            │                │
│  ┌──────┐       ┌──────┐       ┌──────┐           │                │
│  │Update│◄──────│Regen │◄──────│Update│◄──────────┘                │
│  │Angu- │       │NSwag │       │.NET  │                            │
│  │lar UI│       │client│       │DTOs/ │                            │
│  └──────┘       └──────┘       │Ctrls │                            │
│      │                         └──────┘                            │
│      ▼                                                             │
│  Step 8: Commit ALL changes in one PR                              │
│  Step 9: CI validates schema ↔ entities ↔ swagger ↔ client parity │
└─────────────────────────────────────────────────────────────────────┘
```

### Step-by-Step Checklist

#### Step 1 — Modify the SSDT Project

```
Edit the relevant .sql file in src/database/
Example: Add a new column to dbo.Products
```

```sql
-- src/database/Tables/dbo.Products.sql
CREATE TABLE [dbo].[Products]
(
    [Id]          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name]        NVARCHAR(200)     NOT NULL,
    [Price]       DECIMAL(18,2)     NOT NULL,
    [Description] NVARCHAR(MAX)     NULL,       -- ← NEW COLUMN
    [CreatedAt]   DATETIME2         NOT NULL DEFAULT GETUTCDATE()
);
```

#### Step 2 — Build the dacpac

```bash
dotnet build src/database/MyDb.sqlproj -o artifacts/
# Validates the schema compiles without errors
```

#### Step 3 — Deploy dacpac to SQL Server

```bash
# Option A: Using docker compose
docker compose up db-deploy --build

# Option B: Direct SqlPackage (if installed locally)
sqlpackage /Action:Publish \
  /SourceFile:artifacts/MyDb.dacpac \
  /TargetConnectionString:"Server=localhost;Database=MyAppDb;User Id=sa;Password=YourStr0ng!Passw0rd;TrustServerCertificate=True;"
```

#### Step 4 — Regenerate EF Core Entities

```bash
# Using the Docker build step
docker compose run --rm ef-scaffold

# This overwrites src/dotnet-api/DotnetApi/Models/ with fresh entities
```

#### Step 5 — Update .NET API Code

- Review regenerated entities in `Models/`
- Update DTOs in `DTOs/` if the new column should be exposed
- Update controller actions if new endpoints are needed
- Add `[ProducesResponseType]` attributes for new response shapes
- Ensure XML doc comments are present (required for NSwag quality)
- Run the .NET API to update the Swagger spec

#### Step 6 — Regenerate NSwag TypeScript Client

```bash
cd src/angular-ui
npm run generate-api
# Regenerates src/app/api-client/api-client.ts
```

#### Step 7 — Update Angular UI

- Use the newly generated types/methods from the `ApiClient`
- Update components, services, forms as needed
- **Never import `HttpClient` directly** — use the generated client

#### Step 8 — Commit as One PR

All changes go in a **single pull request**:
- `src/database/Tables/dbo.Products.sql` (schema change)
- `src/dotnet-api/DotnetApi/Models/*.cs` (regenerated entities)
- `src/dotnet-api/DotnetApi/DTOs/*.cs` (updated DTOs)
- `src/dotnet-api/DotnetApi/Controllers/*.cs` (updated controllers)
- `src/angular-ui/src/app/api-client/api-client.ts` (regenerated client)
- `src/angular-ui/src/app/features/**` (updated components)

#### Step 9 — CI Validates Parity

CI pipeline checks:
1. Build SSDT → publish to test SQL container → scaffold EF entities → `git diff --exit-code Models/`
2. Start .NET API → fetch swagger.json → run NSwag → `git diff --exit-code api-client/`
3. If either diff is non-empty, **CI fails** (entities or client are stale)

---

## 12. Development Workflow

### First-Time Setup

```bash
# 1. Clone the repository
git clone <repo-url>
cd <repo>

# 2. Copy the environment template and fill in secrets
cp .env.template .env
# Edit .env with your passwords and keys

# 3. Start the full stack
docker compose up --build

# Wait for:
#   - sqlserver to pass health check (~30s)
#   - db-deploy to publish dacpac and exit
#   - dotnet-api to start on port 5000
#   - flask-api to start on port 5001
#   - angular-ui to serve on port 4203
```

### Daily Development

```bash
# Start all services (uses override for hot-reload)
docker compose up

# Angular:  http://localhost:4203  (hot-reload via ng serve)
# .NET API: http://localhost:5000  (hot-reload via dotnet watch)
# Flask:    http://localhost:5001  (hot-reload via --reload)
# SQL:      localhost,1433         (connect with SSMS/Azure Data Studio)
# Swagger:  http://localhost:5000/swagger
```

### Useful Commands

```bash
# Rebuild a specific service
docker compose up --build dotnet-api

# View logs for a specific service
docker compose logs -f flask-api

# Run EF scaffold (after schema change)
docker compose run --rm --profile tools ef-scaffold

# Regenerate NSwag client (after API changes)
cd src/angular-ui && npm run generate-api

# Stop everything
docker compose down

# Stop everything and delete data volumes (reset DB)
docker compose down -v

# Access SQL Server from host
sqlcmd -S localhost,1433 -U sa -P "YourStr0ng!Passw0rd" -C
```

### Host Ports Summary

| Service | Host Port | Container Port |
|---|---|---|
| Angular UI | 4203 | 80 (prod) / 4203 (dev) |
| .NET API | 5000 | 8080 |
| Flask API | 5001 | 5001 |
| SQL Server Express | 1433 | 1433 |

---

## 13. CI/CD Considerations

### Pipeline Stages

```
┌──────┐   ┌──────┐   ┌──────┐   ┌──────┐   ┌──────┐   ┌──────┐
│Build │──►│Deploy│──►│Scaff-│──►│ Test │──►│Parity│──►│ Push │
│Images│   │ DB   │   │old + │   │      │   │Check │   │Images│
│      │   │      │   │NSwag │   │      │   │      │   │      │
└──────┘   └──────┘   └──────┘   └──────┘   └──────┘   └──────┘
```

### Stage Details

1. **Build Images** — Build all Docker images (`sqlserver` is pulled, not built)
2. **Deploy DB** — Start SQL Server Express container, run `db-deploy` to publish dacpac
3. **Scaffold + NSwag** — Run EF scaffold, start .NET API, run NSwag generation
4. **Test** — Run unit tests for .NET (`dotnet test`), Flask (`pytest`), Angular (`ng test`)
5. **Parity Check** — Verify no uncommitted diffs in `Models/` or `api-client/`
6. **Push Images** — Tag and push images to container registry

### Parity Check Script

```bash
#!/bin/bash
set -e

echo "Checking EF entity parity..."
docker compose run --rm ef-scaffold
if ! git diff --exit-code src/dotnet-api/DotnetApi/Models/; then
    echo "ERROR: EF entities are stale. Run scaffold and commit."
    exit 1
fi

echo "Checking NSwag client parity..."
cd src/angular-ui && npm run generate-api
if ! git diff --exit-code src/app/api-client/; then
    echo "ERROR: NSwag client is stale. Run generate-api and commit."
    exit 1
fi

echo "All parity checks passed."
```

### Environment Strategy

| Environment | SQL Server | Secrets Source | Images |
|---|---|---|---|
| Local dev | Docker container (Express) | `.env` file | Built locally |
| CI | Docker container (Express) | CI/CD secret variables | Built in pipeline |
| Staging | Managed SQL / Docker | Key vault / secret store | Registry images |
| Production | Managed SQL | Key vault / secret store | Registry images |

---

## Appendix A — `.gitignore` Essentials

```gitignore
# Secrets
.env
!.env.template

# .NET
**/bin/
**/obj/
*.user

# Python
__pycache__/
*.pyc
.venv/

# Angular
node_modules/
dist/
.angular/

# Docker
docker-compose.override.yml   # Optional: if each dev customizes
```

## Appendix B — Environment Files

### `src/angular-ui/src/environments/environment.ts`

```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000',
  flaskApiBaseUrl: 'http://localhost:5001',
};
```

### `src/angular-ui/src/environments/environment.prod.ts`

```typescript
export const environment = {
  production: true,
  apiBaseUrl: '/api',           // Or the production API URL
  flaskApiBaseUrl: '/pyapi',    // Or the production Flask URL
};
```

## Appendix C — Flask `requirements.txt`

```
Flask>=3.0
gunicorn>=22.0
flask-cors>=4.0
flask-smorest>=0.44
pyodbc>=5.1
PyJWT>=2.8
requests>=2.31
marshmallow>=3.20
python-dotenv>=1.0
```

## Appendix D — .NET NuGet Packages

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.*" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.*" />
  <PackageReference Include="Swashbuckle.AspNetCore" Version="7.*" />
  <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="9.0.*" />
  <PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="8.*" />
</ItemGroup>
```
