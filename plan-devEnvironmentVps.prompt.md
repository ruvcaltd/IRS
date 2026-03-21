# Plan: Dev Environment on VPS with Cloudflare Origin SSL

**TL;DR:** Deploy a fully isolated dev stack at `irs-dev.ruvca-investments.com` to the same VPS. The dev `angular-ui` container binds port `8443:443` — production keeps `443:443` untouched. Cloudflare uses an **Origin Rule** (free tier) to forward `irs-dev` traffic to port 8443 on the VPS, and authenticates via a wildcard **Cloudflare Origin Certificate** (no certbot, no renewal). The dev stack is a separate Docker project (`irs-dev`) in `/IRS-dev/` on the VPS. A new `deploy-dev` GitHub Actions job fires on every push to `develop`, pushing images tagged `:dev`.

---

## One-time VPS & Cloudflare setup (manual)

1. **Cloudflare Origin Certificate** (Cloudflare dashboard → SSL/TLS → Origin Server): Create a wildcard cert for `*.ruvca-investments.com` (15-year validity). Download `origin-cert.pem` and `origin-key.pem`. SCP both files to `/etc/cloudflare-origin/` on the VPS.

2. **VPS firewall:** `ufw allow 8443/tcp` to open the dev HTTPS port.

3. **Cloudflare DNS:** Add an A record `irs-dev` → `185.249.73.172` (orange cloud / proxied), SSL mode **Full**.

4. **Cloudflare Origin Rule** (Rules → Origin Rules → Create): Condition: `hostname equals irs-dev.ruvca-investments.com` → Override destination port to `8443`. This makes Cloudflare connect to the VPS on 8443 instead of 443, leaving production's port 443 clean.

5. **VPS — create IRS_Dev database:** `docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<pw>" -C -Q "CREATE DATABASE IRS_Dev;"` then run the dacpac schema deploy scripts targeting `IRS_Dev`.

---

## Repo changes

6. **New `src/angular-ui/nginx.dev.conf`:** Copy of `nginx.conf` but:
   - Port 80 block: remove the `irs.ruvca-investments.com` redirect — just serve the SPA and `/api` proxy (handles Cloudflare health checks)
   - Port 443 `server_name irs-dev.ruvca-investments.com`
   - `ssl_certificate /etc/cloudflare-origin/origin-cert.pem;`
   - `ssl_certificate_key /etc/cloudflare-origin/origin-key.pem;`
   - Proxy `/api` upstream to `http://dotnet-api:8080` (same as prod)
   - Add `set_real_ip_from` Cloudflare IP ranges + `real_ip_header CF-Connecting-IP` (since traffic comes via Cloudflare proxy)

7. **New `docker-compose.dev-vps.yml`** at repo root:
   - Top-level `name: irs-dev` (keeps it a separate Docker project from prod)
   - All three services pull `:dev`-tagged images (`eshivakant/irs-*:dev`)
   - `angular-ui`: ports `80:80` and `8443:443`; two volumes — `/etc/cloudflare-origin:/etc/cloudflare-origin:ro` and a bind mount of `./nginx.dev.conf:/etc/nginx/conf.d/default.conf:ro` so the dev nginx config is used without a separate build
   - `dotnet-api`: no host-port exposure; `ASPNETCORE_ENVIRONMENT: Development`; connection string `Server=host.docker.internal,1433;Database=IRS_Dev;...`; CORS origins set to `https://irs-dev.ruvca-investments.com`; `extra_hosts: ["host.docker.internal:host-gateway"]`
   - `flask-api`: no host-port exposure; same `host.docker.internal` connection string targeting `IRS_Dev`; `extra_hosts` same as above
   - No `sqlserver` service (shares production's SQL Server via host bridge)
   - `env_file: .env` (dev `.env` placed manually at `/IRS-dev/.env` on VPS)
   - Own named network `irs-dev-network`

8. **New `.env.dev.template`** at repo root: Clone of `.env.template` with `DB_NAME=IRS_Dev`, `VPS_DOMAIN=irs-dev.ruvca-investments.com`, and a header comment indicating it is the dev environment template.

9. **Update `.github/workflows/docker-build.yml` — build job:** In the `Build and push ${{ matrix.service }}` step, replace the hardcoded `tags` block with a conditional:
   - On `main`: push `:latest` and `:<sha>` (existing behaviour)
   - On `develop`: push `:dev` and `:<sha>` (new)
   - Use a `Set image tags` step before `build-push-action` that sets an output variable based on `github.ref_name`, then reference it in `tags`.

10. **Update `.github/workflows/docker-build.yml` — new `deploy-dev` job:**
    - `needs: [build]`, condition: `github.event_name == 'push' && github.ref == 'refs/heads/develop'`
    - Uses same `production` GitHub Actions environment (same three secrets)
    - SCP step: copies `docker-compose.dev-vps.yml` **and** `src/angular-ui/nginx.dev.conf` to `/IRS-dev/` on the VPS
    - SSH step: `cd /IRS-dev && docker compose pull && docker compose up -d --force-recreate && docker image prune -f`

11. **Update `notify` job** in `.github/workflows/docker-build.yml`: add `deploy-dev` to the `needs` array alongside `deploy` (use `if: always()` so it doesn't fail when only one deploy ran).

---

## Verification

- Push a commit to `develop` → GitHub Actions `deploy-dev` job completes green
- Visit `https://irs-dev.ruvca-investments.com` → Angular app loads with padlock (Cloudflare HTTPS)
- Open Network tab → `/api/health` returns 200 from dev dotnet-api
- Verify dev dotnet-api is hitting `IRS_Dev` database (check `SELECT DB_NAME()` or a known seeded value difference)
- Push a commit to `main` → confirm prod stack (`https://irs.ruvca-investments.com`) is completely unaffected

---

## Decisions

- **Port 8443 + Cloudflare Origin Rule** over a host-level nginx proxy — keeps prod and dev containers fully independent, no shared proxy that could take both down
- **Cloudflare Origin Certificate** (wildcard, 15-year) over Let's Encrypt — zero ongoing maintenance, works perfectly behind Cloudflare's proxy
- **`host.docker.internal` to shared SQL Server** over a second SQL Server container — cheaper on VPS RAM; prod SQL Server is already running and its port 1433 is already bound to the host
- **Separate Docker project name `irs-dev`** — prevents any accidental container name collisions with the prod `irs` project
