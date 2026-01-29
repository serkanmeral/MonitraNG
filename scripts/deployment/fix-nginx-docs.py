#!/usr/bin/env python3
"""Nginx config'te docs.monitrang.com'u düzelt - static root yap"""

config_file = '/etc/nginx/sites-available/monitrang'

with open(config_file, 'r') as f:
    lines = f.readlines()

# docs.monitrang.com server bloğunu bul ve düzelt
in_docs_server = False
start_line = None
end_line = None

for i, line in enumerate(lines):
    if 'server_name docs.monitrang.com' in line and 'listen 443' in lines[max(0, i-5):i]:
        in_docs_server = True
        start_line = i
    elif in_docs_server and line.strip() == '}' and i > start_line + 10:
        end_line = i
        break

if start_line and end_line:
    # Location bloğunu bul
    location_start = None
    location_end = None
    for i in range(start_line, end_line):
        if '# GitLab Pages' in lines[i] or '# MkDocs static' in lines[i]:
            location_start = i
        elif location_start and lines[i].strip() == '}' and i > location_start:
            location_end = i
            break
    
    if location_start and location_end:
        # Location bloğunu değiştir
        new_location = '''    # MkDocs static files (production build)
    root /var/www/docs.monitrang.com;
    index index.html;
    
    location / {
        try_files $uri $uri/ /index.html;
    }
    
    # Cache static assets
    location ~* \\.(jpg|jpeg|png|gif|ico|css|js|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public" always;
    }
'''
        lines[location_start:location_end+1] = [new_location]
        
        with open(config_file, 'w') as f:
            f.writelines(lines)
        print('✅ Nginx config düzeltildi')
    else:
        print('❌ Location bloğu bulunamadı')
else:
    print('❌ docs.monitrang.com server bloğu bulunamadı')
