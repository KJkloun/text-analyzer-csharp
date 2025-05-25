using System.Threading.Tasks;
using FileAnalysisService.Models;

namespace FileAnalysisService.Services
{
    public interface IPlagiarismService
    {
        Task<PlagiarismResult> CheckPlagiarism(string fileId);
        Task<PlagiarismResult> CheckPlagiarismAsync(string fileId);
        Task<DuplicateCheckResult> CheckForDuplicateAsync(string fileId, string fileContent);
        Task<ComparisonResult> CompareFilesAsync(string fileId1, string fileId2);
        void RemoveFromDuplicateCache(string fileId);
    }
    
    public class DuplicateCheckResult
    {
        public bool IsDuplicate { get; set; }
        public string DuplicateFileId { get; set; } = string.Empty;
    }
} 