# Production Deployment Guide

**Document Version:** 2.0  
**Last Updated:** February 27, 2026  
**Live URL:** https://irs.ruvca-investments.com

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [First-Time VPS Setup](#first-time-vps-setup)
3. [GitHub Actions CI/CD Setup](#github-actions-cicd-setup)
4. [SSL Certificate (Let's Encrypt)](#ssl-certificate-lets-encrypt)
5. [Custom Domain via Cloudflare](#custom-domain-via-cloudflare)
6. [Environment Variables Reference](#environment-variables-reference)
7. [Ongoing Operations](#ongoing-operations)
8. [Troubleshooting — Lessons Learned](#troubleshooting--lessons-learned)

---

## Architecture Overview

```
Browser
  │
  ▼
Cloudflare (DNS proxy + SSL termination)
  │  HTTPS :443
  ▼
VPS (185.249.73.172)
  │
  ├── angular-ui container  :80 / :443
  │     ├── serves Angular SPA static files
  │     └── proxies /api/* → dotnet-api:8080
  │
  ├── dotnet-api container  :8080 (internal), :5000 (host)
  │     └── connects to flask-api:5001, sqlserver:1433
  │
  ├── flask-api container   :5001
  │     └── connects to sqlserver:1433, Yahoo Finance API
  │
  └── sqlserver container   :1433
        └── persisted to named Docker volume: sqlserver-data
```

All containers communicate on a private Docker bridge network (`app-network`).
Only `angular-ui` is exposed to the internet on ports 80/443.

---

## First-Time VPS Setup

### 1. Create the working directory on the VPS

```bash
mkdir -p /IRS
cd /IRS
```

### 2. Copy required files from your local machine

Run this **from your local machine** (Windows PowerShell or WSL):

```bash
scp docker-compose.vps.yml docker-compose.yml .env root@185.249.73.172:/IRS
```

> **Important:** `.env` is gitignored and **must always be copied manually**. It is never committed to git.
> Every time you add a new variable to `.env`, copy it to the VPS again.

### 3. Start the stack

```bash
cd /IRS
docker compose -f docker-compose.vps.yml pull
docker compose -f docker-compose.vps.yml up -d
```

### 4. Verify all containers are healthy

```bash
docker compose -f docker-compose.vps.yml ps
docker logs dotnet-api --tail 20
docker logs flask-api --tail 20
docker logs angular-ui --tail 20
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

SSL is terminated on the VPS inside the nginx (`angular-ui`) container using free Let's Encrypt certificates.
The cert files live on the VPS host and are mounted as a read-only volume into the container.

### Issuing the certificate (first time only)

> **Prerequisite:** The Cloudflare DNS A record for `irs.ruvca-investments.com` must be **grey cloud (DNS only / unproxied)** when running certbot. Certbot must be able to reach port 80 on the VPS directly to complete the ACME HTTP-01 challenge.

```bash
# Stop nginx to free port 80 for certbot's ACME challenge
docker stop angular-ui

# Install certbot
apt update && apt install -y certbot

# Issue the certificate
certbot certonly --standalone -d irs.ruvca-investments.com

# Fix permissions — certbot creates its dirs as mode 700 (root only).
# The nginx process inside Docker runs as a non-root user and cannot read them.
chmod 755 /etc/letsencrypt/live
chmod 755 /etc/letsencrypt/archive
chmod 755 /etc/letsencrypt/archive/irs.ruvca-investments.com
chmod 644 /etc/letsencrypt/archive/irs.ruvca-investments.com/*.pem

# Start nginx back up
docker start angular-ui

# Confirm it started cleanly — must see no [emerg] errors
docker logs angular-ui --tail 10
```

### Certificate auto-renewal (cron)

Let's Encrypt certificates expire every 90 days. This cron job renews them on the 1st of each month at 03:00:

```bash
echo "0 3 1 * * docker stop angular-ui && certbot renew --quiet && chmod 755 /etc/letsencrypt/live /etc/letsencrypt/archive /etc/letsencrypt/archive/irs.ruvca-investments.com && chmod 644 /etc/letsencrypt/archive/irs.ruvca-investments.com/*.pem && docker start angular-ui" | crontab -

# Verify it was saved
crontab -l
```

### Renewing manually

```bash
docker stop angular-ui
certbot renew
chmod 755 /etc/letsencrypt/live /etc/letsencrypt/archive /etc/letsencrypt/archive/irs.ruvca-investments.com
chmod 644 /etc/letsencrypt/archive/irs.ruvca-investments.com/*.pem
docker start angular-ui
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

After the cert is issued and nginx is running:

1. Switch DNS record to **orange cloud (Proxied)**
2. Go to **SSL/TLS → Overview**
3. Set mode to **Full**

| Mode | Browser→CF | CF→VPS | When to use |
|---|---|---|---|
| Off | HTTP | HTTP | Never — insecure |
| Flexible | HTTPS | HTTP | Only if no cert on VPS |
| **Full** | HTTPS | HTTPS | ✅ Our setup — real cert on VPS |
| Full (strict) | HTTPS | HTTPS | Requires specific CA chain |

> Do **not** use Flexible when you have a real cert — it causes redirect loops (nginx redirects HTTP → HTTPS, Cloudflare sends HTTP, infinite loop).

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
docker logs sqlserver --tail 50
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

### nginx fails to start — SSL cert not found

**Symptom:**
```
[emerg] cannot load certificate ".../fullchain.pem": No such file or directory
```

**Cause:** certbot has not been run yet. The cert files don't exist at `/etc/letsencrypt/`.

**Fix:** Run certbot (see [SSL Certificate](#ssl-certificate-lets-encrypt) section above).

---

### nginx fails to start — Permission denied on cert

**Symptom:**
```
[emerg] cannot load certificate ".../fullchain.pem": Permission denied
```

**Cause:** certbot creates `/etc/letsencrypt/live` and `/etc/letsencrypt/archive` with mode `700` (root-only). The nginx process inside Docker runs as a non-root user.

**Fix:**
```bash
chmod 755 /etc/letsencrypt/live
chmod 755 /etc/letsencrypt/archive
chmod 755 /etc/letsencrypt/archive/irs.ruvca-investments.com
chmod 644 /etc/letsencrypt/archive/irs.ruvca-investments.com/*.pem
docker restart angular-ui
```

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

