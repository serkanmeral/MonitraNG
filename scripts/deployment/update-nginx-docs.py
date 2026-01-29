#!/usr/bin/env python3
"""Nginx config'te docs.monitrang.com'u static root olarak güncelle"""

import re
import sys

config_file = '/etc/nginx/sites-available/monitrang'

with open(config_file, 'r') as f:
    content = f.read()

# Önce mevcut "immutable" hatalarını düzelt
content = content.replace('add_header Cache-Control \\ public, immutable";', 'add_header Cache-Control "public" always;')
content = content.replace('add_header Cache-Control "public, immutable";', 'add_header Cache-Control "public" always;')

# docs.monitrang.com location bloğunu değiştir
old_pattern = r'(# GitLab Pages \(dokümantasyon\).*?proxy_read_timeout 60s;)'
new_content = '''    # MkDocs static files (production build)
    root /var/www/docs.monitrang.com;
    index index.html;
    
    location / {
        try_files $uri $uri/ /index.html;
    }
    
    # Cache static assets
    location ~* \.(jpg|jpeg|png|gif|ico|css|js|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public" always;
    }'''

content = re.sub(old_pattern, new_content, content, flags=re.DOTALL)

with open(config_file, 'w') as f:
    f.write(content)

print('✅ Nginx config güncellendi: docs.monitrang.com artık static root kullanıyor')
