#!/bin/sh
# CI/CD Configuration Backup Script for MonitraNG
# This script creates a backup of all CI/CD and deployment configurations
# sh-compatible version

BACKUP_DIR="${BACKUP_DIR:-/root/backups}"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_NAME="cicd-config-backup_$DATE"
BACKUP_PATH="$BACKUP_DIR/$BACKUP_NAME"

echo "=========================================="
echo "MonitraNG CI/CD Configuration Backup"
echo "Date: $DATE"
echo "Backup Name: $BACKUP_NAME"
echo "=========================================="

# Create backup directory structure
mkdir -p "$BACKUP_PATH/gitlab-ci"
mkdir -p "$BACKUP_PATH/docker-compose"
mkdir -p "$BACKUP_PATH/scripts"
mkdir -p "$BACKUP_PATH/gitlab-config"
mkdir -p "$BACKUP_PATH/docs"
mkdir -p "$BACKUP_PATH/git-state"

# 1. GitLab CI/CD Configuration
echo "Backing up GitLab CI/CD configuration..."
if [ -f ".gitlab-ci.yml" ]; then
  cp .gitlab-ci.yml "$BACKUP_PATH/gitlab-ci/.gitlab-ci.yml" 2>/dev/null || true
  echo "✓ .gitlab-ci.yml backed up"
else
  echo "WARNING: .gitlab-ci.yml not found"
fi

# 2. Docker Compose Files
echo "Backing up Docker Compose files..."
COMPOSE_DIR="ApplicationResources"
if [ -d "$COMPOSE_DIR" ]; then
  # Production docker-compose
  if [ -f "$COMPOSE_DIR/mng_apps/docker-compose.production.yml" ]; then
    cp "$COMPOSE_DIR/mng_apps/docker-compose.production.yml" "$BACKUP_PATH/docker-compose/docker-compose.production.yml" 2>/dev/null || true
    echo "✓ docker-compose.production.yml backed up"
  fi
  
  # Common docker-compose (GitLab, Runner, Infrastructure)
  if [ -f "$COMPOSE_DIR/mng_common/docker-compose.yml" ]; then
    cp "$COMPOSE_DIR/mng_common/docker-compose.yml" "$BACKUP_PATH/docker-compose/docker-compose.common.yml" 2>/dev/null || true
    echo "✓ docker-compose.common.yml backed up"
  fi
  
  # Environment files (if exist)
  if [ -f "$COMPOSE_DIR/mng_apps/.env" ]; then
    cp "$COMPOSE_DIR/mng_apps/.env" "$BACKUP_PATH/docker-compose/.env.production" 2>/dev/null || true
    echo "✓ .env.production backed up"
  fi
else
  echo "WARNING: ApplicationResources directory not found"
fi

# 3. Deployment and Backup Scripts
echo "Backing up scripts..."
SCRIPTS_DIR="scripts"
if [ -d "$SCRIPTS_DIR" ]; then
  # Backup all important scripts
  for script in backup-pre-deploy.sh restore-backup.sh monitor-services.sh; do
    if [ -f "$SCRIPTS_DIR/$script" ]; then
      cp "$SCRIPTS_DIR/$script" "$BACKUP_PATH/scripts/$script" 2>/dev/null || true
      echo "✓ $script backed up"
    fi
  done
else
  echo "WARNING: scripts directory not found"
fi

# 4. GitLab Runner Configuration (if accessible)
echo "Backing up GitLab Runner configuration..."
if docker ps | grep -q gitlab-runner; then
  # Try to get runner config from container
  if docker exec gitlab-runner cat /etc/gitlab-runner/config.toml > "$BACKUP_PATH/gitlab-config/runner-config.toml" 2>/dev/null; then
    echo "✓ GitLab Runner config.toml backed up"
  else
    echo "WARNING: Could not backup GitLab Runner config"
  fi
else
  echo "WARNING: GitLab Runner container not running"
fi

# 5. GitLab Configuration (if accessible)
echo "Backing up GitLab configuration..."
if docker ps | grep -q gitlab; then
  # Try to get GitLab config
  if docker exec gitlab cat /etc/gitlab/gitlab.rb > "$BACKUP_PATH/gitlab-config/gitlab.rb" 2>/dev/null; then
    echo "✓ GitLab gitlab.rb backed up"
  else
    echo "WARNING: Could not backup GitLab gitlab.rb"
  fi
  
  # GitLab secrets (if accessible)
  if docker exec gitlab cat /etc/gitlab/gitlab-secrets.json > "$BACKUP_PATH/gitlab-config/gitlab-secrets.json" 2>/dev/null; then
    echo "✓ GitLab secrets.json backed up"
  else
    echo "WARNING: Could not backup GitLab secrets.json"
  fi
