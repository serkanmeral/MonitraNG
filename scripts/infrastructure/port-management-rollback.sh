#!/bin/bash
# Port Yönetimi Rollback Scripti
# Kullanım: ./port-management-rollback.sh <backup_directory>

set -e

# Renkli çıktı için
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Backup dizini kontrolü
if [ -z "$1" ]; then
    echo -e "${RED}❌ Hata: Backup dizini belirtilmedi${NC}"
    echo "Kullanım: $0 <backup_directory>"
    echo "Örnek: $0 ~/backups/port-management-20260104-120000"
    exit 1
fi

BACKUP_DIR="$1"
MONITRANG_DIR="/root/MonitraNG"

if [ ! -d "$BACKUP_DIR" ]; then
    echo -e "${RED}❌ Hata: Backup dizini bulunamadı: $BACKUP_DIR${NC}"
    exit 1
fi

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${YELLOW}⚠️  Port Yönetimi Rollback İşlemi${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${YELLOW}Backup Dizini:${NC} $BACKUP_DIR"
echo ""
read -p "Rollback işlemini başlatmak istediğinizden emin misiniz? (yes/no): " confirm

if [ "$confirm" != "yes" ]; then
    echo -e "${YELLOW}Rollback işlemi iptal edildi.${NC}"
    exit 0
fi

echo ""

# 1. Nginx Container'ını Durdurma
echo -e "${YELLOW}🛑 1. Nginx Container'ını Durdurma${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if docker ps | grep -q nginx; then
    docker stop nginx
    docker rm nginx
    echo -e "${GREEN}✅ Nginx container durduruldu ve kaldırıldı${NC}"
else
    echo -e "${YELLOW}⚠️  Nginx container zaten durdurulmuş${NC}"
fi

# 2. Host Nginx'i Başlatma
echo ""
echo -e "${YELLOW}🚀 2. Host Nginx'i Başlatma${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ -f "$BACKUP_DIR/nginx/monitrang" ]; then
    sudo cp "$BACKUP_DIR/nginx/monitrang" /etc/nginx/sites-available/monitrang
    if [ -f "$BACKUP_DIR/nginx/monitrang-enabled" ]; then
        sudo ln -sf /etc/nginx/sites-available/monitrang /etc/nginx/sites-enabled/monitrang
    fi
    
    # Nginx yapılandırmasını test et
    if sudo nginx -t; then
        sudo systemctl start nginx
        sudo systemctl enable nginx
        echo -e "${GREEN}✅ Host Nginx başlatıldı${NC}"
    else
        echo -e "${RED}❌ Nginx yapılandırması hatalı!${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}⚠️  Nginx yapılandırması backup'ta bulunamadı${NC}"
fi

# 3. Docker Compose Dosyalarını Geri Yükleme
echo ""
echo -e "${YELLOW}📁 3. Docker Compose Dosyalarını Geri Yükleme${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

cd "$MONITRANG_DIR"

# mng_common docker-compose.yml
if [ -f "$BACKUP_DIR/mng_common-docker-compose.yml" ]; then
    cp "$BACKUP_DIR/mng_common-docker-compose.yml" "ApplicationResources/mng_common/docker-compose.yml"
    echo -e "${GREEN}✅ mng_common/docker-compose.yml geri yüklendi${NC}"
fi

# mng_apps docker-compose.yml
if [ -f "$BACKUP_DIR/mng_apps-docker-compose.yml" ]; then
    cp "$BACKUP_DIR/mng_apps-docker-compose.yml" "ApplicationResources/mng_apps/docker-compose.yml"
    echo -e "${GREEN}✅ mng_apps/docker-compose.yml geri yüklendi${NC}"
fi

# mng_apps docker-compose.production.yml
if [ -f "$BACKUP_DIR/mng_apps-docker-compose-production.yml" ]; then
    cp "$BACKUP_DIR/mng_apps-docker-compose-production.yml" "ApplicationResources/mng_apps/docker-compose.production.yml"
    echo -e "${GREEN}✅ mng_apps/docker-compose.production.yml geri yüklendi${NC}"
fi

# 4. Servisleri Yeniden Başlatma
echo ""
echo -e "${YELLOW}🔄 4. Servisleri Yeniden Başlatma${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

read -p "Servisleri yeniden başlatmak istiyor musunuz? (yes/no): " restart_services

if [ "$restart_services" = "yes" ]; then
    # mng_common servisleri
    if [ -f "ApplicationResources/mng_common/docker-compose.yml" ]; then
        cd ApplicationResources/mng_common
        docker compose down
        docker compose up -d
        echo -e "${GREEN}✅ mng_common servisleri yeniden başlatıldı${NC}"
    fi
    
    # mng_apps servisleri
    if [ -f "ApplicationResources/mng_apps/docker-compose.yml" ]; then
        cd "$MONITRANG_DIR/ApplicationResources/mng_apps"
        docker compose down
        docker compose up -d
        echo -e "${GREEN}✅ mng_apps servisleri yeniden başlatıldı${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  Servisler manuel olarak yeniden başlatılmalı${NC}"
fi

# 5. Özet
echo ""
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ Rollback İşlemi Tamamlandı!${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${YELLOW}Yapılan İşlemler:${NC}"
echo "  1. Nginx container durduruldu"
echo "  2. Host Nginx başlatıldı"
echo "  3. Docker Compose dosyaları geri yüklendi"
if [ "$restart_services" = "yes" ]; then
    echo "  4. Servisler yeniden başlatıldı"
fi
echo ""
echo -e "${BLUE}Durum Kontrolü:${NC}"
echo "  - Nginx durumu: $(systemctl is-active nginx)"
echo "  - Docker container'lar: $(docker ps -q | wc -l) çalışıyor"
echo ""

