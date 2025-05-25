using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using FileStoringService.Services.Validation;
using System.Text;

namespace FileStoringService.Tests.Services.Validation
{
    public class FileValidationServiceTests
    {
        private readonly IFileValidationService _validationService;

        public FileValidationServiceTests()
        {
            _validationService = new FileValidationService();
        }

        [Fact]
        public void ValidateFile_WhenFileIsNull_ReturnsInvalid()
        {
            // Act
            var result = _validationService.ValidateFile(null);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("File is empty or not provided", result.ErrorMessage);
        }

        [Fact]
        public void ValidateFile_WhenFileLengthIsZero_ReturnsInvalid()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(0);

            // Act
            var result = _validationService.ValidateFile(fileMock.Object);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("File is empty or not provided", result.ErrorMessage);
        }

        [Fact]
        public void ValidateFile_WhenFileSizeExceedsLimit_ReturnsInvalid()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11MB
            fileMock.Setup(f => f.FileName).Returns("test.txt");

            // Act
            var result = _validationService.ValidateFile(fileMock.Object);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("File size exceeds maximum allowed size", result.ErrorMessage);
        }

        [Fact]
        public void ValidateFile_WhenFileExtensionIsNotAllowed_ReturnsInvalid()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(1024);
            fileMock.Setup(f => f.FileName).Returns("test.pdf");

            // Act
            var result = _validationService.ValidateFile(fileMock.Object);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("File type not allowed", result.ErrorMessage);
        }

        [Fact]
        public void ValidateFile_WhenFileIsValid_ReturnsValid()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(1024);
            fileMock.Setup(f => f.FileName).Returns("test.txt");

            // Act
            var result = _validationService.ValidateFile(fileMock.Object);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.ErrorMessage);
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

        [Fact]
        public void ValidateFile_WithNullFile_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _validationService.ValidateFile(null));
        }

        [Fact]
        public void ValidateFile_WithEmptyFileName_ThrowsArgumentException()
        {
            // Arrange
            var file = CreateMockFile("", "content");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _validationService.ValidateFile(file));
            Assert.Contains("File name cannot be empty", exception.Message);
        }

        [Fact]
        public void ValidateFile_WithZeroLength_ThrowsArgumentException()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "", 0);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _validationService.ValidateFile(file));
            Assert.Contains("File cannot be empty", exception.Message);
        }

        [Fact]
        public void ValidateFile_WithTooLargeFile_ThrowsArgumentException()
        {
            // Arrange
            var file = CreateMockFile("large.txt", "content", 11 * 1024 * 1024); // 11MB

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _validationService.ValidateFile(file));
            Assert.Contains("File size exceeds maximum allowed", exception.Message);
        }

        [Fact]
        public void ValidateFile_WithInvalidExtension_ThrowsArgumentException()
        {
            // Arrange
            var file = CreateMockFile("test.exe", "content");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _validationService.ValidateFile(file));
            Assert.Contains("File type not allowed", exception.Message);
        }

        [Theory]
        [InlineData("test.txt")]
        [InlineData("document.doc")]
        [InlineData("document.docx")]
        [InlineData("document.pdf")]
        [InlineData("document.rtf")]
        public void ValidateFile_WithValidExtensions_DoesNotThrow(string fileName)
        {
            // Arrange
            var file = CreateMockFile(fileName, "content");

            // Act & Assert
            var exception = Record.Exception(() => _validationService.ValidateFile(file));
            Assert.Null(exception);
        }

        [Fact]
        public void ValidateFile_WithValidFile_DoesNotThrow()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Valid content");

            // Act & Assert
            var exception = Record.Exception(() => _validationService.ValidateFile(file));
            Assert.Null(exception);
        }

        [Fact]
        public void ValidateFileId_WithNullOrEmptyId_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _validationService.ValidateFileId(null));
            Assert.Throws<ArgumentException>(() => _validationService.ValidateFileId(""));
            Assert.Throws<ArgumentException>(() => _validationService.ValidateFileId("   "));
        }

        [Fact]
        public void ValidateFileId_WithInvalidGuid_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _validationService.ValidateFileId("invalid-guid"));
            Assert.Contains("Invalid file ID format", exception.Message);
        }

        [Fact]
        public void ValidateFileId_WithValidGuid_DoesNotThrow()
        {
            // Arrange
            var validGuid = Guid.NewGuid().ToString();

            // Act & Assert
            var exception = Record.Exception(() => _validationService.ValidateFileId(validGuid));
            Assert.Null(exception);
        }

        [Theory]
        [InlineData("test.TXT")]
        [InlineData("TEST.txt")]
        [InlineData("Document.PDF")]
        public void ValidateFile_WithMixedCaseExtensions_DoesNotThrow(string fileName)
        {
            // Arrange
            var file = CreateMockFile(fileName, "content");

            // Act & Assert
            var exception = Record.Exception(() => _validationService.ValidateFile(file));
            Assert.Null(exception);
        }

        [Fact]
        public void ValidateFile_WithFileNameContainingSpaces_DoesNotThrow()
        {
            // Arrange
            var file = CreateMockFile("my document.txt", "content");

            // Act & Assert
            var exception = Record.Exception(() => _validationService.ValidateFile(file));
            Assert.Null(exception);
        }

        [Fact]
        public void ValidateFile_WithUnicodeFileName_DoesNotThrow()
        {
            // Arrange
            var file = CreateMockFile("тест файл.txt", "content");

            // Act & Assert
            var exception = Record.Exception(() => _validationService.ValidateFile(file));
            Assert.Null(exception);
        }

        private IFormFile CreateMockFile(string fileName, string content, long? customLength = null)
        {
            var mock = new Mock<IFormFile>();
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(contentBytes);
            
            mock.Setup(f => f.FileName).Returns(fileName);
            mock.Setup(f => f.Length).Returns(customLength ?? contentBytes.Length);
            mock.Setup(f => f.OpenReadStream()).Returns(stream);
            mock.Setup(f => f.ContentType).Returns("text/plain");
            
            return mock.Object;
        }
    }
} 