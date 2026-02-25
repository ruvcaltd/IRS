# ── Build stage ──
FROM node:22.13.0-alpine AS build
WORKDIR /app
COPY package*.json ./
# install dependencies. the lockfile generated on Windows may not
# include the musl-specific binary, so add it manually afterwards.
# we need curl to fetch the tarball
RUN apk add --no-cache curl \
    && npm ci && npm rebuild esbuild \
    # ensure musl rollup binary is present even if lockfile is Windows-only
    && mkdir -p node_modules/@rollup/rollup-linux-x64-musl \
    && curl -fsSL https://registry.npmjs.org/@rollup/rollup-linux-x64-musl/-/rollup-linux-x64-musl-4.59.0.tgz \
        | tar -xz --strip-components=1 -C node_modules/@rollup/rollup-linux-x64-musl || true

COPY . .
RUN npm run build -- --configuration=production

# ── Serve stage ──
FROM nginx:1.27.0-alpine

# Add non-root user for security (ignore if exists)
RUN addgroup -g 1001 -S nginx || true && \
    adduser -S nginx -u 1001 || true

COPY --from=build /app/dist/angular-ui/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Set permissions
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d && \
    touch /var/run/nginx.pid && \
    chown -R nginx:nginx /var/run/nginx.pid

# Run as non-root user
USER nginx

EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=30s \
    CMD wget --quiet --tries=1 --spider http://localhost/index.html || exit 1

CMD ["nginx", "-g", "daemon off;"]
