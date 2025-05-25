using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using FileAnalysisService.Models;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace FileAnalysisService.Services
{
    /// <summary>
    /// Интерфейс сервиса для статистического анализа текста
    /// </summary>
    public interface IStatisticsService
    {
        /// <summary>
        /// Вычисляет статистические показатели текста
        /// </summary>
        /// <param name="content">Содержимое текстового файла</param>
        /// <returns>Статистические показатели текста</returns>
        Task<StatisticsResult> CalculateStatisticsFromContentAsync(string content);
        
        /// <summary>
        /// Вычисляет и сохраняет статистику для файла
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Статистические показатели текста</returns>
        Task<FileStatistics> CalculateStatisticsAsync(string fileId);
        
        /// <summary>
        /// Получает сохраненную статистику для файла
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Статистические показатели текста или null</returns>
        Task<FileStatistics> GetStatisticsAsync(string fileId);
        
        /// <summary>
        /// Удаляет статистику файла из кэша
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        void RemoveFromCache(string fileId);
    }

    /// <summary>
    /// Результат статистического анализа файла
    /// </summary>
    public class FileStatistics
    {
        public string FileId { get; set; }
        public int ParagraphCount { get; set; }
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public DateTime AnalyzedAt { get; set; }
    }

    /// <summary>
    /// Реализация сервиса для статистического анализа текста
    /// </summary>
    public class StatisticsService : IStatisticsService
    {
        private readonly ILogger<StatisticsService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, FileStatistics> _statsCache = new();

        /// <summary>
        /// Инициализирует новый экземпляр сервиса статистического анализа
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="httpClientFactory">Фабрика HTTP клиентов</param>
        /// <param name="configuration">Конфигурация</param>
        public StatisticsService(ILogger<StatisticsService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Вычисляет статистические показатели текста
        /// </summary>
        /// <param name="content">Содержимое текстового файла</param>
        /// <returns>Статистические показатели текста</returns>
        public Task<StatisticsResult> CalculateStatisticsFromContentAsync(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Попытка анализа пустого текста");
                return Task.FromResult(new StatisticsResult
                {
                    Paragraphs = 0,
                    Words = 0,
                    Chars = 0,
                    CharsNoSpaces = 0
                });
            }

            _logger.LogInformation("Начало статистического анализа текста длиной {TextLength} символов", content.Length);
            
            try
            {
                // Подсчет абзацев (разделенных двойным переносом строки)
                var paragraphs = Regex.Split(content, @"\n\s*\n").Length;
                _logger.LogDebug("Подсчитано абзацев: {ParagraphCount}", paragraphs);
                
                // Подсчет слов
                var words = Regex.Matches(content, @"\b\w+\b").Count;
                _logger.LogDebug("Подсчитано слов: {WordCount}", words);
                
                // Подсчет символов (включая пробелы)
                var chars = content.Length;
                _logger.LogDebug("Подсчитано символов (с пробелами): {CharCount}", chars);
                
                // Подсчет символов (без пробелов)
                var charsNoSpaces = content.Replace(" ", "").Replace("\n", "").Replace("\t", "").Length;
                _logger.LogDebug("Подсчитано символов (без пробелов): {CharNoSpacesCount}", charsNoSpaces);
                
                var result = new StatisticsResult
                {
                    Paragraphs = paragraphs,
                    Words = words,
                    Chars = chars,
                    CharsNoSpaces = charsNoSpaces
                };
                
                _logger.LogInformation("Статистический анализ текста успешно завершен");
                
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при статистическом анализе текста");
                throw new InvalidOperationException("Не удалось выполнить статистический анализ текста", ex);
            }
        }

        /// <summary>
        /// Вычисляет и сохраняет статистику для файла
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Статистические показатели текста</returns>
        public async Task<FileStatistics> CalculateStatisticsAsync(string fileId)
        {
            // Получаем файл из File Storage Service
            var fileStorageUrl = _configuration["ServiceUrls:FileStoringService"] ?? "http://file-storing-service:8001";
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(fileStorageUrl);
            
            var fileResponse = await httpClient.GetAsync($"/files/{fileId}");
            if (!fileResponse.IsSuccessStatusCode)
            {
                throw new FileNotFoundException($"File {fileId} not found in storage");
            }

            var fileContent = await fileResponse.Content.ReadAsStringAsync();
            var stats = await CalculateStatisticsFromContentAsync(fileContent);
            
            var fileStats = new FileStatistics
            {
                FileId = fileId,
                ParagraphCount = stats.Paragraphs,
                WordCount = stats.Words,
                CharacterCount = stats.Chars,
                AnalyzedAt = DateTime.UtcNow
            };
            
            // Сохраняем в кэш (в реальном приложении - в БД)
            _statsCache[fileId] = fileStats;
            
            return fileStats;
        }

        /// <summary>
        /// Получает сохраненную статистику для файла
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Статистические показатели текста или null</returns>
        public Task<FileStatistics> GetStatisticsAsync(string fileId)
        {
            _statsCache.TryGetValue(fileId, out var stats);
            return Task.FromResult(stats);
        }

        /// <summary>
        /// Удаляет статистику файла из кэша
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        public void RemoveFromCache(string fileId)
        {
            _statsCache.Remove(fileId);
        }
    }
}
