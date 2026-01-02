#!/bin/bash
# Çalışan Certbot process'lerini bulma script'i

echo "=========================================="
echo "Çalışan Certbot Process'leri"
echo "=========================================="
echo ""

# Certbot process'lerini bul
ps aux | grep certbot | grep -v grep

echo ""
echo "=========================================="
echo "Certbot log dosyaları:"
echo "=========================================="
ls -lah /tmp/certbot-log-* 2>/dev/null || echo "Log dosyası bulunamadı"

echo ""
echo "=========================================="
echo "Son Certbot log'u:"
echo "=========================================="
LATEST_LOG=$(ls -t /tmp/certbot-log-*/log 2>/dev/null | head -1)
if [ ! -z "$LATEST_LOG" ]; then
    tail -20 "$LATEST_LOG"
else
    echo "Log dosyası bulunamadı"
fi

