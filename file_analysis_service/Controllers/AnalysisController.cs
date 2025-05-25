using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using FileAnalysisService.Services;
using Microsoft.Extensions.Logging;
using FileAnalysisService.Services.Validation;
using System;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FileAnalysisService.Controllers
{
    [ApiController]
    [Route("")]
    public class AnalysisController : ControllerBase
    {
        private readonly IPlagiarismService _plagiarismService;
        private readonly IWordCloudService _wordCloudService;
        private readonly IStatisticsService _statisticsService;
        private readonly IFileValidationService _validationService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AnalysisController> _logger;

        public AnalysisController(
            IPlagiarismService plagiarismService,
            IWordCloudService wordCloudService,
            IStatisticsService statisticsService,
            IFileValidationService validationService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AnalysisController> logger)
        {
            _plagiarismService = plagiarismService;
            _wordCloudService = wordCloudService;
            _statisticsService = statisticsService;
            _validationService = validationService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Анализирует загруженный файл
        /// </summary>
        /// <param name="request">Запрос с file_id</param>
        /// <returns>Статистика файла или duplicate_of</returns>
        [HttpPost("analyze")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AnalyzeFile([FromBody] AnalyzeRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.file_id))
            {
                return BadRequest(new { error = "file_id is required" });
            }

            try
            {
                var validationResult = _validationService.ValidateFileId(request.file_id);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning($"File ID validation failed: {validationResult.ErrorMessage}");
                    return BadRequest(new { error = validationResult.ErrorMessage });
                }

                // 1. Получаем файл из File Storage Service
                var fileStorageUrl = _configuration["ServiceUrls:FileStoringService"] ?? "http://file-storing-service:8001";
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.BaseAddress = new Uri(fileStorageUrl);
                
                var fileResponse = await httpClient.GetAsync($"/files/{request.file_id}");
                if (!fileResponse.IsSuccessStatusCode)
                {
                    if (fileResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound(new { error = "File not found" });
                    }
                    return StatusCode(500, new { error = "Failed to retrieve file from storage" });
                }

                var fileContent = await fileResponse.Content.ReadAsStringAsync();

                // 2. Проверяем на дубликат
                var duplicateCheck = await _plagiarismService.CheckForDuplicateAsync(request.file_id, fileContent);
                if (duplicateCheck.IsDuplicate)
                {
                    return Ok(new { duplicate_of = duplicateCheck.DuplicateFileId });
                }

                // 3. Если не дубликат, вычисляем статистику
                var stats = await _statisticsService.CalculateStatisticsAsync(request.file_id);
                
                return Ok(new
                {
                    file_id = request.file_id,
                    paragraphs = stats.ParagraphCount,
                    words = stats.WordCount,
                    chars = stats.CharacterCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing file {FileId}", request.file_id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Error analyzing file: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получение статистики файла
        /// </summary>
        /// <param name="id">Идентификатор файла</param>
        /// <returns>Статистика файла</returns>
        [HttpGet("stats/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStats(string id)
        {
            try
            {
                var validationResult = _validationService.ValidateFileId(id);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { error = validationResult.ErrorMessage });
                }

                var stats = await _statisticsService.GetStatisticsAsync(id);
                if (stats == null)
                {
                    // Если статистика не найдена в кэше, пересчитываем её
                    try
                    {
                        stats = await _statisticsService.CalculateStatisticsAsync(id);
                    }
                    catch (FileNotFoundException)
                    {
                        return NotFound(new { error = "File not found" });
                    }
                }

                return Ok(new
                {
                    paragraphs = stats.ParagraphCount,
                    words = stats.WordCount,
                    chars = stats.CharacterCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics for file {FileId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Error getting statistics: {ex.Message}" });
            }
        }

        /// <summary>
        /// Сравнение двух файлов
        /// </summary>
        /// <param name="request">Запрос с идентификаторами файлов</param>
        /// <returns>Результат сравнения</returns>
        [HttpPost("compare")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CompareFiles([FromBody] CompareRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.file_id) || string.IsNullOrEmpty(request.other_file_id))
            {
                return BadRequest(new { error = "Both file_id and other_file_id are required" });
            }

            try
            {
                var result = await _plagiarismService.CompareFilesAsync(request.file_id, request.other_file_id);
                return Ok(new
                {
                    identical = result.Identical,
                    jaccard_similarity = result.JaccardSimilarity
                });
            }
            catch (FileNotFoundException)
            {
                return NotFound(new { error = "One or both files not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing files {FileId1} and {FileId2}", request.file_id, request.other_file_id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Error comparing files: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получение URL облака слов
        /// </summary>
        /// <param name="id">Идентификатор файла</param>
        /// <returns>URL облака слов</returns>
        [HttpGet("cloud/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetWordCloud(string id)
        {
            try
            {
                var validationResult = _validationService.ValidateFileId(id);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { error = validationResult.ErrorMessage });
                }

                var result = await _wordCloudService.GenerateWordCloudAsync(id);
                if (result == null)
                {
                    return NotFound(new { error = "File not found" });
                }

                return Ok(new
                {
                    file_id = id,
                    word_cloud_url = result.WordCloudUrl
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { error = "Word cloud service temporarily unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating word cloud for file {FileId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Error generating word cloud: {ex.Message}" });
            }
        }

        // Legacy endpoint for backward compatibility
        [HttpPost("api/analysis/wordcloud/{fileId}")]
        public async Task<IActionResult> GenerateWordCloudLegacy(string fileId)
        {
            return await GetWordCloud(fileId);
        }

        /// <summary>
        /// Удаляет файл из кэша анализа
        /// </summary>
        /// <param name="id">Идентификатор файла</param>
        /// <returns>Результат удаления</returns>
        [HttpDelete("cache/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult RemoveFromCache(string id)
        {
            try
            {
                var validationResult = _validationService.ValidateFileId(id);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { error = validationResult.ErrorMessage });
                }

                _plagiarismService.RemoveFromDuplicateCache(id);
                _statisticsService.RemoveFromCache(id);
                
                _logger.LogInformation("File {FileId} removed from analysis cache", id);
                
                return Ok(new { message = "File removed from cache", fileId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing file {FileId} from cache", id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Error removing file from cache: {ex.Message}" });
            }
        }
    }

    public class AnalyzeRequest
    {
        public string file_id { get; set; }
    }

    public class CompareRequest
    {
        public string file_id { get; set; }
        public string other_file_id { get; set; }
    }
} 