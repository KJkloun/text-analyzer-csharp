using System;
using System.Collections.Generic;

namespace FileStoringService.Models
{
    /// <summary>
    /// Представляет метаданные файла в системе
    /// </summary>
    public class FileMetadata
    {
        /// <summary>
        /// Уникальный идентификатор файла
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Имя файла в системе хранения
        /// </summary>
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// Оригинальное имя загруженного файла
        /// </summary>
        public string OriginalName { get; set; } = string.Empty;
        
        /// <summary>
        /// MIME-тип содержимого файла
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
        
        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Дата и время загрузки файла
        /// </summary>
        public DateTime UploadDate { get; set; }
        
        /// <summary>
        /// Хеш-сумма содержимого файла для проверки на плагиат
        /// </summary>
        public string Hash { get; set; } = string.Empty;
        
        /// <summary>
        /// Идентификатор файла-дубликата, если текущий файл является дубликатом
        /// </summary>
        public string? DuplicateOf { get; set; }
    }

    /// <summary>
    /// Представляет статистику анализа файла
    /// </summary>
    public class FileStatistics
    {
        /// <summary>
        /// Количество абзацев в файле
        /// </summary>
        public int Paragraphs { get; set; }
        
        /// <summary>
        /// Количество слов в файле
        /// </summary>
        public int Words { get; set; }
        
        /// <summary>
        /// Количество символов в файле
        /// </summary>
        public int Chars { get; set; }
    }

    /// <summary>
    /// Представляет ответ на запрос загрузки файла
    /// </summary>
    public class FileUploadResponse
    {
        /// <summary>
        /// Уникальный идентификатор загруженного файла
        /// </summary>
        public string FileId { get; set; } = string.Empty;
        
        /// <summary>
        /// Имя загруженного файла
        /// </summary>
        public string Filename { get; set; } = string.Empty;
        
        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Флаг, указывающий, является ли файл дубликатом существующего файла
        /// </summary>
        public bool Duplicate { get; set; }
        
        /// <summary>
        /// Идентификатор оригинального файла, если текущий файл является дубликатом
        /// </summary>
        public string? DuplicateOf { get; set; }
        
        /// <summary>
        /// Статистика анализа файла
        /// </summary>
        public FileStatistics Stats { get; set; } = new FileStatistics();
    }

    /// <summary>
    /// Представляет ответ на запрос списка файлов
    /// </summary>
    public class FileListResponse
    {
        /// <summary>
        /// Список информации о файлах
        /// </summary>
        public List<FileInfo> Files { get; set; } = new List<FileInfo>();
    }

    /// <summary>
    /// Представляет краткую информацию о файле
    /// </summary>
    public class FileInfo
    {
        /// <summary>
        /// Уникальный идентификатор файла
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Имя файла
        /// </summary>
        public string Filename { get; set; } = string.Empty;
        
        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Дата и время загрузки файла
        /// </summary>
        public DateTime UploadDate { get; set; }
        
        /// <summary>
        /// Флаг, указывающий, является ли файл дубликатом
        /// </summary>
        public bool Duplicate { get; set; }
    }
}
