#!/bin/bash
set -e
chown -R www-data:www-data /var/www/html/tmp /var/www/html/logs 2>/dev/null || true
chmod -R 775 /var/www/html/tmp /var/www/html/logs 2>/dev/null || true
exec apache2-foreground
