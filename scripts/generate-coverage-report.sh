#!/bin/bash

echo "Генерация отчётов о покрытии кода для проекта Text-Scanner"
echo "============================================================"

# Проверяем наличие ReportGenerator
if ! command -v reportgenerator &> /dev/null; then
    echo "Установка ReportGenerator..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# Создаем директории для отчётов
mkdir -p reports/CoverageReport
mkdir -p reports/TestResults

echo ""
echo "Шаг 1: Запуск тестов с измерением покрытия"
echo "-------------------------------------------"

# File Storing Service
echo "Тестирование File Storing Service..."
cd file_storing_service.tests
dotnet test --collect:"XPlat Code Coverage" --results-directory ../reports/TestResults/file-storing --logger "console;verbosity=minimal"
cd ..

# File Analysis Service  
echo "Тестирование File Analysis Service..."
cd file_analysis_service.tests
dotnet test --collect:"XPlat Code Coverage" --results-directory ../reports/TestResults/file-analysis --logger "console;verbosity=minimal"
cd ..

# API Gateway
echo "Тестирование API Gateway..."
cd api_gateway.tests
dotnet test --collect:"XPlat Code Coverage" --results-directory ../reports/TestResults/api-gateway --logger "console;verbosity=minimal"
cd ..

echo ""
echo "Шаг 2: Генерация сводного отчёта о покрытии"
echo "--------------------------------------------"

# Поиск всех файлов покрытия
COVERAGE_FILES=$(find reports/TestResults -name "coverage.cobertura.xml" 2>/dev/null | tr '\n' ';')

if [ -z "$COVERAGE_FILES" ]; then
    echo "Файлы покрытия не найдены. Проверьте выполнение тестов."
    exit 1
fi

echo "Найденные файлы покрытия:"
echo "$COVERAGE_FILES" | tr ';' '\n'

# Генерация HTML отчёта
reportgenerator \
    -reports:"$COVERAGE_FILES" \
    -targetdir:"reports/CoverageReport" \
    -reporttypes:"Html;TextSummary;Cobertura;JsonSummary" \
    -title:"Text-Scanner Coverage Report" \
    -tag:"$(date +%Y%m%d-%H%M%S)"

echo ""
echo "Шаг 3: Создание индивидуальных отчётов по сервисам"
echo "---------------------------------------------------"

# Отчёт по File Storing Service
if ls reports/TestResults/file-storing/*/coverage.cobertura.xml 1> /dev/null 2>&1; then
    reportgenerator \
        -reports:"reports/TestResults/file-storing/*/coverage.cobertura.xml" \
        -targetdir:"reports/CoverageReport/file-storing" \
        -reporttypes:"Html;TextSummary" \
        -title:"File Storing Service Coverage"
    echo "Отчёт File Storing Service: reports/CoverageReport/file-storing/index.html"
fi

# Отчёт по File Analysis Service
if ls reports/TestResults/file-analysis/*/coverage.cobertura.xml 1> /dev/null 2>&1; then
    reportgenerator \
        -reports:"reports/TestResults/file-analysis/*/coverage.cobertura.xml" \
        -targetdir:"reports/CoverageReport/file-analysis" \
        -reporttypes:"Html;TextSummary" \
        -title:"File Analysis Service Coverage"
    echo "Отчёт File Analysis Service: reports/CoverageReport/file-analysis/index.html"
fi

# Отчёт по API Gateway
if ls reports/TestResults/api-gateway/*/coverage.cobertura.xml 1> /dev/null 2>&1; then
    reportgenerator \
        -reports:"reports/TestResults/api-gateway/*/coverage.cobertura.xml" \
        -targetdir:"reports/CoverageReport/api-gateway" \
        -reporttypes:"Html;TextSummary" \
        -title:"API Gateway Coverage"
    echo "Отчёт API Gateway: reports/CoverageReport/api-gateway/index.html"
fi

echo ""
echo "Шаг 4: Вывод сводной информации"
echo "--------------------------------"

# Выводим текстовую сводку
if [ -f "reports/CoverageReport/Summary.txt" ]; then
    echo "Сводная статистика покрытия:"
    cat reports/CoverageReport/Summary.txt
fi

echo ""
echo "Результаты:"
echo "  Сводный отчёт: reports/CoverageReport/index.html"
echo "  Индивидуальные отчёты:"
echo "    - File Storing: reports/CoverageReport/file-storing/index.html"
echo "    - File Analysis: reports/CoverageReport/file-analysis/index.html"  
echo "    - API Gateway: reports/CoverageReport/api-gateway/index.html"

echo ""
echo "Для просмотра отчётов выполните:"
echo "  open reports/CoverageReport/index.html"
echo ""
echo "Генерация отчётов завершена." 