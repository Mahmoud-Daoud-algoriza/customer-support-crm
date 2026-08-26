# Build context is the repository root (see docker-compose.yml).
FROM node:22 AS build
WORKDIR /src

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

FROM nginx:alpine AS runtime
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/support-crm-web/browser /usr/share/nginx/html
EXPOSE 80
