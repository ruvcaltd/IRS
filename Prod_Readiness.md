# Production Readiness Report

**Date:** February 24, 2026  
**Status:** ⚠️ NOT READY FOR PRODUCTION  
**Severity:** CRITICAL - Multiple security vulnerabilities must be resolved before deploying to DockerHub

---

## Executive Summary

The current Docker configuration contains multiple critical security issues that will expose sensitive credentials when committed to GitHub or pushed to DockerHub. Images will fail in production due to hardcoded development configurations and exposed secrets. This report identifies all issues and provides remediation tasks.

---

## 🔴 CRITICAL ISSUES - MUST FIX BEFORE PRODUCTION

### 1. **Hardcoded Secrets in appsettings Files**

**Severity:** 🔴 CRITICAL  
**Status:** ❌ LEAKED CREDENTIALS

#### Issue:
The following files contain actual secrets hardcoded in the repository:
- `src/dotnet-api/src/IRS.Api/appsettings.Development.json`
- `src/dotnet-api/src/IRS.Api/appsettings.Test.json`
- `src/dotnet-api/src/IRS.Api.IntegrationTests/appsettings.Test.json`

#### Contents with Secrets:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=IRS;User Id=sa;Password=YourStr0ng!Passw0rd123;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "your-secret-key-minimum-32-characters-long-for-jwt-signing"
  },
  "OpenFigi": {
    "ApiKey": "012c5587-ae3c-47fa-b783-97bc0d4962e0"
  },
  "LlmEncryption": {
    "Key": "change-this-to-a-secure-32-character-key-in-production-environment"
  },
  "Encryption": {
    "Key": "TXlTZWNyZXRLZXkxMjM0NU15U2VjcmV0S2V5MTIzNDU=",
    "IV": "TXlJbml0VmVjdG9yMTIzNA=="
  }
}
```

#### Impact:
- 🔓 **EXPOSED API KEYS:** OpenFigi API key is compromised
- 🔓 **DATABASE CREDENTIALS:** SQL Server password is visible
- 🔓 **ENCRYPTION KEYS:** All encryption keys are exposed
- 📤 **WHEN COMMITTED:** Secrets will be permanently in GitHub history
- 📦 **WHEN PUSHED:** All DockerHub users can extract credentials from image

#### Why Production Will Fail:
- These hardcoded credentials won't exist in production
- Applications will fail to start if they rely on these specific values
- The appsettings.Development.json uses localhost which won't work in containerized environments

---

### 2. **Missing Production appsettings File**

**Severity:** 🔴 CRITICAL  
**Status:** ❌ NOT CONFIGURED

#### Issue:
No `appsettings.Production.json` exists with environment variable placeholders.

#### Impact:
- Production image will use appsettings.json with empty credential values
- Application will fail to authenticate to database
- API will crash on startup
- Web service will be completely non-functional

---

### 3. **ASPNETCORE_ENVIRONMENT Set to Development**

**Severity:** 🔴 CRITICAL  
**Status:** ❌ WRONG CONFIGURATION

#### Issue:
In `docker-compose.yml`:
```yaml
dotnet-api:
  environment:
    ASPNETCORE_ENVIRONMENT: "Development"  # ❌ WRONG FOR PRODUCTION
```

#### Impact:
- ASP.NET Core will load appsettings.Development.json
- Swagger UI will be enabled (security risk)
- Debug error pages will be exposed
- Performance will be degraded

---

### 4. **.gitignore Does Not Exclude appsettings Properly**

**Severity:** 🔴 CRITICAL  
**Status:** ❌ INCOMPLETE

#### Issue:
Current `.gitignore` doesn't exclude development appsettings:
```
.env
!.env.template
```

But doesn't have:
```
appsettings.Development.json
appsettings.Test.json
```

#### Impact:
- Development configuration files with secrets will be committed
- Once committed, secrets are in git history forever
- Rewriting history is complex and error-prone
- All DockerHub users can access previous commit secrets

---

### 5. **dockerfile.dev Included in Production Build Path**

**Severity:** 🟡 HIGH  
**Status:** ❌ SECURITY RISK

#### Issue:
`src/angular-ui/Dockerfile.dev` exists alongside `Dockerfile`:
```dockerfile
FROM node:22-alpine
WORKDIR /app
COPY . .
# ... development build commands
```

#### Impact:
- If wrong Dockerfile is built, includes development dependencies
- No build optimization for production
- Development tools shipping with production image
- Image size unnecessarily large (~1.5GB vs 200MB optimized)

---

### 6. **SQL Server Port Exposed Publicly**

**Severity:** 🔴 CRITICAL  
**Status:** ❌ NETWORK EXPOSURE

#### Issue:
In `docker-compose.yml`:
```yaml
sqlserver:
  ports:
    - "1433:1433"  # ❌ EXPOSED TO INTERNET
