#!/bin/bash
# Mail Gönderme Test Scripti - Production Sunucusu İçinden
# Kullanım: ./test-send-mail.sh <to_email> [subject] [body]

set -e

# Renkli çıktı için
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Varsayılan değerler
FROM_EMAIL="${FROM_EMAIL:-noreply@monitrang.com}"
SMTP_HOST="${SMTP_HOST:-127.0.0.1}"
SMTP_PORT="${SMTP_PORT:-25}"

# Parametreler
TO_EMAIL="${1:-serkan.meral@outlook.com}"
SUBJECT="${2:-Test Mail from MonitraNG Server}"
BODY="${3:-Bu bir test mailidir. Production sunucusundan gönderilmiştir.}"

echo -e "${YELLOW}📧 Mail Gönderme Testi${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Gönderen: $FROM_EMAIL"
echo "Alıcı: $TO_EMAIL"
echo "Konu: $SUBJECT"
echo "SMTP: $SMTP_HOST:$SMTP_PORT"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Mail gönderme (swaks kullanılıyorsa)
if command -v swaks &> /dev/null; then
    echo -e "${YELLOW}swaks kullanılıyor...${NC}"
    swaks \
        --to "$TO_EMAIL" \
        --from "$FROM_EMAIL" \
        --server "$SMTP_HOST" \
        --port "$SMTP_PORT" \
        --h-Subject: "$SUBJECT" \
        --body "$BODY" \
        --silent
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Mail başarıyla gönderildi!${NC}"
        exit 0
    else
        echo -e "${RED}❌ Mail gönderilemedi!${NC}"
        exit 1
    fi
fi

# swaks yoksa, sendmail veya mailx kullan
if command -v sendmail &> /dev/null; then
    echo -e "${YELLOW}sendmail kullanılıyor...${NC}"
    {
        echo "From: $FROM_EMAIL"
        echo "To: $TO_EMAIL"
        echo "Subject: $SUBJECT"
        echo ""
        echo "$BODY"
    } | sendmail -t "$TO_EMAIL"
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Mail başarıyla gönderildi!${NC}"
        exit 0
    else
        echo -e "${RED}❌ Mail gönderilemedi!${NC}"
        exit 1
    fi
fi

# mailx kullan
if command -v mailx &> /dev/null; then
    echo -e "${YELLOW}mailx kullanılıyor...${NC}"
    echo "$BODY" | mailx -s "$SUBJECT" -r "$FROM_EMAIL" "$TO_EMAIL"
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Mail başarıyla gönderildi!${NC}"
        exit 0
    else
        echo -e "${RED}❌ Mail gönderilemedi!${NC}"
        exit 1
    fi
fi

# Python ile mail gönderme (son çare)
if command -v python3 &> /dev/null; then
    echo -e "${YELLOW}Python3 kullanılıyor...${NC}"
    python3 << EOF
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart

msg = MIMEMultipart()
msg['From'] = "$FROM_EMAIL"
msg['To'] = "$TO_EMAIL"
msg['Subject'] = "$SUBJECT"
msg.attach(MIMEText("$BODY", 'plain'))

try:
    server = smtplib.SMTP("$SMTP_HOST", $SMTP_PORT)
    server.sendmail("$FROM_EMAIL", "$TO_EMAIL", msg.as_string())
    server.quit()
    print("✅ Mail başarıyla gönderildi!")
    exit(0)
except Exception as e:
    print(f"❌ Mail gönderilemedi: {e}")
    exit(1)
EOF
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Mail başarıyla gönderildi!${NC}"
        exit 0
    else
        echo -e "${RED}❌ Mail gönderilemedi!${NC}"
        exit 1
    fi
fi

# Hiçbir araç bulunamadı
echo -e "${RED}❌ Mail göndermek için gerekli araç bulunamadı!${NC}"
echo "Lütfen şunlardan birini kurun:"
echo "  - swaks: apt-get install swaks"
echo "  - sendmail: apt-get install sendmail"
echo "  - mailx: apt-get install mailutils"
echo "  - python3: apt-get install python3"
exit 1

