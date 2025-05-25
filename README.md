# Text Analyzer - Система анализа текстов

Простая система для анализа текстовых файлов. Загружаете файл, получаете статистику и можете сравнивать документы.

## Что умеет система

- Загружать файлы .txt (до 10MB)
- Считать слова, символы, абзацы
- Находить одинаковые файлы
- Сравнивать похожесть документов
- Создавать облака слов

## Быстрый старт

### 1. Установить .NET 8
```bash
# macOS
brew install dotnet@8

# Windows - скачать с сайта Microsoft
# Linux
sudo apt install dotnet-sdk-8.0
```

### 2. Запустить систему
```bash
git clone <ссылка-на-репозиторий>
cd text-analyzer-csharp
./start-services.sh
```

### 3. Открыть в браузере
```
http://localhost:8000
```

## Как пользоваться

### Через веб-интерфейс
1. Откройте http://localhost:8000
2. Перетащите файл .txt в область загрузки
3. Посмотрите статистику
4. Сравните с другими файлами

### Через API
```bash
# Загрузить файл
curl -X POST -F "file=@test.txt" http://localhost:8000/api/files

# Получить список файлов  
curl http://localhost:8000/api/files

# Получить статистику файла (замените ID на реальный)
curl http://localhost:8000/api/files/your-file-id/stats

# Сравнить два файла
curl -X POST -H "Content-Type: application/json" \
  -d '{"file_id":"id1", "other_file_id":"id2"}' \
  http://localhost:8000/api/analysis/compare

# Получить облако слов
curl http://localhost:8000/api/analysis/wordcloud/your-file-id

# Проверить работу системы
curl http://localhost:8000/health

# Прямой доступ к сервисам (если нужно)
curl http://localhost:8001/health  # File Storing Service
curl http://localhost:8002/health  # File Analysis Service
```

## Важно: Архитектура доступа

**Использовать только порт 8000** для работы с системой:
- ✅ **http://localhost:8000** - веб-интерфейс и API
- ❌ **http://localhost:8001** - только внутренний API (File Storage)  
- ❌ **http://localhost:8002** - только внутренний API (File Analysis)

Порты 8001 и 8002 предназначены для внутреннего взаимодействия микросервисов.

## Структура проекта

```
text-analyzer-csharp/
├── api_gateway/              # Веб-интерфейс (порт 8000)
├── file_storing_service/     # Хранение файлов (порт 8001) 
├── file_analysis_service/    # Анализ текста (порт 8002)
└── start-services.sh         # Скрипт запуска
```

## Что внутри

### API Gateway (порт 8000)
- Главная страница с красивым интерфейсом
- Принимает файлы от пользователей
- Передаёт запросы другим сервисам

### File Storing Service (порт 8001)
- Сохраняет файлы на диск
- Проверяет дубликаты по хешу SHA-256
- Ведёт базу метаданных в JSON

### File Analysis Service (порт 8002)
- Считает статистику текста
- Сравнивает файлы по алгоритму Жаккара
- Создаёт облака слов через QuickChart

## Технологии

- .NET 8 - основа
- ASP.NET Core - веб-сервер
- xUnit - тесты
- HTML/CSS/JS - интерфейс

## API методы

### API Gateway (http://localhost:8000)
| Метод | URL | Описание |
|-------|-----|----------|
| GET | `/` | Главная страница с веб-интерфейсом |
| GET | `/health` | Проверка состояния всех сервисов |
| POST | `/api/files` | Загрузить файл |
| GET | `/api/files` | Список всех файлов |
| GET | `/api/files/{id}` | Информация о файле |
| DELETE | `/api/files/{id}` | Удалить файл |
| GET | `/api/files/{id}/content` | Скачать содержимое файла |
| GET | `/api/files/{id}/stats` | Статистика файла |
| POST | `/api/analysis/compare` | Сравнить два файла |
| GET | `/api/analysis/wordcloud/{id}` | Облако слов |
| GET | `/swagger` | API документация |