```

#### Impact:
- Database is directly accessible from internet
- Attackers can attempt brute force attacks
- Credentials visible in image/compose logs
- In production, database should NOT be exposed
- Only internal services should access database

---

### 7. **Environment Variables Visible in Image Metadata**

**Severity:** 🟡 HIGH  
**Status:** ❌ EXPOSED IN INSPECT

#### Issue:
The `docker-compose.yml` passes secrets via environment variables:
```yaml
environment:
  SA_PASSWORD: "${SA_PASSWORD}"
  JWT_SECRET_KEY: "${JWT_SECRET_KEY}"
  ConnectionStrings__DefaultConnection: "Server=sqlserver;...Password=${SA_PASSWORD}..."
```

#### Impact:
- Secrets visible via: `docker inspect <container_id>`
- Secrets visible via: `docker history <image_id>`
- All image layers contain environment variables
- Kubernetes would log these in events
- Any user with Docker access can extract credentials

---

### 8. **Debug/Development Stages in Multi-Stage Build**

**Severity:** 🟡 HIGH  
**Status:** ⚠️ UNNECESSARY

#### Issue:
In `src/dotnet-api/Dockerfile`:
```dockerfile
# Development stage - SHOULD NOT BE IN PRODUCTION IMAGE
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /app
COPY . .  # ❌ Full source code
# Entrypoint overridden by docker-compose.override.yml
```

#### Impact:
- If production uses this stage by mistake, includes full SDK
- Source code could be exposed in image layers
- Drastically increases image size (multi-GB)
- Build context includes all development files

---

### 9. **No Non-Root User in Containers**

**Severity:** 🟡 HIGH  
**Status:** ❌ SECURITY BEST PRACTICE

#### Issue:
No `USER` directive in Dockerfiles - containers run as root:
```dockerfile
# Missing:
RUN addgroup -g 1001 -S nodejs || true
RUN adduser -S nodejs -u 1001
USER nodejs
```

#### Impact:
- Compromised process has root access
- Can modify container file system
- Can access all container resources
- Security best practice violation
- Kubernetes security policies will reject

---

### 10. **Base Images Not Pinned to Specific Versions**

**Severity:** 🟡 HIGH  
**Status:** ❌ INCONSISTENCY RISK

#### Issue:
Using floating versions:
```dockerfile
FROM node:22-alpine          # ❌ Will change
FROM python:3.12-slim        # ❌ Will change
FROM mcr.microsoft.com/dotnet/sdk:9.0      # ❌ Will change
FROM mcr.microsoft.com/mssql/server:2022-latest  # ❌ DANGEROUS
```

#### Impact:
- Different builds on different dates use different base images
- Security patches may break compatibility
- No reproducible builds
- SQL Server "latest" tag is especially dangerous
- For MSSQL, "latest" could jump multiple major versions

---

## 🟡 MEDIUM ISSUES - FIX BEFORE PRODUCTION

### 11. **Flask API Does Not Validate HTTPS**

**Severity:** 🟡 MEDIUM  
**Status:** ⚠️ INSECURE

#### Issue:
In `src/flask-api/Dockerfile`:
```dockerfile
RUN apt-get update && apt-get install -y \
    curl gnupg2 apt-transport-https \
    # ... missing: && curl https://... curl -fsSL (no cert validation flag)
```

#### Impact:
- Curl downloads without strict SSL verification
- Vulnerable to MITM attacks during build
- Could install compromised packages

---

### 12. **No Health Checks in All Services**

**Severity:** 🟡 MEDIUM  
**Status:** ⚠️ INCOMPLETE

#### Issue:
SQL Server has health check:
```yaml
sqlserver:
  healthcheck:
    test: /opt/mssql-tools18/bin/sqlcmd -S localhost...
```

But Flask API health check is brittle:
```yaml
flask-api:
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:5001/health"]
    # curl might not be available in production
```

#### Impact:
- Containers may appear healthy but be non-functional
- Pod readiness probes will fail if curl is missing
- No way to detect startup failures

---

### 13. **No Secret Rotation Strategy**

**Severity:** 🟡 MEDIUM  
**Status:** ❌ NO PLAN

#### Impact:
- Secrets are hard-coded in .env
- No automated rotation mechanism
- No way to update secrets without rebuilding images
- Compromised credentials require image rebuild and redeploy

---

### 14. **No Image Scanning Configuration**

**Severity:** 🟡 MEDIUM  
**Status:** ❌ NO PLAN

#### Impact:
- Pushed images won't be scanned for vulnerabilities
- Vulnerable dependencies won't be detected
- Package versions not pinned for reproducibility

---

### 15. **Angular UI Default Port Conflicts**

**Severity:** 🟡 MEDIUM  
**Status:** ⚠️ INCONSISTENT

#### Issue:
In `docker-compose.yml`:
```yaml
angular-ui:
  ports:
    - "80:80"
