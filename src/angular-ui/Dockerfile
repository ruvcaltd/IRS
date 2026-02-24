# ── Build stage ──
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
# install dependencies. the lockfile generated on Windows may not
# include the musl-specific binary, so add it manually afterwards.
# we need curl to fetch the tarball
RUN apk add --no-cache curl \
    && npm ci && npm rebuild esbuild \
    # ensure musl rollup binary is present even if lockfile is Windows-only
    && mkdir -p node_modules/@rollup/rollup-linux-x64-musl \
    && curl -L https://registry.npmjs.org/@rollup/rollup-linux-x64-musl/-/rollup-linux-x64-musl-4.59.0.tgz \
        | tar -xz --strip-components=1 -C node_modules/@rollup/rollup-linux-x64-musl || true

COPY . .
RUN npm run build -- --configuration=production

# ── Serve stage ──
FROM nginx:alpine
COPY --from=build /app/dist/angular-ui/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
