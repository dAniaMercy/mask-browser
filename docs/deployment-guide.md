# Руководство по развёртыванию MASK BROWSER

## Быстрый старт

### Подготовка сервера

1. **Подключение к серверу:**
```bash
ssh root@109.172.101.73
```

2. **Установка зависимостей:**
```bash
apt update && apt upgrade -y
apt install -y git curl ufw docker.io docker-compose nginx net-tools fail2ban
```

3. **Настройка портов:**
```bash
ufw allow 22
ufw allow 80
ufw allow 443
ufw allow 5050
ufw allow 5052
ufw allow 9090
ufw allow 3000
ufw allow 9092  # Kafka
ufw allow 5672  # RabbitMQ
ufw enable
```

### Развёртывание проекта

1. **Клонирование:**
```bash
cd /opt
git clone https://github.com/<your_repo>/mask-browser.git
cd mask-browser
```

2. **Настройка окружения:**
```bash
touch .env
cat > .env << 'EOF'
POSTGRES_USER=maskadmin
POSTGRES_PASSWORD=SuperSecurePass!
POSTGRES_DB=maskbrowser
REDIS_PASSWORD=MaskRedis123
JWT_SECRET=superlongsecretjwtstring
SERVER_IP=109.172.101.73
KAFKA_BROKER=109.172.101.73:9092
RABBITMQ_HOST=109.172.101.73
RABBITMQ_USER=maskqueue
RABBITMQ_PASS=MaskQueue123
CLOUDFLARE_TUNNEL_TOKEN=your_tunnel_token_here
CLOUDFLARE_TUNNEL_ID=your_tunnel_id
CLOUDFLARE_DOMAIN=yourdomain.com
EOF
```

3. **Сборка и запуск:**
```bash
cd infra
docker-compose up -d --build
docker ps  # Проверка всех контейнеров
```

4. **Настройка базы данных:**
```bash
cd ../server
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet ef migrations add AddTwoFactorAuthentication
dotnet ef database update
```

5. **Настройка Kafka топиков:**
```bash
docker exec -it maskbrowser-kafka bash
kafka-topics.sh --create --topic profile-events --bootstrap-server localhost:9092
kafka-topics.sh --create --topic container-logs --bootstrap-server localhost:9092
exit
```

6. **Настройка RabbitMQ:**
```bash
docker exec -it maskbrowser-rabbitmq bash
rabbitmqctl add_user maskqueue MaskQueue123
rabbitmqctl set_permissions -p / maskqueue ".*" ".*" ".*"
exit
```

7. **Настройка Cloudflare:**
```bash
cd /opt/mask-browser/infra
chmod +x setup-cloudflare.sh
./setup-cloudflare.sh
```

Следуйте инструкциям в [docs/cloudflare-setup.md](cloudflare-setup.md)

## Проверка работы

### Проверка API

```bash
curl http://109.172.101.73/api/health
# Ожидаемый ответ: {"status":"healthy","timestamp":"..."}
```

### Проверка веб-интерфейса

Откройте в браузере:
- `http://109.172.101.73` — главная страница
- `http://109.172.101.73:3000` — Grafana (admin/admin)
- `http://109.172.101.73:9090` — Prometheus

### Проверка через Cloudflare

После настройки Cloudflare:
- `https://yourdomain.com` — главная страница
- `https://api.yourdomain.com/health` — API health check

## Обслуживание

### Обновление проекта

```bash
cd /opt/mask-browser
git pull
cd infra
# Если были изменения в docker-compose.yml или .env.example
cp .env.example .env  # Создайте новый .env если его нет
docker-compose up -d --build
cd ../server
dotnet ef database update
```

### Запуск опциональных сервисов

**Cloudflare Tunnel** (если настроен):
```bash
cd /opt/mask-browser/infra
# Убедитесь, что CLOUDFLARE_TUNNEL_TOKEN установлен в .env
docker-compose --profile cloudflare up -d cf_tunnel
```

**Nginx Load Balancer** (если нужен, вместо существующего):
```bash
cd /opt/mask-browser/infra
docker-compose --profile nginx up -d nginx
```

⚠️ **ВАЖНО**: Nginx использует порты **8080** и **8443** вместо 80 и 443, чтобы избежать конфликтов!

### Просмотр логов

```bash
# Все логи
docker-compose -f infra/docker-compose.yml logs -f

# Конкретный сервис
docker logs maskbrowser-api -f
docker logs maskbrowser-web -f
docker logs maskbrowser-agent -f
```

### Перезапуск сервисов

```bash
cd /opt/mask-browser/infra
docker-compose restart api
docker-compose restart web
docker-compose restart agent
```

### Бэкап базы данных

```bash
docker exec maskbrowser-postgres pg_dump -U maskuser maskbrowser > backup_$(date +%Y%m%d).sql
```

### Восстановление базы данных

```bash
docker exec -i maskbrowser-postgres psql -U maskuser maskbrowser < backup_20240101.sql
```

## Масштабирование

### Добавление новой ноды

1. На новом сервере установите Docker
2. Зарегистрируйте через API:

```bash
curl -X POST http://109.172.101.73/api/servers/register \
  -H "Content-Type: application/json" \
  -d '{
    "ip": "NEW_SERVER_IP",
    "capacity": 1000,
    "role": "node"
  }'
```

3. LoadBalancerService автоматически начнёт использовать новую ноду

### Горизонтальное масштабирование API

```bash
cd /opt/mask-browser/infra
docker-compose up -d --scale api=3
```

## Мониторинг

### Grafana

- URL: `http://109.172.101.73:3000`
- Логин: `admin`
- Пароль: `admin`

### Prometheus

- URL: `http://109.172.101.73:9090`
- Метрики доступны без аутентификации

### Логи Loki

Логи доступны через Grafana:
1. Откройте Grafana
2. Добавьте Loki как datasource (уже настроен)
3. Создайте dashboard для просмотра логов

## Troubleshooting

### API не отвечает

1. Проверьте логи: `docker logs maskbrowser-api`
2. Проверьте подключение к БД: `docker exec maskbrowser-postgres pg_isready`
3. Проверьте Redis: `docker exec maskbrowser-redis redis-cli ping`

### Контейнеры не создаются

1. Проверьте agent: `docker logs maskbrowser-agent`
2. Проверьте RabbitMQ: `docker exec maskbrowser-rabbitmq rabbitmqctl list_queues`
3. Проверьте Docker socket: `ls -la /var/run/docker.sock`

### Высокая нагрузка

1. Проверьте метрики в Prometheus
2. Добавьте новые ноды через API
3. Проверьте балансировку в LoadBalancerService

## Безопасность

1. Регулярно обновляйте зависимости
2. Меняйте пароли в `.env` файле
3. Создавайте бэкапы RSA ключей
4. Мониторьте логи на подозрительную активность
5. Обновляйте Cloudflare WAF правила

