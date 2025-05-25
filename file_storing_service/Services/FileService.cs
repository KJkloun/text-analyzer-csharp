using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FileStoringService.Models;

namespace FileStoringService.Services
{
    /// <summary>
    /// Интерфейс сервиса для работы с файлами
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Загружает файл в систему хранения
        /// </summary>
        /// <param name="file">Загружаемый файл</param>
        /// <returns>Информация о загруженном файле</returns>
        Task<FileUploadResponse> UploadFileAsync(IFormFile file);
        
        /// <summary>
        /// Получает список всех файлов
        /// </summary>
        /// <returns>Список файлов</returns>
        Task<FileListResponse> GetFilesAsync();
        
        /// <summary>
        /// Получает метаданные файла по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Метаданные файла</returns>
        Task<FileMetadata> GetFileMetadataAsync(string fileId);
        
        /// <summary>
        /// Получает содержимое файла по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Поток содержимого файла, MIME-тип и имя файла</returns>
        Task<(Stream FileStream, string ContentType, string FileName)> GetFileAsync(string fileId);
        
        /// <summary>
        /// Удаляет файл по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Результат удаления</returns>
        Task<bool> DeleteFileAsync(string fileId);
        
        /// <summary>
        /// Получает список всех файлов (альтернативный метод для тестов)
        /// </summary>
        /// <returns>Список файлов</returns>
        Task<List<FileMetadata>> GetAllFilesAsync();
        
        /// <summary>
        /// Проверяет существование файла по идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>True если файл существует, False если нет</returns>
        Task<bool> FileExistsAsync(string fileId);
        
