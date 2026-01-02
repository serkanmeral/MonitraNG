#!/bin/sh
# Restore Backup Script for MonitraNG
# This script restores a backup created by backup-pre-deploy.sh
# sh-compatible version - non-interactive for automated rollback

# set -e kaldırıldı - hataları manuel kontrol ediyoruz
BACKUP_DIR="${BACKUP_DIR:-/root/backups}"
BACKUP_NAME="$1"
SKIP_CONFIRM="${SKIP_CONFIRM:-false}"

if [ -z "$BACKUP_NAME" ]; then
  echo "Usage: $0 <backup_name> [--skip-confirm]"
  echo "Example: $0 pre-deploy-backup_20260101_120000"
  echo "Example (automated): SKIP_CONFIRM=true $0 pre-deploy-backup_20260101_120000"
  echo ""
  echo "Available backups:"
  if [ -d "$BACKUP_DIR" ]; then
    ls -1 "$BACKUP_DIR" 2>/dev/null | grep "^pre-deploy-backup_" || echo "No backups found"
  else
    echo "Backup directory not found: $BACKUP_DIR"
  fi
  exit 1
fi

# Check for --skip-confirm flag
if [ "$2" = "--skip-confirm" ]; then
  SKIP_CONFIRM="true"
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

# Confirm restore (skip if SKIP_CONFIRM=true or --skip-confirm flag)
if [ "$SKIP_CONFIRM" != "true" ]; then
  echo "WARNING: This will overwrite current data!"
  echo "Press Ctrl+C to cancel, or Enter to continue..."
  read -r confirm
fi

# 1. Restore MongoDB
if [ -d "$BACKUP_PATH/mongodb" ]; then
  MONGO_BACKUP=$(ls -1 "$BACKUP_PATH/mongodb"/*.archive.gz 2>/dev/null | head -1)
  if [ -n "$MONGO_BACKUP" ] && [ -f "$MONGO_BACKUP" ]; then
    echo "Restoring MongoDB..."
    if docker ps | grep -q mongo; then
      if docker exec -i mongo mongorestore --archive --gzip < "$MONGO_BACKUP" 2>/dev/null; then
        echo "✓ MongoDB restored"
      else
        echo "WARNING: MongoDB restore failed, continuing..."
      fi
    else
      echo "WARNING: MongoDB container not running, skipping restore"
    fi
  else
    echo "WARNING: MongoDB backup file not found, skipping restore"
  fi
else
  echo "WARNING: MongoDB backup directory not found, skipping restore"
fi

# 2. Restore Keycloak (PostgreSQL)
if [ -d "$BACKUP_PATH/postgres" ]; then
  POSTGRES_BACKUP=$(ls -1 "$BACKUP_PATH/postgres"/*.sql.gz 2>/dev/null | head -1)
  if [ -n "$POSTGRES_BACKUP" ] && [ -f "$POSTGRES_BACKUP" ]; then
    echo "Restoring Keycloak database..."
    if docker ps | grep -q postgres; then
      if gunzip -c "$POSTGRES_BACKUP" 2>/dev/null | docker exec -i postgres psql -U keycloak keycloak 2>/dev/null; then
        echo "✓ Keycloak restored"
      else
        echo "WARNING: PostgreSQL restore failed, continuing..."
      fi
    else
      echo "WARNING: PostgreSQL container not running, skipping restore"
    fi
  else
    echo "WARNING: PostgreSQL backup file not found, skipping restore"
  fi
else
  echo "WARNING: PostgreSQL backup directory not found, skipping restore"
fi

# 3. Restore Docker Volumes
if [ -d "$BACKUP_PATH/docker-volumes" ]; then
  VOLUME_BACKUP=$(ls -1 "$BACKUP_PATH/docker-volumes"/*.tar.gz 2>/dev/null | head -1)
  if [ -n "$VOLUME_BACKUP" ] && [ -f "$VOLUME_BACKUP" ]; then
    echo "Restoring Docker volumes..."
    if docker volume ls | grep -q mng_common_mongo_data; then
      VOLUME_BACKUP_NAME=$(basename "$VOLUME_BACKUP")
      if docker run --rm \
        -v mng_common_mongo_data:/data \
        -v "$BACKUP_PATH/docker-volumes:/backup" \
        alpine sh -c "cd /data && rm -rf * && tar xzf /backup/$VOLUME_BACKUP_NAME" 2>/dev/null; then
        echo "✓ Docker volumes restored"
      else
        echo "WARNING: Docker volume restore failed, continuing..."
      fi
    else
      echo "WARNING: Docker volumes not found, skipping restore"
    fi
  else
    echo "WARNING: Docker volume backup file not found, skipping restore"
  fi
else
  echo "WARNING: Docker volumes backup directory not found, skipping restore"
fi

# 4. Restore Configuration
if [ -d "$BACKUP_PATH/config" ]; then
  CONFIG_BACKUP=$(ls -1 "$BACKUP_PATH/config"/*.tar.gz 2>/dev/null | head -1)
  if [ -n "$CONFIG_BACKUP" ] && [ -f "$CONFIG_BACKUP" ]; then
    echo "Restoring configuration files..."
    CONFIG_DIR="/root/MonitraNG/ApplicationResources/mng_apps"
    if [ -d "$CONFIG_DIR" ]; then
      cd "$CONFIG_DIR" 2>/dev/null
      if [ $? -eq 0 ]; then
        if tar xzf "$CONFIG_BACKUP" 2>/dev/null; then
          echo "✓ Configuration restored"
        else
          echo "WARNING: Configuration restore failed, continuing..."
        fi
      else
        echo "WARNING: Cannot change to $CONFIG_DIR, skipping config restore"
      fi
    else
      echo "WARNING: Configuration directory not found, skipping config restore"
    fi
  else
    echo "WARNING: Configuration backup file not found, skipping restore"
  fi
else
  echo "WARNING: Configuration backup directory not found, skipping restore"
fi

# 5. Restore Git State (optional - show what commit was deployed)
if [ -f "$BACKUP_PATH/git-state/commit_hash.txt" ]; then
  echo ""
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
