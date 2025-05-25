using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FileAnalysisService.Services;
using System.Net;
using System.Net.Http;
using System.Threading;
using Moq;
using Moq.Protected;

namespace FileAnalysisService.Tests.Services
{
    public class ComparisonServiceTests
    {
        private readonly IComparisonService _comparisonService;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public ComparisonServiceTests()
        {
            _comparisonService = new ComparisonService();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        }

        [Fact]
        public async Task CompareFiles_WhenFilesAreIdentical_ReturnsIdentical()
        {
            // Arrange
            var content1 = "This is identical content for testing";
            var content2 = "This is identical content for testing";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.True(result.Identical);
            Assert.Equal(1.0, result.JaccardSimilarity, 2); // Точность до 2 знаков
        }

        [Fact]
        public async Task CompareFiles_WhenFilesAreCompletelyDifferent_ReturnsLowSimilarity()
        {
            // Arrange
            var content1 = "apple banana cherry";
            var content2 = "dog elephant fox";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.False(result.Identical);
            Assert.Equal(0.0, result.JaccardSimilarity); // Нет общих слов
        }

        [Fact]
        public async Task CompareFiles_WhenFilesHavePartialSimilarity_ReturnsCorrectSimilarity()
        {
            // Arrange
            var content1 = "apple banana cherry date";  // 4 слова
            var content2 = "apple banana grape lemon"; // 4 слова, 2 общих

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.False(result.Identical);
            // Jaccard = |пересечение| / |объединение| = 2 / 6 = 0.333...
            Assert.Equal(0.33, result.JaccardSimilarity, 2);
        }

        [Fact]
        public async Task CompareFiles_WhenOneFileIsEmpty_ReturnsZeroSimilarity()
        {
            // Arrange
            var content1 = "This file has content";
            var content2 = "";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.False(result.Identical);
            Assert.Equal(0.0, result.JaccardSimilarity);
        }

        [Fact]
        public async Task CompareFiles_WhenBothFilesAreEmpty_ReturnsIdentical()
        {
            // Arrange
            var content1 = "";
            var content2 = "";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.True(result.Identical);
            Assert.Equal(1.0, result.JaccardSimilarity);
        }

        [Fact]
        public async Task CompareFiles_WhenFilesHaveDifferentCasing_IgnoresCase()
        {
            // Arrange
            var content1 = "Apple Banana Cherry";
            var content2 = "apple banana cherry";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.True(result.Identical);
            Assert.Equal(1.0, result.JaccardSimilarity);
        }

        [Fact]
        public async Task CompareFiles_WhenFilesHavePunctuation_RemovesPunctuationCorrectly()
        {
            // Arrange
            var content1 = "Hello, world! How are you?";
            var content2 = "Hello world How are you";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.True(result.Identical);
            Assert.Equal(1.0, result.JaccardSimilarity);
        }

        [Fact]
        public async Task CompareFiles_WithDuplicateWordsInFile_CountsUniqueWords()
        {
            // Arrange
            var content1 = "apple apple banana banana";
            var content2 = "apple banana";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.True(result.Identical);
            Assert.Equal(1.0, result.JaccardSimilarity);
        }

        [Fact]
        public async Task CompareFiles_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var content1 = "тест проверка кириллица";
            var content2 = "тест проверка латиница";

            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.False(result.Identical);
            // 2 общих слова из 4 уникальных = 2/4 = 0.5
            Assert.Equal(0.5, result.JaccardSimilarity, 2);
        }

        [Theory]
        [InlineData("word", "word", 1.0)]
        [InlineData("word1 word2", "word2 word3", 0.33)]
        [InlineData("a b c", "c d e", 0.2)]
        public async Task CompareFiles_WithVariousInputs_ReturnsExpectedSimilarity(
            string content1, string content2, double expectedSimilarity)
        {
            // Act
            var result = await _comparisonService.CompareFilesAsync(content1, content2);

            // Assert
            Assert.Equal(expectedSimilarity, result.JaccardSimilarity, 2);
        }

