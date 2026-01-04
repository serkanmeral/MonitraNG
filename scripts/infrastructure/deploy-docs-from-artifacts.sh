#!/bin/bash
# GitLab Pages Artifacts'ı Nginx'e Deploy Scripti
# Kullanım: ./deploy-docs-from-artifacts.sh [artifacts-path]

set -e

# Renkli çıktı için
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Yapılandırma
DOCS_ROOT="/var/www/docs.monitrang.com"
NGINX_CONTAINER="nginx"
BACKUP_DIR="/var/www/backups/docs.monitrang.com-$(date +%Y%m%d-%H%M%S)"

# Parametreler
ARTIFACTS_PATH="${1:-public}"

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}GitLab Pages Artifacts Deploy${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# 1. Artifacts path kontrolü
echo -e "${YELLOW}📦 1. Artifacts Kontrolü${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ ! -d "$ARTIFACTS_PATH" ]; then
    echo -e "${RED}❌ Artifacts klasörü bulunamadı: $ARTIFACTS_PATH${NC}"
    echo ""
    echo "Kullanım:"
    echo "  $0 [artifacts-path]"
    echo ""
    echo "Örnek:"
    echo "  $0 public                    # Mevcut dizindeki public/ klasörü"
    echo "  $0 /tmp/public               # Belirtilen path"
    echo "  $0 /path/to/artifacts/public # Artifacts extract edilmiş path"
    exit 1
fi

if [ ! -f "$ARTIFACTS_PATH/index.html" ]; then
    echo -e "${RED}❌ Artifacts içinde index.html bulunamadı!${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Artifacts klasörü bulundu: $ARTIFACTS_PATH${NC}"
echo -e "${BLUE}   Dosya sayısı: $(find "$ARTIFACTS_PATH" -type f | wc -l)${NC}"
echo -e "${BLUE}   Boyut: $(du -sh "$ARTIFACTS_PATH" | cut -f1)${NC}"
echo ""

# 2. Backup
echo -e "${YELLOW}💾 2. Backup${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ -d "$DOCS_ROOT" ] && [ "$(ls -A $DOCS_ROOT)" ]; then
    mkdir -p "$(dirname "$BACKUP_DIR")"
    cp -r "$DOCS_ROOT" "$BACKUP_DIR"
    echo -e "${GREEN}✅ Backup oluşturuldu: $BACKUP_DIR${NC}"
else
    echo -e "${YELLOW}⚠️  Mevcut dokümantasyon bulunamadı, backup atlanıyor${NC}"
fi
echo ""

# 3. Klasör oluşturma
echo -e "${YELLOW}📁 3. Klasör Hazırlığı${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

mkdir -p "$DOCS_ROOT"
echo -e "${GREEN}✅ Klasör hazır: $DOCS_ROOT${NC}"
echo ""

# 4. Artifacts'ı kopyalama
echo -e "${YELLOW}📋 4. Artifacts Kopyalama${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Eski içeriği temizle (backup'tan sonra)
rm -rf "$DOCS_ROOT"/*

# Artifacts'ı kopyala
cp -r "$ARTIFACTS_PATH"/* "$DOCS_ROOT/"

echo -e "${GREEN}✅ Artifacts kopyalandı${NC}"
echo -e "${BLUE}   Hedef: $DOCS_ROOT${NC}"
echo -e "${BLUE}   Dosya sayısı: $(find "$DOCS_ROOT" -type f | wc -l)${NC}"
echo -e "${BLUE}   Boyut: $(du -sh "$DOCS_ROOT" | cut -f1)${NC}"
echo ""

# 5. İzinler
echo -e "${YELLOW}🔐 5. İzinler${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

chown -R root:root "$DOCS_ROOT"
chmod -R 755 "$DOCS_ROOT"
echo -e "${GREEN}✅ İzinler ayarlandı${NC}"
echo ""

# 6. Nginx test (eğer container erişilebilirse)
echo -e "${YELLOW}🔧 6. Nginx Yapılandırması Test${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if docker ps | grep -q "$NGINX_CONTAINER"; then
    if docker exec "$NGINX_CONTAINER" nginx -t 2>&1 | grep -q "successful"; then
        echo -e "${GREEN}✅ Nginx yapılandırması geçerli${NC}"
        echo ""
        echo -e "${YELLOW}💡 Nginx container'ı restart etmek için:${NC}"
        echo "   docker compose restart nginx"
        echo "   veya"
        echo "   docker exec $NGINX_CONTAINER nginx -s reload"
    else
        echo -e "${YELLOW}⚠️  Nginx container bulundu ama test edilemedi${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  Nginx container bulunamadı, test atlanıyor${NC}"
fi
echo ""

# 7. Özet
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ Deploy Tamamlandı!${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${YELLOW}📁 Dokümantasyon Klasörü:${NC} $DOCS_ROOT"
echo -e "${YELLOW}💾 Backup:${NC} $BACKUP_DIR"
echo ""
echo -e "${BLUE}Sonraki Adımlar:${NC}"
echo "  1. Nginx container'ı restart et: docker compose restart nginx"
echo "  2. Test et: curl -I https://docs.monitrang.com"
echo "  3. Tarayıcıda kontrol et: https://docs.monitrang.com"
echo ""

