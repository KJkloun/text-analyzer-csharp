using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileAnalysisService.Services
{
    public class ComparisonService : IComparisonService
    {
        public async Task<ComparisonResult> CompareFilesAsync(string content1, string content2)
        {
            if (content1 == null)
                throw new ArgumentNullException(nameof(content1));
            
            if (content2 == null)
                throw new ArgumentNullException(nameof(content2));

            // Если оба файла пустые, они идентичны
            if (string.IsNullOrWhiteSpace(content1) && string.IsNullOrWhiteSpace(content2))
            {
                return new ComparisonResult 
                { 
                    Identical = true, 
                    JaccardSimilarity = 1.0 
                };
            }

            // Если один файл пустой, а другой нет
            if (string.IsNullOrWhiteSpace(content1) || string.IsNullOrWhiteSpace(content2))
            {
                return new ComparisonResult 
                { 
                    Identical = false, 
                    JaccardSimilarity = 0.0 
                };
            }

            // Извлекаем слова из обоих файлов
            var words1 = ExtractWords(content1);
            var words2 = ExtractWords(content2);

            // Вычисляем коэффициент Жаккара
            var jaccardSimilarity = CalculateJaccardSimilarity(words1, words2);
            var identical = Math.Abs(jaccardSimilarity - 1.0) < 0.001; // С учетом погрешности

            return await Task.FromResult(new ComparisonResult
            {
                Identical = identical,
                JaccardSimilarity = Math.Round(jaccardSimilarity, 3)
            });
        }

        private HashSet<string> ExtractWords(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new HashSet<string>();

            // Удаляем знаки препинания и разбиваем на слова
            var cleanContent = Regex.Replace(content, @"[^\w\s]", " ");
            
            // Разбиваем на слова, приводим к нижнему регистру и удаляем пустые
            var words = cleanContent
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.ToLowerInvariant())
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .ToHashSet();

            return words;
        }

        private double CalculateJaccardSimilarity(HashSet<string> words1, HashSet<string> words2)
        {
            if (words1.Count == 0 && words2.Count == 0)
                return 1.0;

            if (words1.Count == 0 || words2.Count == 0)
                return 0.0;

            // Пересечение множеств
            var intersection = words1.Intersect(words2).Count();
            
            // Объединение множеств
            var union = words1.Union(words2).Count();

            return union == 0 ? 0.0 : (double)intersection / union;
        }
    }
} 