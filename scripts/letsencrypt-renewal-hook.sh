#!/bin/bash
# Let's Encrypt Manuel Sertifika Yenileme Hook Script'i
# Bu script manuel sertifika için DNS doğrulama yaparak yenileme yapar

DOMAIN="monitrang.com"
EMAIL="admin@monitrang.com"
RENEWAL_LOG="/var/log/letsencrypt-renewal.log"

echo "$(date): Let's Encrypt sertifika yenileme başlatılıyor..." >> "$RENEWAL_LOG"

# Certbot ile manuel yenileme (DNS doğrulama)
certbot certonly --manual --preferred-challenges dns \
  -d "*.monitrang.com" \
  -d "monitrang.com" \
  --email "$EMAIL" \
  --agree-tos \
  --no-eff-email \
  --manual-public-ip-logging-ok \
  --non-interactive \
  --manual-auth-hook /root/letsencrypt-dns-auth.sh \
  --manual-cleanup-hook /root/letsencrypt-dns-cleanup.sh

if [ $? -eq 0 ]; then
    echo "$(date): Sertifika başarıyla yenilendi" >> "$RENEWAL_LOG"
    
    # Nginx'i yeniden yükle
    systemctl reload nginx
    echo "$(date): Nginx yeniden yüklendi" >> "$RENEWAL_LOG"
    
    exit 0
else
    echo "$(date): Sertifika yenileme başarısız oldu" >> "$RENEWAL_LOG"
    exit 1
fi

