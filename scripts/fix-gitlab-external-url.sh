#!/bin/bash
# GitLab external URL düzeltme scripti

echo "GitLab external URL düzeltiliyor..."

# GitLab container'ına girip dosyayı düzelt
docker exec gitlab bash -c "sed -i '3625s|.*|external_url \"http://45.141.151.52:8090\"|' /etc/gitlab/gitlab.rb"

# Düzeltmeyi kontrol et
echo "Düzeltilen satır:"
docker exec gitlab sed -n "3625p" /etc/gitlab/gitlab.rb

# GitLab'ı yeniden yapılandır
echo "GitLab yeniden yapılandırılıyor..."
docker exec gitlab gitlab-ctl reconfigure

echo "GitLab yeniden başlatılıyor..."
docker exec gitlab gitlab-ctl restart

echo "GitLab external URL düzeltmesi tamamlandı!"

