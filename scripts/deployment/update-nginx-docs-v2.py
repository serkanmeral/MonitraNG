#!/usr/bin/env python3
"""Nginx config'te docs.monitrang.com location bloğunu static root yap"""

config_file = '/etc/nginx/sites-available/monitrang'

with open(config_file, 'r') as f:
    content = f.read()

# docs.monitrang.com HTTPS server bloğundaki location'ı bul ve değiştir
# Pattern: # GitLab Pages ile başlayıp proxy_read_timeout ile biten bloğu bul
import re

pattern = r'(# GitLab Pages \(dokümantasyon\).*?proxy_read_timeout 60s;)'
replacement = '''    # MkDocs static files (production build)
    root /var/www/docs.monitrang.com;
    index index.html;
    
    location / {
        try_files $uri $uri/ /index.html;
    }
    
    # Cache static assets
    location ~* \\.(jpg|jpeg|png|gif|ico|css|js|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public" always;
    }'''

new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)

if new_content != content:
    with open(config_file, 'w') as f:
        f.write(new_content)
    print('✅ Nginx config güncellendi')
else:
    print('⚠️  Pattern bulunamadı, manuel kontrol gerekebilir')
