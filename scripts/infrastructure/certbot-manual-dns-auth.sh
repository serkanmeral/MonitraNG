#!/bin/bash
# Certbot manual DNS-01 auth hook for monitrang.com wildcard renewal.
# Logs required TXT value and waits until public DNS resolves it.

set -euo pipefail

DOMAIN="${CERTBOT_DOMAIN}"
VALIDATION="${CERTBOT_VALIDATION}"
FQDN="_acme-challenge.monitrang.com"
LOG="/root/acme-dns-challenges.log"
CURRENT="/root/acme-current-challenge.txt"

mkdir -p /root/scripts

{
  echo "========================================"
  echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) NEW CHALLENGE"
  echo "Certbot domain: ${DOMAIN}"
  echo "DNS name: _acme-challenge (zone: monitrang.com)"
  echo "Full FQDN: ${FQDN}"
  echo "TXT value: ${VALIDATION}"
  echo "========================================"
} | tee -a "${LOG}"

cat > "${CURRENT}" <<EOF
Add this TXT record in Hosting Dünyam DNS panel:

Type: TXT
Name: _acme-challenge
Content: ${VALIDATION}
TTL: 300

Note: If a second challenge follows, keep this record and add another TXT
with the same Name (_acme-challenge) and the new Content value.
EOF

echo "Waiting for DNS propagation: ${FQDN} -> ${VALIDATION}"

AUTH_NS=$(dig +short NS monitrang.com | head -2 | tr '\n' ' ')
RESOLVERS=(8.8.8.8 1.1.1.1 9.9.9.9)
for ns in ${AUTH_NS}; do
  RESOLVERS+=("${ns}")
done

for attempt in $(seq 1 120); do
  for resolver in "${RESOLVERS[@]}"; do
    if dig +short TXT "${FQDN}" @"${resolver}" 2>/dev/null | tr -d '"' | grep -Fq "${VALIDATION}"; then
      echo "DNS verified via ${resolver} (attempt ${attempt})"
      exit 0
    fi
  done
  sleep 15
done

echo "ERROR: DNS TXT record not found after 30 minutes." >&2
exit 1
