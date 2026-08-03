#!/usr/bin/env bash
# Minimal Debian/Ubuntu install helper (P3a). Run as root from publish folder.
set -euo pipefail

PREFIX="${PREFIX:-/opt/mnglogs/agent}"
CONFIG_DIR="${CONFIG_DIR:-/etc/mnglogs/agent}"
DATA_DIR="${DATA_DIR:-/var/lib/mnglogs/agent}"
SERVICE_SRC="$(dirname "$0")/mnglogs-agent.service"

if [[ "$(id -u)" -ne 0 ]]; then
  echo "Root required." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# When packaged beside published binaries, install from parent of packaging/
PUBLISH_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
if [[ ! -x "$PUBLISH_DIR/MngLogs.Agent" && ! -f "$PUBLISH_DIR/MngLogs.Agent" ]]; then
  PUBLISH_DIR="$(pwd)"
fi

mkdir -p "$PREFIX" "$CONFIG_DIR" "$DATA_DIR/queue" "$DATA_DIR/logs"
rsync -a --delete --exclude packaging --exclude '*.pdb' "$PUBLISH_DIR/" "$PREFIX/" 2>/dev/null || \
  cp -a "$PUBLISH_DIR"/. "$PREFIX/"

if [[ ! -f "$CONFIG_DIR/system.json" ]]; then
  cat > "$CONFIG_DIR/system.json" <<EOF
{
  "collectorBaseUrl": "http://127.0.0.1:5091",
  "apiKey": "",
  "hostId": "",
  "localUiHost": "127.0.0.1",
  "localUiPort": 5092,
  "dataDirectory": "$DATA_DIR",
  "configDirectory": "$CONFIG_DIR"
}
EOF
fi

install -m 644 "$SERVICE_SRC" /etc/systemd/system/mnglogs-agent.service
sed -i "s|/opt/mnglogs/agent|$PREFIX|g" /etc/systemd/system/mnglogs-agent.service

systemctl daemon-reload
systemctl enable mnglogs-agent.service
systemctl restart mnglogs-agent.service
systemctl --no-pager --full status mnglogs-agent.service || true

echo
echo "Installed. Configure: $PREFIX/MngLogs.Agent config set --collector <url> --api-key <key>"
echo "Local UI: http://127.0.0.1:5092/"
