# Проверка доступности MaskAdmin

## Быстрая проверка

```bash
# 1. Проверить, что контейнер запущен
docker ps | grep maskadmin

# 2. Проверить логи (должна быть строка "Listening on:")
docker logs maskbrowser-maskadmin | grep -i "listening\|started\|running"

# 3. Проверить порты контейнера
docker port maskbrowser-maskadmin

# 4. Проверить доступность изнутри контейнера
docker exec maskbrowser-maskadmin bash -c 'timeout 3 bash -c "</dev/tcp/localhost/80" && echo "Port 80 is OPEN" || echo "Port 80 is CLOSED"'

# 5. Проверить доступность с хоста
curl -v http://localhost:5100/health
# или
wget -O- http://localhost:5100/health

# 6. Проверить сеть Docker
docker network inspect infra_maskbrowser-network | grep -A 10 maskadmin
```

## Если порт 5100 не отвечает

### Проверка 1: Контейнер запущен?
```bash
docker ps -a | grep maskadmin
```
Если статус не "Up", проверьте логи:
```bash
docker logs maskbrowser-maskadmin
```

### Проверка 2: Порт проброшен правильно?
```bash
docker port maskbrowser-maskadmin
```
Должно показать: `80/tcp -> 0.0.0.0:5100`

### Проверка 3: Порт 5100 занят другим процессом?
```bash
# Linux
netstat -tuln | grep 5100
# или
ss -tuln | grep 5100
lsof -i :5100

# Windows
netstat -ano | findstr :5100
```

### Проверка 4: Приложение слушает порт 80 внутри контейнера?
```bash
# Проверить процессы внутри контейнера
docker exec maskbrowser-maskadmin ps aux | grep dotnet

# Проверить открытые порты внутри контейнера
docker exec maskbrowser-maskadmin netstat -tuln | grep 80
# или
docker exec maskbrowser-maskadmin ss -tuln | grep 80
```

### Проверка 5: Firewall блокирует порт?
```bash
# Проверить правила firewall
sudo iptables -L -n | grep 5100
# или
sudo ufw status | grep 5100
```

## Решение проблем

### Проблема: Контейнер не запускается
```bash
# Пересобрать и запустить
docker-compose -f infra/docker-compose.yml stop maskadmin
docker-compose -f infra/docker-compose.yml rm -f maskadmin
docker-compose -f infra/docker-compose.yml build --no-cache maskadmin
docker-compose -f infra/docker-compose.yml up -d maskadmin
docker logs -f maskbrowser-maskadmin
```

### Проблема: Порт 5100 занят
```bash
# Найти процесс
lsof -i :5100
# или
fuser 5100/tcp

# Остановить процесс или изменить порт в docker-compose.yml
```

### Проблема: Приложение не слушает порт
Проверьте логи на ошибки запуска:
```bash
docker logs maskbrowser-maskadmin | tail -50
```

Убедитесь, что в логах есть:
- `MaskAdmin starting up...`
- `Listening on: http://[::]:80` или подобное

### Проблема: Ошибки подключения к БД
```bash
# Проверить подключение к PostgreSQL
docker exec maskbrowser-maskadmin ping -c 3 postgres

# Проверить переменные окружения
docker exec maskbrowser-maskadmin env | grep -E "ConnectionStrings|PostgreSQL"
```

## Тестирование доступа

### Из контейнера
```bash
docker exec maskbrowser-maskadmin curl http://localhost:80/health
docker exec maskbrowser-maskadmin curl http://localhost:80/
```

### С хоста
```bash
curl http://localhost:5100/health
curl http://localhost:5100/
curl http://<server-ip>:5100/health
```

### Через браузер
Откройте: `http://localhost:5100` или `http://<server-ip>:5100`

## Проверка health check

```bash
# Проверить статус health check
docker inspect maskbrowser-maskadmin | grep -A 10 Health

# Запустить health check вручную
docker exec maskbrowser-maskadmin bash -c 'timeout 3 bash -c "</dev/tcp/localhost/80" && echo "OK" || echo "FAIL"'
```
