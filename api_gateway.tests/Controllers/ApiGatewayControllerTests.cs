using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiGateway.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ApiGateway.Tests.Controllers
{
    public class ApiGatewayControllerTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<ApiGatewayController>> _loggerMock;
        private readonly ApiGatewayController _controller;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public ApiGatewayControllerTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<ApiGatewayController>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8001")
            };

            _httpClientFactoryMock.Setup(x => x.CreateClient("FileStoringService"))
                .Returns(httpClient);
            _httpClientFactoryMock.Setup(x => x.CreateClient("FileAnalysisService"))
                .Returns(httpClient);

            _controller = new ApiGatewayController(_httpClientFactoryMock.Object, _configurationMock.Object);
        }

        [Fact]
        public async Task GetFile_WithValidFileId_ReturnsFile()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var fileContent = "Test file content";
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(fileContent)
                });

            // Act
            var result = await _controller.GetFile(fileId);

            // Assert
            Assert.IsType<FileContentResult>(result);
        }

        [Fact]
        public async Task GetFile_WithInvalidFileId_ReturnsError()
        {
            // Arrange
            var fileId = "invalid-id";
            
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
            var result = await _controller.GetFile(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(404, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task AnalyzeFileStatistics_WithValidFileId_ReturnsOk()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var responseContent = @"{""paragraphs"": 5, ""words"": 100, ""chars"": 500}";
            
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
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task CheckPlagiarism_WithValidFileId_ReturnsOk()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var responseContent = @"{""plagiarismDetected"": false, ""similarity"": 0.2}";
            
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
            var result = await _controller.CheckPlagiarism(fileId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GenerateWordCloud_WithValidFileId_ReturnsOk()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var responseContent = @"{""wordCloudUrl"": ""https://quickchart.io/wordcloud?text=example""}";
            
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
            var result = await _controller.GenerateWordCloud(fileId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetFile_WhenServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Service unavailable"));

            // Act
            var result = await _controller.GetFile(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task AnalyzeFileStatistics_WhenServiceUnavailable_ReturnsError()
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
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent("Service unavailable")
                });

            // Act
            var result = await _controller.AnalyzeFileStatistics(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task CheckPlagiarism_WhenServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            // Act
            var result = await _controller.CheckPlagiarism(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GenerateWordCloud_WhenServiceReturnsError_ReturnsError()
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
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Invalid request")
                });

            // Act
            var result = await _controller.GenerateWordCloud(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, statusCodeResult.StatusCode);
        }
    }
} 