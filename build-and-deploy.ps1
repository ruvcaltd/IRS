# Build and Deploy Script for Docker Workflows
# This script builds and starts all Docker containers

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Building and Deploying Docker Workflows" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
Write-Host "Checking Docker status..." -ForegroundColor Yellow
try {
    docker ps | Out-Null
    Write-Host "Docker is running!" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Docker Desktop is not running!" -ForegroundColor Red
    Write-Host "Please start Docker Desktop and try again." -ForegroundColor Red
    exit 1
}

# Check if .env file exists
if (-not (Test-Path ".env")) {
    Write-Host "ERROR: .env file not found!" -ForegroundColor Red
    Write-Host "Please copy .env.template to .env and fill in the values." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Stopping any existing containers..." -ForegroundColor Yellow
docker compose down

Write-Host ""
Write-Host "Building Docker images..." -ForegroundColor Yellow
docker compose build --no-cache

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Starting containers..." -ForegroundColor Yellow
docker compose up -d

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to start containers!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Services are starting up. Access them at:" -ForegroundColor Yellow
Write-Host "  - Angular UI:     http://localhost:4203" -ForegroundColor White
Write-Host "  - .NET API:       http://localhost:5000" -ForegroundColor White
Write-Host "  - .NET Swagger:   http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  - Flask API:      http://localhost:5001" -ForegroundColor White
Write-Host "  - Flask Swagger:  http://localhost:5001/swagger" -ForegroundColor White
Write-Host ""
Write-Host "To view logs, run: docker compose logs -f" -ForegroundColor Gray
Write-Host "To stop services, run: docker compose down" -ForegroundColor Gray
