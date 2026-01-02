#!/bin/sh
# Restore CI/CD Configuration Backup Script for MonitraNG
# This script restores a CI/CD configuration backup created by backup-cicd-config.sh
# sh-compatible version - non-interactive for automated restore

BACKUP_DIR="${BACKUP_DIR:-/root/backups}"
BACKUP_NAME="$1"
SKIP_CONFIRM="${SKIP_CONFIRM:-false}"

if [ -z "$BACKUP_NAME" ]; then
  echo "Usage: $0 <backup_name> [--skip-confirm]"
  echo "Example: $0 cicd-config-backup_20260101_120000"
  echo "Example (automated): SKIP_CONFIRM=true $0 cicd-config-backup_20260101_120000"
  echo ""
  echo "Available backups:"
  if [ -d "$BACKUP_DIR" ]; then
    ls -1 "$BACKUP_DIR" 2>/dev/null | grep "^cicd-config-backup_" || echo "No CI/CD config backups found"
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
echo "MonitraNG CI/CD Configuration Restore"
echo "Backup: $BACKUP_NAME"
echo "Path: $BACKUP_PATH"
echo "=========================================="

# Confirm restore (skip if SKIP_CONFIRM=true or --skip-confirm flag)
if [ "$SKIP_CONFIRM" != "true" ]; then
  echo "WARNING: This will overwrite current CI/CD configurations!"
  echo "Press Ctrl+C to cancel, or Enter to continue..."
  read -r confirm
fi

# 1. Restore GitLab CI/CD Configuration
if [ -f "$BACKUP_PATH/gitlab-ci/.gitlab-ci.yml" ]; then
  echo "Restoring .gitlab-ci.yml..."
  if cp "$BACKUP_PATH/gitlab-ci/.gitlab-ci.yml" ".gitlab-ci.yml" 2>/dev/null; then
    echo "✓ .gitlab-ci.yml restored"
  else
    echo "WARNING: Failed to restore .gitlab-ci.yml"
  fi
else
  echo "WARNING: .gitlab-ci.yml backup not found"
fi

# 2. Restore Docker Compose Files
if [ -f "$BACKUP_PATH/docker-compose/docker-compose.production.yml" ]; then
  echo "Restoring docker-compose.production.yml..."
  COMPOSE_DIR="ApplicationResources/mng_apps"
  if [ -d "$COMPOSE_DIR" ]; then
    if cp "$BACKUP_PATH/docker-compose/docker-compose.production.yml" "$COMPOSE_DIR/docker-compose.production.yml" 2>/dev/null; then
      echo "✓ docker-compose.production.yml restored"
    else
      echo "WARNING: Failed to restore docker-compose.production.yml"
    fi
  else
    echo "WARNING: $COMPOSE_DIR directory not found"
  fi
fi

if [ -f "$BACKUP_PATH/docker-compose/docker-compose.common.yml" ]; then
  echo "Restoring docker-compose.common.yml..."
  COMPOSE_DIR="ApplicationResources/mng_common"
  if [ -d "$COMPOSE_DIR" ]; then
    if cp "$BACKUP_PATH/docker-compose/docker-compose.common.yml" "$COMPOSE_DIR/docker-compose.yml" 2>/dev/null; then
      echo "✓ docker-compose.common.yml restored"
    else
      echo "WARNING: Failed to restore docker-compose.common.yml"
    fi
  else
    echo "WARNING: $COMPOSE_DIR directory not found"
  fi
fi

if [ -f "$BACKUP_PATH/docker-compose/.env.production" ]; then
  echo "Restoring .env.production..."
  COMPOSE_DIR="ApplicationResources/mng_apps"
  if [ -d "$COMPOSE_DIR" ]; then
    if cp "$BACKUP_PATH/docker-compose/.env.production" "$COMPOSE_DIR/.env" 2>/dev/null; then
      echo "✓ .env.production restored"
    else
      echo "WARNING: Failed to restore .env.production"
    fi
  else
    echo "WARNING: $COMPOSE_DIR directory not found"
  fi