```

But Dockerfile might expect different port.

#### Impact:
- Port 80 might require elevated privileges
- Nginx configuration assumes port 80
- Difficult to run multiple instances

---

## 🟢 GOOD PRACTICES ALREADY IN PLACE

✅ **Multi-stage Docker builds** - Development and production separated  
✅ **Layer caching optimization** - Dependencies cached separately  
✅ **.env file template** - `.env.template` exists for configuration  
✅ **Health checks** - Some services have health checks  
✅ **Docker network isolation** - Using app-network bridge  
✅ **.gitignore configured** - .env and node_modules excluded (mostly)  
✅ **Dependency pinning** - package-lock.json and requirements.txt used  

---

## 📋 REMEDIATION TASKS

### Phase 1: Immediate (Before Any Code Commit) - Priority: 🔴 CRITICAL

#### Task 1.1: Create appsettings.Production.json
**Owner:** DevOps/Backend  
**Time:** 15 minutes  
**Status:** Not Started

- [ ] Create `src/dotnet-api/src/IRS.Api/appsettings.Production.json`
- [ ] Use environment variable placeholders for all secrets:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": ""  // Will be set via environment variable
    },
    "Jwt": {
      "Key": "",  // Will be set via environment variable
      "Issuer": "IRS.Api",
      "Audience": "IRS.Client",
      "ExpiryMinutes": 60
    },
    "OpenFigi": {
      "ApiKey": ""  // Will be set via environment variable
    },
    "LlmEncryption": {
      "Key": ""  // Will be set via environment variable
    },
    "Encryption": {
      "Key": "",  // Will be set via environment variable
      "IV": ""    // Will be set via environment variable
    }
  }
  ```
- [ ] Reference in Program.cs to load from environment when in Production:
  ```csharp
  var config = new ConfigurationBuilder()
      .SetBasePath(env.ContentRootPath)
      .AddJsonFile("appsettings.json", optional: false)
      .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
      .AddEnvironmentVariables()
      .Build();
  ```

---

#### Task 1.2: Rotate All Exposed Credentials (IMMEDIATE)
**Owner:** Security/DevOps  
**Time:** 30 minutes  
**Status:** Not Started  
**Blocker:** DO THIS FIRST BEFORE COMMITTING

- [ ] Generate NEW OpenFigi API key (old key: `012c5587-ae3c-47fa-b783-97bc0d4962e0` is exposed)
  - Register at: https://openfigidev.guidata.com/
  - Document in secure location (1Password, Azure KeyVault, etc.)
- [ ] Invalidate old JWT secret
- [ ] Generate new random JWT secret (min 32 chars, use: `openssl rand -base64 32`)
- [ ] Generate new SQL Server password (min 8 chars, mixed case + numbers + symbols)
- [ ] Generate new encryption keys (random 32-byte base64 encoded)
- [ ] Update `.env.template` with placeholder format only:
  ```
  SA_PASSWORD=                    # CHANGE THIS: Strong password (min 8 chars with symbols)
  JWT_SECRET_KEY=                 # CHANGE THIS: Min 32 chars, use 'openssl rand -base64 32'
  OPENFIGI_API_KEY=               # CHANGE THIS: Register new key at openfigidev.guidata.com
  LLM_ENCRYPTION_KEY=             # CHANGE THIS: Use 'openssl rand -base64 32'
  ENCRYPTION_KEY=                 # CHANGE THIS: Use 'openssl rand -base64 32'
  ENCRYPTION_IV=                  # CHANGE THIS: Use initialization vector
  ```

---

#### Task 1.3: Clean Development Secrets from appsettings Files
**Owner:** Backend  
**Time:** 20 minutes  
**Status:** Not Started

- [ ] Update `src/dotnet-api/src/IRS.Api/appsettings.Development.json`
  - Replace all secret values with empty strings `""`
  - Keep server variables as `localhost,1433` for local development
  - Keep JWT settings as example values only
- [ ] Update `src/dotnet-api/src/IRS.Api/appsettings.Test.json`
  - Use test-specific placeholders, not real credentials
- [ ] Update `src/dotnet-api/src/IRS.Api.IntegrationTests/appsettings.Test.json`
  - Same as above

---

#### Task 1.4: Update .gitignore
**Owner:** DevOps  
**Time:** 10 minutes  
**Status:** Not Started

