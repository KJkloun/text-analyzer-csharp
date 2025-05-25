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

namespace FileAnalysisService.Tests.Services
{
    public class PlagiarismServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly IPlagiarismService _plagiarismService;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public PlagiarismServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://file-storing-service:5001")
            };
            
            _httpClientFactoryMock.Setup(x => x.CreateClient("FileStoringService"))
                .Returns(httpClient);
                
            var loggerMock = new Mock<ILogger<PlagiarismService>>();
            var configMock = new Mock<IConfiguration>();
            _plagiarismService = new PlagiarismService(loggerMock.Object, _httpClientFactoryMock.Object, configMock.Object);
        }

        [Fact]
        public async Task CheckPlagiarismAsync_WhenFileIsNotDuplicate_ReturnsNonDuplicate()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var metadata = new FileMetadata
            {
                Id = fileId,
                FileName = "test.txt",
                DuplicateOf = null
            };
            
            SetupHttpResponse(fileId, metadata, HttpStatusCode.OK);

            // Act
            var result = await _plagiarismService.CheckPlagiarismAsync(fileId);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsDuplicate);
            Assert.Null(result.DuplicateOf);
        }

        [Fact]
        public async Task CheckPlagiarismAsync_WhenFileIsDuplicate_ReturnsDuplicate()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var duplicateFileId = Guid.NewGuid().ToString();
            var metadata = new FileMetadata
            {
                Id = fileId,
                FileName = "test.txt",
                DuplicateOf = duplicateFileId
            };
            
            SetupHttpResponse(fileId, metadata, HttpStatusCode.OK);

            // Act
            var result = await _plagiarismService.CheckPlagiarismAsync(fileId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsDuplicate);
            Assert.Equal(duplicateFileId, result.DuplicateOf);
        }

        [Fact]
        public async Task CheckPlagiarismAsync_WhenFileNotFound_ThrowsException()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Get && 
                        req.RequestUri.AbsolutePath == $"/files/{fileId}/metadata"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent(JsonSerializer.Serialize(new { error = "File not found" }))
                });

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _plagiarismService.CheckPlagiarismAsync(fileId));
        }

        [Fact]
        public async Task CheckPlagiarismAsync_WhenApiError_ThrowsException()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            SetupHttpResponse(HttpStatusCode.InternalServerError, "Server error");

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _plagiarismService.CheckPlagiarismAsync(fileId));
        }

        [Fact]
        public async Task CheckForDuplicateAsync_WithUniqueContent_ReturnsNotDuplicate()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var content = "This is unique content that has never been seen before";
            SetupHttpResponse(HttpStatusCode.OK, @"{""files"":[]}");

            // Act
            var result = await _plagiarismService.CheckForDuplicateAsync(fileId, content);

            // Assert
            Assert.False(result.IsDuplicate);
            Assert.Null(result.DuplicateFileId);
        }

        [Fact]
        public async Task CheckForDuplicateAsync_WithDuplicateContent_ReturnsDuplicate()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var content = "This is duplicate content";
            var existingFileId = Guid.NewGuid().ToString();
            var filesResponse = $@"{{""files"":[{{""id"":""{existingFileId}"",""hash"":""{ComputeHash(content)}""}}]}}";
            SetupHttpResponse(HttpStatusCode.OK, filesResponse);

            // Act
            var result = await _plagiarismService.CheckForDuplicateAsync(fileId, content);

            // Assert
            Assert.True(result.IsDuplicate);
            Assert.Equal(existingFileId, result.DuplicateFileId);
        }

        [Fact]
        public async Task CheckForDuplicateAsync_WithSameFileId_ReturnsNotDuplicate()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var content = "Some content";
            var filesResponse = $@"{{""files"":[{{""id"":""{fileId}"",""hash"":""{ComputeHash(content)}""}}]}}";
            SetupHttpResponse(HttpStatusCode.OK, filesResponse);

            // Act
            var result = await _plagiarismService.CheckForDuplicateAsync(fileId, content);

            // Assert
            Assert.False(result.IsDuplicate); // Same file ID should not be considered duplicate
            Assert.Null(result.DuplicateFileId);
        }

        [Fact]
        public async Task CheckForDuplicateAsync_WithMultipleFiles_FindsCorrectDuplicate()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var content = "Duplicate content";
            var duplicateFileId = Guid.NewGuid().ToString();
            var otherFileId = Guid.NewGuid().ToString();
            
            var filesResponse = $@"{{""files"":[
                {{""id"":""{otherFileId}"",""hash"":""different-hash""}},
                {{""id"":""{duplicateFileId}"",""hash"":""{ComputeHash(content)}""}},
                {{""id"":""{Guid.NewGuid()}"",""hash"":""another-hash""}}
            ]}}";
            SetupHttpResponse(HttpStatusCode.OK, filesResponse);

            // Act
            var result = await _plagiarismService.CheckForDuplicateAsync(fileId, content);

            // Assert
            Assert.True(result.IsDuplicate);
            Assert.Equal(duplicateFileId, result.DuplicateFileId);
        }

        [Fact]
        public async Task CheckForDuplicateAsync_WithEmptyContent_ReturnsNotDuplicate()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var content = "";
            SetupHttpResponse(HttpStatusCode.OK, @"{""files"":[]}");

            // Act
            var result = await _plagiarismService.CheckForDuplicateAsync(fileId, content);

            // Assert
            Assert.False(result.IsDuplicate);
        }

        [Fact]
        public async Task CheckForDuplicateAsync_WithNullContent_ThrowsArgumentException()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _plagiarismService.CheckForDuplicateAsync(fileId, null));
        }

        [Fact]
        public async Task CheckForDuplicateAsync_WithInvalidJson_ThrowsException()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var content = "Some content";
            SetupHttpResponse(HttpStatusCode.OK, "invalid json");

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => 
                _plagiarismService.CheckForDuplicateAsync(fileId, content));
        }

        [Fact]
        public async Task CheckPlagiarismAsync_WithValidFileId_ReturnsResult()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var responseContent = @"{""plagiarismDetected"": true, ""similarity"": 0.85}";
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var result = await _plagiarismService.CheckPlagiarismAsync(fileId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.PlagiarismDetected);
            Assert.Equal(0.85, result.Similarity);
        }

        private string ComputeHash(string content)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
            return Convert.ToBase64String(hash);
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Get && 
                        req.RequestUri.AbsolutePath == $"/files/{It.IsAny<string>()}/metadata"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });
        }
    }

    // Вспомогательные классы для тестирования
    public class FileMetadata
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
        public DateTime UploadDate { get; set; }
        public string Hash { get; set; }
        public string DuplicateOf { get; set; }
    }
} 