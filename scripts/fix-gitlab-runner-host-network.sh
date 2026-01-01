#!/bin/bash

# GitLab Runner Host Network Fix Script
# This script applies Option 1: Runner Container in Host Network
# Date: 1 Ocak 2026

set -e

echo "🚀 GitLab Runner Host Network Fix - Seçenek 1 Uygulanıyor"
echo ""

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Step 1: Find GitLab container IP
echo -e "${CYAN}📋 Adım 1: GitLab Container IP'sini Bul${NC}"
GITLAB_IP=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab 2>/dev/null || echo "")

if [ -z "$GITLAB_IP" ]; then
    echo -e "${RED}❌ GitLab container IP bulunamadı!${NC}"
    echo "GitLab container'ının çalıştığını kontrol edin: docker ps | grep gitlab"
    exit 1
fi

echo -e "${GREEN}✅ GitLab Container IP: ${GITLAB_IP}${NC}"
echo ""

# Step 2: Check current runner config
echo -e "${CYAN}📋 Adım 2: Mevcut Runner Config'i Kontrol Et${NC}"
RUNNER_CONFIG="/var/lib/docker/volumes/mng_common_gitlab_runner_config/_data/config.toml"

if [ ! -f "$RUNNER_CONFIG" ]; then
    # Try alternative path
    RUNNER_CONFIG=$(docker volume inspect mng_common_gitlab_runner_config --format '{{ .Mountpoint }}' 2>/dev/null)/config.toml
    
    if [ ! -f "$RUNNER_CONFIG" ]; then
        echo -e "${YELLOW}⚠️  Runner config dosyası bulunamadı, container içinden kontrol ediliyor...${NC}"
        docker exec gitlab-runner cat /etc/gitlab-runner/config.toml > /tmp/runner-config.toml 2>/dev/null || {
            echo -e "${RED}❌ Runner config dosyasına erişilemiyor!${NC}"
            exit 1
        }
        RUNNER_CONFIG="/tmp/runner-config.toml"
    fi
fi

echo -e "${GREEN}✅ Runner Config: ${RUNNER_CONFIG}${NC}"
echo ""

# Step 3: Backup current config
echo -e "${CYAN}📋 Adım 3: Mevcut Config'i Yedekle${NC}"
BACKUP_FILE="${RUNNER_CONFIG}.backup.$(date +%Y%m%d_%H%M%S)"
if [ -f "$RUNNER_CONFIG" ]; then
    cp "$RUNNER_CONFIG" "$BACKUP_FILE"
    echo -e "${GREEN}✅ Yedek oluşturuldu: ${BACKUP_FILE}${NC}"
else
    echo -e "${YELLOW}⚠️  Config dosyası bulunamadı, yedek oluşturulamadı${NC}"
fi
echo ""

# Step 4: Update runner config URL
echo -e "${CYAN}📋 Adım 4: Runner Config URL'yi Güncelle${NC}"

# Check if config is in container
if docker exec gitlab-runner test -f /etc/gitlab-runner/config.toml 2>/dev/null; then
    echo "Runner config container içinde, güncelleniyor..."
    
    # Read current config
    CURRENT_URL=$(docker exec gitlab-runner grep -E "^[[:space:]]*url[[:space:]]*=" /etc/gitlab-runner/config.toml | head -1 | sed 's/.*url[[:space:]]*=[[:space:]]*"\([^"]*\)".*/\1/' || echo "")
    
    if [ -z "$CURRENT_URL" ]; then
        echo -e "${YELLOW}⚠️  Mevcut URL bulunamadı, config dosyasını kontrol edin${NC}"
    else
        echo -e "${GREEN}✅ Mevcut URL: ${CURRENT_URL}${NC}"
    fi
    
    # Update URL to GitLab IP
    NEW_URL="http://${GITLAB_IP}"
    echo -e "${CYAN}🔄 URL güncelleniyor: ${CURRENT_URL} → ${NEW_URL}${NC}"
    
    # Use sed to update URL in container
    docker exec gitlab-runner sed -i "s|url[[:space:]]*=[[:space:]]*\"http://gitlab\"|url = \"${NEW_URL}\"|g" /etc/gitlab-runner/config.toml 2>/dev/null || {
        echo -e "${YELLOW}⚠️  sed komutu başarısız, manuel güncelleme gerekebilir${NC}"
        echo -e "${CYAN}Manuel güncelleme için:${NC}"
        echo "  docker exec -it gitlab-runner vi /etc/gitlab-runner/config.toml"
        echo "  URL'yi şu şekilde değiştirin: url = \"${NEW_URL}\""
    }
    
    # Verify update
    UPDATED_URL=$(docker exec gitlab-runner grep -E "^[[:space:]]*url[[:space:]]*=" /etc/gitlab-runner/config.toml | head -1 | sed 's/.*url[[:space:]]*=[[:space:]]*"\([^"]*\)".*/\1/' || echo "")
    if [ "$UPDATED_URL" = "$NEW_URL" ]; then
        echo -e "${GREEN}✅ URL başarıyla güncellendi: ${UPDATED_URL}${NC}"
    else
        echo -e "${YELLOW}⚠️  URL güncellenemedi, manuel kontrol gerekebilir${NC}"
        echo -e "${CYAN}Güncel URL: ${UPDATED_URL}${NC}"
    fi
