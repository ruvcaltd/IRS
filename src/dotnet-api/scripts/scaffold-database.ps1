param (
    [string]$ConnectionString = "Server=localhost;Database=IRS;Trusted_Connection=True;TrustServerCertificate=True;",
    [string]$OutputDir = "../IRS.Domain/Entities",
    [string]$ContextDir = "Data",
    [string]$ContextName = "IrsDbContext"
)

Write-Host "[IRS] Starting database scaffold process..." -ForegroundColor Green

# Navigate to Infrastructure project
Set-Location -Path (Join-Path $PSScriptRoot "../src/IRS.Infrastructure")

Write-Host "[IRS] Removing existing generated files..." -ForegroundColor Yellow

# Clean up existing generated files
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}

if (Test-Path "$ContextDir/$ContextName.cs") {
    Remove-Item -Path "$ContextDir/$ContextName.cs" -Force
}

Write-Host "[IRS] Scaffolding EF Core models from database..." -ForegroundColor Yellow

# Scaffold models (database-first)
dotnet ef dbcontext scaffold `
    $ConnectionString `
    Microsoft.EntityFrameworkCore.SqlServer `
    --context $ContextName `
    --context-dir $ContextDir `
    --output-dir $OutputDir `
    --use-database-names `
    --no-onconfiguring `
    --data-annotations `
    --force

if ($LASTEXITCODE -eq 0) {
    Write-Host "[IRS] Database scaffold complete!" -ForegroundColor Green
    Write-Host "[IRS] Generated files:" -ForegroundColor Cyan
    Write-Host "  - Entities: $OutputDir" -ForegroundColor Cyan
    Write-Host "  - DbContext: $ContextDir/$ContextName.cs" -ForegroundColor Cyan
} else {
    Write-Host "[IRS] Scaffold failed with exit code $LASTEXITCODE" -ForegroundColor Red
}

# Return to scripts directory
Set-Location -Path $PSScriptRoot
