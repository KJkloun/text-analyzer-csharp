using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FileAnalysisService.Models;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace FileAnalysisService.Services
{
    /// <summary>
    /// Интерфейс сервиса для генерации облака слов
    /// </summary>
    public interface IWordCloudService
    {
        /// <summary>
        /// Генерирует облако слов на основе текстового содержимого
        /// </summary>
        /// <param name="content">Текстовое содержимое для генерации облака слов</param>
        /// <returns>Результат генерации облака слов с URL изображения</returns>
        Task<WordCloudResult> GenerateWordCloudFromContentAsync(string content);
        
        /// <summary>
        /// Генерирует облако слов для файла по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Результат генерации облака слов с URL изображения</returns>
        Task<WordCloudResult> GenerateWordCloudAsync(string fileId);
    }

    /// <summary>
    /// Реализация сервиса для генерации облака слов
    /// </summary>
    public class WordCloudService : IWordCloudService
    {
        private readonly ILogger<WordCloudService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Инициализирует новый экземпляр сервиса генерации облака слов
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="httpClientFactory">Фабрика HTTP клиентов</param>
        /// <param name="configuration">Конфигурация</param>
        public WordCloudService(ILogger<WordCloudService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Генерирует облако слов для файла по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Результат генерации облака слов с URL изображения</returns>
        public async Task<WordCloudResult> GenerateWordCloudAsync(string fileId)
        {
            _logger.LogInformation($"Generating word cloud for file {fileId}");
            
            // Получаем файл из File Storage Service
            var fileStorageUrl = _configuration["ServiceUrls:FileStoringService"] ?? "http://file-storing-service:8001";
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(fileStorageUrl);
            
            var fileResponse = await httpClient.GetAsync($"/files/{fileId}");
            if (!fileResponse.IsSuccessStatusCode)
            {
                if (fileResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null; // Файл не найден
                }
                throw new HttpRequestException($"Failed to retrieve file {fileId} from storage");
            }

            var fileContent = await fileResponse.Content.ReadAsStringAsync();
            return await GenerateWordCloudFromContentAsync(fileContent);
        }

        /// <summary>
        /// Генерирует облако слов на основе текстового содержимого
        /// </summary>
        /// <param name="content">Текстовое содержимое для генерации облака слов</param>
        /// <returns>Результат генерации облака слов с URL изображения</returns>
        public async Task<WordCloudResult> GenerateWordCloudFromContentAsync(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Попытка генерации облака слов для пустого текста");
                throw new ArgumentException("Текст не может быть пустым", nameof(content));
            }

            _logger.LogInformation("Начало генерации облака слов для текста длиной {TextLength} символов", content.Length);
            
            try
            {
                // Извлекаем слова из текста
                var words = ExtractWords(content.ToLower());
                
                // Фильтруем слова длиной >= 4 символов
                var filteredWords = words.Where(w => w.Key.Length >= 4);
                
                // Берем топ-60 слов по частоте
                var top60Words = filteredWords
                    .OrderByDescending(w => w.Value)
                    .Take(60)
                    .ToDictionary(w => w.Key, w => w.Value);
                
                if (top60Words.Count == 0)
                {
                    _logger.LogWarning("Не найдено слов длиной >= 4 символов");
                    return new WordCloudResult
                    {
                        WordCloudUrl = "https://quickchart.io/chart?c={type:'bar',data:{labels:['No words found'],datasets:[{label:'Count',data:[0]}]}}"
                    };
                }
                
                // Формируем текст для QuickChart API в формате word:count
                var wordCloudText = string.Join(" ", top60Words.Select(w => $"{w.Key}:{w.Value}"));
                
                // Кодируем текст для URL
                var encodedText = Uri.EscapeDataString(wordCloudText);
                
                // Формируем URL для API QuickChart
                var apiUrl = $"https://quickchart.io/wordcloud?text={encodedText}&format=png&width=600&height=600&fontScale=15&scale=linear";
                
                _logger.LogInformation("Облако слов успешно сгенерировано с {WordCount} словами", top60Words.Count);
                
                return new WordCloudResult
                {
                    WordCloudUrl = apiUrl
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Ошибка при обращении к внешнему сервису");
                throw new HttpRequestException("QuickChart service is unavailable", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации облака слов");
                throw new InvalidOperationException("Не удалось сгенерировать облако слов", ex);
            }
        }

        /// <summary>
        /// Извлекает слова и их частоту из текста
        /// </summary>
        /// <param name="text">Текст для анализа</param>
        /// <returns>Словарь слов и их частоты</returns>
        private Dictionary<string, int> ExtractWords(string text)
        {
            var wordCount = new Dictionary<string, int>();
            var matches = Regex.Matches(text, @"\b\w+\b");
            
            foreach (Match match in matches)
            {
                var word = match.Value;
                if (wordCount.ContainsKey(word))
                {
                    wordCount[word]++;
                }
                else
                {
                    wordCount[word] = 1;
                }
            }
            
            return wordCount;
        }
    }
}
