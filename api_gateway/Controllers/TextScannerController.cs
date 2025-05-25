using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace ApiGateway.Controllers
{
    /// <summary>
    /// Главный контроллер API Gateway для проекта Text Scanner
    /// </summary>
    [ApiController]
    [Route("")]
    public class TextScannerController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TextScannerController> _logger;

        public TextScannerController(IHttpClientFactory httpClientFactory, ILogger<TextScannerController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Загружает файл для анализа. Файл проходит проверку на дубликаты и статистический анализ.
        /// </summary>
        /// <param name="file">Текстовый файл</param>
        /// <returns>file_id и статистика или duplicate_of</returns>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            // Валидация входных данных
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            if (!file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Only .txt files are allowed" });
            }

            if (file.Length > 1048576) // 1 MB
            {
                return StatusCode(413, new { error = "File size exceeds 1 MB limit" });
            }

            try
            {
                // Шаг 1: Сохранение файла в File Storage Service
                var fileStorageClient = _httpClientFactory.CreateClient("FileStoringService");
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(file.OpenReadStream());
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "text/plain");
                content.Add(streamContent, "file", file.FileName);

                var storageResponse = await fileStorageClient.PostAsync("/files", content);
                if (!storageResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"File Storage Service returned {storageResponse.StatusCode}");
                    return StatusCode(502, new { error = "Failed to store file" });
                }

                var storageResult = await storageResponse.Content.ReadAsStringAsync();
                var storageData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(storageResult);
                
                if (!storageData.TryGetValue("fileId", out var fileIdElement))
                {
                    return StatusCode(502, new { error = "Invalid response from storage service" });
                }

                var fileId = fileIdElement.GetString();

                // Шаг 2: Анализ файла через File Analysis Service
                var analysisClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var analyzeRequest = JsonSerializer.Serialize(new { file_id = fileId });
                var analyzeContent = new StringContent(analyzeRequest, Encoding.UTF8, "application/json");

                var analysisResponse = await analysisClient.PostAsync("/analyze", analyzeContent);
                if (!analysisResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"Analysis Service returned {analysisResponse.StatusCode}");
                    return StatusCode(502, new { error = "Failed to analyze file" });
                }

                var analysisResult = await analysisResponse.Content.ReadAsStringAsync();
                var analysisData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(analysisResult);

                // Проверка на дубликат - если файл уже существует, возвращаем ID оригинала
                if (analysisData.TryGetValue("duplicate_of", out var duplicateElement))
                {
                    return Ok(new { duplicate_of = duplicateElement.GetString() });
                }

                // Возврат результата анализа для нового файла
                return Ok(new
                {
                    file_id = fileId,
                    stats = new
                    {
                        paragraphs = analysisData.GetValueOrDefault("paragraphs").GetInt32(),
                        words = analysisData.GetValueOrDefault("words").GetInt32(),
                        chars = analysisData.GetValueOrDefault("chars").GetInt32()
                    }
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while communicating with services");
                return StatusCode(502, new { error = "Service communication error" });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout");
                return StatusCode(504, new { error = "Request timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file upload");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Получает статистику анализа файла по его ID
        /// </summary>
        /// <param name="id">Идентификатор файла</param>
        /// <returns>Статистика файла</returns>
        [HttpGet("stats/{id}")]
        [ProducesResponseType(typeof(StatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetStats(string id)
        {
            // Валидация GUID формата
            if (!Guid.TryParse(id, out _))
            {
                return BadRequest(new { error = "Invalid file ID format" });
            }

            try
            {
                var analysisClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var response = await analysisClient.GetAsync($"/stats/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound(new { error = "File not found" });
                    }
                    return StatusCode(502, new { error = "Failed to get statistics" });
                }

                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics for file {FileId}", id);
                return StatusCode(502, new { error = "Service communication error" });
            }
        }

        /// <summary>
        /// Сравнивает два файла и возвращает коэффициент схожести Жаккара
        /// </summary>
        /// <param name="request">Запрос с идентификаторами файлов</param>
        /// <returns>Результат сравнения</returns>
        [HttpPost("compare")]
        [ProducesResponseType(typeof(CompareResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> CompareFiles([FromBody] CompareRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.file_id) || string.IsNullOrEmpty(request.other_file_id))
            {
                return BadRequest(new { error = "Both file_id and other_file_id are required" });
            }

            try
            {
                var analysisClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var compareContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await analysisClient.PostAsync("/compare", compareContent);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound(new { error = "One or both files not found" });
                    }
                    return StatusCode(502, new { error = "Failed to compare files" });
                }

                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing files {FileId1} and {FileId2}", request.file_id, request.other_file_id);
                return StatusCode(502, new { error = "Service communication error" });
            }
        }

        /// <summary>
        /// Получение URL облака слов
        /// </summary>
        /// <param name="id">Идентификатор файла</param>
        /// <returns>URL облака слов</returns>
        [HttpGet("cloud/{id}")]
        [ProducesResponseType(typeof(CloudResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetWordCloud(string id)
        {
            try
            {
                var analysisClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var response = await analysisClient.GetAsync($"/cloud/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound(new { error = "File not found" });
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    {
                        return StatusCode(503, new { error = "Word cloud service temporarily unavailable" });
                    }
                    return StatusCode(502, new { error = "Failed to generate word cloud" });
                }

                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting word cloud for file {FileId}", id);
                return StatusCode(502, new { error = "Service communication error" });
            }
        }

        /// <summary>
        /// Удаление файла
        /// </summary>
        /// <param name="id">Идентификатор файла</param>
        /// <returns>Результат удаления</returns>
        [HttpDelete("files/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> DeleteFile(string id)
        {
            try
            {
                // 1. Удаляем из кэша Analysis Service
                var analysisClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var cacheResponse = await analysisClient.DeleteAsync($"/cache/{id}");
                if (!cacheResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to clear analysis cache for file {id}: {cacheResponse.StatusCode}");
                }

                // 2. Удаляем файл из File Storage Service
                var storageClient = _httpClientFactory.CreateClient("FileStoringService");
                var response = await storageClient.DeleteAsync($"/files/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound(new { error = "File not found" });
                    }
                    return StatusCode(502, new { error = "Failed to delete file" });
                }

                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileId}", id);
                return StatusCode(502, new { error = "Service communication error" });
            }
        }
    }

    #region Request/Response Models

    public class UploadResponse
    {
        public string file_id { get; set; } = string.Empty;
        public StatsData stats { get; set; } = new StatsData();
    }

    public class StatsData
    {
        public int paragraphs { get; set; }
        public int words { get; set; }
        public int chars { get; set; }
    }

    public class StatsResponse
    {
        public string file_id { get; set; } = string.Empty;
        public int paragraphs { get; set; }
        public int words { get; set; }
        public int chars { get; set; }
    }

    public class CompareRequest
    {
        public string file_id { get; set; } = string.Empty;
        public string other_file_id { get; set; } = string.Empty;
    }

    public class CompareResponse
    {
        public bool identical { get; set; }
        public double jaccard_similarity { get; set; }
    }

    public class CloudResponse
    {
        public string file_id { get; set; } = string.Empty;
        public string word_cloud_url { get; set; } = string.Empty;
    }

    #endregion
} 