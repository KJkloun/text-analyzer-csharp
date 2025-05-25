using System.Threading.Tasks;
using Xunit;
using FileAnalysisService.Services;
using FileAnalysisService.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using System.Text;

namespace FileAnalysisService.Tests.Services
{
    public class StatisticsServiceTests
    {
        private readonly IStatisticsService _statisticsService;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        public StatisticsServiceTests()
        {
            var loggerMock = new Mock<ILogger<StatisticsService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var configMock = new Mock<IConfiguration>();
            _statisticsService = new StatisticsService(loggerMock.Object, _httpClientFactoryMock.Object, configMock.Object);
        }

        [Fact]
        public async Task CalculateStatistics_EmptyText_ReturnsZeroValues()
        {
            // Arrange
            var content = string.Empty;

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(0, result.Paragraphs); // Пустой текст считается нулем абзацев
            Assert.Equal(0, result.Words);
            Assert.Equal(0, result.Chars);
            Assert.Equal(0, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_SingleWord_ReturnsCorrectValues()
        {
            // Arrange
            var content = "Word";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(1, result.Paragraphs);
            Assert.Equal(1, result.Words);
            Assert.Equal(4, result.Chars);
            Assert.Equal(4, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_TextWithSpaces_ReturnsCorrectValues()
        {
            // Arrange
            var content = "This is a test";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(1, result.Paragraphs);
            Assert.Equal(4, result.Words);
            Assert.Equal(14, result.Chars);
            Assert.Equal(11, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_MultipleParagraphs_ReturnsCorrectValues()
        {
            // Arrange
            var content = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(3, result.Paragraphs);
            Assert.Equal(6, result.Words);
            Assert.Equal(55, result.Chars);
            Assert.Equal(44, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_TextWithPunctuation_CountsWords()
        {
            // Arrange
            var content = "Hello, world! This is a test.";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(1, result.Paragraphs);
            Assert.Equal(6, result.Words);
            Assert.Equal(31, result.Chars);
            Assert.Equal(26, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_TextWithNewlinesAndTabs_RemovesThemForCharsNoSpaces()
        {
            // Arrange
            var content = "Line 1\nLine 2\tTab";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(1, result.Paragraphs);
            Assert.Equal(5, result.Words);
            Assert.Equal(16, result.Chars);
            Assert.Equal(14, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_ComplexText_ReturnsCorrectValues()
        {
            // Arrange
            var content = "Hello world!\n\nThis is a test.\nWith multiple lines.\n\nAnd paragraphs.";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(3, result.Paragraphs); // 3 paragraphs separated by double newlines
            Assert.Equal(10, result.Words); // Hello world This is a test With multiple lines And paragraphs
            Assert.Equal(content.Length, result.Chars);
            Assert.Equal(content.Replace(" ", "").Replace("\n", "").Replace("\r", "").Length, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_WithSpecialCharacters_ReturnsCorrectValues()
        {
            // Arrange
            var content = "Test: 123-456, (test@example.com) & more!";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(1, result.Paragraphs); // Single paragraph
            Assert.Equal(5, result.Words); // Test 123-456 test@example.com more
            Assert.Equal(content.Length, result.Chars);
            Assert.Equal(content.Replace(" ", "").Length, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_OnlyWhitespace_ReturnsZeroValues()
        {
            // Arrange
            var content = "   \n\n\t  \r\n  ";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(0, result.Paragraphs);
            Assert.Equal(0, result.Words);
            Assert.Equal(content.Length, result.Chars);
            Assert.Equal(0, result.CharsNoSpaces); // Only whitespace removed
        }

        [Fact]
        public async Task CalculateStatistics_MixedLanguages_ReturnsCorrectValues()
        {
            // Arrange
            var content = "Hello мир! Testing тест.";

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(1, result.Paragraphs);
            Assert.Equal(4, result.Words); // Hello мир Testing тест
            Assert.Equal(content.Length, result.Chars);
            Assert.Equal(content.Replace(" ", "").Length, result.CharsNoSpaces);
        }

        [Fact]
        public async Task CalculateStatistics_LongText_HandlesCorrectly()
        {
            // Arrange
            var content = string.Join(" ", Enumerable.Repeat("word", 1000));

            // Act
            var result = await _statisticsService.CalculateStatisticsFromContentAsync(content);

            // Assert
            Assert.Equal(1, result.Paragraphs);
            Assert.Equal(1000, result.Words);
            Assert.Equal(content.Length, result.Chars);
            Assert.Equal(content.Replace(" ", "").Length, result.CharsNoSpaces);
        }
    }
} 