fi

# 3. Restore Scripts
if [ -d "$BACKUP_PATH/scripts" ]; then
  echo "Restoring scripts..."
  SCRIPTS_DIR="scripts"
  if [ -d "$SCRIPTS_DIR" ]; then
    for script in backup-pre-deploy.sh restore-backup.sh monitor-services.sh; do
      if [ -f "$BACKUP_PATH/scripts/$script" ]; then
        if cp "$BACKUP_PATH/scripts/$script" "$SCRIPTS_DIR/$script" 2>/dev/null; then
          chmod +x "$SCRIPTS_DIR/$script" 2>/dev/null || true
          echo "✓ $script restored"
        else
          echo "WARNING: Failed to restore $script"
        fi
      fi
    done
  else
    echo "WARNING: scripts directory not found"
  fi
fi

# 4. Restore GitLab Runner Configuration (if container is running)
if [ -f "$BACKUP_PATH/gitlab-config/runner-config.toml" ]; then
  echo "Restoring GitLab Runner configuration..."
  if docker ps | grep -q gitlab-runner; then
    echo "WARNING: GitLab Runner is running. Manual restore required:"
    echo "  1. Stop GitLab Runner: docker stop gitlab-runner"
    echo "  2. Copy config: docker cp $BACKUP_PATH/gitlab-config/runner-config.toml gitlab-runner:/etc/gitlab-runner/config.toml"
    echo "  3. Start GitLab Runner: docker start gitlab-runner"
  else
    echo "INFO: GitLab Runner not running. Config file saved at: $BACKUP_PATH/gitlab-config/runner-config.toml"
  fi
fi

# 5. Restore GitLab Configuration (if container is running)
if [ -f "$BACKUP_PATH/gitlab-config/gitlab.rb" ]; then
  echo "Restoring GitLab configuration..."
  if docker ps | grep -q gitlab; then
    echo "WARNING: GitLab is running. Manual restore required:"
    echo "  1. Stop GitLab: docker stop gitlab"
    echo "  2. Copy config: docker cp $BACKUP_PATH/gitlab-config/gitlab.rb gitlab:/etc/gitlab/gitlab.rb"
    echo "  3. Reconfigure: docker exec gitlab gitlab-ctl reconfigure"
    echo "  4. Start GitLab: docker start gitlab"
  else
    echo "INFO: GitLab not running. Config file saved at: $BACKUP_PATH/gitlab-config/gitlab.rb"
  fi
fi

# 6. Restore Documentation (optional - usually not critical)
if [ -d "$BACKUP_PATH/docs" ]; then
  echo "Restoring documentation (optional)..."
  DOCS_DIR="docs/content/cicd"
  if [ -d "$DOCS_DIR" ]; then
    for doc in "$BACKUP_PATH/docs"/*.md; do
      if [ -f "$doc" ]; then
        DOC_NAME=$(basename "$doc")
        if cp "$doc" "$DOCS_DIR/$DOC_NAME" 2>/dev/null; then
          echo "✓ $DOC_NAME restored"
        else
          echo "WARNING: Failed to restore $DOC_NAME"
        fi
      fi
    done
  else
    echo "WARNING: docs/content/cicd directory not found"
  fi
fi

# 7. Show Git State (informational)
if [ -f "$BACKUP_PATH/git-state/commit_hash.txt" ]; then
  echo ""
  echo "Backup was created from Git commit:"
  cat "$BACKUP_PATH/git-state/commit_hash.txt" 2>/dev/null || true
  echo ""
  echo "To restore to this commit, run:"
  echo "  cd /root/MonitraNG && git checkout $(cat $BACKUP_PATH/git-state/commit_hash.txt 2>/dev/null)"
fi

echo ""
echo "=========================================="
echo "Restore completed!"
echo ""
echo "Next steps:"
echo "  1. Review restored files"
echo "  2. If GitLab/Runner configs were restored, restart containers"
echo "  3. Test CI/CD pipeline"
echo "=========================================="

