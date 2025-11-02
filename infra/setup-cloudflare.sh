#!/bin/bash

# Скрипт настройки Cloudflare для MASK BROWSER
# Запускать на сервере 109.172.101.73

set -e

echo "🚀 Настройка Cloudflare для MASK BROWSER"

# Проверка прав
if [ "$EUID" -ne 0 ]; then 
    echo "❌ Запустите скрипт от root"
    exit 1
fi

# 1. Установка Cloudflared
echo "📦 Установка Cloudflared..."
cd /tmp
wget -q https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
dpkg -i cloudflared-linux-amd64.deb || apt-get install -f -y
rm cloudflared-linux-amd64.deb

# 2. Создание директории для конфигурации
mkdir -p /etc/cloudflared
mkdir -p /opt/mask-browser/infra

# 3. Настройка UFW для Cloudflare IPs
echo "🔥 Настройка UFW..."
ufw allow from 173.245.48.0/20 to any port 80
ufw allow from 173.245.48.0/20 to any port 443
ufw allow from 103.21.244.0/22 to any port 80
ufw allow from 103.21.244.0/22 to any port 443

# 4. Копирование конфигурации Nginx
echo "📝 Настройка Nginx..."
if [ -f /opt/mask-browser/infra/nginx-cloudflare.conf ]; then
    cp /opt/mask-browser/infra/nginx-cloudflare.conf /etc/nginx/nginx.conf
    nginx -t
    systemctl restart nginx
    echo "✅ Nginx настроен для Cloudflare"
else
    echo "⚠️ Файл nginx-cloudflare.conf не найден"
fi

# 5. Настройка Fail2Ban
echo "🛡️ Настройка Fail2Ban..."
apt-get install -y fail2ban

cat > /etc/fail2ban/jail.local << 'EOF'
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5

[sshd]
enabled = true

[nginx-limit-req]
enabled = true
port = http,https
logpath = /var/log/nginx/error.log
findtime = 600
maxretry = 100
bantime = 3600

[nginx-botsearch]
enabled = true
port = http,https
logpath = /var/log/nginx/access.log
maxretry = 2
EOF

systemctl restart fail2ban

# 6. Создание .env файла с переменными Cloudflare
echo "📋 Создание .env файла..."
if [ ! -f /opt/mask-browser/.env ]; then
    cat >> /opt/mask-browser/.env << 'EOF'

# Cloudflare Tunnel Configuration
CLOUDFLARE_TUNNEL_TOKEN=your_tunnel_token_here
CLOUDFLARE_TUNNEL_ID=your_tunnel_id_here
CLOUDFLARE_DOMAIN=yourdomain.com
EOF
    echo "✅ .env файл создан. Не забудьте добавить ваши токены!"
else
    echo "⚠️ .env файл уже существует"
fi

echo ""
echo "✅ Настройка завершена!"
echo ""
echo "📝 Следующие шаги:"
echo "1. Войдите в Cloudflare Dashboard"
echo "2. Создайте Tunnel и скопируйте токен"
echo "3. Обновите CLOUDFLARE_TUNNEL_TOKEN в /opt/mask-browser/.env"
echo "4. Настройте DNS записи в Cloudflare"
echo "5. Запустите: cd /opt/mask-browser/infra && docker-compose up -d cf_tunnel"
echo ""
echo "📖 Документация: /opt/mask-browser/docs/cloudflare-setup.md"

