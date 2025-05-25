using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http.Json;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Linq;

namespace ApiGateway.Controllers
{
    /// <summary>
    /// Контроллер API Gateway для маршрутизации запросов к микросервисам
    /// </summary>
    [ApiController]
    [Route("api")]
    public class ApiGatewayController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Конструктор контроллера API Gateway
        /// </summary>
        /// <param name="httpClientFactory">Фабрика HTTP-клиентов для взаимодействия с микросервисами</param>
        /// <param name="configuration">Конфигурация приложения</param>
        public ApiGatewayController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Получение статуса здоровья всех микросервисов
        /// </summary>
        /// <returns>Статус здоровья API Gateway и всех микросервисов</returns>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealthStatus()
        {
            var healthStatus = new Dictionary<string, string>
            {
                { "ApiGateway", "ok" }
            };
            
            try
            {
                using var fileServiceClient = _httpClientFactory.CreateClient("FileStoringService");
                var fileResponse = await fileServiceClient.GetAsync("/health");
                healthStatus.Add("FileService", fileResponse.IsSuccessStatusCode ? "ok" : "error");
            }
            catch (Exception ex)
            {
                healthStatus.Add("FileService", $"error: {ex.Message}");
            }
            
            try
            {
                using var analysisServiceClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var analysisResponse = await analysisServiceClient.GetAsync("/health");
                healthStatus.Add("AnalysisService", analysisResponse.IsSuccessStatusCode ? "ok" : "error");
            }
            catch (Exception ex)
            {
                healthStatus.Add("AnalysisService", $"error: {ex.Message}");
            }
            
            return Ok(healthStatus);
        }

        /// <summary>
        /// Загрузка файла через API Gateway
        /// </summary>
        /// <param name="file">Загружаемый файл</param>
        /// <returns>Результат загрузки файла</returns>
        [HttpPost("files")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            try
            {
                using var fileServiceClient = _httpClientFactory.CreateClient("FileStoringService");
                using var content = new MultipartFormDataContent();
                using var fileStream = file.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                
                content.Add(streamContent, "file", file.FileName);
                
                var response = await fileServiceClient.PostAsync("files", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Error from File Service: {errorContent}" });
                }
                
                var result = await response.Content.ReadFromJsonAsync<object>();
                return StatusCode((int)response.StatusCode, result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error uploading file: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получение списка файлов через API Gateway
        /// </summary>
        /// <returns>Список файлов</returns>
        [HttpGet("files")]
        public async Task<IActionResult> GetFiles()
        {
            try
            {
                using var fileServiceClient = _httpClientFactory.CreateClient("FileStoringService");
                var response = await fileServiceClient.GetAsync("files");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Error from File Service: {errorContent}" });
                }
                
                var result = await response.Content.ReadFromJsonAsync<object>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error getting files: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получение файла по ID через API Gateway
        /// </summary>
        /// <param name="fileId">ID файла</param>
        /// <returns>Содержимое файла</returns>
        [HttpGet("files/{fileId}")]
        public async Task<IActionResult> GetFile(string fileId)
        {
            try
            {
                using var fileServiceClient = _httpClientFactory.CreateClient("FileStoringService");
                var response = await fileServiceClient.GetAsync($"files/{fileId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Error from File Service: {errorContent}" });
                }
                
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileName ?? $"{fileId}.txt";
                var content = await response.Content.ReadAsStreamAsync();
                
                return File(content, contentType, fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error getting file: {ex.Message}" });
            }
        }

        /// <summary>
        /// Анализ статистики файла через API Gateway
        /// </summary>
        /// <param name="fileId">ID файла</param>
        /// <returns>Результаты статистического анализа</returns>
        [HttpPost("analysis/statistics/{fileId}")]
        public async Task<IActionResult> AnalyzeFileStatistics(string fileId)
        {
            try
            {
                using var analysisServiceClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var response = await analysisServiceClient.PostAsync($"statistics/{fileId}", null);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Error from Analysis Service: {errorContent}" });
                }
                
                var result = await response.Content.ReadFromJsonAsync<object>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error analyzing file statistics: {ex.Message}" });
            }
        }

        /// <summary>
        /// Проверка файла на плагиат через API Gateway
        /// </summary>
        /// <param name="fileId">ID файла</param>
        /// <returns>Результаты проверки на плагиат</returns>
        [HttpPost("analysis/plagiarism/{fileId}")]
        public async Task<IActionResult> CheckPlagiarism(string fileId)
        {
            try
            {
                using var analysisServiceClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var response = await analysisServiceClient.PostAsync($"plagiarism/{fileId}", null);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Error from Analysis Service: {errorContent}" });
                }
                
                var result = await response.Content.ReadFromJsonAsync<object>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error checking plagiarism: {ex.Message}" });
            }
        }

        /// <summary>
        /// Генерация облака слов для файла через API Gateway
        /// </summary>
        /// <param name="fileId">ID файла</param>
        /// <returns>URL облака слов</returns>
        [HttpPost("analysis/wordcloud/{fileId}")]
        public async Task<IActionResult> GenerateWordCloud(string fileId)
        {
            try
            {
                using var analysisServiceClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var response = await analysisServiceClient.PostAsync($"wordcloud/{fileId}", null);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Error from Analysis Service: {errorContent}" });
                }
                
                var result = await response.Content.ReadFromJsonAsync<object>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error generating wordcloud: {ex.Message}" });
            }
        }

        // Прокси для Swagger UI сервиса анализа (GET api/analysis/swagger)
        [HttpGet("analysis/swagger/{**slug}")]
        public async Task<IActionResult> ProxyAnalysisSwagger(string slug)
        {
            var client = _httpClientFactory.CreateClient("FileAnalysisService");
            var targetUrl = $"/swagger/{slug}{Request.QueryString}";
            var response = await client.GetAsync(targetUrl);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }

            // Копируем заголовки
            foreach (var header in response.Headers)
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in response.Content.Headers)
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Копируем содержимое
            var stream = await response.Content.ReadAsStreamAsync();
            return new FileStreamResult(stream, response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
        }

        /// <summary>
        /// Получение результатов анализа по ID через API Gateway
        /// </summary>
        /// <param name="analysisId">ID результатов анализа</param>
        /// <returns>Результаты анализа</returns>
        [HttpGet("analysis/results/{analysisId}")]
        public async Task<IActionResult> GetAnalysisResults(string analysisId)
        {
            try
            {
                using var analysisServiceClient = _httpClientFactory.CreateClient("FileAnalysisService");
                var response = await analysisServiceClient.GetAsync($"results/{analysisId}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Error from Analysis Service: {errorContent}" });
                }

                var result = await response.Content.ReadFromJsonAsync<object>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Error getting analysis results: {ex.Message}" });
            }
        }
    }
}
