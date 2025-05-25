#!/bin/bash

echo "Генерация HTML отчётов из существующих файлов покрытия"
echo "======================================================"

# Проверяем наличие ReportGenerator
if ! command -v reportgenerator &> /dev/null; then
    echo "Ошибка: ReportGenerator не установлен."
    echo "Установите его командой: dotnet tool install -g dotnet-reportgenerator-globaltool"
    exit 1
fi

# Создаем директории для отчётов
mkdir -p reports/CoverageReport

# Поиск файлов покрытия
COVERAGE_FILES=$(find reports/TestResults -name "coverage.cobertura.xml" 2>/dev/null)

if [ -z "$COVERAGE_FILES" ]; then
    echo "Файлы покрытия не найдены в директории reports/TestResults/"
    echo "Сначала запустите тесты с покрытием:"
    echo "  dotnet test --collect:\"XPlat Code Coverage\""
    exit 1
fi

echo "Найденные файлы покрытия:"
echo "$COVERAGE_FILES"

# Генерация сводного HTML отчёта
echo ""
echo "Генерация сводного отчёта..."

reportgenerator \
    -reports:"$(echo "$COVERAGE_FILES" | tr '\n' ';')" \
    -targetdir:"reports/CoverageReport" \
    -reporttypes:"Html;TextSummary;Cobertura;JsonSummary" \
    -title:"Text-Scanner Code Coverage Report" \
    -tag:"$(date +%Y-%m-%d_%H-%M-%S)"

# Генерация индивидуальных отчётов
echo ""
echo "Генерация индивидуальных отчётов по сервисам..."

# File Storing Service
FILE_STORING_COVERAGE=$(find reports/TestResults/file-storing -name "coverage.cobertura.xml" 2>/dev/null | head -1)
if [ -n "$FILE_STORING_COVERAGE" ]; then
    reportgenerator \
        -reports:"$FILE_STORING_COVERAGE" \
        -targetdir:"reports/CoverageReport/file-storing" \
        -reporttypes:"Html;TextSummary" \
        -title:"File Storing Service Coverage"
    echo "  File Storing Service: reports/CoverageReport/file-storing/index.html"
fi

# File Analysis Service
FILE_ANALYSIS_COVERAGE=$(find reports/TestResults/file-analysis -name "coverage.cobertura.xml" 2>/dev/null | head -1)
if [ -n "$FILE_ANALYSIS_COVERAGE" ]; then
    reportgenerator \
        -reports:"$FILE_ANALYSIS_COVERAGE" \
        -targetdir:"reports/CoverageReport/file-analysis" \
        -reporttypes:"Html;TextSummary" \
        -title:"File Analysis Service Coverage"
    echo "  File Analysis Service: reports/CoverageReport/file-analysis/index.html"
fi

# API Gateway
API_GATEWAY_COVERAGE=$(find reports/TestResults/api-gateway -name "coverage.cobertura.xml" 2>/dev/null | head -1)
if [ -n "$API_GATEWAY_COVERAGE" ]; then
    reportgenerator \
        -reports:"$API_GATEWAY_COVERAGE" \
        -targetdir:"reports/CoverageReport/api-gateway" \
        -reporttypes:"Html;TextSummary" \
        -title:"API Gateway Coverage"
    echo "  API Gateway: reports/CoverageReport/api-gateway/index.html"
fi

# Вывод сводной информации
echo ""
echo "Сводная статистика покрытия:"
if [ -f "reports/CoverageReport/Summary.txt" ]; then
    cat reports/CoverageReport/Summary.txt
else
    echo "  (сводка будет доступна в HTML отчёте)"
fi

echo ""
echo "Отчёты созданы:"
echo "  Основной отчёт: reports/CoverageReport/index.html"
echo ""
echo "Для просмотра выполните:"
echo "  open reports/CoverageReport/index.html" 