- [ ] Add to root `.gitignore`:
  ```
  # Secrets and Environment
  .env
  .env.local
  .env.*.local
  !.env.template
  
  # Development Configuration Files with Secrets
  appsettings.Development.json
  appsettings.Production.json
  appsettings.Test.json
  **/*.user
  
  # IDE
  .vs/
  .vscode/
  .idea/
  *.swp
  *.swo
  
  # Build Artifacts
  **/bin/
  **/obj/
  dist/
  node_modules/
  __pycache__/
  *.pyc
  .venv/
  
  # Docker
  docker-compose.override.yml
  ```

---

#### Task 1.5: Git History Cleanup (After Above Tasks)
**Owner:** DevOps/Security  
**Time:** 1 hour  
**Status:** Not Started  
**Prerequisites:** Complete Tasks 1.1-1.4 first

**WARNING: This is destructive and requires team coordination**

- [ ] Use BFG Repo-Cleaner to remove exposed credentials from history:
  ```bash
  # Install bfg: brew install bfg (Windows: download from https://rtyley.github.io/bfg-repo-cleaner/)
  bfg --delete-files appsettings.Development.json
  bfg --delete-files appsettings.Test.json
  bfg --replace-text secrets.txt
  ```
- [ ] Notify all team members to re-clone repository
- [ ] Push with `--force-with-lease`:
  ```bash
  git reflog expire --expire=now --all
  git gc --prune=now --aggressive
  git push --force-with-lease
  ```
- [ ] Verify old commits no longer accessible: `git log --all | grep -i appsettings`

---

### Phase 2: Docker Configuration (Before Building for DockerHub) - Priority: 🔴 CRITICAL

#### Task 2.1: Update Dockerfile for .NET API
**Owner:** DevOps  
**Time:** 30 minutes  
**Status:** Not Started

- [ ] Update `src/dotnet-api/Dockerfile`:
  ```dockerfile
  # Use the official .NET SDK image to build and publish the app
  FROM mcr.microsoft.com/dotnet/sdk:9.0.0 AS build  # ✅ Pin version
  WORKDIR /app
  
  # Copy solution and restore as distinct layers
  COPY ./src/IRS.Api/IRS.Api.csproj ./src/IRS.Api/
  COPY ./src/IRS.Application/IRS.Application.csproj ./src/IRS.Application/
  COPY ./src/IRS.Domain/IRS.Domain.csproj ./src/IRS.Domain/
  COPY ./src/IRS.Infrastructure/IRS.Infrastructure.csproj ./src/IRS.Infrastructure/
  RUN dotnet restore ./src/IRS.Api/IRS.Api.csproj
  
  # Copy everything else and build
  COPY ./src/ ./src/
  WORKDIR /app/src/IRS.Api
  RUN dotnet publish -c Release -o /app/publish --no-restore
  
  # Build runtime image (PRODUCTION ONLY)
  FROM mcr.microsoft.com/dotnet/aspnet:9.0.0 AS final  # ✅ Pin version
  
  # ✅ Add non-root user
  RUN addgroup -g 1001 -S dotnetuser || true && \
      adduser -S dotnetuser -u 1001
  
  WORKDIR /app
  COPY --from=build /app/publish .
  
  # ✅ Set user
  USER dotnetuser
  
  EXPOSE 8080
  ENTRYPOINT ["dotnet", "IRS.Api.dll"]
  ```

- [ ] Remove the development stage (lines 14-20 in current Dockerfile)
- [ ] Update docker-compose.yml to use correct image:
  ```yaml
  dotnet-api:
    build:
      context: ./src/dotnet-api
      dockerfile: Dockerfile
      target: final  # ✅ Explicitly use final stage
  ```

---

#### Task 2.2: Update Dockerfile for Flask API
**Owner:** DevOps  
**Time:** 20 minutes  
**Status:** Not Started

- [ ] Update `src/flask-api/Dockerfile`:
  ```dockerfile
  FROM python:3.12.1-slim  # ✅ Pin version
  
  # Install ODBC Driver 18 for SQL Server with security flags
  RUN apt-get update && apt-get install -y --no-install-recommends \
      curl gnupg2 apt-transport-https ca-certificates \
      && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
      && curl -fsSL https://packages.microsoft.com/config/debian/12/prod.list | tee /etc/apt/sources.list.d/mssql-release.list \
      && apt-get update \
      && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 unixodbc-dev \
      && apt-get clean && rm -rf /var/lib/apt/lists/*
  
  WORKDIR /app
  
  COPY requirements.txt .
  RUN pip install --no-cache-dir -r requirements.txt
  
  # ✅ Add non-root user
  RUN addgroup -S flask || true && \
      adduser -S flask
  
  COPY . .
  
  # ✅ Set user
  USER flask
  
  EXPOSE 5001
  
  # ✅ Ensure health endpoint is available
  HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
      CMD python -c "import requests; requests.get('http://localhost:5001/health')" || exit 1
  
  CMD ["gunicorn", "--config", "gunicorn.conf.py", "yahoo_app:app"]
  ```

