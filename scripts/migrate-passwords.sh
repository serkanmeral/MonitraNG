#!/bin/bash
# Password Migration Script
# This script migrates all services to use the new default password from .env file
# WARNING: This will delete existing volumes and recreate them with new passwords
# Only run this in test environments where data loss is acceptable

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}=========================================="
echo "Password Migration Script"
echo "==========================================${NC}"
echo ""
echo -e "${RED}WARNING: This script will:${NC}"
echo "  1. Stop all running containers"
echo "  2. Delete existing volumes (MongoDB, PostgreSQL, Redis, RabbitMQ, MinIO, Seq)"
echo "  3. Recreate containers with new passwords from .env file"
echo ""
echo -e "${YELLOW}This will result in DATA LOSS!${NC}"
echo ""
read -p "Are you sure you want to continue? (yes/no): " confirm

if [ "$confirm" != "yes" ]; then
    echo -e "${RED}Migration cancelled.${NC}"
    exit 1
fi

# Get the directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"
COMMON_DIR="$PROJECT_ROOT/ApplicationResources/mng_common"

# Check if .env file exists
if [ ! -f "$COMMON_DIR/.env" ]; then
    echo -e "${RED}Error: .env file not found at $COMMON_DIR/.env${NC}"
    echo "Please create .env file from env.example first."
    exit 1
fi

# Load environment variables
export $(grep -v '^#' "$COMMON_DIR/.env" | xargs)

echo ""
echo -e "${GREEN}Step 1: Stopping all containers...${NC}"
cd "$COMMON_DIR"
docker compose down

echo ""
echo -e "${GREEN}Step 2: Removing volumes (this will delete all data)...${NC}"
docker volume rm mng_common_postgres_data 2>/dev/null || true
docker volume rm mng_common_mongo_data 2>/dev/null || true
docker volume rm mng_common_redis_data 2>/dev/null || true
docker volume rm mng_common_rabbitmq_data 2>/dev/null || true
docker volume rm mng_common_minio_data 2>/dev/null || true
docker volume rm mng_common_seq_data 2>/dev/null || true

# Note: GitLab volumes are NOT deleted (gitlab-postgres, gitlab-redis, gitlab_config, gitlab_logs, gitlab_data)
echo -e "${YELLOW}Note: GitLab volumes are preserved (not deleted)${NC}"

echo ""
echo -e "${GREEN}Step 3: Starting containers with new passwords...${NC}"
docker compose up -d

echo ""
echo -e "${GREEN}Step 4: Waiting for services to be ready...${NC}"
sleep 10

echo ""
echo -e "${GREEN}Migration completed!${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Verify services are running: docker compose ps"
echo "  2. Test connections with new passwords"
echo "  3. Update application connection strings if needed"
echo ""
echo -e "${GREEN}New password: ${MONGO_ROOT_PASSWORD:-NOT_SET}${NC}"

