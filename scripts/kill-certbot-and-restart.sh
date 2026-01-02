#!/bin/bash
# Çalışan Certbot process'lerini sonlandır ve yeniden başlat

echo "=========================================="
echo "Certbot Process'lerini Sonlandırma"
echo "=========================================="
echo ""

# Çalışan Certbot process'lerini bul ve sonlandır
CERTBOT_PIDS=$(ps aux | grep certbot | grep -v grep | awk '{print $2}')

if [ -z "$CERTBOT_PIDS" ]; then
    echo "✅ Çalışan Certbot process'i bulunamadı"
else
    echo "Çalışan Certbot process'leri:"
    ps aux | grep certbot | grep -v grep
    echo ""
    echo "Process'ler sonlandırılıyor..."
    for pid in $CERTBOT_PIDS; do
        echo "  - PID $pid sonlandırılıyor..."
        kill $pid 2>/dev/null
    done
    sleep 2
    
    # Hala çalışıyorsa force kill
    REMAINING=$(ps aux | grep certbot | grep -v grep | awk '{print $2}')
    if [ ! -z "$REMAINING" ]; then
        echo "  - Force kill yapılıyor..."
        for pid in $REMAINING; do
            kill -9 $pid 2>/dev/null
        done
    fi
    
    echo "✅ Tüm Certbot process'leri sonlandırıldı"
fi

echo ""
echo "=========================================="
echo "Certbot'u Yeniden Başlatma"
echo "=========================================="
echo ""
echo "Şimdi Certbot komutunu tekrar çalıştırabilirsiniz:"
echo ""
echo "certbot certonly --manual --preferred-challenges dns \\"
echo "  -d \"*.monitrang.com\" \\"
echo "  -d \"monitrang.com\" \\"
echo "  --email admin@monitrang.com \\"
echo "  --agree-tos \\"
echo "  --no-eff-email \\"
echo "  --manual-public-ip-logging-ok"
echo ""

