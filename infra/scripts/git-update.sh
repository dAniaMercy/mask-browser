#!/bin/bash

# Простой скрипт для обновления кода из git

set -e

PROJECT_DIR="${PROJECT_DIR:-/opt/mask-browser}"
GIT_BRANCH="${GIT_BRANCH:-main}"

cd "$PROJECT_DIR" || exit 1

echo "Обновление кода из git (ветка: $GIT_BRANCH)..."

# Сохраняем текущий коммит
OLD_COMMIT=$(git rev-parse HEAD 2>/dev/null || echo "unknown")
echo "Текущий коммит: $OLD_COMMIT"

# Получаем изменения
git fetch origin "$GIT_BRANCH" || exit 1
git pull origin "$GIT_BRANCH" || exit 1

# Показываем новый коммит
NEW_COMMIT=$(git rev-parse HEAD)
echo "Новый коммит: $NEW_COMMIT"

if [ "$OLD_COMMIT" != "$NEW_COMMIT" ]; then
    echo "Обновлено с $OLD_COMMIT на $NEW_COMMIT"
    echo ""
    echo "Изменения:"
    git log --oneline "$OLD_COMMIT..$NEW_COMMIT" | head -10
else
    echo "Код уже актуален"
fi