- [ ] Ensure health endpoint exists in Flask app or add it

---

#### Task 2.3: Update Dockerfile for Angular UI
**Owner:** Frontend  
**Time:** 20 minutes  
**Status:** Not Started

- [ ] Update `src/angular-ui/Dockerfile`:
  ```dockerfile
  # ── Build stage ──
  FROM node:22.13.0-alpine AS build  # ✅ Pin version
  WORKDIR /app
  COPY package*.json ./
  
  RUN apk add --no-cache curl \
      && npm ci && npm rebuild esbuild \
      && mkdir -p node_modules/@rollup/rollup-linux-x64-musl \
      && curl -fsSL https://registry.npmjs.org/@rollup/rollup-linux-x64-musl/-/rollup-linux-x64-musl-4.59.0.tgz \
          | tar -xz --strip-components=1 -C node_modules/@rollup/rollup-linux-x64-musl || true
  
  COPY . .
  RUN npm run build -- --configuration=production
  
  # ── Serve stage ──
  FROM nginx:1.27.0-alpine  # ✅ Pin version
  
  # ✅ Add non-root user
  RUN addgroup -g 1001 -S nginx || true && \
      adduser -S nginx -u 1001
  
  COPY --from=build /app/dist/angular-ui/browser /usr/share/nginx/html
  COPY nginx.conf /etc/nginx/conf.d/default.conf
  
  # ✅ Set user
  USER nginx
  
  EXPOSE 80
  
  # ✅ Add health check
  HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
      CMD wget --quiet --tries=1 --spider http://localhost/health || exit 1
  
  CMD ["nginx", "-g", "daemon off;"]
  ```

---

#### Task 2.4: Update docker-compose.yml for Production
**Owner:** DevOps  
**Time:** 30 minutes  
**Status:** Not Started

- [ ] Create `docker-compose.prod.yml`:
  ```yaml
  services:
    sqlserver:
      image: mcr.microsoft.com/mssql/server:2022-cu13  # ✅ Pin specific patch version
      environment:
        SA_PASSWORD: "${SA_PASSWORD}"
        MSSQL_PID: "Express"
      # ❌ REMOVE ports - only for local development
      # Accept EULA with environment variable (already configured)
      healthcheck:
        test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$${MSSQL_SA_PASSWORD}" -C -Q "SELECT 1" || exit 1
        interval: 15s
        timeout: 5s
        retries: 10
        start_period: 40s
  
    dotnet-api:
      image: your-dockerhub/irs-api:${VERSION:-latest}
      environment:
        ASPNETCORE_ENVIRONMENT: "Production"  # ✅ PRODUCTION
        ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=${DB_NAME};User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=True;"
        Jwt__Key: "${JWT_SECRET_KEY}"
        Jwt__Issuer: "${JWT_ISSUER}"
        Jwt__Audience: "${JWT_AUDIENCE}"
        OpenFigi__ApiKey: "${OPENFIGI_API_KEY}"
        LlmEncryption__Key: "${LLM_ENCRYPTION_KEY}"
        Encryption__Key: "${ENCRYPTION_KEY}"
        Encryption__IV: "${ENCRYPTION_IV}"
      # ports: remove or use load balancer only
      depends_on:
        db-deploy:
          condition: service_completed_successfully
      healthcheck:
        test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
        interval: 30s
        timeout: 10s
        retries: 3
        start_period: 60s
  
    flask-api:
      image: your-dockerhub/irs-flask-api:${VERSION:-latest}
      environment:
        FLASK_ENV: "production"
        DB_CONNECTION_STRING: "Driver={ODBC Driver 18 for SQL Server};Server=sqlserver;Database=${DB_NAME};Uid=sa;Pwd=${SA_PASSWORD};TrustServerCertificate=yes;"
        JWT_SECRET_KEY: "${JWT_SECRET_KEY}"
      # ports: remove or use load balancer only
      depends_on:
        sqlserver:
          condition: service_healthy
      healthcheck:
        test: ["CMD", "python", "-c", "import requests; requests.get('http://localhost:5001/health')"]
        interval: 30s
        timeout: 10s
        retries: 3
        start_period: 60s
  
    angular-ui:
      image: your-dockerhub/irs-ui:${VERSION:-latest}
      environment:
        API_BASE_URL: "${DOTNET_API_BASE_URL:-http://api.example.com}"  # ✅ External URL
      # ports: only expose via load balancer/ingress
      depends_on:
        dotnet-api:
          condition: service_healthy
      healthcheck:
        test: ["CMD", "wget", "--quiet", "--tries=1", "--spider", "http://localhost/health"]
        interval: 30s
        timeout: 10s
        retries: 3
  ```

- [ ] Update production docker-compose.yml:
  ```bash
  docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
  ```

