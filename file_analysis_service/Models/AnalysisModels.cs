using System;
using System.Collections.Generic;

namespace FileAnalysisService.Models
{
    /// <summary>
    /// Результат анализа файла
    /// </summary>
    public class AnalysisResult
    {
        /// <summary>
        /// Идентификатор результата анализа
        /// </summary>
        public string Id { get; set; } = null!;
        
        /// <summary>
        /// Идентификатор анализируемого файла
        /// </summary>
        public string FileId { get; set; } = null!;
        
        /// <summary>
        /// Тип анализа (статистика, плагиат и т.д.)
        /// </summary>
        public string Type { get; set; } = null!;
        
        /// <summary>
        /// Результаты анализа
        /// </summary>
        public object Results { get; set; } = null!;
    }

    /// <summary>
    /// Статистика файла (для совместимости с тестами)
    /// </summary>
    public class FileStatistics
    {
        /// <summary>
        /// Количество абзацев
        /// </summary>
        public int Paragraphs { get; set; }
        
        /// <summary>
        /// Количество слов
        /// </summary>
        public int Words { get; set; }
        
        /// <summary>
        /// Количество символов (с пробелами)
        /// </summary>
        public int Chars { get; set; }
        
        /// <summary>
        /// Количество символов (без пробелов)
        /// </summary>
        public int CharsNoSpaces { get; set; }
    }

    /// <summary>
    /// Результат подсчета статистики
    /// </summary>
    public class StatisticsResult
    {
        /// <summary>
        /// Количество абзацев
        /// </summary>
        public int Paragraphs { get; set; }
        
        /// <summary>
        /// Количество слов
        /// </summary>
        public int Words { get; set; }
        
        /// <summary>
        /// Количество символов (с пробелами)
        /// </summary>
        public int Chars { get; set; }
        
        /// <summary>
        /// Количество символов (без пробелов)
        /// </summary>
        public int CharsNoSpaces { get; set; }
    }

    /// <summary>
    /// Результат проверки на плагиат
    /// </summary>
    public class PlagiarismResult
    {
        /// <summary>
        /// Является ли файл дубликатом
        /// </summary>
        public bool IsDuplicate { get; set; }
        
        /// <summary>
        /// Идентификатор оригинального файла
        /// </summary>
        public string? DuplicateOf { get; set; }
    }

    /// <summary>
    /// Результат генерации облака слов
    /// </summary>
    public class WordCloudResult
    {
        /// <summary>
        /// URL к изображению облака слов
        /// </summary>
        public string WordCloudUrl { get; set; } = null!;
    }

    /// <summary>
    /// Метаданные файла
    /// </summary>
    public class FileMetadata
    {
        /// <summary>
        /// Идентификатор файла
        /// </summary>
        public string Id { get; set; } = null!;
        
        /// <summary>
        /// Имя файла в хранилище
        /// </summary>
        public string FileName { get; set; } = null!;
        
        /// <summary>
        /// Оригинальное имя файла
        /// </summary>
        public string OriginalName { get; set; } = null!;
        
        /// <summary>
        /// Тип содержимого файла
        /// </summary>
        public string ContentType { get; set; } = null!;
        
        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Дата загрузки файла
        /// </summary>
        public DateTime UploadDate { get; set; }
        
        /// <summary>
        /// Хеш содержимого файла
        /// </summary>
        public string Hash { get; set; } = null!;
        
        /// <summary>
        /// Идентификатор оригинального файла (если текущий файл - дубликат)
        /// </summary>
        public string? DuplicateOf { get; set; }
    }
}
