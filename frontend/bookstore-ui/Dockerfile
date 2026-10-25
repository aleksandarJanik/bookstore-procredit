FROM node:24.15-alpine AS build
WORKDIR /app
COPY frontend/bookstore-ui/package*.json ./
RUN npm ci
COPY frontend/bookstore-ui/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS final
COPY frontend/bookstore-ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/bookstore-ui/browser /usr/share/nginx/html
EXPOSE 8080
