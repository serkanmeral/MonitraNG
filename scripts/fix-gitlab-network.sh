#!/bin/bash
# GitLab container'ını network'e bağlama ve başlatma script'i

echo "=========================================="
echo "GitLab Container Düzeltme"
echo "=========================================="
echo ""

# Mevcut container'ı durdur ve sil
echo "🗑️  Mevcut GitLab container'ı temizleniyor..."
docker rm -f gitlab 2>/dev/null

# Docker compose ile GitLab'ı başlat
echo "🚀 GitLab container'ı başlatılıyor..."
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose up -d gitlab

if [ $? -eq 0 ]; then
    echo "✅ GitLab container başlatıldı"
    echo ""
    echo "⏳ GitLab'ın başlaması için 30 saniye bekleniyor..."
    sleep 30
    
    echo ""
    echo "🔍 Container durumu:"
    docker ps | grep gitlab
    
    echo ""
    echo "🔍 Network bağlantısı:"
    docker inspect gitlab | grep -A 5 'Networks' | head -10
    
    echo ""
    echo "🔍 Port 8090 testi:"
    curl -I http://localhost:8090 2>&1 | head -3
    
    echo ""
    echo "=========================================="
    echo "✅ Tamamlandı!"
    echo "=========================================="
else
    echo "❌ GitLab container başlatılamadı"
    exit 1
fi