---

#### Task 2.5: Pin All Base Image Versions
**Owner:** DevOps  
**Time:** 30 minutes  
**Status:** Not Started

- [ ] Update to specific versions:
  ```
  node:22.13.0-alpine        (from node:22-alpine)
  nginx:1.27.0-alpine        (from nginx:alpine)
  python:3.12.1-slim         (from python:3.12-slim)
  mcr.microsoft.com/dotnet/sdk:9.0.0
  mcr.microsoft.com/dotnet/aspnet:9.0.0
  mcr.microsoft.com/mssql/server:2022-cu13  (SPECIFIC PATCH)
  ```

- [ ] Document chosen versions in `image-versions.txt`:
  ```
  Node.js: 22.13.0
  Nginx: 1.27.0
  Python: 3.12.1
  .NET SDK: 9.0.0
  .NET Runtime: 9.0.0
  SQL Server: 2022-cu13
  
  Update Schedule: Monthly security patches
  Last Updated: 2026-02-24
  ```

---

### Phase 3: Deployment and Secret Management - Priority: 🟡 HIGH

#### Task 3.1: Implement Secrets Management
**Owner:** DevOps/Security  
**Time:** 2-4 hours  
**Status:** Not Started

**Option A: Docker Secrets (for Docker Swarm)**
```bash
echo "SuperSecretPassword123!" | docker secret create sa_password -
docker service create --secret sa_password ...
```

**Option B: Kubernetes Secrets (Recommended)**
```bash
kubectl create secret generic app-secrets \
  --from-literal=SA_PASSWORD='...' \
  --from-literal=JWT_SECRET_KEY='...'
```

**Option C: Azure KeyVault (for Azure Container Instances)**
```bash
az keyvault create --name irs-secrets
az keyvault secret set --vault-name irs-secrets --name SA-PASSWORD --value '...'
```

**Option D: GitHub Actions Secrets (for CI/CD)**
```yaml
env:
  SA_PASSWORD: ${{ secrets.SA_PASSWORD }}
  JWT_SECRET_KEY: ${{ secrets.JWT_SECRET_KEY }}
```

- [ ] Choose appropriate secrets management solution
- [ ] Never pass secrets to `docker build` command
- [ ] Never store secrets in image layers
- [ ] Use `--mount=type=secret` in Dockerfile when available

---

#### Task 3.2: Create Production Deployment Documentation
**Owner:** DevOps  
**Time:** 1 hour  
**Status:** Not Started

Create `DEPLOYMENT.md`:
```markdown
# Production Deployment Guide

## Prerequisites
- Docker/Kubernetes cluster with secret management
- All environment variables populated from secure source
- Database backups configured
- Monitoring and logging configured

## Environment Variables (Required)
- SA_PASSWORD: SQL Server SA password
- JWT_SECRET_KEY: JWT signing key (min 32 chars)
- OPENFIGI_API_KEY: OpenFigi API key
- LLM_ENCRYPTION_KEY: LLM encryption key
- ENCRYPTION_KEY: Data encryption key
- ENCRYPTION_IV: Data encryption IV
- DB_NAME: Database name (default: IRS)

## Deployment Steps

### Option 1: Docker Compose (Single Server)
```bash
# Load secrets from secure location
export SA_PASSWORD=$(./get-secret.sh sa_password)
export JWT_SECRET_KEY=$(./get-secret.sh jwt_key)
# ... other secrets ...

docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

### Option 2: Kubernetes (Cloud/Enterprise)
```bash
kubectl apply -f k8s-deployment.yaml
```

