using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace FileStoringService.Services.Validation
{
    public interface IFileValidationService
    {
        (bool IsValid, string ErrorMessage) ValidateFile(IFormFile file);
        (bool IsValid, string ErrorMessage) ValidateFileId(string fileId);
    }

    public class FileValidationService : IFileValidationService
    {
        private readonly string[] _allowedExtensions = { ".txt" };
        private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

        public (bool IsValid, string ErrorMessage) ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "File is empty or not provided");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return (false, $"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024}MB");
            }

            var extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                return (false, $"File type not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}");
            }

            return (true, string.Empty);
        }

        public (bool IsValid, string ErrorMessage) ValidateFileId(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return (false, "File ID cannot be empty");
            }

            if (!Guid.TryParse(fileId, out _))
            {
                return (false, "Invalid file ID format");
            }

            return (true, string.Empty);
        }
    }
} 