# Docker Architecture Reference

This document explains every Dockerfile and Docker Compose file in the project, the overall container orchestration, and how to run the stack locally and in production.

---

## Table of Contents

1. [Container Overview](#container-overview)
2. [Dockerfiles](#dockerfiles)
   - [angular-ui — Production](#angular-ui--production-dockerfile)
   - [angular-ui — Development](#angular-ui--development-dockerfiledev)
   - [dotnet-api](#dotnet-api-dockerfile)
   - [flask-api](#flask-api-dockerfile)
   - [db-deploy (database tool)](#db-deploy-dockerfile)
3. [Docker Compose Files](#docker-compose-files)
   - [docker-compose.yml — Base](#docker-composeyml--base)
   - [docker-compose.override.yml — Local Dev](#docker-composeoverrideyml--local-dev)
   - [docker-compose.prod.yml — Production Reference](#docker-composeprodymyl--production-reference)
   - [docker-compose.vps.yml — VPS Deployment](#docker-composevpsyml--vps-deployment)
4. [nginx.conf — Angular Container Routing](#nginxconf--angular-container-routing)
5. [Local Development Setup](#local-development-setup)
6. [Production Deployment Overview](#production-deployment-overview)
7. [Network Architecture](#network-architecture)
8. [Volume Architecture](#volume-architecture)

---

## Container Overview

### Infra project containers (separate repo — shared on VPS)

These containers are **not** managed by this project. They are prerequisites that must already be running.

| Container | Image | Exposes (host) | Purpose |
|---|---|---|---|
| `traefik` | `traefik:latest` | `80`, `443`, `8080` | Reverse proxy, SSL termination, HTTP→HTTPS redirect |
| `mssql` | MCR SQL Server 2022 | `1433` | SQL Server database |
| `postgres` | `postgres:15` | — | PostgreSQL database |
| `kafka` | Confluent Kafka | `9092` | Message broker |
| `redis` | `redis:7` | `6379` | Cache / session store |

### This project's containers

| Container | Image source | Exposes (host) | Internal port | Purpose |
|---|---|---|---|---|
| `db-deploy` | Built locally / pulled | — | — | One-shot: deploys SSDT dacpac schema to `mssql` |
| `dotnet-api` | Built / pulled | — (Traefik routes) | `8080` | ASP.NET Core 9 REST API |
| `flask-api` | Built / pulled | — | `5001` | Python Flask API (Yahoo Finance, AI agents) |
| `angular-ui` | Built / pulled | — (Traefik routes) | `80` | Angular SPA served via nginx |

All app containers join the `app-network` Docker bridge network (created and owned by the infra project, declared `external: true` here). Containers communicate using container names as hostnames (e.g. `http://dotnet-api:8080`, `http://flask-api:5001`, `Server=mssql`).

Traefik discovers the `dotnet-api` and `angular-ui` routes automatically via Docker labels and routes HTTPS traffic accordingly. No host port mappings are needed on the app containers.

---

## Dockerfiles

### angular-ui — Production (`Dockerfile`)

**Path:** `src/angular-ui/Dockerfile`  
**Used by:** CI/CD pipeline (builds the image pushed to Docker Hub), `docker-compose.yml` when run locally without override.

**Multi-stage build:**

```
Stage 1 — build (node:22.13.0-alpine)
  ├── Install npm dependencies (npm ci)
  ├── Rebuild esbuild native binary for Alpine/musl
  ├── Manually fetch @rollup/rollup-linux-x64-musl binary
  │   (Windows lockfiles don't include the musl variant needed in Alpine containers)
  ├── Copy application source
  └── Run: npm run build --configuration=production
        → produces /app/dist/ClientApp/browser

Stage 2 — serve (nginx:1.27.0-alpine)
  ├── Copy compiled static assets from Stage 1
  ├── Copy nginx.conf → /etc/nginx/conf.d/default.conf
  ├── Create non-root nginx user for security
  ├── Set ownership/permissions on nginx working directories
  ├── Switch to non-root USER nginx
  └── EXPOSE 80 → CMD ["nginx", "-g", "daemon off;"]
```

**Key design decisions:**
- Uses multi-stage build so the final image only contains nginx + static files (~50MB), not Node.js (~400MB)
- Runs as non-root user inside the container
- The rollup musl binary is fetched manually during build because `package-lock.json` generated on Windows doesn't include the Linux musl optional binary, which causes build failures in Alpine containers
- `npm run build -- --configuration=production` activates `fileReplacements` in `angular.json`, substituting `environment.prod.ts` for `environment.ts` — this is critical for correct API URLs in production

---

### angular-ui — Development (`Dockerfile.dev`)

**Path:** `src/angular-ui/Dockerfile.dev`  
**Used by:** `docker-compose.override.yml` for local development only.

```
FROM node:22-alpine
  ├── Install npm dependencies + rebuild esbuild + fetch musl rollup binary
  ├── Copy source files
  ├── EXPOSE 4200
  └── CMD: npx ng serve --host 0.0.0.0 --poll 2000
```

**Key differences from production:**
- Does **not** build — uses `ng serve` (webpack dev server with live reload)
- The source directory is **bind-mounted** from the host (`docker-compose.override.yml`), so code changes are reflected immediately without rebuilding the image
- Port `4200` instead of `80`/`443`
- `node_modules` is a separate named Docker volume (`angular-ui-node-modules`) so the Linux binary dependencies from the container image are not overwritten by the host's Windows `node_modules`

---

### dotnet-api (`Dockerfile`)

**Path:** `src/dotnet-api/Dockerfile`  
**Used by:** CI/CD pipeline, `docker-compose.yml`.

```
Stage 1 — build (mcr.microsoft.com/dotnet/sdk:9.0)
  ├── Copy .csproj files for all projects and run dotnet restore
  │   (restoring before copying all files enables Docker layer caching —
  │    dependencies are only re-downloaded when .csproj files change)
  ├── Copy all source files
  └── dotnet publish -c Release -o /app/publish

Stage 2 — final (mcr.microsoft.com/dotnet/aspnet:9.0)
  ├── Create non-root user: dotnetuser
  ├── Copy published output from Stage 1
  ├── Set ownership to dotnetuser
  ├── Switch to USER dotnetuser
  └── EXPOSE 8080 → ENTRYPOINT ["dotnet", "IRS.Api.dll"]
```

**Key design decisions:**
- SDK image (~700MB) is used only for building; the runtime image (~200MB) is used for the final container
- Listens on port `8080` internally — never `80` or `443` (those are for nginx)
- Runs as non-root user

**Note on ports:** The host mapping is `5000:8080` — so `http://localhost:5000` on your machine reaches the container's port 8080. Inside Docker, other containers reach it as `http://dotnet-api:8080`.

---

### flask-api (`Dockerfile`)

**Path:** `src/flask-api/Dockerfile`  
**Used by:** CI/CD pipeline, `docker-compose.yml`.

```
FROM python:3.12.1-slim
  ├── Install system dependencies:
  │     curl, gnupg, ca-certificates (build tools)
  │     msodbcsql18, unixodbc-dev (Microsoft ODBC Driver 18 for SQL Server)
  ├── Copy requirements.txt and run pip install
  ├── Create non-root user: flask
  ├── Copy application source
  ├── Set ownership to flask user
  ├── Switch to USER flask
  ├── EXPOSE 5001
  ├── HEALTHCHECK: python -c "import requests; requests.get('http://localhost:5001/health')"
  └── CMD: gunicorn --config gunicorn.conf.py yahoo_app:app
```

**Key design decisions:**
- Based on `slim` variant (no dev tools) to keep image small
- Uses **gunicorn** (production WSGI server) in all environments, not Flask's built-in dev server
- ODBC Driver 18 is installed from Microsoft's apt repo — required to connect to SQL Server
- Runs as non-root user

**Development override:** `docker-compose.override.yml` replaces the CMD with `flask run` (with `--reload`) and bind-mounts the source directory for hot reload.

---

### db-deploy (`Dockerfile`)

**Path:** `tools/db-deploy/Dockerfile`  
**Used by:** `docker-compose.yml` only (not in VPS deployment — schema is already deployed).

```
FROM mcr.microsoft.com/dotnet/sdk:9.0
  ├── Install: msodbcsql18, mssql-tools18 (sqlcmd)
  ├── Download and install sqlpackage (Microsoft's DACPAC deployment tool)
  ├── Copy pre-built .dacpac file from src/database/out/
  ├── Copy deploy.sh script
  └── ENTRYPOINT: ["/deploy/deploy.sh"]
```

**Purpose:** This is a **one-shot container** — it starts, deploys the SSDT database schema to SQL Server using `sqlpackage`, then exits. It does not remain running.

The DACPAC (Data-tier Application Package) contains the full SQL Server schema definition. `sqlpackage` performs a differential deployment — only applying the changes needed to bring the database to the current schema state.

**Dependency:** `docker-compose.yml` configures `dotnet-api` to depend on `db-deploy` completing successfully (`condition: service_completed_successfully`). This ensures the schema exists before the API starts.

**VPS note:** In `docker-compose.vps.yml`, `db-deploy` is absent. The database schema is assumed to already be deployed. Schema migrations must be handled manually on the VPS when needed.

---

## Docker Compose Files

### `docker-compose.yml` — Base

**When used:** Always — this is the base file for all local development and CI/CD testing.

**What it defines:**
- All five services: `sqlserver`, `db-deploy`, `dotnet-api`, `flask-api`, `angular-ui`
- Builds images from local source code (`build:` directives) — no pre-built images
- Shared `app-network` bridge network
- Named volume `sqlserver-data` for database persistence
- Health checks on all services
- Environment variables read from `.env` file automatically

**Service startup order:**
```
sqlserver (health check)
    └── db-deploy (waits for sqlserver healthy)
            └── dotnet-api (waits for db-deploy completed_successfully)
                    └── angular-ui (waits for dotnet-api)
    └── flask-api (waits for sqlserver healthy, in parallel with dotnet-api)
```

**Used with:**
- Locally: `docker compose up` (automatically merges with `docker-compose.override.yml`)
- CI/CD integration tests: `docker compose -f docker-compose.yml up -d`

---

### `docker-compose.override.yml` — Local Dev

**When used:** Automatically merged by Docker Compose when you run `docker compose up` in the project root. You never need to specify it explicitly.

**What it overrides for local development:**

| Service | Override |
|---|---|
| `dotnet-api` | Uses `development` build target; bind-mounts `./src/dotnet-api:/app`; runs `dotnet watch run` for hot reload; sets `ASPNETCORE_ENVIRONMENT: Development` |
| `flask-api` | Bind-mounts `./src/flask-api:/app`; loads `.env` via `env_file:`; runs `flask run --reload` instead of gunicorn |
| `angular-ui` | Uses `Dockerfile.dev` instead of `Dockerfile`; bind-mounts source; uses named volume for `node_modules`; exposes `4200:4200`; runs `ng serve` |

**Key concept — bind mounts + named volume for node_modules:**

```yaml
volumes:
  - ./src/angular-ui:/app:rw          # host source mounted into container
  - angular-ui-node-modules:/app/node_modules  # container's own node_modules (Linux binaries)
```

Without the `node_modules` named volume, the host's Windows `node_modules` (containing Windows binaries) would overwrite the container's Linux binaries, breaking the build.

---

### `docker-compose.prod.yml` — Production Reference

**When used:** Not used in the current VPS deployment. This file is a reference/template for a more traditional server-based deployment (e.g. with a load balancer, external volumes on mounted block storage).

**What it defines:**
- Removes direct port exposures on `dotnet-api` and `flask-api` (traffic goes through load balancer)
- Adds `restart: always` to all services
- Configures `sqlserver` with a bind-mounted external volume (`/data/sqlserver`)
- Sets Docker network MTU to `1450` (common requirement in cloud/overlay networks)

This file would be used as:
```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

---

### `docker-compose.vps.yml` — VPS Deployment

**When used:** On the VPS only. Deployed via the CI/CD pipeline (`appleboy/scp-action`) and run via SSH.

**Key differences from `docker-compose.yml`:**
- Uses **pre-built images from Docker Hub** (`image: eshivakant/irs-*:latest`) — does not build from source
- No `sqlserver` service — SQL Server (`mssql`) is provided by the infra project
- No `db-deploy` service — schema is already deployed via `db-deploy` which runs as a one-shot container during initial setup; re-run manually when schema changes
- `app-network` is declared as `external: true` — must exist before this stack starts (created by infra project)
- All secrets injected from the VPS `.env` file via `${VAR_NAME}` interpolation
- CORS allowed origins explicitly set for the VPS IP and domain
- **Traefik labels** on `dotnet-api` and `angular-ui` register routes with the Traefik instance (infra project) — no host port mappings needed
- No `depends_on` conditions (faster startup; health is managed by healthchecks)

**How Traefik routing is registered:**

```
Container starts → Docker daemon notifies Traefik
  │  (Traefik watches /var/run/docker.sock)
  ▼
Traefik reads labels:
  traefik.http.routers.irs-api.rule=Host(`irs.ruvca-investments.com`) && PathPrefix(`/api`)
  traefik.http.services.irs-api.loadbalancer.server.port=8080
  │
  ▼
Traefik creates router: HTTPS /api/* → dotnet-api:8080
```

**How secrets flow:**

```
VPS /IRS/.env
  │  (read by docker compose at startup)
  ▼
docker-compose.vps.yml  ${VAR_NAME} references
  │  (expanded to actual values)
  ▼
Container environment variables
```

---

## `nginx.conf` — Angular Container Routing

**Path:** `src/angular-ui/nginx.conf`  
**Baked into:** the production `angular-ui` Docker image at `/etc/nginx/conf.d/default.conf`

In the current architecture, **nginx only serves the Angular SPA static files on port 80** inside the container. SSL termination and HTTP→HTTPS redirect are handled externally by Traefik (infra project) before traffic ever reaches nginx.

```nginx
# HTTP only — Traefik handles HTTPS and redirect externally
server {
    listen 80;
    server_name localhost irs.ruvca-investments.com;
    root /usr/share/nginx/html;
    index index.html;

    # Angular SPA — all unmatched routes fall back to index.html
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

> **Note:** The `/api` proxy block and the HTTPS (443) server block that existed in the old nginx config are now dead code in the VPS deployment. Traefik routes `/api/*` directly to `dotnet-api:8080` before the request reaches `angular-ui`. If you see those blocks in the baked image, they are harmless but unused — Traefik's routing rules take precedence.

**How API calls work in production:**

```
Browser → https://irs.ruvca-investments.com/api/v1/auth/login
  │
  ▼ (Traefik matches PathPrefix(`/api`) rule)
Traefik (infra project)
  │  routes to dotnet-api:8080
  ▼
dotnet-api container (internal Docker network)
```

The Angular app uses `apiBaseUrl: ''` (empty string in `environment.prod.ts`), so API calls go to `/api/...` — the same origin. Traefik splits the traffic at the edge: `/api/*` → `dotnet-api`, everything else → `angular-ui`.

---

## Local Development Setup

### Prerequisites

- Docker Desktop (Windows/Mac) or Docker Engine + Compose plugin (Linux)
- Node.js 22+ (for running `ng` CLI commands outside Docker, optional)
- `.env` file in project root (copy from a team member or create from the reference above)

### Starting the full stack

```bash
cd C:\Work\IRS-Dockerised

# Start everything (docker-compose.yml + docker-compose.override.yml auto-merged)
docker compose up
```

On first run this will:
1. Build all images from source
2. Start SQL Server and wait for it to be healthy
3. Run `db-deploy` to apply the database schema
4. Start `dotnet-api` with `dotnet watch run` (hot reload)
5. Start `flask-api` with `flask run --reload` (hot reload)
6. Start `angular-ui` with `ng serve` (webpack dev server, hot reload)

### Access points (local)

| Service | URL |
|---|---|
| Angular UI | http://localhost:4200 |
| .NET API | http://localhost:5000 |
| Flask API | http://localhost:5001 |
| SQL Server | `localhost,1433` (SA credentials from `.env`) |

### Stopping

```bash
docker compose down          # stop and remove containers (keeps volumes)
docker compose down -v       # stop and remove containers AND volumes (deletes DB data)
```

### Rebuilding after code changes

The dev setup uses bind mounts + hot reload — most code changes apply automatically without rebuilding. You only need to rebuild if you:
- Change `package.json` (npm dependencies)
- Change `requirements.txt` (Python dependencies)
- Change `.csproj` files (NuGet dependencies)

```bash
docker compose up --build
```

### Viewing logs

```bash
docker compose logs -f                      # all services
docker compose logs -f dotnet-api           # single service
docker compose logs -f flask-api angular-ui # multiple services
```

---

## Production Deployment Overview

In production, the CI/CD pipeline builds the images locally (not on the VPS) and deploys pre-built images:

```
Developer pushes to main
  │
  ▼
GitHub Actions (.github/workflows/docker-build.yml)
  │
  ├── security-scan job
  │     ├── TruffleHog secret scan
  │     └── Check .gitignore configuration
  │
  ├── build job (matrix: dotnet-api, flask-api, angular-ui)
  │     ├── docker buildx build --platform linux/amd64
  │     └── push to Docker Hub as eshivakant/irs-{service}:latest
  │
  ├── scan-images job
  │     └── Trivy vulnerability scan → GitHub Security tab
  │
  └── deploy job
        ├── scp docker-compose.vps.yml → /IRS on VPS
        └── ssh into VPS:
              docker compose -f docker-compose.vps.yml pull
              docker compose -f docker-compose.vps.yml up -d --force-recreate
              docker image prune -f
```

**On the VPS**, Docker Compose reads `docker-compose.vps.yml` which pulls images from Docker Hub — it does not build from source. The VPS only needs Docker + the compose file + the `.env` file.

---

## Network Architecture

```
Internet / Browser
  │
  ▼ (Cloudflare DNS proxy — Full SSL mode)
VPS :80 / :443
  │
  ▼
┌──────────────────────────────────────────────────────────────┐
│  traefik container  (infra project)                          │
│  :80  → HTTP catchall → redirect to HTTPS (dynamic.yml)      │
│  :443 → TLS termination (Let's Encrypt auto-cert)            │
│  :8080 → dashboard (internal only)                           │
│                                                              │
│  Routing rules (from Docker labels on app containers):       │
│    Host(`irs…`) && PathPrefix(`/api`) → dotnet-api:8080      │
│    Host(`irs…`)                       → angular-ui:80        │
└──────────────────────────────┬───────────────────────────────┘
                               │  docker network: app-network
┌──────────────────────────────┼───────────────────────────────┐
│                              │                               │
│  ┌───────────────┐    ┌──────┴──────┐    ┌───────────────┐  │
│  │  angular-ui   │    │  dotnet-api │    │   flask-api   │  │
│  │  nginx :80    │    │  :8080      │───▶│   :5001       │  │
│  └───────────────┘    └──────┬──────┘    └───────────────┘  │
│                              │                               │
│                              ▼                               │
│                     ┌──────────────┐                         │
│                     │    mssql     │  (infra project)        │
│                     │    :1433     │                         │
│                     └──────────────┘                         │
└──────────────────────────────────────────────────────────────┘
```

**Hostname resolution inside Docker:**  
Containers reach each other by container name:
- `http://dotnet-api:8080`
- `http://flask-api:5001`
- `Server=mssql;Port=1433`

**External access:**  
No app container has ports exposed directly to the host. All inbound traffic enters through Traefik on ports 80/443. The Traefik dashboard is available internally on port 8080 (not exposed to the internet).

---

## Volume Architecture

| Volume | Type | Used by | Purpose |
|---|---|---|---|
| `sqlserver-data` | Named Docker volume | `sqlserver` (local dev only) | Persists all database data across container restarts |
| `angular-ui-node-modules` | Named Docker volume (dev only) | `angular-ui` (dev) | Stores Linux npm binaries; prevents Windows host `node_modules` from overwriting them |

**Note:** On the VPS, database persistence is handled by the infra project (named volume `mssql_data` on the `mssql` container). This project does not declare any persistent volumes for VPS use. Never run `docker compose down -v` in the infra project unless you want to wipe all database data.
