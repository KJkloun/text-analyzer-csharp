using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("plagiarism")]
    public class PlagiarismProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PlagiarismProxyController> _logger;

        public PlagiarismProxyController(IHttpClientFactory httpClientFactory, ILogger<PlagiarismProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("{fileId}")]
        public async Task<IActionResult> CheckPlagiarism(string fileId)
        {
            var client = _httpClientFactory.CreateClient("FileAnalysisService");
            var response = await client.PostAsync($"/plagiarism/{fileId}", null);
            var responseBody = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, responseBody);
        }
    }
} 