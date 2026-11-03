set $collabora_upstream collabora;
proxy_pass http://$collabora_upstream:9980;
proxy_http_version 1.1;
proxy_set_header Host $http_host;
proxy_set_header X-Real-IP $remote_addr;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-Host $host;
proxy_set_header Upgrade $http_upgrade;
proxy_set_header Connection $collabora_connection;
proxy_read_timeout 36000s;
proxy_send_timeout 36000s;
proxy_buffering off;
# Bu location kendi başlığını tanımlayınca sunucu SAMEORIGIN kuralı devralınmaz.
# Çerçeve iznini Collabora frame-ancestors belirler.
add_header X-Content-Type-Options "nosniff" always;
