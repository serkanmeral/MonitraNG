#!/bin/bash
# Nginx SSL güncellemesini sunucuya deploy etme script'i
# Bu script yerel makinede çalıştırılır ve sunucuya bağlanır

SERVER="root@monitrang-server"
REMOTE_SCRIPT="/root/update-nginx-ssl.sh"
LOCAL_SCRIPT="scripts/update-nginx-ssl.sh"

echo "=========================================="
echo "Nginx SSL Güncellemesi - Deploy"
echo "=========================================="
echo ""

# Script dosyasının varlığını kontrol et
if [ ! -f "$LOCAL_SCRIPT" ]; then
    echo "❌ Script dosyası bulunamadı: $LOCAL_SCRIPT"
    exit 1
fi

echo "📤 Script sunucuya kopyalanıyor..."
scp "$LOCAL_SCRIPT" "$SERVER:$REMOTE_SCRIPT"

if [ $? -eq 0 ]; then
    echo "✅ Script başarıyla kopyalandı"
    echo ""
    echo "🚀 Sunucuda script çalıştırılıyor..."
    ssh "$SERVER" "chmod +x $REMOTE_SCRIPT && sudo $REMOTE_SCRIPT"
    
    if [ $? -eq 0 ]; then
        echo ""
        echo "✅ Nginx SSL güncellemesi tamamlandı!"
    else
        echo ""
        echo "❌ Script çalıştırılırken hata oluştu"
        exit 1
    fi
else
    echo "❌ Script kopyalanamadı"
    exit 1
fi