else
    echo -e "${RED}❌ Runner config dosyasına erişilemiyor!${NC}"
    exit 1
fi
echo ""

# Step 5: Restart runner container
echo -e "${CYAN}📋 Adım 5: Runner Container'ını Restart Et${NC}"
cd /root/MonitraNG/ApplicationResources/mng_common || cd /home/deploy/MonitraNG/ApplicationResources/mng_common || {
    echo -e "${RED}❌ docker-compose.yml dosyası bulunamadı!${NC}"
    exit 1
}

echo "Runner container durduruluyor..."
docker compose stop gitlab-runner || docker stop gitlab-runner || {
    echo -e "${YELLOW}⚠️  Runner container durdurulamadı (zaten durmuş olabilir)${NC}"
}

echo "Runner container başlatılıyor (host network ile)..."
docker compose up -d gitlab-runner || {
    echo -e "${RED}❌ Runner container başlatılamadı!${NC}"
    echo "Manuel olarak başlatmayı deneyin:"
    echo "  cd /root/MonitraNG/ApplicationResources/mng_common"
    echo "  docker compose up -d gitlab-runner"
    exit 1
}

echo -e "${GREEN}✅ Runner container başlatıldı${NC}"
echo ""

# Step 6: Verify runner is running
echo -e "${CYAN}📋 Adım 6: Runner Durumunu Kontrol Et${NC}"
sleep 3
if docker ps | grep -q gitlab-runner; then
    echo -e "${GREEN}✅ Runner container çalışıyor${NC}"
    
    # Check network mode
    NETWORK_MODE=$(docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}' 2>/dev/null || echo "")
    if [ "$NETWORK_MODE" = "host" ]; then
        echo -e "${GREEN}✅ Network mode: host (doğru)${NC}"
    else
        echo -e "${YELLOW}⚠️  Network mode: ${NETWORK_MODE} (host olmalı)${NC}"
    fi
    
    # Check runner status
    echo "Runner durumu kontrol ediliyor..."
    docker exec gitlab-runner gitlab-runner verify 2>&1 | head -20 || {
        echo -e "${YELLOW}⚠️  Runner verify komutu başarısız (normal olabilir)${NC}"
    }
else
    echo -e "${RED}❌ Runner container çalışmıyor!${NC}"
    echo "Logları kontrol edin: docker logs gitlab-runner"
    exit 1
fi
echo ""

# Summary
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ repository'yi çekebildiğini doğrula)","status":"completed"},{"id":"12","content":"Deployment pipeline'ı tekrar ekle (.gitlab-ci.yml'ye)","status":"completed"},{"id":"13","content":"Deployment pipeline'ı test et","status":"in_progress"},{"id":"14","content":"GitLab UI erişilebilirliğini sağla (nginx port düzeltmesi)","status":"completed"},{"id":"15","content":"Runner config'e extra_hosts ekle (gitlab hostname çözümlemesi için)","status":"completed"},{"id":"16","content":"Runner network_mode sorunu - container'lar external IP'ye erişemiyor","status":"completed"},{"id":"17","content":"GitLab internal git URL yapılandırması veya network_mode alternatifi","status":"completed"},{"id":"18","content":"GitLab UI erişilebilirlik sorunu çözüldü (nginx port düzeltmesi)","status":"completed"},{"id":"19","content":"Docker build job'ları Docker socket kullanacak şekilde yapılandırıldı","status":"completed"},{"id":"20","content":"Artifacts optional yapma - Build job'larından artifacts kaldır, test job'larını güncelle, extract-openapi-specs'i güncelle, runner config'den network_mode kaldır","status":"completed"},{"id":"21","content":"Git fetch sorunu çözülemedi - network_mode=host çalışmadı, alternatif çözümler değerlendiriliyor","status":"in_progress"},{"id":"22","content":"GitLab external_url'i external IP'ye güncelle (docker-compose.yml)","status":"completed"},{"id":"45","content":"Runner config'de URL'yi IP'ye çevir","status":"pending"},{"id":"46","content":"Runner container'ını restart et","status":"pending"},{"id":"47","content":"Pipeline'ı test et","status":"pending"}]
