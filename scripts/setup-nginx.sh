#!/bin/sh
# Nginx Yapılandırması Kurulum Script'i
# Production sunucusunda çalıştırılacak

set -e

echo "=========================================="
echo "MonitraNG - Nginx Yapılandırması Kurulumu"
echo "=========================================="

# Nginx kurulumunu kontrol et
if ! command -v nginx >/dev/null 2>&1; then
    echo "Nginx bulunamadı. Kurulum yapılıyor..."
    apt update
    apt install -y nginx
else
    echo "✓ Nginx kurulu: $(nginx -v 2>&1 | cut -d' ' -f3)"
fi

# Yapılandırma dosyasını oluştur
CONFIG_FILE="/etc/nginx/sites-available/monitrang"
TEMPLATE_FILE="/root/MonitraNG/scripts/nginx-config-template.conf"

if [ ! -f "$TEMPLATE_FILE" ]; then
    echo "HATA: Template dosyası bulunamadı: $TEMPLATE_FILE"
    echo "Lütfen template dosyasını sunucuya kopyalayın."
    exit 1
fi

echo "Yapılandırma dosyası oluşturuluyor: $CONFIG_FILE"
cp "$TEMPLATE_FILE" "$CONFIG_FILE"
chmod 644 "$CONFIG_FILE"

# Yapılandırmayı aktifleştir
echo "Yapılandırma aktifleştiriliyor..."
if [ -L "/etc/nginx/sites-enabled/monitrang" ]; then
    echo "Yapılandırma zaten aktif."
else
    ln -s /etc/nginx/sites-available/monitrang /etc/nginx/sites-enabled/monitrang
    echo "✓ Yapılandırma aktifleştirildi"
fi

# Varsayılan yapılandırmayı devre dışı bırak (opsiyonel)
if [ -L "/etc/nginx/sites-enabled/default" ]; then
    echo "Varsayılan yapılandırma devre dışı bırakılıyor..."
    rm /etc/nginx/sites-enabled/default
    echo "✓ Varsayılan yapılandırma devre dışı bırakıldı"
fi

# Nginx yapılandırmasını test et
echo "Nginx yapılandırması test ediliyor..."
if nginx -t; then
    echo "✓ Nginx yapılandırması geçerli"
else
    echo "HATA: Nginx yapılandırması geçersiz!"
    exit 1
fi

# Nginx'i yeniden başlat
echo "Nginx yeniden başlatılıyor..."
systemctl reload nginx

# Nginx durumunu kontrol et
if systemctl is-active --quiet nginx; then
    echo "✓ Nginx başarıyla çalışıyor"
else
    echo "HATA: Nginx çalışmıyor!"
    systemctl status nginx
    exit 1
fi

echo ""
echo "=========================================="
echo "Nginx yapılandırması başarıyla tamamlandı!"
echo "=========================================="
echo ""
echo "Yapılandırma dosyası: $CONFIG_FILE"
echo "Aktif yapılandırma: /etc/nginx/sites-enabled/monitrang"
echo ""
echo "Test için:"
echo "  curl -I http://app.monitrang.com"
echo "  curl -I http://api.monitrang.com"
echo "  curl -I http://auth.monitrang.com"
echo ""
echo "Not: SSL sertifikası kurulana kadar HTTPS çalışmayacak."
echo "     HTTP istekleri HTTPS'ye yönlendirilecek."

