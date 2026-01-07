#!/bin/bash
# Mailu DKIM Key Alma Scripti
# Kullanım: ./get-dkim-key.sh [domain]

DOMAIN=${1:-monitrang.com}
MAILU_DIR="/root/MonitraNG/ApplicationResources/mng_common/mailu"

echo "=== Mailu DKIM Key Alma ==="
echo "Domain: $DOMAIN"
echo ""

# DKIM dosyasını kontrol et
echo "1. DKIM dosyasını kontrol ediliyor..."
DKIM_FILE="$MAILU_DIR/dkim/${DOMAIN}.txt"

if [ -f "$DKIM_FILE" ]; then
    echo "✓ DKIM dosyası bulundu: $DKIM_FILE"
    echo ""
    echo "=== DKIM TXT Kaydı ==="
    cat "$DKIM_FILE"
    echo ""
    echo "=== DNS Kaydı ==="
    SELECTOR=$(cat "$DKIM_FILE" | grep -oP 'v=DKIM1; k=rsa; p=\K[^;]+' | head -1)
    if [ -z "$SELECTOR" ]; then
        SELECTOR="mailu"
    fi
    echo "Hostname: ${SELECTOR}._domainkey.${DOMAIN}"
    echo "Type: TXT"
    echo "Value: $(cat "$DKIM_FILE" | tr -d '\n' | sed 's/.*"\(.*\)".*/\1/')"
else
    echo "✗ DKIM dosyası bulunamadı: $DKIM_FILE"
    echo ""
    echo "DKIM key'i oluşturmak için:"
    echo "1. Admin panelden domain oluşturun: https://mail.monitrang.com/admin"
    echo "2. Domain oluşturulduktan sonra DKIM key otomatik oluşturulur"
    echo "3. Admin panelden 'Domains' > 'monitrang.com' > 'DKIM' sekmesinden key'i alabilirsiniz"
fi

