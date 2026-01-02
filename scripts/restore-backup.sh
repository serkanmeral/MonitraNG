#!/bin/bash
# Restore Backup Script for MonitraNG
# This script restores a backup created by backup-pre-deploy.sh

set -e

BACKUP_DIR="${BACKUP_DIR:-/root/backups}"
BACKUP_NAME="$1"

if [ -z "$BACKUP_NAME" ]; then
  echo "Usage: $0 <backup_name>"
  echo "Example: $0 pre-deploy-backup_20260101_120000"
  echo ""
  echo "Available backups:"
  ls -1 "$BACKUP_DIR" 2>/dev/null | grep "^pre-deploy-backup_" || echo "No backups found"
  exit 1
fi

BACKUP_PATH="$BACKUP_DIR/$BACKUP_NAME"

if [ ! -d "$BACKUP_PATH" ]; then
  echo "ERROR: Backup not found: $BACKUP_PATH"
  exit 1
fi

echo "=========================================="
echo "MonitraNG Backup Restore"
echo "Backup: $BACKUP_NAME"
echo "Path: $BACKUP_PATH"
echo "=========================================="

# Confirm restore
read -p "Are you sure you want to restore this backup? This will overwrite current data! (yes/no): " confirm
if [ "$confirm" != "yes" ]; then
  echo "Restore cancelled."
  exit 0
fi

# 1. Restore MongoDB
if [ -f "$BACKUP_PATH/mongodb"/*.archive.gz ]; then
  echo "Restoring MongoDB..."
  MONGO_BACKUP=$(ls -1 "$BACKUP_PATH/mongodb"/*.archive.gz | head -1)
  if docker ps | grep -q mongo; then
    docker exec -i mongo mongorestore --archive --gzip < "$MONGO_BACKUP" || {
      echo "WARNING: MongoDB restore failed"
    }
    echo "✓ MongoDB restored"
  else
    echo "WARNING: MongoDB container not running, skipping restore"
  fi
fi

# 2. Restore Keycloak (PostgreSQL)
if [ -f "$BACKUP_PATH/postgres"/*.sql.gz ]; then
  echo "Restoring Keycloak database..."
  POSTGRES_BACKUP=$(ls -1 "$BACKUP_PATH/postgres"/*.sql.gz | head -1)
  if docker ps | grep -q postgres; then
    gunzip -c "$POSTGRES_BACKUP" | docker exec -i postgres psql -U keycloak keycloak || {
      echo "WARNING: PostgreSQL restore failed"
    }
    echo "✓ Keycloak restored"
  else
    echo "WARNING: PostgreSQL container not running, skipping restore"
  fi
fi

# 3. Restore Docker Volumes
if [ -f "$BACKUP_PATH/docker-volumes"/*.tar.gz ]; then
  echo "Restoring Docker volumes..."
  VOLUME_BACKUP=$(ls -1 "$BACKUP_PATH/docker-volumes"/*.tar.gz | head -1)
  if docker volume ls | grep -q mng_common_mongo_data; then
    docker run --rm \
      -v mng_common_mongo_data:/data \
      -v "$BACKUP_PATH/docker-volumes:/backup" \
      alpine sh -c "cd /data && rm -rf * && tar xzf /backup/$(basename $VOLUME_BACKUP)" || {
      echo "WARNING: Docker volume restore failed"
    }
    echo "✓ Docker volumes restored"
  else
    echo "WARNING: Docker volumes not found, skipping restore"
  fi
fi

# 4. Restore Configuration
if [ -f "$BACKUP_PATH/config"/*.tar.gz ]; then
  echo "Restoring configuration files..."
  CONFIG_BACKUP=$(ls -1 "$BACKUP_PATH/config"/*.tar.gz | head -1)
  cd /root/MonitraNG/ApplicationResources/mng_apps || exit 1
  tar xzf "$CONFIG_BACKUP" || {
    echo "WARNING: Configuration restore failed"
  }
  echo "✓ Configuration restored"
fi

# 5. Restore Git State (optional - show what commit was deployed)
if [ -f "$BACKUP_PATH/git-state/commit_hash.txt" ]; then
  echo "Previous deployment commit:"
  cat "$BACKUP_PATH/git-state/commit_hash.txt"
  echo ""
  echo "To restore to this commit, run:"
  echo "  cd /root/MonitraNG && git checkout $(cat $BACKUP_PATH/git-state/commit_hash.txt)"
fi

echo ""
echo "=========================================="
echo "Restore completed!"
echo "Note: You may need to restart containers for changes to take effect."
echo "=========================================="

