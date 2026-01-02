#!/bin/bash
# Let's Encrypt Wildcard Sertifikası Alma Script'i
# Bu script interaktif bir süreçtir - DNS TXT kaydını eklemeniz gerekecek

echo "=========================================="
echo "Let's Encrypt Wildcard Sertifikası Kurulumu"
echo "=========================================="
echo ""
echo "Bu script wildcard sertifikası için DNS doğrulama yapacak."
echo "Certbot size bir TXT kaydı verecek, bunu DNS paneline eklemeniz gerekecek."
echo ""
echo "Devam etmek için Enter'a basın..."
read

certbot certonly --manual --preferred-challenges dns \
  -d "*.monitrang.com" \
  -d "monitrang.com" \
  --email admin@monitrang.com \
  --agree-tos \
  --no-eff-email \
  --manual-public-ip-logging-ok

echo ""
echo "=========================================="
if [ $? -eq 0 ]; then
    echo "✅ Sertifika başarıyla oluşturuldu!"
    echo "Sertifika konumu: /etc/letsencrypt/live/monitrang.com/"
    echo ""
    echo "Sonraki adım: Nginx yapılandırmasını güncelleyin"
else
    echo "❌ Sertifika oluşturma başarısız oldu"
    echo "Lütfen hata mesajlarını kontrol edin"
fi
echo "=========================================="

