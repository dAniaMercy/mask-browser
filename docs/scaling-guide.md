# Руководство по масштабированию MASK BROWSER

## Горизонтальное масштабирование

### Масштабирование API серверов

1. **Добавление новых API инстансов**

Отредактируйте `infra/nginx.conf`:
```nginx
upstream api_backend {
    least_conn;
    server api:8080;
    server api2:8080;
    server api3:8080;
}
```

Запустите новые контейнеры:
```bash
docker-compose up -d --scale api=3
```

### Масштабирование серверных нод

1. **Регистрация новой ноды через API**

```bash
curl -X POST http://localhost:5050/api/server/register \
  -H "Content-Type: application/json" \
  -d '{
    "name": "node-2",
    "ipAddress": "192.168.1.102",
    "maxContainers": 1000
  }'
```

2. **Автоматическое добавление через Cybernetics API**

Система может автоматически создавать новые серверные ноды через API провайдера Cybernetics:

```csharp
public async Task<ServerNode> CreateNodeAsync()
{
    var cyberneticsApi = new CyberneticsApiClient();
    var server = await cyberneticsApi.CreateServerAsync(new ServerConfig
    {
        Region = "eu-west-1",
        InstanceType = "large",
        Image = "ubuntu-22.04"
    });
    
    // Регистрация ноды в системе
    await _loadBalancerService.RegisterNodeAsync(
        server.Name,
        server.IpAddress,
        1000
    );
    
    return server;
}
```

### Автоматическое масштабирование

Настройте автоматическое масштабирование на основе метрик Prometheus:

1. **Создайте правило в Prometheus:**

```yaml
# prometheus-rules.yml
groups:
  - name: scaling
    rules:
      - alert: HighContainerLoad
        expr: avg(container_count) / max_container_capacity > 0.8
        for: 5m
        annotations:
          summary: "High container load detected"
```

2. **Настройте Alertmanager для создания новых нод:**

```yaml
# alertmanager.yml
route:
  - match:
      alertname: HighContainerLoad
    receiver: 'scaling-service'
    actions:
      - webhook:
          url: 'http://api:8080/api/server/scale-up'
```

## Вертикальное масштабирование

### Увеличение ресурсов контейнера

Отредактируйте `server/Services/DockerService.cs`:

```csharp
HostConfig = new HostConfig
{
    Memory = 1024 * 1024 * 1024, // 1GB вместо 512MB
    NanoCPUs = 1_000_000_000, // 1 CPU вместо 0.5
}
```

### Увеличение лимитов ноды

Обновите конфигурацию в БД:

```sql
UPDATE ServerNodes 
SET MaxContainers = 2000 
WHERE IpAddress = '192.168.1.101';
```

## Балансировка нагрузки

### Алгоритм выбора ноды

**LoadBalancerService** использует следующий алгоритм:

1. Фильтрация здоровых нод (health-check < 30 секунд)
2. Исключение нод с нагрузкой >= MaxContainers
3. Сортировка по:
   - Количество активных контейнеров (по возрастанию)
   - Использование CPU (по возрастанию)
4. Выбор первой ноды из отсортированного списка

### Настройка весов нод

Для разных типов нод можно настроить приоритет:

```csharp
var node = await _context.ServerNodes
    .Where(n => n.IsHealthy)
    .OrderBy(n => n.ActiveContainers / (double)n.MaxContainers) // Load percentage
    .ThenBy(n => n.CpuUsage)
    .FirstOrDefaultAsync();
```

## Мониторинг масштабирования

### Дашборды Grafana

1. **Метрики нод:**
   - CPU Usage по нодам
   - Memory Usage по нодам
   - Active Containers по нодам

2. **Метрики балансировки:**
   - Распределение контейнеров
   - Среднее время ответа
   - Количество запросов на ноду

3. **Алерты:**
   - Превышение лимитов ноды
   - Недостаток ресурсов
   - Недоступность ноды

## Рекомендации

### Производительность

- Максимум 1000 контейнеров на ноду (зависит от ресурсов)
- Рекомендуется 16GB RAM и 8 CPU cores на ноду
- Используйте SSD для Docker volume storage

### Отказоустойчивость

- Минимум 2 ноды для высокой доступности
- Настройте автоматический перезапуск контейнеров
- Регулярное резервное копирование БД (каждые 6 часов)

### Безопасность

- Ограничение ресурсов контейнеров
- Изоляция сетевых зон
- Мониторинг аномальной активности

## Примеры масштабирования

### Сценарий 1: Рост нагрузки

**Текущее состояние:**
- 2 API сервера
- 3 серверные ноды
- 2000 активных контейнеров

**Действия:**
1. Увеличить API серверы до 5
2. Добавить 2 новые ноды
3. Обновить балансировку в Nginx

### Сценарий 2: Пиковая нагрузка

**Текущее состояние:**
- 5000 активных контейнеров
- Загрузка нод ~90%

**Действия:**
1. Включить автоматическое масштабирование
2. Добавить 5 новых нод через API
3. Перераспределить контейнеры (опционально)

### Сценарий 3: Профилактическое масштабирование

**Планируемое событие:**
- Ожидается рост нагрузки в 2 раза

**Действия:**
1. Заблаговременно добавить ноды (за 1 час)
2. Протестировать балансировку
3. Настроить мониторинг для автоматического реагирования

