using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiGateway.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using System.IO;
using System.Text;

namespace ApiGateway.Tests.Controllers
{
    public class TextScannerControllerExtensiveTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<TextScannerController>> _loggerMock;
        private readonly TextScannerController _controller;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public TextScannerControllerExtensiveTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<TextScannerController>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8001")
            };

            _httpClientFactoryMock.Setup(x => x.CreateClient("FileStoringService"))
                .Returns(httpClient);
            _httpClientFactoryMock.Setup(x => x.CreateClient("FileAnalysisService"))
                .Returns(httpClient);

            _controller = new TextScannerController(_httpClientFactoryMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task UploadFile_WithValidFile_ReturnsOk()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Test content");
            var responseContent = @"{""fileId"": ""123"", ""fileName"": ""test.txt"", ""duplicate"": false}";
            
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var okResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WithDuplicateFile_ReturnsOkWithDuplicateInfo()
        {
            // Arrange
            var file = CreateMockFile("duplicate.txt", "Duplicate content");
            var responseContent = @"{""fileId"": ""456"", ""fileName"": ""duplicate.txt"", ""duplicate"": true, ""duplicateOf"": ""123""}";
            
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var okResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WithLargeFile_HandlesCorrectly()
        {
            // Arrange
            var largeContent = new string('A', 5000); // 5KB file
            var file = CreateMockFile("large.txt", largeContent);
            var responseContent = @"{""fileId"": ""789"", ""fileName"": ""large.txt"", ""duplicate"": false}";
            
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var okResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WithSpecialCharactersInName_HandlesCorrectly()
        {
            // Arrange
            var file = CreateMockFile("тест файл.txt", "Content with unicode name");
            var responseContent = @"{""fileId"": ""999"", ""fileName"": ""тест файл.txt"", ""duplicate"": false}";
            
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var okResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WithEmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var file = CreateMockFile("empty.txt", "");
            
            SetupHttpResponse(HttpStatusCode.BadRequest, @"{""error"": ""File is empty""}");

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var badRequestResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WithUnsupportedFileType_ReturnsBadRequest()
        {
            // Arrange
            var file = CreateMockFile("test.exe", "Executable content");
            
            SetupHttpResponse(HttpStatusCode.BadRequest, @"{""error"": ""File type not supported""}");

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var badRequestResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WhenServiceUnavailable_ReturnsServiceUnavailable()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Test content");
            
            SetupHttpResponse(HttpStatusCode.ServiceUnavailable, "Service temporarily unavailable");

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var serviceUnavailableResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, serviceUnavailableResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WhenInternalServerError_ReturnsInternalServerError()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Test content");
            
            SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal server error");

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, internalServerErrorResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WhenNetworkException_ReturnsInternalServerError()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Test content");
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, internalServerErrorResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WhenTimeout_ReturnsInternalServerError()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Test content");
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, internalServerErrorResult.StatusCode);
        }

        [Theory]
        [InlineData("test.txt", "text/plain")]
        [InlineData("document.doc", "application/msword")]
        [InlineData("document.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
        [InlineData("document.pdf", "application/pdf")]
        public async Task UploadFile_WithDifferentFileTypes_HandlesCorrectly(string fileName, string contentType)
        {
            // Arrange
            var file = CreateMockFile(fileName, "File content", contentType);
            var responseContent = $@"{{""fileId"": ""123"", ""fileName"": ""{fileName}"", ""duplicate"": false}}";
            
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var okResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }

        [Fact]
        public async Task UploadFile_WithNullFile_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.UploadFile(null);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("No file uploaded", badRequestResult.Value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task UploadFile_WithInvalidFileLength_ReturnsBadRequest(long fileLength)
        {
            // Arrange
            var file = CreateMockFileWithLength("test.txt", "content", fileLength);

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("File is empty", badRequestResult.Value);
        }

        [Fact]
        public async Task UploadFile_WithTooLargeFile_ReturnsBadRequest()
        {
            // Arrange
            var file = CreateMockFileWithLength("large.txt", "content", 100 * 1024 * 1024); // 100MB

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("File too large", badRequestResult.Value);
        }

        private IFormFile CreateMockFile(string fileName, string content, string contentType = "text/plain")
        {
            var mock = new Mock<IFormFile>();
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);

            mock.Setup(f => f.FileName).Returns(fileName);
            mock.Setup(f => f.Length).Returns(bytes.Length);
            mock.Setup(f => f.ContentType).Returns(contentType);
            mock.Setup(f => f.OpenReadStream()).Returns(stream);

            return mock.Object;
        }

        private IFormFile CreateMockFileWithLength(string fileName, string content, long length)
        {
            var mock = new Mock<IFormFile>();
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);

            mock.Setup(f => f.FileName).Returns(fileName);
            mock.Setup(f => f.Length).Returns(length);
            mock.Setup(f => f.ContentType).Returns("text/plain");
            mock.Setup(f => f.OpenReadStream()).Returns(stream);

            return mock.Object;
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });
        }
    }
} 