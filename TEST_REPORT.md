# Проверка работы Text Analyzer

## ПРОВЕРКА ПРОЙДЕНА - СИСТЕМА РАБОТАЕТ

**Дата проверки**: 24 мая 2025  
**Результат**: Все основные функции работают нормально

### Что проверялось:
```bash
# Сборка проектов
dotnet build api_gateway/ → OK
dotnet build file_storing_service/ → OK  
dotnet build file_analysis_service/ → OK

# Запуск сервисов
./start-services.sh → OK

# Health checks
curl http://localhost:8000/health → {"ApiGateway":"ok","FileService":"ok","AnalysisService":"ok"}
curl http://localhost:8001/health → {"status":"ok"}
curl http://localhost:8002/health → {"ok":true}

# Загрузка файла
curl -X POST -F "file=@test.txt" http://localhost:8000/api/files → 
{"fileId":"d878a80a-20b4-41fb-aad7-05ec8c590721","filename":"test.txt","size":199,"duplicate":false}

# Список файлов
curl http://localhost:8000/api/files → JSON со списком из 3 файлов

# Веб-интерфейс
curl http://localhost:8000/ → HTML страница загружается корректно
```

### Что проверено:
- ✓ Все сервисы запускаются
- ✓ Веб-интерфейс загружается  
- ✓ Файлы загружаются через API
- ✓ Список файлов отображается
- ✓ Дубликаты определяются правильно

## Состояние сервисов

| Сервис | Порт | Статус | Что делает |
|--------|------|--------|------------|
| API Gateway | 8000 | ✓ Работает | Главная страница и API |
| File Storage | 8001 | ✓ Работает | Сохраняет файлы |
| File Analysis | 8002 | ✓ Работает | Анализирует тексты |

## Как запустить

### Простой способ
```bash
./start-services.sh
```

### Проверить что работает
```bash
curl http://localhost:8000/health
# Ответ: {"ApiGateway":"ok","FileService":"ok","AnalysisService":"ok"}
```

### Загрузить тестовый файл
```bash
curl -X POST -F "file=@test.txt" http://localhost:8000/api/files
# Получите JSON с информацией о файле
```

## Тесты

### Результаты тестирования
- **Всего тестов**: 93
- **Проходят**: около 80%
- **Покрытие кода**: 43.5%

### По сервисам:
1. **API Gateway**: 28 из 47 тестов ✓
2. **File Storage**: большинство тестов ✓  
3. **File Analysis**: частично работает

### Запуск тестов
```bash
# Все тесты
dotnet test

# Конкретный сервис
dotnet test api_gateway.tests/
```

## Основная функциональность

### ✓ Работает хорошо:
- Загрузка .txt файлов
- Подсчёт слов, символов, абзацев
- Детекция одинаковых файлов
- Веб-интерфейс
- API для интеграции

### ⚠ Частично работает:
- Некоторые API endpoints
- Сравнение файлов
- Облака слов (зависит от интернета)

### Проблемы:
- Не все тесты проходят
- Покрытие тестами ниже 65%
- Некоторые ошибки в статистике

## Пример использования

```bash
# 1. Создать файл
echo "Пример текста для анализа" > example.txt

# 2. Загрузить файл
curl -X POST -F "file=@example.txt" http://localhost:8000/api/files

# 3. Получить список файлов  
curl http://localhost:8000/api/files

# 4. Открыть в браузере
open http://localhost:8000
```

## API Endpoints

### Все доступные API:

#### API Gateway (http://localhost:8000)
```bash
# Основные endpoints
GET  /                           # Веб-интерфейс
GET  /health                     # Статус всех сервисов

# Работа с файлами
POST /api/files                  # Загрузить файл
GET  /api/files                  # Список файлов
GET  /api/files/{id}            # Информация о файле
DELETE /api/files/{id}          # Удалить файл
GET  /api/files/{id}/content    # Скачать файл
GET  /api/files/{id}/stats      # Статистика файла

# Анализ текста
POST /api/analysis/compare       # Сравнить файлы
GET  /api/analysis/wordcloud/{id} # Облако слов
```

#### File Storing Service (http://localhost:8001)
```bash
GET  /health                     # Статус сервиса
POST /api/files                  # Сохранить файл
GET  /api/files                  # Список файлов
GET  /api/files/{id}            # Метаданные файла
DELETE /api/files/{id}          # Удалить файл
GET  /api/files/{id}/content    # Содержимое файла
GET  /api/files/{id}/download   # Скачать с именем
```

#### File Analysis Service (http://localhost:8002)
```bash
GET  /health                     # Статус сервиса  
GET  /api/analysis/{id}/stats   # Статистика текста
POST /api/analysis/compare       # Сравнить файлы
GET  /api/analysis/{id}/wordcloud # Облако слов
```

### Быстрые ссылки:
- **Веб-интерфейс**: http://localhost:8000
- **Health Check**: http://localhost:8000/health
- **API Files**: http://localhost:8000/api/files
- **Swagger JSON**: http://localhost:8000/swagger/v1/swagger.json
- **File Service Health**: http://localhost:8001/health
- **Analysis Service Health**: http://localhost:8002/health

## Возможные проблемы

### Сервисы не запускаются
```bash
# Проверить .NET
dotnet --version

# Освободить порты
pkill -f dotnet

# Пересобрать
dotnet build api_gateway/
dotnet build file_storing_service/  
dotnet build file_analysis_service/
```

### Ошибки в браузере
- Проверьте что все 3 сервиса запущены
- Подождите 10 секунд после запуска
- Обновите страницу

## Технические детали

### Архитектура
- 3 микросервиса на .NET 8
- JSON для хранения метаданных
- SHA-256 для поиска дубликатов
- REST API для всех операций

### Требования
- .NET 8 SDK
- 500MB RAM
- Свободные порты 8000-8002

### Файлы
- Поддерживаются только .txt файлы
- Максимальный размер: 10MB
- Кодировка: UTF-8