## Verification
```bash
docker compose ps
curl http://localhost:8080/health
curl http://localhost:5001/health
```
```

---

#### Task 3.3: Implement Image Scanning (Before DockerHub Push)
**Owner:** DevOps  
**Time:** 30 minutes  
**Status:** Not Started

- [ ] Add Trivy scanning to build pipeline:
  ```bash
  # Local scanning before push
  trivy image --severity HIGH,CRITICAL your-repo/irs-api:latest
  trivy image-scan ./src/dotnet-api/Dockerfile
  ```

- [ ] Configure DockerHub automated security scanning
  - Go to: DockerHub > Repositories > Settings > Security Options
  - Enable: "Scan on push"

- [ ] Add to CI/CD pipeline:
  ```yaml
  - name: Scan with Trivy
    run: |
      trivy image --exit-code 1 --severity HIGH,CRITICAL \
        your-repo/irs-api:${{ github.sha }}
  ```

---

#### Task 3.4: Set Up Credential Rotation (Ongoing)
**Owner:** Security  
**Time:** Ongoing  
**Status:** Not Started

- [ ] Schedule quarterly credential rotation
- [ ] Document rotation procedure
- [ ] Automated rotation if possible (HashiCorp Vault, AWS Secrets Manager)
- [ ] Monitor credential access logs

---

### Phase 4: CI/CD Integration (Before Automated Pushes) - Priority: 🟡 HIGH

#### Task 4.1: Create GitHub Actions Workflow
**Owner:** DevOps  
**Time:** 1 hour  
**Status:** Not Started

Create `.github/workflows/docker-build.yml`:
```yaml
name: Build and Push Docker Images

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3
      
      - name: Scan for exposed secrets (TruffleHog)
        run: |
          pip install trufflehog
          trufflehog filesystem .
      
      - name: Scan appsettings files
        run: |
          if grep -r "password\|secret\|apikey\|token" src/**/*.Development.json 2>/dev/null; then
            echo "ERROR: Found secrets in Development config!"
            exit 1
          fi
      
      - name: Login to DockerHub
        if: github.event_name == 'push'
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}
      
      - name: Build .NET API
        uses: docker/build-push-action@v5
        with:
          context: ./src/dotnet-api
          dockerfile: Dockerfile
          target: final  # ✅ Only final stage
          push: ${{ github.event_name == 'push' }}
          tags: ${{ secrets.DOCKERHUB_REPO }}/irs-api:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
      
      - name: Scan image with Trivy
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: ${{ secrets.DOCKERHUB_REPO }}/irs-api:${{ github.sha }}
          format: 'sarif'
          output: 'trivy-results.sarif'
          severity: 'HIGH,CRITICAL'
      
      - name: Upload Trivy report
        uses: github/codeql-action/upload-sarif@v2
        with:
          sarif_file: 'trivy-results.sarif'
```

- [ ] Add GitHub Actions secrets:
  - `DOCKERHUB_USERNAME`
  - `DOCKERHUB_TOKEN` (not password)
  - `DOCKERHUB_REPO` (e.g., myuser/irs)

---

#### Task 4.2: Add Secret Detection to Pre-Commit Hooks
**Owner:** DevOps  
**Time:** 30 minutes  
**Status:** Not Started

Create `.pre-commit-config.yaml`:
```yaml
repos:
  - repo: https://github.com/Yelp/detect-secrets
    rev: v1.4.0
    hooks:
      - id: detect-secrets
        args: ['--baseline', '.secrets.baseline']

  - repo: https://github.com/pre-commit/pre-commit-hooks
    rev: v4.5.0
    hooks:
      - id: detect-private-key
      - id: forbid-new-submodules
