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
    public class PlagiarismProxyControllerTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<PlagiarismProxyController>> _loggerMock;
        private readonly PlagiarismProxyController _controller;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public PlagiarismProxyControllerTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<PlagiarismProxyController>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8002")
            };

            _httpClientFactoryMock.Setup(x => x.CreateClient("FileAnalysisService"))
                .Returns(httpClient);

            _controller = new PlagiarismProxyController(_httpClientFactoryMock.Object, _loggerMock.Object);
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
            var okResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task CheckPlagiarism_WithInvalidFileId_ReturnsBadRequest()
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
            var result = await _controller.CheckPlagiarism(invalidFileId);

            // Assert
            var badRequestResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task CheckPlagiarism_WhenServiceUnavailable_ReturnsServiceUnavailable()
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
            var result = await _controller.CheckPlagiarism(fileId);

            // Assert
            var serviceUnavailableResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, serviceUnavailableResult.StatusCode);
        }
    }
} 