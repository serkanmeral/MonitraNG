#!/bin/bash
# Optional cleanup after ACME DNS challenge (record may be removed manually).

LOG="/root/acme-dns-challenges.log"
echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) cleanup for ${CERTBOT_DOMAIN:-unknown}" >> "${LOG}"
