# MkDocs Documentation Dockerfile
# Multi-stage build: Build stage + Production stage

# ============================================
# Stage 1: Build
# ============================================
FROM python:3.11-slim as builder

# Set working directory
WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    git \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy documentation source
COPY . .

# Build MkDocs site
RUN mkdocs build

# ============================================
# Stage 2: Production (Nginx)
# ============================================
FROM nginx:alpine

# Copy built site from builder
COPY --from=builder /app/site /usr/share/nginx/html

# Copy custom nginx configuration (optional)
# If you need custom nginx config, uncomment and create nginx.conf
# COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost/ || exit 1

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
