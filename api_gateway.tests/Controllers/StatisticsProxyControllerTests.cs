using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiGateway.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ApiGateway.Tests.Controllers
{
    public class StatisticsProxyControllerTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<StatisticsProxyController>> _loggerMock;
        private readonly StatisticsProxyController _controller;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public StatisticsProxyControllerTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<StatisticsProxyController>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8002")
            };

            _httpClientFactoryMock.Setup(x => x.CreateClient("FileAnalysisService"))
                .Returns(httpClient);

            _controller = new StatisticsProxyController(_httpClientFactoryMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task AnalyzeFileStatistics_WithValidFileId_ReturnsOk()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var responseContent = @"{""fileId"": """ + fileId + @""", ""paragraphs"": 5, ""words"": 100, ""chars"": 500}";
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            // Act
            var result = await _controller.AnalyzeFileStatistics(fileId);

            // Assert
            var okResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task AnalyzeFileStatistics_WithInvalidFileId_ReturnsBadRequest()
        {
            // Arrange
            var invalidFileId = "invalid-id";
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Invalid file ID")
                });

            // Act
            var result = await _controller.AnalyzeFileStatistics(invalidFileId);

            // Assert
            var badRequestResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task AnalyzeFileStatistics_WhenFileNotFound_ReturnsNotFound()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("File not found")
                });

            // Act
            var result = await _controller.AnalyzeFileStatistics(fileId);

            // Assert
            var notFoundResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }
    }
} 