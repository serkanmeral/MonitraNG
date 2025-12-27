#!/bin/bash
# Backup Script for MonitraNG

set -e

BACKUP_DIR="/home/deploy/backups"
DATE=$(date +%Y%m%d_%H%M%S)

echo "=========================================="
echo "MonitraNG Backup Script"
echo "Date: $DATE"
echo "=========================================="

# Create backup directory
mkdir -p $BACKUP_DIR/mongodb
mkdir -p $BACKUP_DIR/keycloak
mkdir -p $BACKUP_DIR/docker-volumes

# MongoDB Backup
echo "Backing up MongoDB..."
docker exec mongo mongodump --archive --gzip > $BACKUP_DIR/mongodb/mongodb_$DATE.archive.gz
echo "MongoDB backup completed: mongodb_$DATE.archive.gz"

# Keycloak (PostgreSQL) Backup
echo "Backing up Keycloak database..."
docker exec postgres pg_dump -U keycloak keycloak | gzip > $BACKUP_DIR/keycloak/keycloak_$DATE.sql.gz
echo "Keycloak backup completed: keycloak_$DATE.sql.gz"

# Docker Volumes Backup
echo "Backing up Docker volumes..."
docker run --rm \
  -v mng_common_mongo_data:/data:ro \
  -v $BACKUP_DIR/docker-volumes:/backup \
  alpine tar czf /backup/mongo_data_$DATE.tar.gz -C /data .

# Cleanup old backups (keep last 7 days)
echo "Cleaning up old backups..."
find $BACKUP_DIR -name "*.gz" -mtime +7 -delete
find $BACKUP_DIR -name "*.tar.gz" -mtime +7 -delete

echo ""
echo "Backup completed successfully!"
echo "Backup location: $BACKUP_DIR"
echo ""

