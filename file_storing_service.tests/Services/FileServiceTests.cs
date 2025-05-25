using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileStoringService.Services;
using FileStoringService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace FileStoringService.Tests.Services
{
    public class FileServiceTests
    {
        private readonly Mock<ILogger<FileService>> _loggerMock;
        private readonly IFileService _fileService;

        public FileServiceTests()
        {
            _loggerMock = new Mock<ILogger<FileService>>();
            var configMock = new Mock<IConfiguration>();
            // Use unique directory for each test run to avoid conflicts
            var testDir = Path.Combine(Directory.GetCurrentDirectory(), "test_uploads", Guid.NewGuid().ToString());
            configMock.Setup(c => c["UploadDir"]).Returns(testDir);
            _fileService = new FileService(configMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task UploadFileAsync_WithValidFile_ReturnsFileUploadResult()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "This is test content for file upload");

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.FileId));
            Assert.Equal("test.txt", result.FileName);
            Assert.False(result.Duplicate);
            Assert.NotNull(result.Stats);
        }

        [Fact]
        public async Task UploadFileAsync_WithDuplicateFile_ReturnsNoDuplicate()
        {
            // Arrange
            var content = "This is identical content for testing duplicates";
            var file1 = CreateMockFile("test1.txt", content);
            var file2 = CreateMockFile("test2.txt", content);

            // Act
            var result1 = await _fileService.UploadFileAsync(file1);
            var result2 = await _fileService.UploadFileAsync(file2);

            // Assert
            Assert.False(result1.Duplicate);
            Assert.True(result2.Duplicate);
            Assert.Equal(result1.FileId, result2.DuplicateOf);
        }

        [Fact]
        public async Task UploadFileAsync_WithEmptyFile_ThrowsArgumentException()
        {
            // Arrange
            var file = CreateMockFile("empty.txt", "");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _fileService.UploadFileAsync(file));
        }

        [Fact]
        public async Task GetFileAsync_WithValidFileId_ReturnsFileContent()
        {
            // Arrange
            var content = "This is test content";
            var file = CreateMockFile("test.txt", content);
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            var (fileStream, contentType, fileName) = await _fileService.GetFileAsync(uploadResult.FileId);

            // Assert
            Assert.NotNull(fileStream);
            Assert.Equal("text/plain", contentType);
            Assert.Equal("test.txt", fileName);

            var reader = new StreamReader(fileStream);
            var retrievedContent = await reader.ReadToEndAsync();
            Assert.Equal(content, retrievedContent);
        }

        [Fact]
        public async Task GetFileAsync_WithInvalidFileId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var invalidFileId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _fileService.GetFileAsync(invalidFileId));
        }

        [Fact]
        public async Task DeleteFileAsync_WithValidFileId_RemovesFile()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Content to be deleted");
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            await _fileService.DeleteFileAsync(uploadResult.FileId);

            // Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _fileService.GetFileAsync(uploadResult.FileId));
        }

        [Fact]
        public async Task DeleteFileAsync_WithInvalidFileId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var invalidFileId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _fileService.DeleteFileAsync(invalidFileId));
        }

        [Fact]
        public async Task GetAllFilesAsync_WhenFilesExist_ReturnsAllFiles()
        {
            // Arrange
            var file1 = CreateMockFile("file1.txt", "Content 1");
            var file2 = CreateMockFile("file2.txt", "Content 2");
            
            await _fileService.UploadFileAsync(file1);
            await _fileService.UploadFileAsync(file2);

            // Act
            var result = await _fileService.GetAllFilesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetAllFilesAsync_WhenNoFiles_ReturnsEmptyList()
        {
            // Act
            var result = await _fileService.GetAllFilesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFileMetadataAsync_WithValidFileId_ReturnsMetadata()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Test content for metadata");
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            var metadata = await _fileService.GetFileMetadataAsync(uploadResult.FileId);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(uploadResult.FileId, metadata.Id);
            Assert.Equal($"{uploadResult.FileId}.txt", metadata.FileName);
            Assert.Equal("test.txt", metadata.OriginalName);
            Assert.True(string.IsNullOrEmpty(metadata.DuplicateOf));
        }

        [Fact]
        public async Task GetFileMetadataAsync_WithInvalidFileId_ThrowsKeyNotFoundException()
        {
            // Arrange
            var invalidFileId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _fileService.GetFileMetadataAsync(invalidFileId));
        }

        [Fact]
        public async Task FileExistsAsync_WithValidFileId_ReturnsTrue()
        {
            // Arrange
            var file = CreateMockFile("test.txt", "Test content");
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            var exists = await _fileService.FileExistsAsync(uploadResult.FileId);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task FileExistsAsync_WithInvalidFileId_ReturnsFalse()
        {
            // Act
            var exists = await _fileService.FileExistsAsync("non-existent-id");

            // Assert
            Assert.False(exists);
        }

        [Theory]
        [InlineData("Simple text", 1, 2, 11)]
        [InlineData("Line 1\n\nLine 2", 2, 4, 14)]
        [InlineData("Word1 Word2 Word3", 1, 3, 17)]
        public async Task UploadFileAsync_CalculatesStatisticsCorrectly(
            string content, int expectedParagraphs, int expectedWords, int expectedChars)
        {
            // Arrange
            var file = CreateMockFile("test.txt", content);

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.Equal(expectedParagraphs, result.Stats.Paragraphs);
            Assert.Equal(expectedWords, result.Stats.Words);
            Assert.Equal(expectedChars, result.Stats.Chars);
        }

        [Fact]
        public async Task UploadFileAsync_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var content = "Тест с русскими символами! @#$%^&*()";
            var file = CreateMockFile("test.txt", content);

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Stats.Words > 0);
            Assert.True(result.Stats.Chars > 0);
        }

        [Fact]
        public async Task GetFileContentAsync_WithValidFileId_ReturnsContent()
        {
            // Arrange
            var content = "Test file content for retrieval";
            var file = CreateMockFile("test.txt", content);
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            var retrievedContent = await _fileService.GetFileContentAsync(uploadResult.FileId);

            // Assert
            Assert.Equal(content, retrievedContent);
        }

        [Fact]
        public async Task GetFileContentAsync_WithInvalidFileId_ThrowsKeyNotFoundException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _fileService.GetFileContentAsync("non-existent-id"));
        }

        private IFormFile CreateMockFile(string fileName, string content)
        {
            var fileMock = new Mock<IFormFile>();
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(contentBytes);

            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(contentBytes.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
            fileMock.Setup(f => f.ContentType).Returns("text/plain");
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken token) =>
                {
                    stream.Position = 0;
                    return stream.CopyToAsync(target, token);
                });

            return fileMock.Object;
        }
    }
} 