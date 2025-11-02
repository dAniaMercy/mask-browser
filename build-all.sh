#!/bin/bash

set -e

echo "🚀 Building MASK BROWSER Projects"
echo "=================================="

# Цвета для вывода
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Функция для вывода ошибок
error() {
    echo -e "${RED}❌ Error: $1${NC}"
    exit 1
}

# Функция для вывода успеха
success() {
    echo -e "${GREEN}✅ $1${NC}"
}

# Функция для вывода предупреждений
warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

# 1. Проверка серверного проекта (ASP.NET Core)
echo ""
echo "1️⃣  Building Server (ASP.NET Core)..."
cd server || error "Server directory not found"

# Проверка наличия .NET SDK
if ! command -v dotnet &> /dev/null; then
    error ".NET SDK not found. Please install .NET 8 SDK"
fi

# Очистка
dotnet clean > /dev/null 2>&1

# Восстановление пакетов
echo "   📦 Restoring packages..."
dotnet restore || error "Failed to restore server packages"

# Сборка
echo "   🔨 Building..."
dotnet build --no-restore || error "Failed to build server"

success "Server built successfully"
cd ..

# 2. Проверка Next.js проекта
echo ""
echo "2️⃣  Building Client (Next.js)..."
cd client-web-nextjs || error "Client-web-nextjs directory not found"

# Проверка наличия Node.js
if ! command -v node &> /dev/null; then
    error "Node.js not found. Please install Node.js 20+"
fi

# Проверка наличия npm
if ! command -v npm &> /dev/null; then
    error "npm not found. Please install npm"
fi

# Установка зависимостей (если нужно)
if [ ! -d "node_modules" ]; then
    echo "   📦 Installing dependencies..."
    npm install || error "Failed to install client dependencies"
fi

# Сборка
echo "   🔨 Building..."
npm run build || error "Failed to build client"

success "Client built successfully"
cd ..

# 3. Проверка Agent (Go)
echo ""
echo "3️⃣  Building Agent (Go)..."
cd agent || error "Agent directory not found"

# Проверка наличия Go
if ! command -v go &> /dev/null; then
    error "Go not found. Please install Go 1.21+"
fi

# Обновление зависимостей
echo "   📦 Tidying dependencies..."
go mod tidy || error "Failed to tidy agent dependencies"

# Сборка
echo "   🔨 Building..."
go build -o agent . || error "Failed to build agent"

success "Agent built successfully"
cd ..

# 4. Проверка Desktop (опционально)
if [ -d "desktop" ]; then
    echo ""
    echo "4️⃣  Checking Desktop (C# WPF)..."
    cd desktop || warning "Desktop directory not found"
    
    if command -v dotnet &> /dev/null; then
        echo "   🔨 Building..."
        dotnet build || warning "Failed to build desktop (optional)"
        success "Desktop built successfully"
    else
        warning "Skipping desktop build (.NET not found)"
    fi
    cd ..
fi

# Итоговый отчет
echo ""
echo "=================================="
echo -e "${GREEN}🎉 All projects built successfully!${NC}"
echo ""
echo "Next steps:"
echo "  1. Set up environment variables in infra/.env"
echo "  2. Run: cd infra && docker-compose up -d"
echo "  3. Access:"
echo "     - API: http://localhost:5050"
echo "     - Web: http://localhost:5052"
echo "     - Grafana: http://localhost:3000"
echo ""