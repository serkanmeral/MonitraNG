#!/bin/bash
# Sunucu sistem kaynakları kontrolü
# Kullanım: ssh root@monitrang-server 'bash -s' < scripts/deployment/check-server-resources.sh
# veya sunucuya bağlandıktan sonra: bash check-server-resources.sh

set -e

echo "=============================================="
echo "  SUNUCU SİSTEM KAYNAKLARI"
echo "  Tarih: $(date)"
echo "=============================================="

echo ""
echo "=== BELLEK (RAM) ==="
free -h

echo ""
echo "=== DİSK KULLANIMI ==="
df -h

echo ""
echo "=== CPU ==="
echo "Çekirdek sayısı: $(nproc)"
if command -v lscpu &>/dev/null; then
  lscpu | grep -E 'Model name|CPU\(s\)|Thread|Core|Socket'
fi

echo ""
echo "=== YÜK (Load) ==="
uptime

echo ""
echo "=== DOCKER CONTAINER KAYNAK KULLANIMI (anlık) ==="
if command -v docker &>/dev/null; then
  docker stats --no-stream 2>/dev/null || echo "docker stats çalıştırılamadı"
else
  echo "Docker yüklü değil"
fi

echo ""
echo "=== DOCKER DİSK KULLANIMI ==="
if command -v docker &>/dev/null; then
  docker system df 2>/dev/null || true
fi

echo ""
echo "=============================================="
echo "  Özet: Ollama (örn. qwen2.5:3b) için en az ~4GB RAM, ~2GB disk önerilir"
echo "=============================================="
