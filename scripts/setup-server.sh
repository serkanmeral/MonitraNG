#!/bin/bash
# Server Initial Setup Script

set -e

echo "=========================================="
echo "MonitraNG Server Setup Script"
echo "=========================================="

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo -e "${RED}Please run as root${NC}"
    exit 1
fi

echo -e "${YELLOW}Step 1: System update...${NC}"
apt update && apt upgrade -y

echo -e "${YELLOW}Step 2: Installing basic tools...${NC}"
apt install -y curl wget git vim ufw htop

echo -e "${YELLOW}Step 3: Configuring firewall...${NC}"
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP
ufw allow 443/tcp   # HTTPS
ufw --force enable

echo -e "${YELLOW}Step 4: Installing Docker...${NC}"
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

echo -e "${YELLOW}Step 5: Installing Docker Compose...${NC}"
apt install -y docker-compose-plugin

echo -e "${YELLOW}Step 6: Creating deploy user...${NC}"
if ! id "deploy" &>/dev/null; then
    adduser --disabled-password --gecos "" deploy
    usermod -aG sudo deploy
    usermod -aG docker deploy
    echo -e "${GREEN}User 'deploy' created${NC}"
else
    echo -e "${YELLOW}User 'deploy' already exists${NC}"
fi

echo -e "${YELLOW}Step 7: Installing Nginx...${NC}"
apt install -y nginx

echo -e "${YELLOW}Step 8: Installing Certbot...${NC}"
apt install -y certbot python3-certbot-nginx

echo -e "${YELLOW}Step 9: Configuring Docker log rotation...${NC}"
mkdir -p /etc/docker
cat > /etc/docker/daemon.json <<EOF
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
EOF

systemctl restart docker

echo ""
echo -e "${GREEN}=========================================="
echo "Server setup completed!"
echo "==========================================${NC}"
echo ""
echo "Next steps:"
echo "  1. Switch to deploy user: su - deploy"
echo "  2. Clone repository: git clone <repo-url>"
echo "  3. Configure .env file"
echo "  4. Run deployment script: ./scripts/deploy.sh"
echo ""

