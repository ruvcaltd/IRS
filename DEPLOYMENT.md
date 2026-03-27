# Production Deployment Guide

**Document Version:** 3.0  
**Last Updated:** March 27, 2026  
**Live URL:** https://irs.ruvca-investments.com

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Infrastructure Dependencies](#infrastructure-dependencies)
3. [First-Time VPS Setup](#first-time-vps-setup)
4. [GitHub Actions CI/CD Setup](#github-actions-cicd-setup)
5. [SSL Certificate (Let's Encrypt)](#ssl-certificate-lets-encrypt)
6. [Custom Domain via Cloudflare](#custom-domain-via-cloudflare)
7. [Environment Variables Reference](#environment-variables-reference)
8. [Ongoing Operations](#ongoing-operations)
9. [Troubleshooting — Lessons Learned](#troubleshooting--lessons-learned)

---

## Architecture Overview

The VPS is split into two independent Docker Compose projects:

| Project | Owns | Location on VPS |
|---|---|---|
| **Infrastructure** (separate repo) | Traefik, SQL Server (`mssql`), PostgreSQL, Kafka, Redis, `app-network` | `/infra/` |
| **IRS App** (this repo) | `dotnet-api`, `flask-api`, `angular-ui`, `db-deploy` | `/IRS/` |

```
Browser
  │
  ▼
Cloudflare (DNS proxy — Full SSL mode)
  │  HTTPS :443
  ▼
VPS (185.249.73.172)
  │
  ▼
Traefik :443  ← infra project
  │  (terminates TLS, routes by Host + PathPrefix)
  │
  ├── Host(`irs.ruvca-investments.com`) && PathPrefix(`/api`)
  │     └── dotnet-api:8080  ← this project
  │           └── connects to flask-api:5001, mssql:1433
  │
  └── Host(`irs.ruvca-investments.com`)
        └── angular-ui:80  ← this project
              (serves Angular SPA static files)
```

All containers — from both projects — share a single Docker bridge network called `app-network`, which is **created and owned by the infrastructure project**. This project declares it as `external: true`.

HTTP→HTTPS redirect is handled globally by Traefik's dynamic config (`dynamic.yml`). SSL certificates are obtained and auto-renewed by Traefik via Let's Encrypt (TLS-ALPN-01 challenge). No manual cert management is required.

---

## Infrastructure Dependencies

This project assumes the **infrastructure project is already running** on the same VPS before this stack is deployed.

The infrastructure project must provide:
- `app-network` Docker bridge network
- `mssql` container (SQL Server 2022) reachable on `app-network` as hostname `mssql`
- `traefik` container listening on ports `80` and `443`, watching `app-network` for Docker labels

Verify infrastructure is ready before deploying:
```bash
# Network exists
docker network inspect app-network

# Traefik is running
docker ps | grep traefik

# SQL Server is healthy
docker inspect mssql --format='{{.State.Health.Status}}'
```

---

## First-Time VPS Setup

### 0. Prerequisite: infrastructure project must be running

Before deploying this project, the infrastructure project must already be up on the VPS with `app-network`, `mssql`, and `traefik` running. See the infrastructure project README for setup instructions.

```bash
# Confirm prerequisites
docker network inspect app-network
docker ps --filter name=traefik --filter name=mssql
```

### 1. Create the working directory on the VPS

```bash
mkdir -p /IRS
cd /IRS
```

### 2. Copy required files from your local machine

Run this **from your local machine** (Windows PowerShell or WSL):

```bash
scp docker-compose.vps.yml .env root@185.249.73.172:/IRS
```

> **Important:** `.env` is gitignored and **must always be copied manually**. It is never committed to git.
> Every time you add a new variable to `.env`, copy it to the VPS again.

### 3. Start the stack

```bash
cd /IRS
docker compose -f docker-compose.vps.yml pull
docker compose -f docker-compose.vps.yml up -d
```

### 4. Verify all containers are healthy and routes are registered

```bash
docker compose -f docker-compose.vps.yml ps
docker logs dotnet-api --tail 20
docker logs flask-api --tail 20
docker logs angular-ui --tail 20

# Confirm Traefik has picked up both routes
curl http://localhost:8080/api/http/routers | python3 -m json.tool | grep -E 'name|rule'
```

---

## GitHub Actions CI/CD Setup

Every push to `main` automatically:

1. Scans for exposed secrets (TruffleHog)
2. Builds Docker images for `dotnet-api`, `flask-api`, `angular-ui`
3. Pushes images to Docker Hub (`eshivakant/irs-*:latest`)
4. Scans images for vulnerabilities (Trivy)
5. SCPs the updated `docker-compose.vps.yml` to the VPS
6. SSHs into VPS → pulls new images → recreates all containers

### Required GitHub Secrets

> Secrets must live in a GitHub **Environment** named `production`.
> Go to: **Repository → Settings → Environments → New environment → production**
> Then add secrets inside that environment (not at repo level).

| Secret | Value |
|---|---|
| `DOCKERHUB_USERNAME` | `eshivakant` |
| `DOCKERHUB_TOKEN` | Docker Hub access token (not your password — generate at hub.docker.com → Account Settings → Security) |
| `VPS_SSH_PRIVATE_KEY` | Contents of the SSH private key that has access to the VPS (see below) |

### Generating the SSH deploy key

```bash
# On your local machine
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/github_deploy_key -N ""

# Add public key to VPS authorized_keys
ssh-copy-id -i ~/.ssh/github_deploy_key.pub root@185.249.73.172
# or manually on VPS: cat >> ~/.ssh/authorized_keys

# Print the private key — paste its full contents as VPS_SSH_PRIVATE_KEY secret
cat ~/.ssh/github_deploy_key
```

> **Critical:** The public key on the VPS and the private key in GitHub Secrets must be a matched pair.
> If the SSH connection fails, delete and recreate both — do not try to reuse old keys.

### How the deploy step works

The pipeline does **not** use `git pull` on the VPS (the `/IRS` directory is not a git clone — files are managed manually/via SCP). Instead:

1. `appleboy/scp-action` copies `docker-compose.vps.yml` from the GitHub Actions runner to `/IRS` on the VPS
2. `appleboy/ssh-action` then runs `docker compose pull` + `up -d --force-recreate`

This means `docker-compose.vps.yml` changes are always deployed automatically without manual SCP.
The `.env` file still requires manual management.

---

## SSL Certificate (Let's Encrypt)

SSL is **fully managed by Traefik** (part of the infrastructure project) using Let's Encrypt with the TLS-ALPN-01 challenge. There are no cert files to manage manually, no certbot to install, and no cron jobs needed.

Traefik stores the certificate in `acme.json` (a file volume inside the infrastructure project's directory on the VPS). Certificates **auto-renew** before expiry — Traefik handles this automatically in the background.

The `certresolver=letsencrypt` label on both `dotnet-api` and `angular-ui` instructs Traefik to issue/use a Let's Encrypt cert for `irs.ruvca-investments.com`.

### Important: Cloudflare must be grey cloud during initial cert issuance

Traefik's TLS-ALPN-01 challenge requires a direct TLS connection to the VPS on port 443. If Cloudflare is proxying (orange cloud), it intercepts port 443 and the ACME challenge will fail.

**Steps for first-time cert issuance:**
1. Set the Cloudflare DNS record for `irs.ruvca-investments.com` to **grey cloud (DNS only / unproxied)**
2. Start Traefik (infra project) and the app stack
3. Wait ~30 seconds — Traefik will automatically request the cert on first HTTPS traffic
4. Verify cert was issued: check Traefik dashboard at `http://<VPS_IP>:8080` → Certificates tab
5. Switch Cloudflare DNS back to **orange cloud (Proxied)**

For renewals, Traefik handles them automatically every ~60 days. No action needed.

### Verify cert status

```bash
# Check Traefik ACME status via API
curl -s http://localhost:8080/api/tls/certificates | python3 -m json.tool

# Or check the acme.json file (in infra project directory)
jq '.letsencrypt.Certificates[] | {domain: .domain.main, expiry: .NotAfter}' /infra/traefik/acme.json
```

---

## Custom Domain via Cloudflare

### Creating the subdomain DNS record

1. Log in to [dash.cloudflare.com](https://dash.cloudflare.com)
2. Select domain `ruvca-investments.com`
3. Go to **DNS → Records → Add record**
4. Set:
   - **Type:** `A`
   - **Name:** `irs`
   - **IPv4 address:** `185.249.73.172`
   - **Proxy status:** Grey cloud (DNS only) — required while issuing the cert

### Cloudflare SSL/TLS mode

After Traefik has issued the Let's Encrypt cert:

1. Switch DNS record to **orange cloud (Proxied)**
2. Go to **SSL/TLS → Overview**
3. Set mode to **Full**

| Mode | Browser→CF | CF→VPS | When to use |
|---|---|---|---|
| Off | HTTP | HTTP | Never — insecure |
| Flexible | HTTPS | HTTP | Only if no cert on VPS |
| **Full** | HTTPS | HTTPS | ✅ Our setup — Traefik has a real Let’s Encrypt cert |
| Full (strict) | HTTPS | HTTPS | Requires specific CA chain |

> Do **not** use Flexible when Traefik has a real cert — it causes redirect loops (Traefik redirects HTTP → HTTPS, Cloudflare sends HTTP, infinite loop).

### Per-subdomain SSL override (if primary domain needs different mode)

1. Cloudflare → **Rules → Configuration Rules → Create rule**
2. Condition: `Hostname equals irs.ruvca-investments.com`
3. Setting: **SSL → Full**
4. Save & Deploy

---

## Environment Variables Reference

### `.env` file (gitignored — never committed to git)

```dotenv
# ─── SQL Server ───────────────────────────────
SA_PASSWORD=YourStr0ng!Passw0rd123
DB_NAME=MyAppDb

# ─── JWT Authentication ──────────────────────
JWT_SECRET_KEY=your-secret-key-minimum-32-characters-long
JWT_ISSUER=MyApp
JWT_AUDIENCE=MyApp

# ─── API URLs (for Angular build args) ───────
DOTNET_API_BASE_URL=http://localhost:5000

# ─── VPS ─────────────────────────────────────
VPS_IP=185.249.73.172
VPS_DOMAIN=irs.ruvca-investments.com

# ─── External APIs ───────────────────────────
OPENFIGI_API_KEY=your-key
BRAVE_API_TOKEN=your-token
BRAVE_GOGGLES_URL=https://raw.githubusercontent.com/...

# ─── Encryption ──────────────────────────────
LLM_ENCRYPTION_KEY=32-char-key
ENCRYPTION_KEY=base64-encoded-32-byte-key
ENCRYPTION_IV=base64-encoded-16-byte-iv

# ─── Flask API credentials ───────────────────
Yahoo_Fin_user=your-yahoo-username
Yahoo_fin_secret=your-yahoo-password
SECRET_KEY=flask-session-secret-key
```

### Adding a new environment variable

1. Add to local `.env`
2. Add to `docker-compose.vps.yml` under the relevant service's `environment:` block: `NEW_VAR: "${NEW_VAR}"`
3. Add to VPS `.env`:
   ```bash
   echo "NEW_VAR=value" >> /IRS/.env
   ```
4. Recreate only the affected container:
   ```bash
   docker compose -f /IRS/docker-compose.vps.yml up -d --force-recreate <service-name>
   ```

> **Important:** `docker compose up -d` (without `--force-recreate`) does NOT restart containers that are already running, even if environment variables changed. Always use `--force-recreate` when applying env var changes.

---

## Ongoing Operations

### Deploy a change

Push to `main` — the CI/CD pipeline handles everything:
```bash
git push origin main
```

### Trigger a deployment without a code change

```bash
git commit --allow-empty -m "ci: trigger deployment"
git push origin main
```

### Check container status on VPS

```bash
docker compose -f /IRS/docker-compose.vps.yml ps
docker stats --no-stream
```

### View logs

```bash
docker logs angular-ui --tail 50 -f
docker logs dotnet-api --tail 50 -f
docker logs flask-api --tail 50 -f

# Traefik logs (infra project)
docker logs traefik --tail 50 -f
```

### Restart a single service

```bash
docker compose -f /IRS/docker-compose.vps.yml restart flask-api
```

### Force recreate a service (e.g. after `.env` changes)

```bash
docker compose -f /IRS/docker-compose.vps.yml up -d --force-recreate flask-api
```

### Verify environment variables inside a container

```bash
docker exec flask-api env | grep Yahoo
docker exec dotnet-api env | grep Cors
```

### Manual full redeploy (if CI/CD is unavailable)

```bash
# From local machine
scp docker-compose.vps.yml .env root@185.249.73.172:/IRS

# On VPS
cd /IRS
docker compose -f docker-compose.vps.yml pull
docker compose -f docker-compose.vps.yml up -d --force-recreate
docker image prune -f
```

---

## Troubleshooting — Lessons Learned

### CORS error: request blocked by browser

**Symptom:**
```
Access to XMLHttpRequest at 'http://localhost:5000/api/v1/...' from origin 'http://185.249.73.172' blocked by CORS policy
```

**Root cause 1 — Angular building with dev environment:**  
`angular.json` was missing `fileReplacements` in the production configuration, so `environment.ts` (with `apiBaseUrl: 'http://localhost:5000'`) was always bundled instead of `environment.prod.ts` (with `apiBaseUrl: ''`).

**Fix:** Ensure `angular.json` production config has:
```json
"production": {
  "fileReplacements": [
    {
      "replace": "src/environments/environment.ts",
      "with": "src/environments/environment.prod.ts"
    }
  ]
}
```

**Root cause 2 — CORS allowed origins missing the VPS IP/domain:**  
The dotnet-api CORS policy only listed localhost. The browser's origin (`http://185.249.73.172` or `https://irs.ruvca-investments.com`) was not in the allowed list.

**Fix:** `docker-compose.vps.yml` now injects:
```yaml
Cors__AllowedOrigins__0: "http://${VPS_IP}"
Cors__AllowedOrigins__1: "http://${VPS_DOMAIN}"
Cors__AllowedOrigins__2: "https://${VPS_DOMAIN}"
```

---

### Flask API returns 401 Unauthorized

**Symptom:** `Failed to authenticate: {"error":"Invalid credentials"}`

**Diagnosis:**
```bash
docker exec flask-api env | grep Yahoo
```
If this returns nothing — the container doesn't have the vars.

**Root cause 1 — Variables not in VPS `.env`:**
The VPS `.env` was missing `Yahoo_Fin_user`, `Yahoo_fin_secret`, `SECRET_KEY`.

**Root cause 2 — Old `docker-compose.vps.yml` on VPS:**
The compose file was updated locally and committed, but the VPS still had the old version without the env var mappings. The CI/CD pipeline now copies `docker-compose.vps.yml` to the VPS on every deploy via `scp-action`.

**Root cause 3 — Agent DB record has wrong password:**
The agent record in the database stores an AES-encrypted password. When decrypted, it must exactly match `Yahoo_fin_secret`. If the agent was created in the UI with a different password, update it via the Angular UI.

**Fix sequence:**
1. Verify VPS `.env` has the Yahoo vars
2. Recreate flask-api: `docker compose -f /IRS/docker-compose.vps.yml up -d --force-recreate flask-api`
3. Verify: `docker exec flask-api env | grep Yahoo`
4. If still failing — update the agent's password in the Angular UI to match `Yahoo_fin_secret`

---

### Traefik route not appearing in dashboard

**Symptom:** The `irs-api` or `irs-frontend` router is missing from `http://<VPS_IP>:8080/dashboard/`.

**Check 1 — Container is actually running:**
```bash
docker ps | grep dotnet-api
docker logs dotnet-api --tail 30
```
If the container crashed, check whether the `dotnet-api` healthcheck is passing and whether `mssql` is reachable on `app-network`.

**Check 2 — Container is on `app-network`:**
```bash
docker network inspect app-network | grep dotnet-api
```
If missing, the container isn't attached to the network Traefik is watching.

**Check 3 — Labels are present:**
```bash
docker inspect dotnet-api --format='{{json .Config.Labels}}' | python3 -m json.tool
```
Expect to see `traefik.enable=true` and the router rule labels.

**Check 4 — Traefik provider configuration (infra project):**
In `traefik.yml`, confirm:
```yaml
providers:
  docker:
    network: app-network
    exposedByDefault: false
```

> **Note:** The old `nginx fails to start — SSL cert not found` and `Permission denied on cert` errors no longer apply. SSL is handled entirely by Traefik in the infra project. The `angular-ui` nginx container only serves static files on port 80 inside Docker — there are no cert file mounts on it.

---

### GitHub Actions deploy fails — "not a git repository"

**Symptom:** `fatal: not a git repository (or any of the parent directories): .git`

**Cause:** `/IRS` on the VPS was created manually (not via `git clone`), so there is no `.git` directory. A `git pull` step in the deploy script will always fail.

**Fix:** Remove `git pull` from the deploy script. Use `appleboy/scp-action` to copy changed files instead.

---

### GitHub Actions fails — Docker Hub authentication error

**Symptom:** `Error: Username and password required` or empty credentials

**Cause:** The GitHub Actions job is missing `environment: production`, so secrets stored in the `production` environment are not injected.

**Fix:** Add to every job that needs Docker Hub credentials:
```yaml
environment: production
```

---

### SSH handshake failure in GitHub Actions

**Symptom:** `ssh: handshake failed: ssh: unable to authenticate`

**Cause:** The public key in `~/.ssh/authorized_keys` on the VPS does not match the private key stored in the `VPS_SSH_PRIVATE_KEY` GitHub secret.

**Fix:**
```bash
# Generate a fresh key pair
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/deploy_key -N ""

# Add public key to VPS
ssh-copy-id -i ~/.ssh/deploy_key.pub root@185.249.73.172

# Delete VPS_SSH_PRIVATE_KEY secret in GitHub and recreate it
# with the content of: cat ~/.ssh/deploy_key
```

---

### Cloudflare — infinite redirect loop

**Symptom:** Browser shows `ERR_TOO_MANY_REDIRECTS`

**Cause:** Cloudflare SSL mode is set to **Flexible** while nginx is also redirecting HTTP → HTTPS. Cloudflare sends HTTP to the VPS, nginx redirects to HTTPS, Cloudflare receives the redirect and sends HTTP again — infinite loop.

**Fix:** Set Cloudflare SSL/TLS mode to **Full** (since we have a real cert on the VPS).

