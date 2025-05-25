using System;
using Xunit;
using FileAnalysisService.Services.Validation;

namespace FileAnalysisService.Tests.Services.Validation
{
    public class FileValidationServiceTests
    {
        private readonly IFileValidationService _validationService;

        public FileValidationServiceTests()
        {
            _validationService = new FileValidationService();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void ValidateFileId_WhenFileIdIsNullOrWhitespace_ReturnsInvalid(string fileId)
        {
            // Act
            var result = _validationService.ValidateFileId(fileId);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("File ID cannot be empty", result.ErrorMessage);
        }

        [Theory]
        [InlineData("not-a-guid")]
        [InlineData("123")]
        [InlineData("invalid-format")]
        public void ValidateFileId_WhenFileIdIsNotGuid_ReturnsInvalid(string fileId)
        {
            // Act
            var result = _validationService.ValidateFileId(fileId);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Invalid file ID format", result.ErrorMessage);
        }

        [Fact]
        public void ValidateFileId_WhenFileIdIsValidGuid_ReturnsValid()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();

            // Act
            var result = _validationService.ValidateFileId(fileId);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.ErrorMessage);
        }
    }
} 