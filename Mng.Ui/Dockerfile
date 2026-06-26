# Stage 1: Build
FROM node:18-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
# Use npm install if npm ci fails (fallback)
RUN npm ci || npm install

# Copy source files
COPY . .

# Build-time URL'ler: production'da Hub, Gateway vb. için hangi origin kullanılacak
# Boş HUB_URL = Gateway üzerinden /hub kullan (GATEWAY_URL/hub/ws). Aynı origin için GATEWAY_URL=https://app.monitrang.com yapın.
ARG GATEWAY_URL=
ARG HUB_URL=
ENV GATEWAY_URL=$GATEWAY_URL
ENV HUB_URL=$HUB_URL
ENV NODE_OPTIONS="--max-old-space-size=4096"

# Build the application (SPA için generate kullanıyoruz)
RUN npm run generate

# Stage 2: Production (Nginx)
FROM nginx:alpine

# Remove default nginx files
RUN rm -rf /usr/share/nginx/html/*

# Copy built files from build stage (generate .output/public oluşturur)
COPY --from=build /app/.output/public /usr/share/nginx/html

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 80
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost/health || exit 1

# Start nginx
CMD ["nginx", "-g", "daemon off;"]

