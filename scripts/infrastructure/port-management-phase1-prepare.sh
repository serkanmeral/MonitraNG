#!/bin/bash
# Port Yönetimi Phase 1: Hazırlık ve Planlama Scripti
# Kullanım: ./port-management-phase1-prepare.sh

set -e

# Renkli çıktı için
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Backup dizini
BACKUP_DIR="/root/backups/port-management-$(date +%Y%m%d-%H%M%S)"
MONITRANG_DIR="/root/MonitraNG"

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Port Yönetimi Phase 1: Hazırlık ve Planlama${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# 1. Mevcut Durum Analizi
echo -e "${YELLOW}📊 1. Mevcut Durum Analizi${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo -e "${BLUE}Mevcut port kullanımı:${NC}"
netstat -tlnp 2>/dev/null | grep LISTEN | head -20 || ss -tlnp | grep LISTEN | head -20

echo ""
echo -e "${BLUE}Docker container port mapping'leri:${NC}"
docker ps --format "table {{.Names}}\t{{.Ports}}" | head -20

echo ""
echo -e "${BLUE}Nginx durumu:${NC}"
if systemctl is-active --quiet nginx; then
    echo -e "${GREEN}✅ Nginx çalışıyor${NC}"
    systemctl status nginx --no-pager | head -5
else
    echo -e "${YELLOW}⚠️  Nginx çalışmıyor${NC}"
fi

echo ""
echo -e "${BLUE}Docker network'ler:${NC}"
docker network ls | grep mng

echo ""

# 2. Backup Dizini Oluşturma
echo -e "${YELLOW}💾 2. Backup Dizini Oluşturma${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

mkdir -p "$BACKUP_DIR"
echo -e "${GREEN}✅ Backup dizini oluşturuldu: $BACKUP_DIR${NC}"

# 3. Nginx Yapılandırmasını Yedekleme
echo ""
echo -e "${YELLOW}📁 3. Nginx Yapılandırmasını Yedekleme${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ -f /etc/nginx/sites-available/monitrang ]; then
    mkdir -p "$BACKUP_DIR/nginx"
    cp /etc/nginx/sites-available/monitrang "$BACKUP_DIR/nginx/monitrang"
    if [ -L /etc/nginx/sites-enabled/monitrang ]; then
        cp -L /etc/nginx/sites-enabled/monitrang "$BACKUP_DIR/nginx/monitrang-enabled" 2>/dev/null || true
    fi
    echo -e "${GREEN}✅ Nginx yapılandırması yedeklendi${NC}"
else
    echo -e "${YELLOW}⚠️  Nginx yapılandırması bulunamadı${NC}"
fi

# 4. Docker Compose Dosyalarını Yedekleme
echo ""
echo -e "${YELLOW}🐳 4. Docker Compose Dosyalarını Yedekleme${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

cd "$MONITRANG_DIR"

# mng_common docker-compose.yml
if [ -f "ApplicationResources/mng_common/docker-compose.yml" ]; then
    cp "ApplicationResources/mng_common/docker-compose.yml" "$BACKUP_DIR/mng_common-docker-compose.yml"
    echo -e "${GREEN}✅ mng_common/docker-compose.yml yedeklendi${NC}"
fi

# mng_apps docker-compose.yml
if [ -f "ApplicationResources/mng_apps/docker-compose.yml" ]; then
    cp "ApplicationResources/mng_apps/docker-compose.yml" "$BACKUP_DIR/mng_apps-docker-compose.yml"
    echo -e "${GREEN}✅ mng_apps/docker-compose.yml yedeklendi${NC}"
fi

# mng_apps docker-compose.production.yml
if [ -f "ApplicationResources/mng_apps/docker-compose.production.yml" ]; then
    cp "ApplicationResources/mng_apps/docker-compose.production.yml" "$BACKUP_DIR/mng_apps-docker-compose-production.yml"
    echo -e "${GREEN}✅ mng_apps/docker-compose.production.yml yedeklendi${NC}"
fi

# 5. Mevcut Port Kullanımını Dokümante Etme
echo ""
echo -e "${YELLOW}📝 5. Mevcut Port Kullanımını Dokümante Etme${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

{
    echo "# Mevcut Port Kullanımı - $(date)"
    echo ""
    echo "## Host Portları (netstat)"
    echo '```'
    netstat -tlnp 2>/dev/null | grep LISTEN || ss -tlnp | grep LISTEN
    echo '```'
    echo ""
    echo "## Docker Container Portları"
    echo '```'
    docker ps --format "table {{.Names}}\t{{.Ports}}"
    echo '```'
} > "$BACKUP_DIR/current-port-usage.md"

echo -e "${GREEN}✅ Port kullanımı dokümante edildi: $BACKUP_DIR/current-port-usage.md${NC}"

# 6. Docker Container Durumunu Dokümante Etme
echo ""
echo -e "${YELLOW}🐳 6. Docker Container Durumunu Dokümante Etme${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

{
    echo "# Docker Container Durumu - $(date)"
    echo ""
    echo "## Tüm Container'lar"
    echo '```'
    docker ps -a
    echo '```'
    echo ""
    echo "## Container Network'leri"
    echo '```'
    docker network inspect mng_network 2>/dev/null || echo "mng_network bulunamadı"
    echo '```'
} > "$BACKUP_DIR/docker-container-status.md"

echo -e "${GREEN}✅ Container durumu dokümante edildi: $BACKUP_DIR/docker-container-status.md${NC}"

# 7. Özet
echo ""
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ Phase 1 Hazırlık Tamamlandı!${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${YELLOW}📁 Backup Dizini:${NC} $BACKUP_DIR"
echo ""
echo -e "${YELLOW}Yedeklenen Dosyalar:${NC}"
ls -lh "$BACKUP_DIR" | tail -n +2
echo ""
echo -e "${BLUE}Sonraki Adım:${NC} Phase 2 - Nginx Containerization"
echo ""

