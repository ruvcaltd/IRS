# Build and Release Process

**Version:** 1.0  
**Last Updated:** February 24, 2026  
**Status:** PRODUCTION READY

---

## Table of Contents

1. [Local Build](#local-build)
2. [Version Management](#version-management)
3. [Release Process](#release-process)
4. [Deployment](#deployment)
5. [Rollback](#rollback)

---

## Local Build

### Prerequisites

```bash
# Install required tools
docker --version  # 20.10+
docker compose --version  # 2.0+
git --version
```

### Build Commands

#### Full Build (Development)

```bash
# Build all images without cache
docker compose build --no-cache

# Build specific service
docker compose build dotnet-api
docker compose build flask-api
docker compose build angular-ui

# View build output
docker compose build --progress=plain
```

#### Production Build

```bash
# Build for production (optimized)
docker compose build --file docker-compose.yml --file docker-compose.prod.yml

# Build services in order
docker compose build dotnet-api flask-api angular-ui

# Build with build arguments
docker buildx build \
  --build-arg GIT_COMMIT=$(git rev-parse --short HEAD) \
  --build-arg BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ') \
  -f src/dotnet-api/Dockerfile \
  .
```

### Build Verification

```bash
# View built images
docker images | grep irs

# Inspect image layers
docker history your-registry/irs-api:latest

# Check image size
docker images --format "table {{.Repository}}\t{{.Size}}"

# Scan image for vulnerabilities
trivy image your-registry/irs-api:latest

# Check image config
docker inspect your-registry/irs-api:latest
```

---

## Version Management

### Semantic Versioning

This project follows **Semantic Versioning** (SemVer):

- **MAJOR** (1.0.0): Breaking API changes
- **MINOR** (1.1.0): New features, backward compatible
- **PATCH** (1.0.1): Bug fixes, backward compatible

Format: `v{MAJOR}.{MINOR}.{PATCH}`

### Version Sources

1. **Git Tags:** Primary version source
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Package Files:**
   - `package.json` (Angular)
   - `.csproj` files (.NET)
   - `version.txt` (optional)

### Version in Artifacts

```bash
# Tag Docker images with version
docker tag irs-api:latest your-registry/irs-api:v1.0.0
docker tag irs-api:latest your-registry/irs-api:1.0.0
docker tag irs-api:latest your-registry/irs-api:latest
docker tag irs-api:latest your-registry/irs-api:1.0

# Push all versions
docker push your-registry/irs-api:v1.0.0
docker push your-registry/irs-api:1.0.0
docker push your-registry/irs-api:latest
```

### Version File

Create `version.txt`:
```
VERSION=1.0.0
BUILD_DATE=2026-02-24T00:00:00Z
GIT_COMMIT=abc1234567890def
GIT_BRANCH=main
```

---

## Release Process

### Pre-Release Checklist

- [ ] Feature branch merged to `develop`
- [ ] All tests passing
- [ ] Security scan passed
- [ ] Documentation updated
- [ ] CHANGELOG.md updated
- [ ] Version bumped (major/minor/patch)
- [ ] Code review approved

### Release Steps

#### 1. Update Version

```bash
# Update version in your files
vim package.json         # Update version field
vim src/**/*.csproj      # Update Version property
```

#### 2. Create Release Branch

```bash
git checkout develop
git pull origin develop
git checkout -b release/v1.0.0
```

#### 3. Update CHANGELOG

Create/update `CHANGELOG.md`:

```markdown
## [1.0.0] - 2026-02-24

### Added
- New feature X
- Improved performance of Y

### Changed
- API endpoint Z updated

### Fixed
- Bug #123
- Security issue #456

### Security
- Rotated credentials
- Updated dependencies

### Breaking Changes
- Removed deprecated API endpoint
- Changed authentication format
```

#### 4. Commit Changes

```bash
git add -A
git commit -m "chore: release v1.0.0"
git push origin release/v1.0.0
```

#### 5. Create Release PR

Create Pull Request:
- From: `release/v1.0.0`
- To: `main`
- Title: `Release: v1.0.0`

#### 6. Merge to Main

```bash
git checkout main
git pull origin main
git merge release/v1.0.0 --no-ff
git push origin main
```

#### 7. Tag Release

```bash
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0

# Verify tag
git tag -l -n v1.0.0
```

### CI/CD Auto-Build

On tag push, GitHub Actions will:
1. Run security scans
2. Build Docker images
3. Tag: `latest`, `v1.0.0`, `1.0.0`, `1.0`
4. Push to Docker Hub
5. Scan images with Trivy
6. Create release notes

### Post-Release

```bash
# Merge back to develop
git checkout develop
git pull origin develop
git merge main
git push origin develop

# Cleanup
git branch -d release/v1.0.0
git push origin --delete release/v1.0.0
```

---

## Deployment

### Deployment Environments

```
develop → Staging → Production
  │       │         │
  ├→ 1h   ├→ 4h     └→ approved
```

### Manual Deployment

#### To Staging

```bash
# Load from staging secrets
source get-secrets.sh staging

# Deploy
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Verify
curl https://staging.company.com/api/health
```

#### To Production

```bash
# Backup database first
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [IRS] TO DISK = '/var/opt/mssql/backup/IRS_pre_v1.0.0.bak'"

# Load production secrets
source get-secrets.sh production

# Deploy
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Verify
curl https://api.company.com/health
curl https://api.company.com/api/health
```

### Automated Deployment

On tag creation, GitHub Actions will automatically:
1. Build images
2. Push to registry
3. Trigger deployment pipeline
4. Run smoke tests
5. Send notifications

---

## Monitoring Deployments

### Real-Time Monitoring

```bash
# Watch deployment progress
docker compose logs -f dotnet-api

# Monitor resource usage
docker stats

# Check service health
docker compose ps
```

### Deployment Validation

```bash
#!/bin/bash
# deploy-validate.sh

SERVICE_URLS=(
  "http://localhost:5000/health"
  "http://localhost:5001/health"
  "http://localhost/health"
)

for url in "${SERVICE_URLS[@]}"; do
  echo "Checking $url..."
  if curl -f "$url" >/dev/null 2>&1; then
    echo "✅ $url is healthy"
  else
    echo "❌ $url is unhealthy"
    exit 1
  fi
done

echo "✅ All services are healthy"
```

---

## Rollback

### Immediate Rollback (Last Version)

```bash
# Get previous image version
PREV_VERSION=$(git tag -l | sort -V | tail -2 | head -1)

# Stop current deployment
docker compose down

# Deploy previous version
export VERSION=$PREV_VERSION
docker compose up -d

# Verify health
curl http://localhost:5000/health
```

### Rollback with Version Tag

```bash
#!/bin/bash
# rollback.sh <version>

VERSION=${1:-latest}

docker compose down
docker pull your-registry/irs-api:$VERSION
docker compose up -d
docker compose logs -f dotnet-api
```

### Database Rollback

```bash
# Restore from backup
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [IRS] FROM DISK = '/var/opt/mssql/backup/IRS_pre_v1.0.0.bak' WITH REPLACE"

# Verify restore
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "SELECT COUNT(*) FROM [IRS].sys.tables"
```

---

## Build Artifacts

### Docker Hub Structure

```
your-registry/irs-api
├── latest (current default branch)
├── v1.0.0 (tagged release)
├── 1.0 (major.minor)
├── 1.0.0 (major.minor.patch)
└── main (branch)

your-registry/irs-flask-api
└── [same structure]

your-registry/irs-ui
└── [same structure]
```

### Artifact Retention

| Type | Retention | Action |
|------|-----------|--------|
| Latest | Permanent | Keep all |
| Release Tags | Permanent | Keep all |
| Branch Tags | 90 days | Auto-delete |
| PR Tags | 7 days | Auto-delete |
| Failed Builds | 7 days | Auto-delete |

---

## Build Triggers

### Automatic Builds

```yaml
Trigger              When
─────────────────────────────────────────
Push to main         → Build release
Push to develop      → Build preview
Create Git tag       → Build and push release
Pull Request         → Build for testing
```

### Manual Build

```bash
# Trigger build manually
gh workflow run docker-build.yml -r main

# Monitor workflow
gh run list
gh run view <run-id> --log
```

---

## Build Status

### GitHub Actions Badge

```markdown
[![Docker Build](https://github.com/username/repo/actions/workflows/docker-build.yml/badge.svg)](https://github.com/username/repo/actions/workflows/docker-build.yml)
```

### Check Build Status

```bash
# List recent builds
docker buildx du

# Inspect build cache
docker buildx du --verbose
```

---

## Troubleshooting Builds

### Build Fails: Network Issues

```bash
# Check network connectivity
docker run --rm alpine ping google.com

# Use proxy if behind firewall
docker build --build-arg HTTP_PROXY=http://proxy:8080 .
```

### Build Fails: Out of Space

```bash
# Clear Docker system
docker system prune -a

# Remove dangling images
docker image prune -a
```

### Build Fails: Permission Denied

```bash
# Check Docker daemon access
docker ps

# Add user to docker group
sudo usermod -aG docker $USER
groups $USER
```

### Slow Builds

```bash
# Use buildx for better caching
docker buildx create --name builder
docker buildx use builder

# Enable BuildKit
export DOCKER_BUILDKIT=1
docker build .
```

---

## Performance Optimization

### Build Caching

```dockerfile
# ✅ Good: Separate layers for dependencies
RUN npm ci
COPY src/ .
RUN npm run build

# ❌ Bad: One layer, no caching
COPY . .
RUN npm ci && npm run build
```

### Image Size

```bash
# Check image size
docker images your-registry/irs-api

# Multi-stage reduces size
# Build: 1.2GB → Runtime: 200MB

# View layer sizes
docker history your-registry/irs-api:latest
```

---

## Security in Build

### Secrets in Build

❌ **Don't:**
```bash
RUN echo $SECRET_KEY > .env  # Leaked in layer!
```

✅ **Do:**
```bash
# Pass at runtime only
docker run -e SECRET_KEY=$key ...
```

### Scan During Build

```bash
# Scan Dockerfile
docker run --rm -i hadolint/hadolint < Dockerfile

# Scan built image
trivy image your-registry/irs-api:latest
```

---

## Support

For issues with builds:
1. Check GitHub Actions logs
2. Review SECURITY.md for vulnerability scans
3. Check Docker Hub builds
4. See Troubleshooting section above

