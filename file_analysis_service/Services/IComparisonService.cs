using System.Threading.Tasks;

namespace FileAnalysisService.Services
{
    public interface IComparisonService
    {
        Task<ComparisonResult> CompareFilesAsync(string content1, string content2);
    }

    public class ComparisonResult
    {
        public bool Identical { get; set; }
        public double JaccardSimilarity { get; set; }
    }
} 