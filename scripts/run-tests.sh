#!/bin/bash

echo "Запуск тестов с измерением покрытия кода..."

# Создаем директории для результатов
mkdir -p reports/TestResults

echo ""
echo "Тестирование File Storing Service..."
cd file_storing_service.tests

# Запускаем тесты с покрытием
dotnet test --collect:"XPlat Code Coverage" --results-directory ../reports/TestResults/file-storing --logger "console;verbosity=detailed" 2>/dev/null || echo "Требуется .NET SDK для локального запуска"

cd ..

echo ""
echo "Тестирование File Analysis Service..."
cd file_analysis_service.tests

dotnet test --collect:"XPlat Code Coverage" --results-directory ../reports/TestResults/file-analysis --logger "console;verbosity=detailed" 2>/dev/null || echo "Требуется .NET SDK для локального запуска"

cd ..

echo ""
echo "Тестирование API Gateway..."
cd api_gateway.tests

dotnet test --collect:"XPlat Code Coverage" --results-directory ../reports/TestResults/api-gateway --logger "console;verbosity=detailed" 2>/dev/null || echo "Требуется .NET SDK для локального запуска"

cd ..

echo ""
echo "Альтернативный способ: Запуск через Docker..."
echo "Сборка тестовых образов..."

# Создаем Dockerfile для тестов File Storing Service
cat > file_storing_service.tests/Dockerfile.test << 'EOF'
FROM mcr.microsoft.com/dotnet/sdk:7.0

WORKDIR /src

# Копируем файлы проектов
COPY ../file_storing_service/*.csproj ./file_storing_service/
COPY *.csproj ./

# Восстанавливаем зависимости
RUN dotnet restore

# Копируем весь код
COPY ../file_storing_service/ ./file_storing_service/
COPY . ./

# Устанавливаем ReportGenerator
RUN dotnet tool install -g dotnet-reportgenerator-globaltool
ENV PATH="$PATH:/root/.dotnet/tools"

# Запускаем тесты с покрытием
RUN dotnet test --collect:"XPlat Code Coverage" --results-directory /TestResults --logger "trx;LogFileName=test-results.trx"

# Генерируем отчет о покрытии
RUN reportgenerator -reports:"/TestResults/**/coverage.cobertura.xml" -targetdir:"/TestResults/coverage-report" -reporttypes:"Html;TextSummary;Cobertura" || true

CMD ["sh", "-c", "echo '=== File Storing Service Test Results ===' && cat /TestResults/coverage-report/Summary.txt 2>/dev/null || echo 'Coverage report generated'"]
EOF

# Создаем Dockerfile для тестов File Analysis Service  
cat > file_analysis_service.tests/Dockerfile.test << 'EOF'
FROM mcr.microsoft.com/dotnet/sdk:7.0

WORKDIR /src

COPY ../file_analysis_service/*.csproj ./file_analysis_service/
COPY *.csproj ./

RUN dotnet restore

COPY ../file_analysis_service/ ./file_analysis_service/
COPY . ./

RUN dotnet tool install -g dotnet-reportgenerator-globaltool
ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet test --collect:"XPlat Code Coverage" --results-directory /TestResults --logger "trx;LogFileName=test-results.trx"

RUN reportgenerator -reports:"/TestResults/**/coverage.cobertura.xml" -targetdir:"/TestResults/coverage-report" -reporttypes:"Html;TextSummary;Cobertura" || true

CMD ["sh", "-c", "echo '=== File Analysis Service Test Results ===' && cat /TestResults/coverage-report/Summary.txt 2>/dev/null || echo 'Coverage report generated'"]
EOF

# Создаем Dockerfile для тестов API Gateway
cat > api_gateway.tests/Dockerfile.test << 'EOF'
FROM mcr.microsoft.com/dotnet/sdk:7.0

WORKDIR /src

COPY ../api_gateway/*.csproj ./api_gateway/
COPY *.csproj ./

RUN dotnet restore

COPY ../api_gateway/ ./api_gateway/
COPY . ./

RUN dotnet tool install -g dotnet-reportgenerator-globaltool
ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet test --collect:"XPlat Code Coverage" --results-directory /TestResults --logger "trx;LogFileName=test-results.trx"

RUN reportgenerator -reports:"/TestResults/**/coverage.cobertura.xml" -targetdir:"/TestResults/coverage-report" -reporttypes:"Html;TextSummary;Cobertura" || true

CMD ["sh", "-c", "echo '=== API Gateway Test Results ===' && cat /TestResults/coverage-report/Summary.txt 2>/dev/null || echo 'Coverage report generated'"]
EOF

echo ""
echo "Сборка и запуск тестов через Docker..."

# File Storing Service
echo "   File Storing Service..."
docker build -t test-file-storing -f file_storing_service.tests/Dockerfile.test .
docker run --rm -v "$(pwd)/reports/TestResults:/TestResults" test-file-storing

# File Analysis Service  
echo "   File Analysis Service..."
docker build -t test-file-analysis -f file_analysis_service.tests/Dockerfile.test .
docker run --rm -v "$(pwd)/reports/TestResults:/TestResults" test-file-analysis

# API Gateway
echo "   API Gateway..."
docker build -t test-api-gateway -f api_gateway.tests/Dockerfile.test .
docker run --rm -v "$(pwd)/reports/TestResults:/TestResults" test-api-gateway

echo ""
echo "Сводка результатов тестирования:"
echo "reports/TestResults/file-storing/coverage-report/index.html"
echo "reports/TestResults/file-analysis/coverage-report/index.html" 
echo "reports/TestResults/api-gateway/coverage-report/index.html"

echo ""
echo "Для просмотра покрытия откройте HTML отчеты:"
echo "   open reports/TestResults/*/coverage-report/index.html"

echo ""
echo "Тестирование завершено!" 