# Stage 1: Build
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci || npm install

# Copy source files
COPY . .

# Build the application (SSR mode for server-side API routes)
RUN npm run build

# Stage 2: Production (Node.js with Nitro server)
FROM node:20-alpine
WORKDIR /app

# Install curl for health check
RUN apk add --no-cache curl

# Copy package files and install only production dependencies
# Note: postinstall script (nuxt prepare) is skipped because we already have the build output
COPY package*.json ./
RUN npm ci --omit=dev --ignore-scripts || npm install --omit=dev --ignore-scripts

# Copy built application from build stage
COPY --from=build /app/.output /app/.output

# Create non-root user for security
RUN addgroup -g 1001 -S nodejs && \
    adduser -S nuxt -u 1001 -G nodejs && \
    chown -R nuxt:nodejs /app

USER nuxt

# Expose port 3000 (Nuxt default port)
EXPOSE 3000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:3000/api/health || exit 1

# Start Nitro server
CMD ["node", ".output/server/index.mjs"]

