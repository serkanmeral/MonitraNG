#!/bin/bash
# SSL sertifikasını test etme script'i

DOMAINS=(
    "app.monitrang.com"
    "api.monitrang.com"
    "auth.monitrang.com"
    "docs.monitrang.com"
    "gitlab.monitrang.com"
    "monitrang.com"
    "www.monitrang.com"
)

echo "=========================================="
echo "SSL Sertifika Testi"
echo "=========================================="
echo ""

for domain in "${DOMAINS[@]}"; do
    echo "🔍 Test ediliyor: $domain"
    
    # SSL bağlantısını test et
    result=$(echo | openssl s_client -connect "$domain:443" -servername "$domain" 2>/dev/null | openssl x509 -noout -dates 2>/dev/null)
    
    if [ $? -eq 0 ]; then
        echo "  ✅ SSL bağlantısı başarılı"
        echo "$result" | while read -r line; do
            echo "     $line"
        done
    else
        echo "  ❌ SSL bağlantısı başarısız"
    fi
    
    echo ""
done

echo "=========================================="
echo "Test tamamlandı"
echo "=========================================="

