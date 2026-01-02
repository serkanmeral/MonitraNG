#!/bin/bash
# Create .env file from env.example with default password

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Get the directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"
COMMON_DIR="$PROJECT_ROOT/ApplicationResources/mng_common"

DEFAULT_PASSWORD="!2345Qawsedrf*"

echo -e "${GREEN}Creating .env file...${NC}"

if [ -f "$COMMON_DIR/.env" ]; then
    echo -e "${YELLOW}Warning: .env file already exists at $COMMON_DIR/.env${NC}"
    read -p "Do you want to overwrite it? (yes/no): " confirm
    if [ "$confirm" != "yes" ]; then
        echo "Cancelled."
        exit 1
    fi
fi

# Create .env file from env.example and replace CHANGE_ME with default password
sed "s/CHANGE_ME/$DEFAULT_PASSWORD/g" "$COMMON_DIR/env.example" > "$COMMON_DIR/.env"

echo -e "${GREEN}.env file created successfully!${NC}"
echo -e "${YELLOW}Location: $COMMON_DIR/.env${NC}"
echo ""
echo -e "${GREEN}Default password set to: $DEFAULT_PASSWORD${NC}"
echo ""
echo -e "${YELLOW}Note: .env file is in .gitignore and will not be committed.${NC}"

