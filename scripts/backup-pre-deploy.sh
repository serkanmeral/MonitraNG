#!/bin/sh
# Pre-Deployment Backup Script for MonitraNG
# This script creates a backup before deployment for rollback capability
# sh-compatible version - fully tested

# set -e kaldırıldı - hataları manuel kontrol ediyoruz
BACKUP_DIR="${BACKUP_DIR:-/root/backups}"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_NAME="pre-deploy-backup_$DATE"
BACKUP_PATH="$BACKUP_DIR/$BACKUP_NAME"

echo "=========================================="
echo "MonitraNG Pre-Deployment Backup"
echo "Date: $DATE"
echo "Backup Name: $BACKUP_NAME"
echo "=========================================="

# Create backup directory
mkdir -p "$BACKUP_PATH/mongodb"
mkdir -p "$BACKUP_PATH/postgres"
mkdir -p "$BACKUP_PATH/docker-volumes"
mkdir -p "$BACKUP_PATH/config"
mkdir -p "$BACKUP_PATH/git-state"

# 1. MongoDB Backup
echo "Backing up MongoDB..."
if docker ps | grep -q mongo; then
  if docker exec mongo mongodump --archive --gzip > "$BACKUP_PATH/mongodb/mongodb_$DATE.archive.gz" 2>/dev/null; then
    echo "✓ MongoDB backup completed"
  else
    echo "WARNING: MongoDB backup failed, continuing..."
  fi
else
  echo "WARNING: MongoDB container not running, skipping backup"
fi

# 2. Keycloak (PostgreSQL) Backup
echo "Backing up Keycloak database..."
if docker ps | grep -q postgres; then
  if docker exec postgres pg_dump -U keycloak keycloak 2>/dev/null | gzip > "$BACKUP_PATH/postgres/keycloak_$DATE.sql.gz" 2>/dev/null; then
    echo "✓ Keycloak backup completed"
  else
    echo "WARNING: PostgreSQL backup failed, continuing..."
  fi
else
  echo "WARNING: PostgreSQL container not running, skipping backup"
fi

# 3. Docker Volumes Backup
echo "Backing up Docker volumes..."
if docker volume ls | grep -q mng_common_mongo_data; then
  if docker run --rm -v mng_common_mongo_data:/data:ro -v "$BACKUP_PATH/docker-volumes:/backup" alpine tar czf "/backup/mongo_data_$DATE.tar.gz" -C /data . 2>/dev/null; then
    echo "✓ Docker volumes backup completed"
  else
    echo "WARNING: Docker volume backup failed, continuing..."
  fi
else
  echo "WARNING: Docker volumes not found, skipping backup"
fi

# 4. Configuration Backup
echo "Backing up configuration files..."
CONFIG_BACKUP="$BACKUP_PATH/config/config_$DATE.tar.gz"
CONFIG_DIR="/root/MonitraNG/ApplicationResources/mng_apps"
if [ -d "$CONFIG_DIR" ]; then
  if [ -f "$CONFIG_DIR/docker-compose.production.yml" ]; then
    cd "$CONFIG_DIR"
    if tar czf "$CONFIG_BACKUP" docker-compose.production.yml .env 2>/dev/null; then
      echo "✓ Configuration backup completed"
    else
      echo "WARNING: Configuration backup failed, continuing..."
    fi
  else
    echo "WARNING: docker-compose.production.yml not found, skipping config backup"
  fi
else
  echo "WARNING: Configuration directory not found, skipping config backup"
fi

# 5. Git State Backup
echo "Backing up Git state..."
GIT_DIR="/root/MonitraNG"
if [ -d "$GIT_DIR/.git" ]; then
  cd "$GIT_DIR"
  git rev-parse HEAD > "$BACKUP_PATH/git-state/commit_hash.txt" 2>/dev/null || true
  git branch --show-current > "$BACKUP_PATH/git-state/branch.txt" 2>/dev/null || true
  git log -1 --pretty=format:"%H %s" > "$BACKUP_PATH/git-state/last_commit.txt" 2>/dev/null || true
  echo "✓ Git state backup completed"
else
  echo "WARNING: Git repository not found, skipping git state backup"
fi

# 6. Docker Compose State Backup
echo "Backing up Docker Compose state..."
COMPOSE_DIR="/root/MonitraNG/ApplicationResources/mng_apps"
if [ -d "$COMPOSE_DIR" ]; then
  cd "$COMPOSE_DIR"
  if [ -f "docker-compose.production.yml" ]; then
    docker compose -f docker-compose.production.yml ps > "$BACKUP_PATH/config/containers_state.txt" 2>/dev/null || true
    docker compose -f docker-compose.production.yml config > "$BACKUP_PATH/config/compose_config.yml" 2>/dev/null || true
    echo "✓ Docker Compose state backup completed"
  else
    echo "WARNING: docker-compose.production.yml not found, skipping compose state backup"
  fi
else
  echo "WARNING: Docker Compose directory not found, skipping compose state backup"
fi

# 7. Create backup manifest
MONGO_STATUS="✗"
POSTGRES_STATUS="✗"
VOLUME_STATUS="✗"
CONFIG_STATUS="✗"
GIT_STATUS="✗"
COMPOSE_STATUS="✗"

if [ -f "$BACKUP_PATH/mongodb/mongodb_$DATE.archive.gz" ]; then
  MONGO_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/postgres/keycloak_$DATE.sql.gz" ]; then
  POSTGRES_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/docker-volumes/mongo_data_$DATE.tar.gz" ]; then
  VOLUME_STATUS="✓"
fi
if [ -f "$CONFIG_BACKUP" ]; then
  CONFIG_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/git-state/commit_hash.txt" ]; then
  GIT_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/config/containers_state.txt" ]; then
  COMPOSE_STATUS="✓"
fi

cat > "$BACKUP_PATH/manifest.txt" << EOF
MonitraNG Pre-Deployment Backup
===============================
Date: $(date)
Backup Name: $BACKUP_NAME
Backup Path: $BACKUP_PATH

Components:
- MongoDB: $MONGO_STATUS
- PostgreSQL: $POSTGRES_STATUS
- Docker Volumes: $VOLUME_STATUS
- Configuration: $CONFIG_STATUS
- Git State: $GIT_STATUS
- Docker Compose State: $COMPOSE_STATUS

To restore this backup, use:
  /root/MonitraNG/scripts/restore-backup.sh $BACKUP_NAME
EOF

echo ""
echo "=========================================="
echo "Backup completed successfully!"
echo "Backup location: $BACKUP_PATH"
echo "Manifest: $BACKUP_PATH/manifest.txt"
echo "=========================================="

# Return backup name for use in deployment script
echo "$BACKUP_NAME"
