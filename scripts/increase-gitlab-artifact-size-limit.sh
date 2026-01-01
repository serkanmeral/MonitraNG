#!/bin/bash

# GitLab Artifact Size Limit Artırma Script'i
# Bu script GitLab CE'de artifact size limit'ini artırır

echo "=== GitLab Artifact Size Limit Artırma ==="

# GitLab config dosyasını bul
GITLAB_CONFIG="/etc/gitlab/gitlab.rb"

if [ ! -f "$GITLAB_CONFIG" ]; then
    echo "ERROR: GitLab config dosyası bulunamadı: $GITLAB_CONFIG"
    echo "GitLab container içinde çalıştırıyorsanız, container'a girin:"
    echo "  docker exec -it gitlab bash"
    exit 1
fi

echo "GitLab config dosyası: $GITLAB_CONFIG"

# Mevcut ayarı kontrol et
if grep -q "artifacts_max_size" "$GITLAB_CONFIG"; then
    echo "Mevcut artifact size limit:"
    grep "artifacts_max_size" "$GITLAB_CONFIG"
    echo ""
    echo "Yeni limit eklemek için mevcut satırı düzenleyin veya yeni satır ekleyin."
else
    echo "Artifact size limit ayarı bulunamadı, yeni ayar eklenecek."
fi

echo ""
echo "Önerilen ayar (100MB):"
echo "  gitlab_rails['artifacts_max_size'] = 100.megabytes"
echo ""
echo "Veya daha büyük (500MB):"
echo "  gitlab_rails['artifacts_max_size'] = 500.megabytes"
echo ""
echo "Config dosyasını düzenlemek için:"
echo "  nano $GITLAB_CONFIG"
echo ""
echo "Düzenleme sonrası GitLab'ı yeniden yapılandırmak için:"
echo "  gitlab-ctl reconfigure"
echo ""
echo "GitLab'ı restart etmek için:"
echo "  gitlab-ctl restart"
echo ""

# Otomatik ekleme seçeneği
read -p "Otomatik olarak 100MB limit eklemek ister misiniz? (y/n): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    if grep -q "artifacts_max_size" "$GITLAB_CONFIG"; then
        echo "Mevcut ayar bulundu, güncelleniyor..."
        sed -i "s/.*artifacts_max_size.*/gitlab_rails['artifacts_max_size'] = 100.megabytes/" "$GITLAB_CONFIG"
    else
        echo "Yeni ayar ekleniyor..."
        echo "" >> "$GITLAB_CONFIG"
        echo "# Artifact size limit (100MB)" >> "$GITLAB_CONFIG"
        echo "gitlab_rails['artifacts_max_size'] = 100.megabytes" >> "$GITLAB_CONFIG"
    fi
    
    echo "✅ Ayar eklendi/güncellendi"
    echo ""
    echo "Şimdi GitLab'ı yeniden yapılandırmak için:"
    echo "  gitlab-ctl reconfigure"
    echo ""
    echo "Veya GitLab'ı restart etmek için:"
    echo "  gitlab-ctl restart"
else
    echo "Manuel düzenleme yapabilirsiniz."
fi