else
  echo "WARNING: GitLab container not running"
fi

# 6. CI/CD Documentation
echo "Backing up CI/CD documentation..."
DOCS_DIR="docs/content/cicd"
if [ -d "$DOCS_DIR" ]; then
  # Backup important documentation files
  for doc in CICD_DEPLOYMENT_COMPLETE_GUIDE.md DEPLOYMENT_GUIDE.md SUCCESSFUL_RUNNER_CONFIGURATION.md RUNNER_CONFIGURATION_BACKUP.md current_status.md; do
    if [ -f "$DOCS_DIR/$doc" ]; then
      cp "$DOCS_DIR/$doc" "$BACKUP_PATH/docs/$doc" 2>/dev/null || true
      echo "✓ $doc backed up"
    fi
  done
  
  # Backup Multi-Environment analysis if exists
  if [ -f "$DOCS_DIR/MULTI_ENVIRONMENT_ANALYSIS.md" ]; then
    cp "$DOCS_DIR/MULTI_ENVIRONMENT_ANALYSIS.md" "$BACKUP_PATH/docs/MULTI_ENVIRONMENT_ANALYSIS.md" 2>/dev/null || true
    echo "✓ MULTI_ENVIRONMENT_ANALYSIS.md backed up"
  fi
else
  echo "WARNING: docs/content/cicd directory not found"
fi

# 7. Git State
echo "Backing up Git state..."
if [ -d ".git" ]; then
  git rev-parse HEAD > "$BACKUP_PATH/git-state/commit_hash.txt" 2>/dev/null || true
  git branch --show-current > "$BACKUP_PATH/git-state/branch.txt" 2>/dev/null || true
  git log -1 --pretty=format:"%H %s" > "$BACKUP_PATH/git-state/last_commit.txt" 2>/dev/null || true
  git remote -v > "$BACKUP_PATH/git-state/remotes.txt" 2>/dev/null || true
  echo "✓ Git state backed up"
else
  echo "WARNING: .git directory not found"
fi

# 8. Create backup manifest
echo ""
echo "Creating backup manifest..."

# Check what was backed up
GITLAB_CI_STATUS="✗"
DOCKER_COMPOSE_STATUS="✗"
SCRIPTS_STATUS="✗"
GITLAB_CONFIG_STATUS="✗"
DOCS_STATUS="✗"
GIT_STATUS="✗"

if [ -f "$BACKUP_PATH/gitlab-ci/.gitlab-ci.yml" ]; then
  GITLAB_CI_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/docker-compose/docker-compose.production.yml" ]; then
  DOCKER_COMPOSE_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/scripts/backup-pre-deploy.sh" ]; then
  SCRIPTS_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/gitlab-config/runner-config.toml" ] || [ -f "$BACKUP_PATH/gitlab-config/gitlab.rb" ]; then
  GITLAB_CONFIG_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/docs/CICD_DEPLOYMENT_COMPLETE_GUIDE.md" ]; then
  DOCS_STATUS="✓"
fi
if [ -f "$BACKUP_PATH/git-state/commit_hash.txt" ]; then
  GIT_STATUS="✓"
fi

cat > "$BACKUP_PATH/manifest.txt" << EOF
MonitraNG CI/CD Configuration Backup
=====================================
Date: $(date)
Backup Name: $BACKUP_NAME
Backup Path: $BACKUP_PATH

Components:
- GitLab CI/CD Config: $GITLAB_CI_STATUS
- Docker Compose Files: $DOCKER_COMPOSE_STATUS
- Scripts: $SCRIPTS_STATUS
- GitLab Config: $GITLAB_CONFIG_STATUS
- Documentation: $DOCS_STATUS
- Git State: $GIT_STATUS

Backup Contents:
$(ls -R "$BACKUP_PATH" | grep -v "^$" | head -50)

To restore this backup, use:
  /root/MonitraNG/scripts/restore-cicd-config.sh $BACKUP_NAME
EOF

echo ""
echo "=========================================="
echo "Backup completed successfully!"
echo "Backup location: $BACKUP_PATH"
echo "Manifest: $BACKUP_PATH/manifest.txt"
echo "=========================================="

# Return backup name for use in other scripts
echo "$BACKUP_NAME"

