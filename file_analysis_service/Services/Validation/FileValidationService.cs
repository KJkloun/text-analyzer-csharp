using System;

namespace FileAnalysisService.Services.Validation
{
    public interface IFileValidationService
    {
        (bool IsValid, string ErrorMessage) ValidateFileId(string fileId);
    }

    public class FileValidationService : IFileValidationService
    {
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