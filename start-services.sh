#!/bin/bash

echo "Останавливаем все процессы dotnet..."
pkill -f "dotnet" || true
sleep 3

echo "Запускаем File Storing Service на порту 8001..."
cd file_storing_service
dotnet run &
FILE_STORING_PID=$!
cd ..
sleep 5

echo "Запускаем File Analysis Service на порту 8002..."
cd file_analysis_service
dotnet run &
FILE_ANALYSIS_PID=$!
cd ..
sleep 5

echo "Запускаем API Gateway на порту 8000..."
cd api_gateway
dotnet run &
API_GATEWAY_PID=$!
cd ..
sleep 5

echo "Проверяем доступность сервисов..."
echo "File Storing Service (8001): $(curl -s -o /dev/null -w "%{http_code}" http://localhost:8001 || echo "Не отвечает")"
echo "File Analysis Service (8002): $(curl -s -o /dev/null -w "%{http_code}" http://localhost:8002 || echo "Не отвечает")"
echo "API Gateway (8000): $(curl -s -o /dev/null -w "%{http_code}" http://localhost:8000 || echo "Не отвечает")"

echo ""
echo "PIDs процессов:"
echo "File Storing Service: $FILE_STORING_PID"
echo "File Analysis Service: $FILE_ANALYSIS_PID"
echo "API Gateway: $API_GATEWAY_PID"

echo ""
echo "Для остановки всех сервисов выполните: pkill -f dotnet" 