### File Storing Service (http://localhost:8001)
| Метод | URL | Описание |
|-------|-----|----------|
| GET | `/health` | Проверка работы сервиса |
| POST | `/api/files` | Загрузить файл |
| GET | `/api/files` | Список всех файлов |
| GET | `/api/files/{id}` | Информация о файле |
| DELETE | `/api/files/{id}` | Удалить файл |
| GET | `/api/files/{id}/content` | Скачать содержимое файла |
| GET | `/api/files/{id}/download` | Скачать файл с оригинальным именем |
| GET | `/swagger` | API документация сервиса |

### File Analysis Service (http://localhost:8002)  
| Метод | URL | Описание |
|-------|-----|----------|
| GET | `/health` | Проверка работы сервиса |
| GET | `/api/analysis/{id}/stats` | Статистика текста файла |
| POST | `/api/analysis/compare` | Сравнить два файла |
| GET | `/api/analysis/{id}/wordcloud` | Облако слов для файла |
| GET | `/swagger` | API документация сервиса |

## Требования

- .NET 8 SDK
- 500MB RAM  
- 100MB место на диске
- Windows/macOS/Linux

## Запуск для разработки

```bash
# Терминал 1
cd file_storing_service && dotnet run

# Терминал 2
cd file_analysis_service && dotnet run

# Терминал 3  
cd api_gateway && dotnet run
```

## Тестирование

```bash
# Запустить все тесты
dotnet test

# Только один сервис
dotnet test api_gateway.tests/
```

## Возможные проблемы

### Порты заняты
```bash
# Проверить какие порты используются
lsof -i :8000-8002

# Остановить все dotnet процессы
pkill -f dotnet
```

### Не запускается
```bash
# Проверить версию .NET
dotnet --version

# Пересобрать проект
dotnet build api_gateway/
dotnet build file_storing_service/
dotnet build file_analysis_service/
```

## Примеры использования

### Загрузка файла
```bash
echo "Привет мир" > test.txt
curl -X POST -F "file=@test.txt" http://localhost:8000/api/files
# Ответ: {"fileId":"d878a80a-20b4-41fb-aad7-05ec8c590721","filename":"test.txt","size":199,"duplicate":false}
```

### Получение списка файлов
```bash
curl http://localhost:8000/api/files
# Ответ: {"files":[{"id":"d878a80a-20b4-41fb-aad7-05ec8c590721","filename":"test.txt","size":199,"uploadDate":"2025-05-24T16:11:39.670643Z","duplicate":false}]}
```

### Получение статистики файла
```bash
curl http://localhost:8000/api/files/d878a80a-20b4-41fb-aad7-05ec8c590721/stats
# Ответ: {"FileId":"d878a80a-...","WordCount":25,"CharacterCount":199,"ParagraphCount":2}
```

## Алгоритмы

### Подсчёт статистики
- Слова: разбивка по пробелам
- Символы: общая длина текста  
- Абзацы: разбивка по двойным переносам

### Сравнение файлов
- Коэффициент Жаккара: пересечение / объединение множеств слов
- Диапазон: 0 (разные) до 1 (одинаковые)

### Детекция дубликатов
- SHA-256 хеш от содержимого файла
- Точное сравнение байт в байт

## Swagger API документация

После запуска сервисов API доступно по следующим адресам:

- **API Gateway**: http://localhost:8000/api/*
- **File Storing Service**: http://localhost:8001*  
- **File Analysis Service**: http://localhost:8002*

Для тестирования API рекомендуется использовать curl или Postman.

## Полезные ссылки

- **Веб-интерфейс**: http://localhost:8000
- **Health Check**: http://localhost:8000/health
- **API Files**: http://localhost:8000/api/files
- **Swagger JSON**: http://localhost:8000/swagger/v1/swagger.json
- **File Service Health**: http://localhost:8001/health
- **Analysis Service Health**: http://localhost:8002/health
