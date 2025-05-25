using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("statistics")]
    public class StatisticsProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StatisticsProxyController> _logger;

        public StatisticsProxyController(IHttpClientFactory httpClientFactory, ILogger<StatisticsProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("{fileId}")]
        public async Task<IActionResult> AnalyzeFileStatistics(string fileId)
        {
            var client = _httpClientFactory.CreateClient("FileAnalysisService");
            var response = await client.PostAsync($"/statistics/{fileId}", null);
            var responseBody = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, responseBody);
        }
    }
} 