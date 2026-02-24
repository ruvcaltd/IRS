# Work Log

## 2026-02-20 - Initial Sample Implementation

### Build and Deploy Setup
- Created database initialization script (init-db.sh)
- Added db-init service to docker-compose.yml for automatic database setup
- Created build-and-deploy scripts (PowerShell and Bash)
- Removed obsolete version attribute from docker-compose files
- Fixed Flask route imports in __init__.py
- Created .env file with sample values
- Fixed .NET API Dockerfile path in docker-compose.yml (DotnetApi/Dockerfile)
- Changed Angular Dockerfiles from npm ci to npm install (no package-lock.json)
- Fixed database initialization using SQL Server image with inline bash commands
- Successfully built and deployed all Docker containers
- Verified .NET API and Flask API are healthy and responding
- Changed Angular UI host port from 4200 to 4203 (docker-compose, CORS in .NET and Flask, README, build scripts)

## 2026-02-20 - Initial Sample Implementation

### Created Flask API
- Added Flask application with app factory pattern
- Created `/api/pi/hello` endpoint that returns hello world message with Pi value
- Added health check endpoint
- Configured CORS for Angular frontend
- Set up Dockerfile with Gunicorn

### Created .NET API
- Set up ASP.NET Core 9 Web API project
- Created `PiController` that calls Flask API's Pi endpoint
- Created `UsersController` with full CRUD operations (GET, POST, PUT, DELETE)
- Added DTOs for Pi response and User operations
- Configured EF Core with AppDbContext and User model
- Set up Swagger/OpenAPI documentation
- Configured CORS for Angular frontend
- Added HTTP client service for Flask API communication

### Created Angular UI
- Set up Angular 19 standalone application
- Created Home component that calls .NET API for Pi value
- Created Users component with user management interface (list, add, delete)
- Configured NSwag for API client generation
- Set up routing and navigation
- Added auth interceptor (placeholder for JWT)

### Created Database Schema
- Added Users table definition with Id, UserName, and CreatedAt columns

### Created Docker Configuration
- Created docker-compose.yml with all services
- Created docker-compose.override.yml for development hot-reload
- Configured environment variables template
- Set up Docker network for inter-service communication

### Created Documentation
- Added comprehensive README.md with setup instructions
- Created worklog.md for tracking changes
