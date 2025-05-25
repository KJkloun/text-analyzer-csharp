using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;
using FileAnalysisService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace FileAnalysisService.Tests.Services
{
    public class WordCloudServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly IWordCloudService _wordCloudService;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public WordCloudServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://file-storing-service:5001")
            };
            
            _httpClientFactoryMock.Setup(x => x.CreateClient("FileStoringService"))
                .Returns(httpClient);
                
            var loggerMock = new Mock<ILogger<WordCloudService>>();
            var configMock = new Mock<IConfiguration>();
            _wordCloudService = new WordCloudService(loggerMock.Object, _httpClientFactoryMock.Object, configMock.Object);
        }

        [Fact]
        public async Task GenerateWordCloudAsync_WithNormalText_ReturnsWordCloudUrl()
        {
            // Arrange
            var content = "This is a test content for word cloud generation";
            var encodedContent = Uri.EscapeDataString(content);

            // Act
            var result = await _wordCloudService.GenerateWordCloudAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.WordCloudUrl);
            Assert.Contains(encodedContent, result.WordCloudUrl);
            Assert.Contains("quickchart.io/wordcloud", result.WordCloudUrl);
        }

        [Fact]
        public async Task GenerateWordCloudAsync_WithEmptyText_ReturnsWordCloudUrl()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            SetupFileContent(fileId, "");

            // Act
            var result = await _wordCloudService.GenerateWordCloudAsync(fileId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("quickchart.io", result.WordCloudUrl);
        }

        [Fact]
        public async Task GenerateWordCloudAsync_WithLongText_EncodesUrlCorrectly()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var longText = string.Join(" ", Enumerable.Repeat("word", 100));
            SetupFileContent(fileId, longText);

            // Act
            var result = await _wordCloudService.GenerateWordCloudAsync(fileId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("quickchart.io", result.WordCloudUrl);
            Assert.Contains("text=", result.WordCloudUrl);
        }

        [Theory]
        [InlineData("Hello World")]
        [InlineData("Привет мир")]
        [InlineData("Special characters: !@#$%^&*()")]
        public async Task GenerateWordCloudAsync_WithDifferentCharacters_HandlesCorrectly(string content)
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            SetupFileContent(fileId, content);

            // Act
            var result = await _wordCloudService.GenerateWordCloudAsync(fileId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("quickchart.io", result.WordCloudUrl);
        }

        [Fact]
        public async Task GenerateWordCloudAsync_WithFileNotFound_ThrowsException()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            SetupFileNotFound(fileId);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _wordCloudService.GenerateWordCloudAsync(fileId));
        }

        [Fact]
        public async Task GenerateWordCloudAsync_WithServiceError_ThrowsException()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            SetupServiceError(fileId);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _wordCloudService.GenerateWordCloudAsync(fileId));
        }

        [Fact]
        public async Task GenerateWordCloudAsync_WithSpecialCharacters_EncodesCorrectly()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var content = "Text with spaces & special chars: @#$%";
            SetupFileContent(fileId, content);

            // Act
            var result = await _wordCloudService.GenerateWordCloudAsync(fileId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("quickchart.io", result.WordCloudUrl);
            // URL should be properly encoded
            Assert.DoesNotContain(" ", result.WordCloudUrl.Split('=')[1]);
        }

        private void SetupFileNotFound(string fileId)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains(fileId)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });
        }

        private void SetupServiceError(string fileId)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains(fileId)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });
        }
    }
} 