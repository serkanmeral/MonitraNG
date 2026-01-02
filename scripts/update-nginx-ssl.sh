#!/bin/bash
# Nginx yapılandırmasını Let's Encrypt sertifikalarını kullanacak şekilde güncelleme script'i

NGINX_CONFIG="/etc/nginx/sites-available/monitrang"
BACKUP_DIR="/etc/nginx/backup"
BACKUP_FILE="${BACKUP_DIR}/monitrang-$(date +%Y%m%d-%H%M%S).conf"

echo "=========================================="
echo "Nginx SSL Yapılandırması Güncelleme"
echo "=========================================="
echo ""

# Root kontrolü
if [ "$EUID" -ne 0 ]; then 
    echo "❌ Bu script root olarak çalıştırılmalıdır (sudo)"
    exit 1
fi

# Nginx config dosyası kontrolü
if [ ! -f "$NGINX_CONFIG" ]; then
    echo "❌ Nginx yapılandırma dosyası bulunamadı: $NGINX_CONFIG"
    exit 1
fi

# Backup dizini oluştur
mkdir -p "$BACKUP_DIR"

# Backup al
echo "📦 Yedek oluşturuluyor: $BACKUP_FILE"
cp "$NGINX_CONFIG" "$BACKUP_FILE"
if [ $? -eq 0 ]; then
    echo "✅ Yedek başarıyla oluşturuldu"
else
    echo "❌ Yedek oluşturulamadı"
    exit 1
fi

echo ""
echo "🔧 SSL sertifika satırları güncelleniyor..."

# SSL sertifika satırlarını güncelle
# Yorum satırlarını kaldır ve aktif hale getir
sed -i 's|# ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;|ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;|g' "$NGINX_CONFIG"
sed -i 's|# ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;|ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;|g' "$NGINX_CONFIG"

# Eski yorum satırlarını temizle (eğer varsa)
sed -i "s|# SSL Certificate (Let's Encrypt - gelecekte eklenecek)|# SSL Certificate (Let's Encrypt)|g" "$NGINX_CONFIG"

echo "✅ SSL sertifika satırları güncellendi"
echo ""

# Nginx yapılandırmasını test et
echo "🧪 Nginx yapılandırması test ediliyor..."
nginx -t

if [ $? -eq 0 ]; then
    echo "✅ Nginx yapılandırması geçerli"
    echo ""
    echo "🔄 Nginx yeniden başlatılıyor..."
    systemctl reload nginx
    
    if [ $? -eq 0 ]; then
        echo "✅ Nginx başarıyla yeniden başlatıldı"
        echo ""
        echo "=========================================="
        echo "✅ Tamamlandı!"
        echo "=========================================="
        echo ""
        echo "Sonraki adımlar:"
        echo "1. SSL sertifikasını test edin:"
        echo "   openssl s_client -connect app.monitrang.com:443 -servername app.monitrang.com"
        echo ""
        echo "2. Browser'dan test edin:"
        echo "   https://app.monitrang.com"
        echo ""
        echo "3. Tüm subdomain'leri test edin:"
        echo "   - https://app.monitrang.com"
        echo "   - https://api.monitrang.com"
        echo "   - https://auth.monitrang.com"
        echo "   - https://docs.monitrang.com"
        echo "   - https://gitlab.monitrang.com"
    else
        echo "❌ Nginx yeniden başlatılamadı"
        echo "Yedek dosyayı geri yüklemek için:"
        echo "  cp $BACKUP_FILE $NGINX_CONFIG"
        exit 1
    fi
else
    echo "❌ Nginx yapılandırması geçersiz"
    echo "Yedek dosyayı geri yüklemek için:"
    echo "  cp $BACKUP_FILE $NGINX_CONFIG"
    exit 1
fi