        /// <summary>
        /// Получает содержимое файла как строку
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Содержимое файла</returns>
        Task<string> GetFileContentAsync(string fileId);
    }

    /// <summary>
    /// Реализация сервиса для работы с файлами
    /// </summary>
    public class FileService : IFileService
    {
        private readonly string _uploadDir;
        private readonly string _metadataFile;
        private readonly Dictionary<string, FileMetadata> _fileMetadata = new Dictionary<string, FileMetadata>();
        private readonly ILogger<FileService> _logger;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Инициализирует новый экземпляр сервиса для работы с файлами
        /// </summary>
        /// <param name="configuration">Конфигурация приложения</param>
        /// <param name="logger">Логгер</param>
        public FileService(IConfiguration configuration, ILogger<FileService> logger)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _uploadDir = configuration["UploadDir"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            _metadataFile = Path.Combine(_uploadDir, "metadata.json");
            
            _logger.LogInformation("Инициализация FileService. Директория загрузки: {UploadDir}", _uploadDir);
            
            // Создаем директорию, если она не существует
            if (!Directory.Exists(_uploadDir))
            {
                _logger.LogInformation("Создание директории для загрузки файлов: {UploadDir}", _uploadDir);
                Directory.CreateDirectory(_uploadDir);
            }
            
            // Загружаем метаданные при запуске
            LoadMetadata();
        }

        /// <summary>
        /// Загружает метаданные файлов из JSON-файла
        /// </summary>
        private void LoadMetadata()
        {
            if (File.Exists(_metadataFile))
            {
                try
                {
                    _logger.LogInformation("Загрузка метаданных из файла: {MetadataFile}", _metadataFile);
                    var json = File.ReadAllText(_metadataFile);
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, FileMetadata>>(json);
                    if (metadata != null)
                    {
                        lock (_lockObject)
                        {
                            _fileMetadata.Clear();
                            foreach (var item in metadata)
                            {
                                _fileMetadata[item.Key] = item.Value;
                            }
                        }
                        _logger.LogInformation("Метаданные успешно загружены. Количество файлов: {FileCount}", _fileMetadata.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка загрузки метаданных из файла: {MetadataFile}", _metadataFile);
                }
            }
            else
            {
                _logger.LogInformation("Файл метаданных не найден: {MetadataFile}. Будет создан новый файл.", _metadataFile);
            }
        }

        /// <summary>
        /// Сохраняет метаданные файлов в JSON-файл
        /// </summary>
        private void SaveMetadata()
        {
            try
            {
                _logger.LogInformation("Сохранение метаданных в файл: {MetadataFile}", _metadataFile);
                Dictionary<string, FileMetadata> metadataCopy;
                lock (_lockObject)
                {
                    metadataCopy = new Dictionary<string, FileMetadata>(_fileMetadata);
                }
                
                var json = JsonSerializer.Serialize(metadataCopy, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataFile, json);
                _logger.LogInformation("Метаданные успешно сохранены");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сохранения метаданных в файл: {MetadataFile}", _metadataFile);
                throw new InvalidOperationException($"Не удалось сохранить метаданные: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Загружает файл в систему хранения
        /// </summary>
        /// <param name="file">Загружаемый файл</param>
        /// <returns>Информация о загруженном файле</returns>
        public async Task<FileUploadResponse> UploadFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Попытка загрузки пустого файла");
                throw new ArgumentException("Файл пуст");
            }

            // Проверяем, что файл имеет расширение .txt
            if (!Path.GetExtension(file.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Попытка загрузки файла с неподдерживаемым расширением: {Extension}", 
                    Path.GetExtension(file.FileName));
                throw new ArgumentException("Разрешены только файлы .txt");
            }

            // Проверяем размер файла (например, не более 10 МБ)
            if (file.Length > 10 * 1024 * 1024)
            {
                _logger.LogWarning("Попытка загрузки слишком большого файла: {FileSize} байт", file.Length);
                throw new ArgumentException("Размер файла не должен превышать 10 МБ");
            }

            // Генерируем уникальный ID для файла
            var fileId = Guid.NewGuid().ToString();
            var filePath = Path.Combine(_uploadDir, $"{fileId}.txt");

            _logger.LogInformation("Сохранение файла: {FileName} -> {FilePath}", file.FileName, filePath);

            // Читаем содержимое файла для анализа и сохраняем файл
            string content;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                
                // Читаем содержимое для анализа
                using (var reader = new StreamReader(memoryStream, leaveOpen: true))
                {
                    content = await reader.ReadToEndAsync();
                }
                
                // Сохраняем файл на диск
                memoryStream.Position = 0;
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await memoryStream.CopyToAsync(fileStream);
                }
            }

            // Вычисляем хэш файла для проверки на плагиат
            var fileHash = CalculateFileHash(filePath);
            _logger.LogInformation("Вычислен хэш файла: {FileHash}", fileHash);

            // Проверяем, существует ли файл с таким же хэшем
            var duplicateId = FindDuplicateByHash(fileHash);
            if (!string.IsNullOrEmpty(duplicateId))
            {
                _logger.LogInformation("Обнаружен дубликат файла: {DuplicateId}", duplicateId);
            }

            // Сохраняем метаданные файла
            var metadata = new FileMetadata
            {
                Id = fileId,
                FileName = $"{fileId}.txt",
                OriginalName = file.FileName,
                ContentType = file.ContentType,
                Size = file.Length,
                UploadDate = DateTime.UtcNow,
                Hash = fileHash,
                DuplicateOf = duplicateId
            };

            lock (_lockObject)
            {
                _fileMetadata[fileId] = metadata;
            }
            SaveMetadata();

            _logger.LogInformation("Файл успешно загружен: {FileId}, дубликат: {IsDuplicate}", 
                fileId, !string.IsNullOrEmpty(duplicateId));

            // Вычисляем статистику файла
            var stats = CalculateFileStatistics(content);

            return new FileUploadResponse
            {
                FileId = fileId,
                Filename = file.FileName,
                Size = file.Length,
                Duplicate = !string.IsNullOrEmpty(duplicateId),
                DuplicateOf = duplicateId,
                Stats = stats
            };
        }

        /// <summary>
        /// Получает список всех файлов
        /// </summary>
        /// <returns>Список файлов</returns>
        public Task<FileListResponse> GetFilesAsync()
        {
            _logger.LogInformation("Получение списка файлов");
            
            var response = new FileListResponse();
            
            Dictionary<string, FileMetadata> metadataCopy;
            lock (_lockObject)
            {
                metadataCopy = new Dictionary<string, FileMetadata>(_fileMetadata);
            }
            
            foreach (var metadata in metadataCopy.Values)
            {
                response.Files.Add(new FileStoringService.Models.FileInfo
                {
                    Id = metadata.Id,
                    Filename = metadata.OriginalName,
                    Size = metadata.Size,
                    UploadDate = metadata.UploadDate,
                    Duplicate = !string.IsNullOrEmpty(metadata.DuplicateOf)
                });
            }
            
            _logger.LogInformation("Список файлов получен. Количество: {FileCount}", response.Files.Count);
            
            return Task.FromResult(response);
        }

        /// <summary>
        /// Получает метаданные файла по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Метаданные файла</returns>
        public Task<FileMetadata> GetFileMetadataAsync(string fileId)
        {
            _logger.LogInformation("Получение метаданных файла: {FileId}", fileId);
            
            FileMetadata metadata;
            lock (_lockObject)
            {
                if (!_fileMetadata.TryGetValue(fileId, out metadata))
                {
                    _logger.LogWarning("Файл не найден: {FileId}", fileId);
                    throw new KeyNotFoundException($"Файл не найден: {fileId}");
                }
            }
            
            _logger.LogInformation("Метаданные файла получены: {FileId}", fileId);
            
            return Task.FromResult(metadata);
        }

        /// <summary>
        /// Получает содержимое файла по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Поток содержимого файла, MIME-тип и имя файла</returns>
        public Task<(Stream FileStream, string ContentType, string FileName)> GetFileAsync(string fileId)
        {
            _logger.LogInformation("Получение файла: {FileId}", fileId);
            
            FileMetadata metadata;
            lock (_lockObject)
            {
                if (!_fileMetadata.TryGetValue(fileId, out metadata))
                {
                    _logger.LogWarning("Файл не найден: {FileId}", fileId);
                    throw new KeyNotFoundException($"Файл не найден: {fileId}");
                }
            }
            
            var filePath = Path.Combine(_uploadDir, metadata.FileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Файл не найден на диске: {FilePath}", filePath);
                throw new FileNotFoundException($"Файл не найден на диске: {filePath}");
            }
            
            _logger.LogInformation("Файл найден, открытие потока: {FilePath}", filePath);
            
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return Task.FromResult(((Stream)stream, metadata.ContentType, metadata.OriginalName));
        }

        /// <summary>
        /// Удаляет файл по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Результат удаления</returns>
        public Task<bool> DeleteFileAsync(string fileId)
        {
            _logger.LogInformation("Удаление файла: {FileId}", fileId);
            
            FileMetadata metadata;
            lock (_lockObject)
            {
                if (!_fileMetadata.TryGetValue(fileId, out metadata))
                {
                    _logger.LogWarning("Файл не найден: {FileId}", fileId);
                    throw new KeyNotFoundException($"Файл не найден: {fileId}");
                }
            }
            
            var filePath = Path.Combine(_uploadDir, metadata.FileName);
            if (File.Exists(filePath))
            {
                _logger.LogInformation("Удаление файла с диска: {FilePath}", filePath);
                File.Delete(filePath);
            }
            else
            {
                _logger.LogWarning("Файл не найден на диске: {FilePath}", filePath);
            }
            
            lock (_lockObject)
            {
                _fileMetadata.Remove(fileId);
            }
            SaveMetadata();
            
            _logger.LogInformation("Файл успешно удален: {FileId}", fileId);
            
            return Task.FromResult(true);
        }

        /// <summary>
        /// Вычисляет хэш-сумму файла для проверки на плагиат
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns>Хэш-сумма файла</returns>
        private string CalculateFileHash(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка вычисления хэша файла: {FilePath}", filePath);
                throw new InvalidOperationException($"Не удалось вычислить хэш файла: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Ищет дубликат файла по хэш-сумме
        /// </summary>
        /// <param name="fileHash">Хэш-сумма файла</param>
        /// <returns>Идентификатор дубликата или пустая строка</returns>
        private string FindDuplicateByHash(string fileHash)
        {
            Dictionary<string, FileMetadata> metadataCopy;
            lock (_lockObject)
            {
                metadataCopy = new Dictionary<string, FileMetadata>(_fileMetadata);
            }
            
            foreach (var metadata in metadataCopy.Values)
            {
                if (metadata.Hash == fileHash)
                {
                    return metadata.Id;
                }
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Вычисляет статистику текстового файла
        /// </summary>
        /// <param name="content">Содержимое файла</param>
        /// <returns>Статистика файла</returns>
        private FileStatistics CalculateFileStatistics(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return new FileStatistics
                {
                    Paragraphs = 0,
                    Words = 0,
                    Chars = 0
                };
            }

            // Подсчет символов
            var chars = content.Length;

            // Подсчет слов (разделенных пробелами, табуляциями, переносами строк)
            var words = content
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;

            // Подсчет абзацев (разделенных двойными переносами строк или одиночными)
            var paragraphs = content
                .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Length;

            // Если нет двойных переносов, считаем по одиночным
            if (paragraphs == 1 && content.Contains('\n'))
            {
                paragraphs = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            }

            return new FileStatistics
            {
                Paragraphs = Math.Max(1, paragraphs), // Минимум 1 абзац, если есть содержимое
                Words = words,
                Chars = chars
            };
        }

        /// <summary>
        /// Получает список всех файлов (альтернативный метод для тестов)
        /// </summary>
        /// <returns>Список файлов</returns>
        public Task<List<FileMetadata>> GetAllFilesAsync()
        {
            _logger.LogInformation("Получение списка всех файлов");
            
            List<FileMetadata> files;
            lock (_lockObject)
            {
                files = new List<FileMetadata>(_fileMetadata.Values);
            }
            
            _logger.LogInformation("Список всех файлов получен. Количество: {FileCount}", files.Count);
            
            return Task.FromResult(files);
        }

        /// <summary>
        /// Проверяет существование файла по идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>True если файл существует, False если нет</returns>
        public Task<bool> FileExistsAsync(string fileId)
        {
            _logger.LogInformation("Проверка существования файла: {FileId}", fileId);
            
            bool exists;
            lock (_lockObject)
            {
                exists = _fileMetadata.ContainsKey(fileId);
            }
            
            _logger.LogInformation("Файл {FileId} существует: {Exists}", fileId, exists);
            
            return Task.FromResult(exists);
        }

        /// <summary>
        /// Получает содержимое файла как строку
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Содержимое файла</returns>
        public Task<string> GetFileContentAsync(string fileId)
        {
            _logger.LogInformation("Получение содержимого файла: {FileId}", fileId);
            
            FileMetadata metadata;
            lock (_lockObject)
            {
                if (!_fileMetadata.TryGetValue(fileId, out metadata))
                {
                    _logger.LogWarning("Файл не найден: {FileId}", fileId);
                    throw new KeyNotFoundException($"Файл не найден: {fileId}");
                }
            }
            
            var filePath = Path.Combine(_uploadDir, metadata.FileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Файл не найден на диске: {FilePath}", filePath);
                throw new FileNotFoundException($"Файл не найден на диске: {filePath}");
            }
            
            _logger.LogInformation("Файл найден, чтение содержимого: {FilePath}", filePath);
            
            string content;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new StreamReader(fileStream))
                {
                    content = reader.ReadToEnd();
                }
            }
            
            _logger.LogInformation("Содержимое файла получено: {FileId}", fileId);
            
            return Task.FromResult(content);
        }
    }
}
