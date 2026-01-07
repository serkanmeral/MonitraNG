#!/bin/bash
# ============================================
# admin.monitrang.com Subdomain Setup Script
# ============================================
# Bu script admin subdomain'ini kurar:
# 1. DNS kaydı kontrol eder
# 2. HTTP Basic Auth şifre dosyası oluşturur
# 3. Nginx config'i test eder
# 4. Nginx'i reload eder
# ============================================

set -e  # Exit on error

echo "============================================"
echo "admin.monitrang.com Subdomain Setup"
echo "============================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# ============================================
# 1. DNS Kontrolü
# ============================================
echo -e "${YELLOW}[1/5] DNS kaydı kontrol ediliyor...${NC}"
if host admin.monitrang.com > /dev/null 2>&1; then
    RESOLVED_IP=$(host admin.monitrang.com | grep "has address" | awk '{print $4}' | head -1)
    echo -e "${GREEN}✓ DNS kaydı bulundu: admin.monitrang.com → $RESOLVED_IP${NC}"
else
    echo -e "${RED}✗ DNS kaydı bulunamadı!${NC}"
    echo ""
    echo "Lütfen DNS sağlayıcınızda aşağıdaki kaydı ekleyin:"
    echo "  Type: A"
    echo "  Name: admin"
    echo "  Value: 45.141.151.52"
    echo "  TTL: 300 (veya Auto)"
    echo ""
    echo "DNS kaydı eklendikten sonra bu scripti tekrar çalıştırın."
    exit 1
fi
echo ""

# ============================================
# 2. HTTP Basic Auth Şifre Dosyası Oluşturma
# ============================================
echo -e "${YELLOW}[2/5] HTTP Basic Auth şifre dosyası oluşturuluyor...${NC}"

# htpasswd kurulu mu kontrol et
if ! command -v htpasswd &> /dev/null; then
    echo -e "${YELLOW}htpasswd kurulu değil, yükleniyor...${NC}"
    apt-get update -qq
    apt-get install -y -qq apache2-utils
fi

# Şifre dosyası zaten var mı?
if [ -f /etc/nginx/.htpasswd ]; then
    echo -e "${YELLOW}⚠ Şifre dosyası zaten mevcut: /etc/nginx/.htpasswd${NC}"
    read -p "Yeni şifre oluşturmak ister misiniz? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${GREEN}✓ Mevcut şifre dosyası kullanılacak${NC}"
    else
        rm /etc/nginx/.htpasswd
        echo -e "${YELLOW}Kullanıcı adı (default: admin): ${NC}"
        read USERNAME
        USERNAME=${USERNAME:-admin}
        
        echo -e "${YELLOW}Şifre: ${NC}"
        htpasswd -c /etc/nginx/.htpasswd "$USERNAME"
        echo -e "${GREEN}✓ Şifre dosyası oluşturuldu${NC}"
    fi
else
    echo -e "${YELLOW}Kullanıcı adı (default: admin): ${NC}"
    read USERNAME
    USERNAME=${USERNAME:-admin}
    
    echo -e "${YELLOW}Şifre: ${NC}"
    htpasswd -c /etc/nginx/.htpasswd "$USERNAME"
    echo -e "${GREEN}✓ Şifre dosyası oluşturuldu: /etc/nginx/.htpasswd${NC}"
fi

# Dosya izinlerini ayarla
chmod 644 /etc/nginx/.htpasswd
chown root:root /etc/nginx/.htpasswd
echo ""

# ============================================
# 3. Nginx Config Dosyasının Varlığını Kontrol Et
# ============================================
echo -e "${YELLOW}[3/5] Nginx config dosyası kontrol ediliyor...${NC}"

# Config dosyası container içinde mount edilmiş mi kontrol et
if docker exec nginx test -f /etc/nginx/conf.d/admin.monitrang.conf; then
    echo -e "${GREEN}✓ Config dosyası bulundu: /etc/nginx/conf.d/admin.monitrang.conf${NC}"
else
    echo -e "${RED}✗ Config dosyası bulunamadı!${NC}"
    echo ""
    echo "Lütfen aşağıdaki adımları takip edin:"
    echo "1. ApplicationResources/mng_common/nginx/conf.d/admin.monitrang.conf dosyasını sunucuya kopyalayın"
    echo "2. Docker Compose'u yeniden başlatın: cd ApplicationResources/mng_common && docker-compose restart nginx"
    exit 1
fi
echo ""

# ============================================
# 4. Nginx Config Test
# ============================================
echo -e "${YELLOW}[4/5] Nginx config test ediliyor...${NC}"
if docker exec nginx nginx -t; then
    echo -e "${GREEN}✓ Nginx config geçerli${NC}"
else
    echo -e "${RED}✗ Nginx config hatası!${NC}"
    echo "Lütfen config dosyasını kontrol edin ve düzeltin."
    exit 1
fi
echo ""

# ============================================
# 5. Nginx Reload
# ============================================
echo -e "${YELLOW}[5/5] Nginx reload ediliyor...${NC}"
if docker exec nginx nginx -s reload; then
    echo -e "${GREEN}✓ Nginx başarıyla reload edildi${NC}"
else
    echo -e "${RED}✗ Nginx reload hatası!${NC}"
    exit 1
fi
echo ""

# ============================================
# Başarı Mesajı
# ============================================
echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}✓ admin.monitrang.com başarıyla kuruldu!${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""
echo "Erişim bilgileri:"
echo "  URL: https://admin.monitrang.com"
echo "  Kullanıcı adı: $USERNAME"
echo "  Şifre: (girdiğiniz şifre)"
echo ""
echo "Admin UI'lar:"
echo "  - Portainer: https://admin.monitrang.com/portainer/"
echo "  - RabbitMQ: https://admin.monitrang.com/rabbitmq/"
echo "  - Seq: https://admin.monitrang.com/seq/"
echo "  - Mongo Express: https://admin.monitrang.com/mongo/"
echo "  - Redis Commander: https://admin.monitrang.com/redis/"
echo "  - Node-RED: https://admin.monitrang.com/nodered/"
echo ""
echo "Not: SSL sertifikası wildcard olduğu için otomatik çalışacaktır."
echo ""

