#!/bin/bash
# MngUI Production Environment Variables Kurulum Script'i

MNGUI_DIR="/root/MngUI"
ENV_FILE="${MNGUI_DIR}/.env"

echo "=========================================="
echo "MngUI Production Environment Variables"
echo "=========================================="
echo ""

# MngUI dizini kontrolü
if [ ! -d "$MNGUI_DIR" ]; then
    echo "❌ MngUI dizini bulunamadı: $MNGUI_DIR"
    echo "Lütfen MngUI'nin kurulu olduğu dizini belirtin"
    exit 1
fi

# .env dosyası oluştur
echo "📝 .env dosyası oluşturuluyor..."

cat > "$ENV_FILE" << 'EOF'
# MngUI Production Environment Variables
# API Gateway URL (Tüm servisler için merkezi erişim)
GATEWAY_URL=https://api.monitrang.com

# Not: Gateway kullanıldığında diğer URL'ler kullanılmaz
# Ama yine de tanımlayalım (fallback için)

# MngKeeper API URL (Gateway kullanılmadığında)
KEEPER_URL=https://api.monitrang.com/keeper

# MngDataGateway API URL (Gateway kullanılmadığında)
DATAGATEWAY_URL=https://api.monitrang.com/data
SERVER_URL=https://api.monitrang.com/data

# MngHub API URL (Gateway kullanılmadığında - HTTPS olmalı!)
HUB_URL=https://api.monitrang.com/hub

# Base URL (production için)
BASE_URL=/
EOF

echo "✅ .env dosyası oluşturuldu: $ENV_FILE"
echo ""
echo "📋 İçerik:"
cat "$ENV_FILE"
echo ""
echo "=========================================="
echo "Sonraki adımlar:"
echo "1. MngUI'yi yeniden build edin:"
echo "   cd $MNGUI_DIR && npm run build"
echo ""
echo "2. MngUI'yi yeniden başlatın"
echo "=========================================="

