# Обновление кода из git

## Простая команда

```bash
cd /opt/mask-browser && git pull origin main
```

## С использованием скрипта

```bash
cd /opt/mask-browser/infra
chmod +x scripts/git-update.sh
bash scripts/git-update.sh
```

## С указанием ветки

```bash
cd /opt/mask-browser
git pull origin develop  # или другая ветка
```

## С сохранением локальных изменений

```bash
cd /opt/mask-browser
git stash
git pull origin main
git stash pop  # вернуть локальные изменения (если нужно)
```

## Полная команда с проверкой

```bash
cd /opt/mask-browser
git fetch origin main
git pull origin main
```
