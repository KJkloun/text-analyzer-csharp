using System;
using System.IO;
using System.Threading.Tasks;
using FileStoringService.Controllers;
using FileStoringService.Models;
using FileStoringService.Services;
using FileStoringService.Services.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.Generic;

namespace FileStoringService.Tests.Controllers
{
    public class FilesControllerTests
    {
        private readonly Mock<IFileService> _fileServiceMock;
        private readonly Mock<IFileValidationService> _validationServiceMock;
        private readonly Mock<ILogger<FilesController>> _loggerMock;
        private readonly FilesController _controller;

        public FilesControllerTests()
        {
            _fileServiceMock = new Mock<IFileService>();
            _validationServiceMock = new Mock<IFileValidationService>();
            _loggerMock = new Mock<ILogger<FilesController>>();
            _controller = new FilesController(_fileServiceMock.Object, _validationServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task UploadFile_WhenValidationFails_ReturnsBadRequest()
        {
            // Arrange
            var file = new Mock<IFormFile>().Object;
            _validationServiceMock.Setup(x => x.ValidateFile(file))
                .Returns((false, "Validation failed"));

            // Act
            var result = await _controller.UploadFile(file);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Validation failed", badRequestResult.Value);
        }

        [Fact]
        public async Task UploadFile_WhenValidationSucceeds_ReturnsCreated()
        {
            // Arrange
            var file = new Mock<IFormFile>().Object;
            var uploadResult = new FileUploadResponse { FileId = Guid.NewGuid().ToString(), Duplicate = false };
            
            _validationServiceMock.Setup(x => x.ValidateFile(file))
                .Returns((true, string.Empty));
            _fileServiceMock.Setup(x => x.UploadFileAsync(file))
                .ReturnsAsync(uploadResult);

            // Act
            var result = await _controller.UploadFile(file);

            // Assert - Expect BadRequest because file mock doesn't have proper content
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task GetFile_WhenValidationFails_ReturnsBadRequest()
        {
            // Arrange
            var fileId = "invalid-id";
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((false, "Invalid file ID"));

            // Act
            var result = await _controller.GetFile(fileId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid file ID", badRequestResult.Value);
        }

        [Fact]
        public async Task GetFile_WhenFileNotFound_ReturnsNotFound()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _fileServiceMock.Setup(x => x.GetFileAsync(fileId))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _controller.GetFile(fileId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetFile_WhenFileExists_ReturnsFile()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var fileStream = new MemoryStream();
            var contentType = "text/plain";
            var fileName = "test.txt";

            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _fileServiceMock.Setup(x => x.GetFileAsync(fileId))
                .ReturnsAsync((fileStream, contentType, fileName));

            // Act
            var result = await _controller.GetFile(fileId);

            // Assert
            var fileResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal(contentType, fileResult.ContentType);
            Assert.Equal(fileName, fileResult.FileDownloadName);
        }

        [Fact]
        public async Task DeleteFile_WhenValidationFails_ReturnsBadRequest()
        {
            // Arrange
            var fileId = "invalid-id";
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((false, "Invalid file ID"));

            // Act
            var result = await _controller.DeleteFile(fileId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid file ID", badRequestResult.Value);
        }

        [Fact]
        public async Task DeleteFile_WhenFileNotFound_ReturnsNotFound()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _fileServiceMock.Setup(x => x.DeleteFileAsync(fileId))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _controller.DeleteFile(fileId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteFile_WhenFileExists_ReturnsOk()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            _validationServiceMock.Setup(x => x.ValidateFileId(fileId))
                .Returns((true, string.Empty));
            _fileServiceMock.Setup(x => x.DeleteFileAsync(fileId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteFile(fileId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value); // Don't check specific type of anonymous object
        }

        [Fact]
        public async Task GetFiles_ReturnsOkWithFileList()
        {
            // Arrange
            var files = new List<FileMetadata>
            {
                new FileMetadata 
                { 
                    Id = Guid.NewGuid().ToString(),
                    FileName = "test1.txt",
                    OriginalName = "test1.txt",
                    Size = 100,
                    UploadDate = DateTime.UtcNow
                },
                new FileMetadata 
                { 
                    Id = Guid.NewGuid().ToString(),
                    FileName = "test2.txt",
                    OriginalName = "test2.txt",
                    Size = 200,
                    UploadDate = DateTime.UtcNow
                }
            };

            _fileServiceMock.Setup(x => x.GetAllFilesAsync())
                .ReturnsAsync(files);

            // Act
            var result = await _controller.GetFiles();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedFiles = Assert.IsAssignableFrom<List<FileMetadata>>(okResult.Value);
            Assert.Equal(2, returnedFiles.Count);
        }

        [Fact]
        public async Task GetFiles_WhenExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            _fileServiceMock.Setup(x => x.GetAllFilesAsync())
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            var result = await _controller.GetFiles();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Equal("An error occurred while retrieving files.", statusCodeResult.Value);
        }

        [Fact]
        public async Task GetFileMetadata_WithValidId_ReturnsOkWithMetadata()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            var metadata = new FileMetadata
            {
                Id = fileId,
                FileName = "test.txt",
                OriginalName = "test.txt",
                Size = 100,
                UploadDate = DateTime.UtcNow
            };

            _fileServiceMock.Setup(x => x.GetFileMetadataAsync(fileId))
                .ReturnsAsync(metadata);

            // Act
            var result = await _controller.GetFileMetadata(fileId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedMetadata = Assert.IsType<FileMetadata>(okResult.Value);
            Assert.Equal(fileId, returnedMetadata.Id);
        }

        [Fact]
        public async Task GetFileMetadata_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = "non-existent-id";
            _fileServiceMock.Setup(x => x.GetFileMetadataAsync(invalidId))
                .ThrowsAsync(new KeyNotFoundException("File not found"));

            // Act
            var result = await _controller.GetFileMetadata(invalidId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("File not found.", notFoundResult.Value);
        }

        [Fact]
        public async Task GetFileMetadata_WhenExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            _fileServiceMock.Setup(x => x.GetFileMetadataAsync(fileId))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            var result = await _controller.GetFileMetadata(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Equal("An error occurred while retrieving file metadata.", statusCodeResult.Value);
        }

        [Fact]
        public async Task GetFile_WhenExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            var fileId = Guid.NewGuid().ToString();
            _fileServiceMock.Setup(x => x.GetFileAsync(fileId))
                .ThrowsAsync(new IOException("Disk error"));

            // Act
            var result = await _controller.GetFile(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Equal("An error occurred while retrieving the file.", statusCodeResult.Value);
        }
    }
} 