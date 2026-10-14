# Build context: web/

FROM node:26-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Off for the live (Lean) site; the Full portfolio build passes true (ADR 0011).
ARG VITE_SERIOUS_BINGO_TEASER=false
ENV VITE_SERIOUS_BINGO_TEASER=$VITE_SERIOUS_BINGO_TEASER
RUN npm run build

# Unprivileged nginx listens on 8080 and runs as a non-root user.
FROM nginxinc/nginx-unprivileged:1.31-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
