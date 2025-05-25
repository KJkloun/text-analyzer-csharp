using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FileAnalysisService.Models;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace FileAnalysisService.Services
{
    /// <summary>
    /// Реализация сервиса для проверки текстов на плагиат
    /// </summary>
    public class PlagiarismService : IPlagiarismService
    {
        private readonly ILogger<PlagiarismService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        // Временное хранилище хэшей файлов (в реальном приложении - SQLite/PostgreSQL)
        private static readonly Dictionary<string, string> _fileHashes = new();
        private static readonly Dictionary<string, string> _hashToFileId = new();

        /// <summary>
        /// Инициализирует новый экземпляр сервиса проверки на плагиат
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="httpClientFactory">Фабрика HTTP клиентов</param>
        /// <param name="configuration">Конфигурация</param>
        public PlagiarismService(ILogger<PlagiarismService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Проверяет файл на плагиат по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Результаты проверки на плагиат</returns>
        public async Task<PlagiarismResult> CheckPlagiarism(string fileId)
        {
            _logger.LogInformation($"Checking plagiarism for file {fileId}");
            
            // TODO: Implement actual plagiarism checking logic
            return new PlagiarismResult
            {
                IsDuplicate = false,
                DuplicateOf = null
            };
        }

        /// <summary>
        /// Проверяет файл на плагиат по его идентификатору (асинхронная версия)
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Результаты проверки на плагиат</returns>
        public async Task<PlagiarismResult> CheckPlagiarismAsync(string fileId)
        {
            return await CheckPlagiarism(fileId);
        }

        /// <summary>
        /// Проверяет файл на дубликат используя SHA-256
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <param name="fileContent">Содержимое файла</param>
        /// <returns>Результат проверки на дубликат</returns>
        public async Task<DuplicateCheckResult> CheckForDuplicateAsync(string fileId, string fileContent)
        {
            _logger.LogInformation($"Checking for duplicate of file {fileId}");
            
            // Вычисляем SHA-256 хэш содержимого файла
            var hash = ComputeSha256Hash(fileContent);
            
            // Проверяем, существует ли уже файл с таким хэшем
            if (_hashToFileId.TryGetValue(hash, out var existingFileId) && existingFileId != fileId)
            {
                _logger.LogInformation($"Found duplicate: file {fileId} is a duplicate of {existingFileId}");
                return new DuplicateCheckResult
                {
                    IsDuplicate = true,
                    DuplicateFileId = existingFileId
                };
            }
            
            // Сохраняем хэш для данного файла
            _fileHashes[fileId] = hash;
            _hashToFileId[hash] = fileId;
            
            _logger.LogInformation($"No duplicate found for file {fileId}");
            return new DuplicateCheckResult
            {
                IsDuplicate = false,
                DuplicateFileId = null
            };
        }

        /// <summary>
        /// Сравнивает два файла
        /// </summary>
        /// <param name="fileId1">Идентификатор первого файла</param>
        /// <param name="fileId2">Идентификатор второго файла</param>
        /// <returns>Результат сравнения</returns>
        public async Task<ComparisonResult> CompareFilesAsync(string fileId1, string fileId2)
        {
            _logger.LogInformation($"Comparing files {fileId1} and {fileId2}");
            
            // Получаем содержимое файлов
            var fileStorageUrl = _configuration["ServiceUrls:FileStoringService"] ?? "http://file-storing-service:8001";
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(fileStorageUrl);
            
            var file1Response = await httpClient.GetAsync($"/files/{fileId1}");
            if (!file1Response.IsSuccessStatusCode)
            {
                throw new FileNotFoundException($"File {fileId1} not found");
            }
            
            var file2Response = await httpClient.GetAsync($"/files/{fileId2}");
            if (!file2Response.IsSuccessStatusCode)
            {
                throw new FileNotFoundException($"File {fileId2} not found");
            }
            
            var content1 = await file1Response.Content.ReadAsStringAsync();
            var content2 = await file2Response.Content.ReadAsStringAsync();
            
            // Проверяем идентичность через хэши
            var hash1 = ComputeSha256Hash(content1);
            var hash2 = ComputeSha256Hash(content2);
            bool areIdentical = hash1 == hash2;
            
            // Вычисляем коэффициент Жаккара
            double jaccardSimilarity = CalculateJaccardSimilarity(content1, content2);
            
            _logger.LogInformation($"Comparison result: identical={areIdentical}, jaccard={jaccardSimilarity:F2}");
            
            return new ComparisonResult
            {
                Identical = areIdentical,
                JaccardSimilarity = jaccardSimilarity
            };
        }

        /// <summary>
        /// Вычисляет SHA-256 хэш строки
        /// </summary>
        /// <param name="content">Содержимое для хэширования</param>
        /// <returns>SHA-256 хэш в виде строки</returns>
        private string ComputeSha256Hash(string content)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Вычисляет коэффициент Жаккара для двух текстов
        /// </summary>
        /// <param name="text1">Первый текст</param>
        /// <param name="text2">Второй текст</param>
        /// <returns>Коэффициент Жаккара (0-1)</returns>
        private double CalculateJaccardSimilarity(string text1, string text2)
        {
            // Извлекаем слова из текстов (нижний регистр)
            var words1 = ExtractWords(text1.ToLower());
            var words2 = ExtractWords(text2.ToLower());
            
            if (words1.Count == 0 && words2.Count == 0)
                return 1.0; // Два пустых текста считаются идентичными
            
            if (words1.Count == 0 || words2.Count == 0)
                return 0.0; // Один пустой, другой нет
            
            // Вычисляем пересечение и объединение
            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();
            
            return union == 0 ? 0.0 : (double)intersection / union;
        }

        /// <summary>
        /// Извлекает уникальные слова из текста
        /// </summary>
        /// <param name="text">Текст для анализа</param>
        /// <returns>Множество уникальных слов</returns>
        private HashSet<string> ExtractWords(string text)
        {
            var words = new HashSet<string>();
            var matches = Regex.Matches(text, @"\b\w+\b");
            
            foreach (Match match in matches)
            {
                words.Add(match.Value);
            }
            
            return words;
        }

        /// <summary>
        /// Удаляет файл из кэша дубликатов
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        public void RemoveFromDuplicateCache(string fileId)
        {
            _logger.LogInformation($"Removing file {fileId} from duplicate cache");
            
            if (_fileHashes.TryGetValue(fileId, out var hash))
            {
                _hashToFileId.Remove(hash);
                _fileHashes.Remove(fileId);
                _logger.LogInformation($"File {fileId} removed from duplicate cache");
            }
            else
            {
                _logger.LogInformation($"File {fileId} not found in duplicate cache");
            }
        }
    }
}
