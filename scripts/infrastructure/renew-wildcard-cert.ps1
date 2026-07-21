# Wildcard Let's Encrypt renewal (DNS-01 manual)
# Run on server after adding TXT records shown in /root/acme-current-challenge.txt
#
# From MonitraNG root (upload hooks first):
#   scp scripts/infrastructure/certbot-manual-dns-*.sh monitrang-server:/root/scripts/
#   ssh monitrang-server "chmod +x /root/scripts/certbot-manual-dns-*.sh && /root/scripts/renew-wildcard-cert.sh"

param(
    [switch]$DryRun
)

$server = "monitrang-server"

Write-Host "Uploading certbot DNS hooks..." -ForegroundColor Cyan
scp -o BatchMode=yes -o ConnectTimeout=20 `
    "scripts/infrastructure/certbot-manual-dns-auth.sh" `
    "scripts/infrastructure/certbot-manual-dns-cleanup.sh" `
    "${server}:/root/scripts/"

ssh -o BatchMode=yes -o ConnectTimeout=20 $server @'
set -e
chmod +x /root/scripts/certbot-manual-dns-auth.sh /root/scripts/certbot-manual-dns-cleanup.sh
: > /root/acme-dns-challenges.log
'@

$certbotCmd = @'
certbot certonly --force-renewal \
  --manual --preferred-challenges dns \
  -d "*.monitrang.com" -d "monitrang.com" \
  --manual-auth-hook /root/scripts/certbot-manual-dns-auth.sh \
  --manual-cleanup-hook /root/scripts/certbot-manual-dns-cleanup.sh \
  --non-interactive --agree-tos \
  --email admin@monitrang.com
'@

if ($DryRun) {
    Write-Host "[DryRun] Would run certbot on server." -ForegroundColor Yellow
    exit 0
}

Write-Host "Starting certbot renewal on server (may take up to 30 min per TXT record)..." -ForegroundColor Cyan
Write-Host "Watch: ssh monitrang-server 'tail -f /root/acme-dns-challenges.log'" -ForegroundColor Yellow

ssh -o BatchMode=yes -o ConnectTimeout=20 $server $certbotCmd

Write-Host "Reloading nginx..." -ForegroundColor Cyan
ssh -o BatchMode=yes -o ConnectTimeout=20 $server "nginx -t && systemctl reload nginx && certbot certificates"

Write-Host "Done." -ForegroundColor Green
