#!/bin/bash
# Production Deployment Script

set -e

ENVIRONMENT=${1:-production}
VERSION=${2:-latest}

echo "=========================================="
echo "MonitraNG Deployment Script"
echo "Environment: $ENVIRONMENT"
echo "Version: $VERSION"
echo "=========================================="

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if running as root
if [ "$EUID" -eq 0 ]; then 
    echo -e "${RED}Please do not run as root${NC}"
    exit 1
fi

# Check if .env exists
if [ ! -f "ApplicationResources/mng_apps/.env" ]; then
    echo -e "${RED}.env file not found!${NC}"
    echo "Please copy .env.example to .env and configure it"
    exit 1
fi

# Load environment variables
export $(cat ApplicationResources/mng_apps/.env | grep -v '^#' | xargs)
export VERSION=$VERSION

echo -e "${YELLOW}Step 1: Pulling latest code...${NC}"
git pull origin main

echo -e "${YELLOW}Step 2: Building Docker images...${NC}"
cd ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml build

echo -e "${YELLOW}Step 3: Starting infrastructure services...${NC}"
cd ../mng_common
docker compose up -d

echo -e "${YELLOW}Step 4: Waiting for infrastructure to be ready...${NC}"
sleep 10

# Health check for MongoDB
echo "Checking MongoDB..."
until docker exec mongo mongosh --eval "db.adminCommand('ping')" > /dev/null 2>&1; do
    echo "Waiting for MongoDB..."
    sleep 2
done
echo -e "${GREEN}MongoDB is ready${NC}"

# Health check for Keycloak
echo "Checking Keycloak..."
until curl -f http://localhost:8080/health/ready > /dev/null 2>&1; do
    echo "Waiting for Keycloak..."
    sleep 2
done
echo -e "${GREEN}Keycloak is ready${NC}"

echo -e "${YELLOW}Step 5: Starting application services...${NC}"
cd ../mng_apps
docker compose -f docker-compose.production.yml up -d

echo -e "${YELLOW}Step 6: Waiting for applications to be ready...${NC}"
sleep 15

# Health checks
echo "Checking MngKeeper..."
until curl -k -f https://localhost:5001/api/version/short > /dev/null 2>&1; do
    echo "Waiting for MngKeeper..."
    sleep 2
done
echo -e "${GREEN}MngKeeper is ready${NC}"

echo "Checking MngDataGateway..."
until curl -k -f https://localhost:5010/health > /dev/null 2>&1; do
    echo "Waiting for MngDataGateway..."
    sleep 2
done
echo -e "${GREEN}MngDataGateway is ready${NC}"

echo "Checking MngHub..."
until curl -f http://localhost:5020/health > /dev/null 2>&1; do
    echo "Waiting for MngHub..."
    sleep 2
done
echo -e "${GREEN}MngHub is ready${NC}"

echo -e "${YELLOW}Step 7: Cleaning up old images...${NC}"
docker image prune -f

echo ""
echo -e "${GREEN}=========================================="
echo "Deployment completed successfully!"
echo "==========================================${NC}"
echo ""
echo "Services:"
echo "  - MngKeeper: https://localhost:5001"
echo "  - MngDataGateway: https://localhost:5010"
echo "  - MngHub: http://localhost:5020"
echo ""
echo "Check status:"
echo "  docker compose -f docker-compose.production.yml ps"
echo ""
echo "View logs:"
echo "  docker compose -f docker-compose.production.yml logs -f"
echo ""

