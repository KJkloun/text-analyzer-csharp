using System;
using System.Threading.Tasks;
using FileAnalysisService.Controllers;
using FileAnalysisService.Services;
using FileAnalysisService.Services.Validation;
using FileAnalysisService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace FileAnalysisService.Tests.Controllers
{
    public class AnalysisControllerTests
    {
        private readonly Mock<IPlagiarismService> _plagiarismServiceMock;
        private readonly Mock<IWordCloudService> _wordCloudServiceMock;
        private readonly Mock<IStatisticsService> _statisticsServiceMock;
        private readonly Mock<IFileValidationService> _validationServiceMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<AnalysisController>> _loggerMock;
        private readonly AnalysisController _controller;

        public AnalysisControllerTests()
        {
            _plagiarismServiceMock = new Mock<IPlagiarismService>();
            _wordCloudServiceMock = new Mock<IWordCloudService>();
            _statisticsServiceMock = new Mock<IStatisticsService>();
            _validationServiceMock = new Mock<IFileValidationService>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _configMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<AnalysisController>>();
            _controller = new AnalysisController(
                _plagiarismServiceMock.Object,
                _wordCloudServiceMock.Object,
                _statisticsServiceMock.Object,
                _validationServiceMock.Object,
                _httpClientFactoryMock.Object,
                _configMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task AnalyzeFile_WhenValidationFails_ReturnsBadRequest()
        {
            // Arrange
            var request = new FileAnalysisService.Controllers.AnalyzeRequest { file_id = "invalid-id" };
            _validationServiceMock.Setup(x => x.ValidateFileId("invalid-id"))
                .Returns((false, "Invalid file ID"));

            // Act
            var result = await _controller.AnalyzeFile(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid file ID", badRequestResult.Value);
        }

        [Fact]
        public async Task AnalyzeFile_WhenValidationSucceeds_ReturnsOkWithResult()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var request = new FileAnalysisService.Controllers.AnalyzeRequest { file_id = fileId };
            var plagiarismResult = new PlagiarismResult { IsDuplicate = false };

            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _plagiarismServiceMock.Setup(x => x.CheckPlagiarismAsync(fileId))
                .ReturnsAsync(plagiarismResult);

            // Act
            var result = await _controller.AnalyzeFile(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetWordCloud_WhenValidationFails_ReturnsBadRequest()
        {
            // Arrange
            var fileId = "invalid-id";
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((false, "Invalid file ID"));

            // Act
            var result = await _controller.GetWordCloud(fileId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid file ID", badRequestResult.Value);
        }

        [Fact]
        public async Task GetWordCloud_WhenValidationSucceeds_ReturnsOkWithResult()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var wordCloudResult = new WordCloudResult { WordCloudUrl = "http://example.com/wordcloud.png" };

            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _wordCloudServiceMock.Setup(x => x.GenerateWordCloudAsync(fileId))
                .ReturnsAsync(wordCloudResult);

            // Act
            var result = await _controller.GetWordCloud(fileId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task AnalyzeFile_WhenServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var request = new FileAnalysisService.Controllers.AnalyzeRequest { file_id = fileId };
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _plagiarismServiceMock.Setup(x => x.CheckPlagiarismAsync(fileId))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.AnalyzeFile(request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GetWordCloud_WhenServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _wordCloudServiceMock.Setup(x => x.GenerateWordCloudAsync(fileId))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetWordCloud(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }
    }

    // Helper classes for tests
    public class CompareRequest
    {
        public string file_id { get; set; } = string.Empty;
        public string other_file_id { get; set; } = string.Empty;
    }
} 