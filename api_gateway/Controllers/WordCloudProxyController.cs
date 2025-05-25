using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("wordcloud")]
    public class WordCloudProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WordCloudProxyController> _logger;

        public WordCloudProxyController(IHttpClientFactory httpClientFactory, ILogger<WordCloudProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("{fileId}")]
        public async Task<IActionResult> GenerateWordCloud(string fileId)
        {
            var client = _httpClientFactory.CreateClient("FileAnalysisService");
            var response = await client.PostAsync($"/wordcloud/{fileId}", null);
            var responseBody = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, responseBody);
        }
    }
} 