# Production Deployment Guide

**Document Version:** 1.0  
**Last Updated:** February 24, 2026  
**Status:** PRODUCTION READY

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Environment Setup](#environment-setup)
3. [Deployment Methods](#deployment-methods)
4. [Credential Management](#credential-management)
5. [Health Checks](#health-checks)
6. [Monitoring](#monitoring)
7. [Troubleshooting](#troubleshooting)
8. [Rollback Procedure](#rollback-procedure)

---

## Prerequisites

### Infrastructure Requirements

- **Docker Engine:** 20.10+ or **Docker Desktop** 4.10+
- **Docker Compose:** 2.0+ (standalone)
- **Kubernetes:** 1.24+ (optional, for K8s deployments)
- **Storage:** 20GB minimum for database volumes
- **Memory:** 8GB minimum RAM
- **Network:** TLS/HTTPS configured for production

### Access Requirements

- Azure KeyVault access (or other secrets management system)
- Docker registry credentials
- Database backup location configured
- Monitoring space available (Application Insights, Datadog, etc.)

---

## Environment Setup

### Step 1: Prepare Secrets

Create a `.env.prod` file with actual values (NEVER commit this file):

```bash
# Copy and populate with real values
cp .env.template .env.prod

# Generate secure values
SA_PASSWORD=$(openssl rand -base64 32)
JWT_SECRET_KEY=$(openssl rand -base64 32)

# Edit .env.prod with editor
nano .env.prod  # or edit with your editor
```

### Step 2: Configure Secrets Management

**Option A: Environment Variables (Simple)**
```bash
export SA_PASSWORD="your-secure-password"
export JWT_SECRET_KEY="your-secure-jwt-key"
export OPENFIGI_API_KEY="your-api-key"
export LLM_ENCRYPTION_KEY="generated-key"
export ENCRYPTION_KEY="generated-key"
export ENCRYPTION_IV="generated-iv"
export DB_NAME="IRS"
```

**Option B: Azure KeyVault (Recommended)**
```bash
# Create KeyVault
az keyvault create --name irs-secrets --resource-group mygroup

# Store secrets
az keyvault secret set --vault-name irs-secrets --name "SA-PASSWORD" --value "$SA_PASSWORD"
az keyvault secret set --vault-name irs-secrets --name "JWT-SECRET-KEY" --value "$JWT_SECRET_KEY"

# Access in deployment
export SA_PASSWORD=$(az keyvault secret show --vault-name irs-secrets --name "SA-PASSWORD" -q --query value)
```

**Option C: Kubernetes Secrets**
```bash
kubectl create secret generic app-secrets \
  --from-literal=sa-password='password' \
  --from-literal=jwt-secret-key='key' \
  --from-literal=openfigi-api-key='key' \
  -n production
```

---

## Deployment Methods

### Method 1: Docker Compose (Single Server)

#### Command

```bash
# Load environment from secure location
source get-secrets.sh  # Your script to load from KeyVault/etc

# Start production deployment
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Or with env file
docker compose --env-file .env.prod \
  -f docker-compose.yml \
  -f docker-compose.prod.yml up -d
```

#### Verification

```bash
# Check container status
docker compose ps

# Verify health
docker compose exec dotnet-api curl http://localhost:8080/health
docker compose exec flask-api curl http://localhost:5001/health

# View logs
docker compose logs -f dotnet-api
docker compose logs -f flask-api
docker compose logs -f angular-ui
```

#### Database Initialization

```bash
# Wait for db-deploy to complete
docker compose logs -f db-deploy

# Verify database created
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${SA_PASSWORD}" -Q "SELECT name FROM sys.databases"
```

---

### Method 2: Kubernetes (Cloud/Enterprise)

#### Prerequisite: Create Secrets

```bash
kubectl create secret generic app-secrets \
  --from-literal=SA_PASSWORD="$(openssl rand -base64 32)" \
  --from-literal=JWT_SECRET_KEY="$(openssl rand -base64 32)" \
  --from-literal=OPENFIGI_API_KEY="xxxxx" \
  --from-literal=LLM_ENCRYPTION_KEY="$(openssl rand -base64 32)" \
  --from-literal=ENCRYPTION_KEY="$(openssl rand -base64 32)" \
  --from-literal=ENCRYPTION_IV="$(openssl rand -base64 32)" \
  -n production
```

#### Deploy

```bash
# Apply manifests
kubectl apply -f k8s-namespace.yaml
kubectl apply -f k8s-secrets.yaml
kubectl apply -f k8s-statefulset-db.yaml
kubectl apply -f k8s-deployment-api.yaml
kubectl apply -f k8s-deployment-ui.yaml

# Verify deployment
kubectl get pods -n production
kubectl get svc -n production
```

#### Example K8s Deployment Snippet

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api
  namespace: production
spec:
  replicas: 2
  selector:
    matchLabels:
      app: dotnet-api
  template:
    metadata:
      labels:
        app: dotnet-api
    spec:
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
      containers:
      - name: dotnet-api
        image: your-registry.azurecr.io/irs-api:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: SA_PASSWORD
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: SA_PASSWORD
        - name: JWT_SECRET_KEY
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: JWT_SECRET_KEY
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 60
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
```

---

### Method 3: Azure Container Instances (ACI)

```bash
# Create container group
az container create \
  --resource-group mygroup \
  --name irs-app \
  --image your-registry.azurecr.io/irs-api:latest \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
  --secure-environment-variables \
    SA_PASSWORD=$SA_PASSWORD \
    JWT_SECRET_KEY=$JWT_SECRET_KEY \
  --registry-login-server your-registry.azurecr.io \
  --registry-username username \
  --registry-password password
```

---

## Credential Management

### Best Practices

✅ **DO:**
- Store credentials in: Azure KeyVault, AWS Secrets Manager, HashiCorp Vault
- Use short-lived credentials when possible
- Rotate credentials quarterly
- Audit who accesses credentials
- Use different credentials for each environment

❌ **DON'T:**
- Commit credentials to git
- Store credentials in environment variables on long-lived containers
- Share credentials via email or chat
- Use same credentials across environments
- Log credentials or sensitive data

### Credential Rotation

```bash
#!/bin/bash
# rotate-credentials.sh

# Generate new credentials
NEW_SA_PASSWORD=$(openssl rand -base64 32)
NEW_JWT_KEY=$(openssl rand -base64 32)

# Update in KeyVault
az keyvault secret set --vault-name irs-secrets \
  --name "SA-PASSWORD" --value "$NEW_SA_PASSWORD"
az keyvault secret set --vault-name irs-secrets \
  --name "JWT-SECRET-KEY" --value "$NEW_JWT_KEY"

# Redeploy containers with new secrets
docker compose down
source get-secrets.sh # Reload from KeyVault
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Verify health
docker compose exec dotnet-api curl http://localhost:8080/health

echo "Credentials rotated successfully"
```

---

## Health Checks

### Manual Health Checks

```bash
# .NET API
curl -v http://localhost:5000/health

# Flask API
curl -v http://localhost:5001/health

# Angular UI
curl -v http://localhost/health

# Database
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${SA_PASSWORD}" -Q "SELECT @@VERSION"
```

### Docker Health Check Status

```bash
# View container health
docker compose ps

# Check specific container
docker inspect --format='{{.State.Health.Status}}' container_name

# View health check logs
docker inspect --format='{{json .State.Health.Log}}' container_name
```

### Kubernetes Health Checks

```bash
# View probe status
kubectl describe pod dotnet-api-xxxxx -n production

# View events
kubectl get events -n production --sort-by='.lastTimestamp'

# Check readiness
kubectl get endpoints -n production
```

---

## Monitoring

### Application Insights (Azure)

```bash
# Create Application Insights resource
az monitor app-insights component create \
  --app irs-insights \
  --location eastus \
  --resource-group mygroup

# Add instrumentation key to environment
APPINSIGHTS_INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app irs-insights \
  --resource-group mygroup \
  --query instrumentationKey -o tsv)
```

### Logs and Metrics

```bash
# View container logs
docker compose logs --tail 100 -f dotnet-api

# Export logs
docker compose logs > logs-$(date +%Y%m%d).txt

# Monitor metrics
docker stats

# Create monitoring stack (with Prometheus + Grafana)
docker compose -f monitoring/docker-compose.yml up -d
```

---

## Troubleshooting

### Container Startup Issues

```bash
# Check logs
docker compose logs dotnet-api

# Check network connectivity
docker compose exec dotnet-api curl http://sqlserver:1433
```

### Database Connection Issues

```bash
# Test database connectivity
docker compose exec dotnet-api /bin/bash
sqlcmd -S sqlserver -U sa -P $SA_PASSWORD -Q "SELECT @@VERSION"

# Check database status
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${SA_PASSWORD}" -Q "EXEC sp_helpdb"
```

### API Health Check Failures

```bash
# Check if port is listening
docker compose exec dotnet-api netstat -tuln | grep 8080

# Test direct connectivity
docker compose exec dotnet-api curl -v http://localhost:8080/health

# Check application startup
docker compose logs --tail 50 dotnet-api
```

### Secret/Credential Issues

```bash
# Verify environment variables are set
docker compose exec dotnet-api printenv | grep -i secret

# Check if secrets are being loaded from environment
# (Secrets should NOT appear in docker compose logs)

# Reset and reload secrets
source get-secrets.sh
docker compose down
docker compose up -d
```

---

## Rollback Procedure

### Quick Rollback (Same Version)

```bash
# Stop current deployment
docker compose down

# Remove failed data (CAREFUL!)
docker compose down -v  # Removes volumes - data loss!

# Start with previous environment
source get-secrets.sh
docker compose up -d

# Verify health
docker compose exec dotnet-api curl http://localhost:8080/health
```

### Rollback to Previous Version

```bash
#!/bin/bash
# rollback.sh

PREVIOUS_IMAGE_TAG="v1.2.3"

# Update image versions
docker compose -f docker-compose.yml -f docker-compose.prod.yml stop dotnet-api

# Pull previous version
docker pull your-registry/irs-api:$PREVIOUS_IMAGE_TAG

# Start with previous version
IMAGE_TAG=$PREVIOUS_IMAGE_TAG docker compose up -d dotnet-api

# Verify
docker compose logs -f dotnet-api
```

### Database Rollback

```bash
# Restore from backup
az sql db restore \
  --name IRS \
  --server your-server \
  --resource-group mygroup \
  --time "2026-02-24T10:00:00Z"

# Or restore locally
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${SA_PASSWORD}" \
  -Q "RESTORE DATABASE IRS FROM DISK = '/var/opt/mssql/backup/IRS.bak'"
```

---

## Security Checklist

Before production:

- [ ] All secrets rotated from development values
- [ ] Credentials stored in KeyVault (not in code/environment)
- [ ] HTTPS/TLS configured
- [ ] Database backups configured
- [ ] Monitoring and alerting enabled
- [ ] Network policies restrict database access
- [ ] Container images scanned for vulnerabilities
- [ ] Non-root users configured
- [ ] API authentication enabled
- [ ] Rate limiting configured

---

## Post-Deployment Validation

```bash
#!/bin/bash
# post-deploy-validation.sh

set -e

echo "Validating deployment..."

# Check containers are running
echo "1. Checking container status..."
docker compose ps | grep -E "(dotnet-api|flask-api|angular-ui|sqlserver)" || exit 1

# Check health endpoints
echo "2. Checking health endpoints..."
curl -f http://localhost:5000/health || exit 1
curl -f http://localhost:5001/health || exit 1

# Check database connectivity
echo "3. Checking database..."
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT COUNT(*) FROM sys.databases" || exit 1

# Check API responses
echo "4. Checking API endpoints..."
curl -f http://localhost:5000/api/users || exit 1

echo "✅ All validation checks passed!"
```

---

## Support and Escalation

For production issues:
1. Check logs: `docker compose logs -f`
2. Verify health: `curl http://localhost:5000/health`
3. Check credentials in KeyVault
4. Review monitoring/alerting dashboards
5. Contact: devops-team@company.com

