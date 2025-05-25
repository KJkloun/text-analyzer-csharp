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
    public class FileServiceExtensiveTests
    {
        private readonly Mock<ILogger<FileService>> _loggerMock;
        private readonly IFileService _fileService;

        public FileServiceExtensiveTests()
        {
            _loggerMock = new Mock<ILogger<FileService>>();
            var configMock = new Mock<IConfiguration>();
            var testDir = Path.Combine(Directory.GetCurrentDirectory(), "test_uploads_extensive", Guid.NewGuid().ToString());
            configMock.Setup(c => c["UploadDir"]).Returns(testDir);
            _fileService = new FileService(configMock.Object, _loggerMock.Object);
        }

        [Theory]
        [InlineData("Simple", 1, 1, 6)]
        [InlineData("One two", 1, 2, 7)]
        [InlineData("One\n\nTwo", 2, 2, 7)]
        [InlineData("A B C D E", 1, 5, 9)]
        [InlineData("First\n\nSecond\n\nThird", 3, 3, 18)]
        public async Task UploadFileAsync_WithVariousTexts_CalculatesStatisticsCorrectly(
            string content, int expectedParagraphs, int expectedWords, int expectedChars)
        {
            // Arrange
            var file = CreateMockFile("test.txt", content);

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedParagraphs, result.Stats.Paragraphs);
            Assert.Equal(expectedWords, result.Stats.Words);
            Assert.Equal(expectedChars, result.Stats.Chars);
        }

        [Fact]
        public async Task UploadFileAsync_WithUnicodeContent_HandlesCorrectly()
        {
            // Arrange
            var content = "Привет мир! 你好世界! مرحبا بالعالم!";
            var file = CreateMockFile("unicode.txt", content);

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Stats.Words > 0);
            Assert.Equal(content.Length, result.Stats.Chars);
        }

        [Fact]
        public async Task UploadFileAsync_WithMixedContent_CalculatesCorrectly()
        {
            // Arrange
            var content = "Line 1\nLine 2\n\nParagraph 2\nMore text\n\nParagraph 3";
            var file = CreateMockFile("mixed.txt", content);

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Stats.Paragraphs);
            Assert.True(result.Stats.Words >= 8);
        }

        [Fact]
        public async Task UploadFileAsync_WithPunctuationAndSpaces_HandlesCorrectly()
        {
            // Arrange
            var content = "Hello, world! How are you today? Fine, thanks.";
            var file = CreateMockFile("punctuation.txt", content);

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Stats.Paragraphs);
            Assert.True(result.Stats.Words >= 8);
            Assert.Equal(content.Length, result.Stats.Chars);
        }

        [Fact]
        public async Task UploadFileAsync_WithOnlyWhitespace_ThrowsException()
        {
            // Arrange
            var content = "   \n\n\t\r\n  ";
            var file = CreateMockFile("whitespace.txt", content);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _fileService.UploadFileAsync(file));
        }

        [Fact]
        public async Task UploadFileAsync_WithLongFileName_HandlesCorrectly()
        {
            // Arrange
            var longName = new string('a', 200) + ".txt";
            var file = CreateMockFile(longName, "Content with very long filename");

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.FileId));
        }

        [Fact]
        public async Task UploadFileAsync_WithSpecialCharactersInFileName_HandlesCorrectly()
        {
            // Arrange
            var fileName = "файл с пробелами и символами №1.txt";
            var file = CreateMockFile(fileName, "Content with special filename");

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(fileName, result.FileName);
        }

        [Fact]
        public async Task UploadFileAsync_WithSameContentDifferentNames_DetectsDuplicates()
        {
            // Arrange
            var content = "Identical content for duplicate detection";
            var file1 = CreateMockFile("file1.txt", content);
            var file2 = CreateMockFile("file2.txt", content);
            var file3 = CreateMockFile("file3.txt", content);

            // Act
            var result1 = await _fileService.UploadFileAsync(file1);
            var result2 = await _fileService.UploadFileAsync(file2);
            var result3 = await _fileService.UploadFileAsync(file3);

            // Assert
            Assert.False(result1.Duplicate);
            Assert.True(result2.Duplicate);
            Assert.True(result3.Duplicate);
            Assert.Equal(result1.FileId, result2.DuplicateOf);
            Assert.Equal(result1.FileId, result3.DuplicateOf);
        }

        [Fact]
        public async Task UploadFileAsync_WithSlightlyDifferentContent_DoesNotDetectDuplicates()
        {
            // Arrange
            var content1 = "This is the first version of content";
            var content2 = "This is the second version of content";
            var file1 = CreateMockFile("file1.txt", content1);
            var file2 = CreateMockFile("file2.txt", content2);

            // Act
            var result1 = await _fileService.UploadFileAsync(file1);
            var result2 = await _fileService.UploadFileAsync(file2);

            // Assert
            Assert.False(result1.Duplicate);
            Assert.False(result2.Duplicate);
            Assert.Null(result2.DuplicateOf);
        }

        [Fact]
        public async Task GetFileAsync_WithMultipleFiles_ReturnsCorrectFile()
        {
            // Arrange
            var content1 = "Content of first file";
            var content2 = "Content of second file";
            var file1 = CreateMockFile("file1.txt", content1);
            var file2 = CreateMockFile("file2.txt", content2);

            var result1 = await _fileService.UploadFileAsync(file1);
            var result2 = await _fileService.UploadFileAsync(file2);

            // Act
            var (stream1, contentType1, fileName1) = await _fileService.GetFileAsync(result1.FileId);
            var (stream2, contentType2, fileName2) = await _fileService.GetFileAsync(result2.FileId);

            // Assert
            var reader1 = new StreamReader(stream1);
            var reader2 = new StreamReader(stream2);
            
            Assert.Equal(content1, await reader1.ReadToEndAsync());
            Assert.Equal(content2, await reader2.ReadToEndAsync());
            Assert.Equal("file1.txt", fileName1);
            Assert.Equal("file2.txt", fileName2);
        }

        [Fact]
        public async Task DeleteFileAsync_WithMultipleFiles_DeletesOnlySpecifiedFile()
        {
            // Arrange
            var file1 = CreateMockFile("file1.txt", "Content 1");
            var file2 = CreateMockFile("file2.txt", "Content 2");
            var file3 = CreateMockFile("file3.txt", "Content 3");

            var result1 = await _fileService.UploadFileAsync(file1);
            var result2 = await _fileService.UploadFileAsync(file2);
            var result3 = await _fileService.UploadFileAsync(file3);

            // Act
            await _fileService.DeleteFileAsync(result2.FileId);

            // Assert
            Assert.True(await _fileService.FileExistsAsync(result1.FileId));
            Assert.False(await _fileService.FileExistsAsync(result2.FileId));
            Assert.True(await _fileService.FileExistsAsync(result3.FileId));
        }

        [Fact]
        public async Task GetAllFilesAsync_WithManyFiles_ReturnsAllFiles()
        {
            // Arrange
            var files = new List<IFormFile>();
            for (int i = 0; i < 10; i++)
            {
                files.Add(CreateMockFile($"file{i}.txt", $"Content {i}"));
            }

            // Upload all files
            foreach (var file in files)
            {
                await _fileService.UploadFileAsync(file);
            }

            // Act
            var result = await _fileService.GetAllFilesAsync();

            // Assert
            Assert.Equal(10, result.Count);
        }

        [Fact]
        public async Task GetFileMetadataAsync_WithUploadedFile_ReturnsCorrectMetadata()
        {
            // Arrange
            var content = "Test content for metadata";
            var file = CreateMockFile("metadata-test.txt", content);
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            var metadata = await _fileService.GetFileMetadataAsync(uploadResult.FileId);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(uploadResult.FileId, metadata.Id);
            Assert.Equal("metadata-test.txt", metadata.OriginalName);
            Assert.Equal(content.Length, metadata.Size);
            Assert.True(metadata.UploadDate <= DateTime.UtcNow);
            Assert.True(metadata.UploadDate >= DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task UploadFileAsync_WithVeryLargeFile_HandlesCorrectly()
        {
            // Arrange
            var largeContent = new string('X', 1_000_000); // 1MB of X's
            var file = CreateMockFile("large.txt", largeContent);

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1_000_000, result.Stats.Chars);
            Assert.Equal(1, result.Stats.Words); // All X's form one "word"
        }

        [Theory]
        [InlineData(".txt")]
        [InlineData(".TXT")]
        [InlineData(".Txt")]
        public async Task UploadFileAsync_WithDifferentCaseExtensions_HandlesCorrectly(string extension)
        {
            // Arrange
            var fileName = "test" + extension;
            var file = CreateMockFile(fileName, "Test content");

            // Act
            var result = await _fileService.UploadFileAsync(file);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(fileName, result.FileName);
        }

        [Fact]
        public async Task FileExistsAsync_WithDeletedFile_ReturnsFalse()
        {
            // Arrange
            var file = CreateMockFile("temp.txt", "Temporary content");
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            await _fileService.DeleteFileAsync(uploadResult.FileId);
            var exists = await _fileService.FileExistsAsync(uploadResult.FileId);

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task GetFileContentAsync_WithUploadedFile_ReturnsExactContent()
        {
            // Arrange
            var originalContent = "Exact content with\nspecial\ncharacters\n\nand formatting!";
            var file = CreateMockFile("exact.txt", originalContent);
            var uploadResult = await _fileService.UploadFileAsync(file);

            // Act
            var retrievedContent = await _fileService.GetFileContentAsync(uploadResult.FileId);

            // Assert
            Assert.Equal(originalContent, retrievedContent);
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