```

- [ ] Install and configure locally:
  ```bash
  pip install detect-secrets
  detect-secrets scan > .secrets.baseline
  pre-commit install
  ```

---

### Phase 5: Testing and Validation - Priority: 🟡 HIGH

#### Task 5.1: Test Production Build Locally
**Owner:** DevOps/QA  
**Time:** 1 hour  
**Status:** Not Started

- [ ] Build all images locally without using override compose:
  ```bash
  docker compose build
  ```

- [ ] Test with production environment:
  ```bash
  docker compose -f docker-compose.yml -f docker-compose.prod.yml up
  ```

- [ ] Verify:
  ```bash
  curl http://localhost:8080/health
  curl http://localhost:5001/health
  curl http://localhost/health
  ```

- [ ] Check image layers for secrets:
  ```bash
  docker history your-repo/irs-api:latest
  docker inspect your-repo/irs-api:latest
  ```

- [ ] Ensure no hardcoded secrets visible in layers

---

#### Task 5.2: Container Startup Verification
**Owner:** QA  
**Time:** 1 hour  
**Status:** Not Started

- [ ] Verify each service starts correctly
- [ ] Check container logs for errors:
  ```bash
  docker compose logs -f dotnet-api
  docker compose logs -f flask-api
  docker compose logs -f angular-ui
  ```

- [ ] Verify health endpoints return 200
- [ ] Test inter-service communication

---

### Phase 6: Documentation (Before Sharing) - Priority: 🟢 MEDIUM

#### Task 6.1: Update README.md
**Owner:** Project Lead  
**Time:** 30 minutes  
**Status:** Not Started

Add sections:
- [ ] "Production Deployment" section
- [ ] "Security Considerations" section
- [ ] "Environment Variables" with required list
- [ ] "Credential Rotation" schedule
- [ ] "Troubleshooting" section

---

#### Task 6.2: Create Security Policy
**Owner:** Security/DevOps  
**Time:** 1 hour  
**Status:** Not Started

Create `SECURITY.md`:
- [ ] Information about reporting vulnerabilities
- [ ] Disclosure policy
- [ ] Supported versions
- [ ] Security update schedule
- [ ] List of known limitations

---

#### Task 6.3: Document Build and Release Process
**Owner:** DevOps  
**Time:** 1 hour  
**Status:** Not Started

Create `BUILD.md`:
- [ ] How to build images locally
- [ ] How to push to registry
- [ ] Version tagging strategy
- [ ] Rollback procedure
- [ ] Change tracking

---

## 🎯 VERIFICATION CHECKLIST

Before pushing to DockerHub, verify:

- [ ] **Secrets Audit**
  - [ ] No hardcoded credentials in any source files
  - [ ] `.gitignore` properly configured
  - [ ] `git log` contains no exposed secrets
  - [ ] All appsettings.*.json cleaned of secrets

- [ ] **Docker Configuration**
  - [ ] All base image versions pinned
  - [ ] Non-root user configured in all Dockerfiles
  - [ ] Production target properly built
  - [ ] Development stage excluded from production builds
  - [ ] `ASPNETCORE_ENVIRONMENT=Production`
  - [ ] SQL Server port not exposed in production

- [ ] **Image Scanning**
  - [ ] Trivy scan shows no CRITICAL vulnerabilities
  - [ ] Docker Compose builds without warnings
  - [ ] `docker history` shows no secrets in layers
  - [ ] `docker inspect` shows no sensitive environment variables

- [ ] **Testing**
  - [ ] All health endpoints return 200
  - [ ] Inter-service communication works
  - [ ] Application starts without errors
  - [ ] Database connectivity verified
  - [ ] API endpoints functional

- [ ] **Documentation**
  - [ ] README updated with deployment info
  - [ ] DEPLOYMENT.md created
  - [ ] SECURITY.md created
  - [ ] Build process documented

- [ ] **CI/CD**
  - [ ] GitHub Actions workflow configured
  - [ ] Secret detection enabled
  - [ ] Pre-commit hooks installed
  - [ ] Approval process for releases defined

---

## 📊 RISK ASSESSMENT

| Risk | Severity | Current | After Remediation |
|------|----------|---------|-------------------|
| Exposed API Keys | CRITICAL | 🔓 Yes | ✅ No |
| Database Credentials in Code | CRITICAL | 🔓 Yes | ✅ No |
| Secrets in Git History | CRITICAL | 🔓 Yes | ✅ No |
| Running as Root | HIGH | ⚠️ Yes | ✅ No |
| Public Database Port | CRITICAL | 🔴 Yes | ✅ No |
| Development in Production | HIGH | ⚠️ Possible | ✅ No |
| No Image Scanning | MEDIUM | ❌ None | ✅ Yes |
| Unpinned Base Images | MEDIUM | ⚠️ Some | ✅ All |
| Secret Rotation | MEDIUM | ❌ None | ✅ Planned |

---

## 📅 TIMELINE RECOMMENDATION

**Timeline:** 1-2 weeks

```
Week 1:
  Mon-Tue: Phase 1 (Secrets audit and .gitignore)
  Wed-Thu: Phase 2 (Docker configuration)
  Fri:     Phase 3 (Secrets management setup)

Week 2:
  Mon-Tue: Phase 4 (CI/CD integration)
  Wed:     Phase 5 (Testing and validation)
  Thu-Fri: Phase 6 (Documentation and final review)
```

---

## 📞 NEXT STEPS

1. **Immediate (Within 24 hours):**
   - Rotate exposed credentials (Task 1.2)
   - Add secrets to .gitignore (Task 1.4)
   - Do NOT commit current state

2. **This Week:**
   - Complete Phase 1 (secrets cleanup)
   - Complete Phase 2 (Docker updates)
   - Begin Phase 3 (secrets management)

3. **Before First DockerHub Push:**
   - All 6 phases must be complete
   - Full verification checklist must pass
   - Security review required

4. **After Initial Push:**
   - Monitor for vulnerability reports
   - Schedule monthly base image updates
   - Implement quarterly credential rotation

---

## 🔗 REFERENCES

- **Docker Security Best Practices:** https://docs.docker.com/develop/security-best-practices/
- **OWASP Container Security:** https://cheatsheetseries.owasp.org/cheatsheets/Docker_Security_Cheat_Sheet.html
- **Trivy Scanner:** https://github.com/aquasecurity/trivy
- **Detect Secrets:** https://github.com/Yelp/detect-secrets
- **TruffleHog:** https://github.com/trufflesecurity/trufflehog
- **BFG Repo-Cleaner:** https://rtyley.github.io/bfg-repo-cleaner/
- **CWE-798:** Hardcoded Credentials - https://cwe.mitre.org/data/definitions/798.html
- **CWE-327:** Broken/Risky Cryptography - https://cwe.mitre.org/data/definitions/327.html

---

**Report Generated:** 2026-02-24  
**Status:** NOT READY FOR PRODUCTION  
**Recommendation:** Follow all remediation tasks before committing to GitHub or pushing to DockerHub