        [Fact]
        public async Task CompareFiles_WithNullInputs_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _comparisonService.CompareFilesAsync(null, "content"));
            
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _comparisonService.CompareFilesAsync("content", null));
        }

        [Fact]
        public async Task CompareFiles_WithLongContent_HandlesEfficiently()
        {
            // Arrange
            var words = Enumerable.Range(1, 1000).Select(i => $"word{i}").ToArray();
            var content1 = string.Join(" ", words);
            var content2 = string.Join(" ", words.Take(500).Concat(words.Skip(750))); // 250 уникальных

            // Act
            var startTime = DateTime.UtcNow;
            var result = await _comparisonService.CompareFilesAsync(content1, content2);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.True(duration.TotalSeconds < 1); // Должно выполняться быстро
            Assert.False(result.Identical);
            Assert.True(result.JaccardSimilarity > 0 && result.JaccardSimilarity < 1);
        }

        [Fact]
        public async Task CompareFilesAsync_WithIdenticalFiles_ReturnsHighSimilarity()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            var content = "This is identical content for both files";
            
            SetupFileContent(fileId1, content);
            SetupFileContent(fileId2, content);

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity >= 0.9); // Should be very high similarity
        }

        [Fact]
        public async Task CompareFilesAsync_WithCompletelyDifferentFiles_ReturnsLowSimilarity()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileContent(fileId1, "This is the first file with unique content");
            SetupFileContent(fileId2, "Completely different text without any similarity");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity < 0.5); // Should be low similarity
        }

        [Fact]
        public async Task CompareFilesAsync_WithPartialSimilarity_ReturnsModerateScore()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileContent(fileId1, "The quick brown fox jumps over the lazy dog");
            SetupFileContent(fileId2, "The quick brown cat jumps over the lazy mouse");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity > 0.3 && result.Similarity < 0.9);
        }

        [Fact]
        public async Task CompareFilesAsync_WithEmptyFiles_ReturnsHighSimilarity()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileContent(fileId1, "");
            SetupFileContent(fileId2, "");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity >= 0.9); // Empty files should be considered similar
        }

        [Fact]
        public async Task CompareFilesAsync_WithOneEmptyFile_ReturnsLowSimilarity()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileContent(fileId1, "This file has content");
            SetupFileContent(fileId2, "");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity < 0.5);
        }

        [Fact]
        public async Task CompareFilesAsync_WithSameFileId_ReturnsMaxSimilarity()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            SetupFileContent(fileId, "Some content");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId, fileId);

            // Assert
            Assert.Equal(1.0, result.Similarity, 2); // Should be exactly 1.0
        }

        [Fact]
        public async Task CompareFilesAsync_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileContent(fileId1, "Special chars: !@#$%^&*()_+{}|:<>?");
            SetupFileContent(fileId2, "Special chars: !@#$%^&*()_+{}|:<>?");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity >= 0.9);
        }

        [Fact]
        public async Task CompareFilesAsync_WithUnicodeContent_HandlesCorrectly()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileContent(fileId1, "Привет мир! 你好世界! مرحبا بالعالم!");
            SetupFileContent(fileId2, "Привет мир! 你好世界! مرحبا بالعالم!");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity >= 0.9);
        }

        [Fact]
        public async Task CompareFilesAsync_WithLargeFiles_HandlesCorrectly()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            var largeContent1 = string.Join(" ", Enumerable.Repeat("word", 1000));
            var largeContent2 = string.Join(" ", Enumerable.Repeat("word", 1000));
            
            SetupFileContent(fileId1, largeContent1);
            SetupFileContent(fileId2, largeContent2);

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity >= 0.9);
        }

        [Fact]
        public async Task CompareFilesAsync_WithFileNotFound_ThrowsException()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileNotFound(fileId1);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => 
                _comparisonService.CompareFilesAsync(fileId1, fileId2));
        }

        [Fact]
        public async Task CompareFilesAsync_WithNullFileId_ThrowsArgumentException()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _comparisonService.CompareFilesAsync(null, fileId));
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _comparisonService.CompareFilesAsync(fileId, null));
        }

        [Fact]
        public async Task CompareFilesAsync_WithCaseDifferences_IgnoresCase()
        {
            // Arrange
            var fileId1 = Guid.NewGuid().ToString();
            var fileId2 = Guid.NewGuid().ToString();
            
            SetupFileContent(fileId1, "THE QUICK BROWN FOX");
            SetupFileContent(fileId2, "the quick brown fox");

            // Act
            var result = await _comparisonService.CompareFilesAsync(fileId1, fileId2);

            // Assert
            Assert.True(result.Similarity >= 0.8); // Should be high despite case differences
        }

        private void SetupFileNotFound(string fileId)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains(fileId)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });
        }

        private void SetupFileContent(string fileId, string content)
        {
            // Implementation of SetupFileContent method
        }
    }
} 