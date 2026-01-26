#!/bin/sh
# Sunucuda mngdomainui icin SERVER_SCHEDULER_URL ekler (SERVER_HUB_URL satirindan sonra).
# Kullanim: sunucuda cd /root/MonitraNG/ApplicationResources/mng_apps && sh /path/to/add-scheduler-url-on-server.sh
set -e
COMPOSE="docker-compose.production.yml"
if ! grep -q 'SERVER_SCHEDULER_URL' "$COMPOSE"; then
  LINE='      - SERVER_SCHEDULER_URL=${SERVER_SCHEDULER_URL:-http://mngscheduler:5090}'
  sed -i.bak "/SERVER_HUB_URL=.*mnghub:5020}/a\\
$LINE" "$COMPOSE"
  echo "Added SERVER_SCHEDULER_URL"
else
  echo "SERVER_SCHEDULER_URL already present"
fi
