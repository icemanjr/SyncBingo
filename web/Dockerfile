# Build context: web/
#
# Multi-architecture (ADR 0011): the build runs on the build machine's own architecture (the output is
# plain HTML, CSS and JavaScript); only the nginx stage takes the target architecture's image.

FROM --platform=$BUILDPLATFORM docker.io/library/node:26-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Off for the live (Lean) site; the Full portfolio build passes true (ADR 0011).
ARG VITE_SERIOUS_BINGO_TEASER=false
ENV VITE_SERIOUS_BINGO_TEASER=$VITE_SERIOUS_BINGO_TEASER
RUN npm run build

# The live site (Lean, ADR 0011): Caddy serves the same build, gets the certificates and proxies the API.
# Build it with --target caddy; without a target, the build ends at the nginx image below.
FROM docker.io/library/caddy:2.11-alpine AS caddy
COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist /srv

# Local Compose: unprivileged nginx listens on 8080 and runs as a non-root user.
FROM docker.io/nginxinc/nginx-unprivileged:1.31-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
