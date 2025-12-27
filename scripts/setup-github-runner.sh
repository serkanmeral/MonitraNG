#!/bin/bash
# GitHub Actions Self-Hosted Runner Kurulum Script
# Linux/Mac için

set -e

GITHUB_TOKEN="${1:-}"
RUNNER_NAME="${2:-local-runner}"
RUNNER_WORK_FOLDER="${3:-$HOME/actions-runner}"

if [ -z "$GITHUB_TOKEN" ]; then
    echo "Kullanım: $0 <GITHUB_TOKEN> [RUNNER_NAME] [WORK_FOLDER]"
    echo "Örnek: $0 ghp_xxxxxxxxxxxxx my-runner $HOME/actions-runner"
    exit 1
fi

echo ""
echo "=== GitHub Actions Runner Kurulumu ==="
echo ""

# 1. Runner klasörü oluştur
echo "1. Runner klasörü oluşturuluyor..."
mkdir -p "$RUNNER_WORK_FOLDER"
cd "$RUNNER_WORK_FOLDER"
echo "  ✅ Klasör oluşturuldu: $RUNNER_WORK_FOLDER"

# 2. Runner indir
echo ""
echo "2. Runner indiriliyor..."
RUNNER_VERSION="2.311.0"

# OS tespiti
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    RUNNER_URL="https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    if [[ $(uname -m) == "arm64" ]]; then
        RUNNER_URL="https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-osx-arm64-${RUNNER_VERSION}.tar.gz"
    else
        RUNNER_URL="https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-osx-x64-${RUNNER_VERSION}.tar.gz"
    fi
else
    echo "  ❌ Desteklenmeyen işletim sistemi: $OSTYPE"
    exit 1
fi

curl -o runner.tar.gz -L "$RUNNER_URL"
echo "  ✅ Runner indirildi"

# 3. Tar.gz'i çıkar
echo ""
echo "3. Tar.gz dosyası çıkarılıyor..."
tar xzf runner.tar.gz
rm runner.tar.gz
echo "  ✅ Tar.gz çıkarıldı"

# 4. Runner yapılandır
echo ""
echo "4. Runner yapılandırılıyor..."
./config.sh --url https://github.com/serkanmeral/MonitraNG --token "$GITHUB_TOKEN" --name "$RUNNER_NAME" --work "_work" --unattended
echo "  ✅ Runner yapılandırıldı"

# 5. Servis olarak kur (opsiyonel)
echo ""
read -p "5. Servis olarak kurulsun mu? (y/n): " install_service

if [ "$install_service" = "y" ] || [ "$install_service" = "Y" ]; then
    sudo ./svc.sh install
    sudo ./svc.sh start
    echo "  ✅ Runner servis olarak kuruldu ve başlatıldı"
else
    echo "  ℹ️  Runner'ı manuel çalıştırmak için:"
    echo "     cd $RUNNER_WORK_FOLDER"
    echo "     ./run.sh"
fi

echo ""
echo "=== Kurulum Tamamlandı ==="
echo ""
echo "Runner bilgileri:"
echo "  - Klasör: $RUNNER_WORK_FOLDER"
echo "  - İsim: $RUNNER_NAME"
echo "  - Repository: serkanmeral/MonitraNG"
echo ""
echo "Sonraki adımlar:"
echo "  1. GitHub repository'de Settings > Actions > Runners bölümünden runner'ı kontrol et"
echo "  2. Workflow'ları test et: git push origin main"

