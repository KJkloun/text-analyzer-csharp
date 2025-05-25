using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using FileStoringService.Models;
using FileStoringService.Services;
using Microsoft.Extensions.Logging;
using FileStoringService.Services.Validation;

namespace FileStoringService.Controllers
{
    /// <summary>
    /// Контроллер для управления файлами
    /// </summary>
    [ApiController]
    [Route("files")]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IFileValidationService _validationService;
        private readonly ILogger<FilesController> _logger;

        /// <summary>
        /// Инициализирует новый экземпляр контроллера файлов
        /// </summary>
        /// <param name="fileService">Сервис для работы с файлами</param>
        /// <param name="validationService">Сервис для валидации файлов</param>
        /// <param name="logger">Логгер</param>
        public FilesController(
            IFileService fileService,
            IFileValidationService validationService,
            ILogger<FilesController> logger)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Загружает новый файл
        /// </summary>
        /// <param name="file">Загружаемый файл</param>
        /// <returns>Информация о загруженном файле</returns>
        /// <response code="201">Файл успешно загружен</response>
        /// <response code="400">Некорректный запрос</response>
        /// <response code="500">Внутренняя ошибка сервера</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            var validationResult = _validationService.ValidateFile(file);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning($"File validation failed: {validationResult.ErrorMessage}");
                return BadRequest(validationResult.ErrorMessage);
            }

            _logger.LogInformation("Получен запрос на загрузку файла: {FileName}, размер: {FileSize}", 
                file?.FileName, file?.Length);
            
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("Попытка загрузки пустого файла");
                    return BadRequest(new { error = "Файл пуст" });
                }

                // Проверяем, что файл имеет расширение .txt
                if (!Path.GetExtension(file.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Попытка загрузки файла с неподдерживаемым расширением: {Extension}", 
                        Path.GetExtension(file.FileName));
                    return BadRequest(new { error = "Разрешены только файлы .txt" });
                }

                // Проверяем размер файла (например, не более 10 МБ)
                if (file.Length > 10 * 1024 * 1024)
                {
                    _logger.LogWarning("Попытка загрузки слишком большого файла: {FileSize} байт", file.Length);
                    return BadRequest(new { error = "Размер файла не должен превышать 10 МБ" });
                }

                var result = await _fileService.UploadFileAsync(file);
                
                _logger.LogInformation("Файл успешно загружен: {FileId}, дубликат: {IsDuplicate}", 
                    result.FileId, result.Duplicate);
                
                return StatusCode(StatusCodes.Status201Created, result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка валидации при загрузке файла");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке файла");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Ошибка загрузки файла: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получает список всех файлов
        /// </summary>
        /// <returns>Список файлов</returns>
        /// <response code="200">Список файлов успешно получен</response>
        /// <response code="500">Внутренняя ошибка сервера</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFiles()
        {
            _logger.LogInformation("Получен запрос на список файлов");
            
            try
            {
                var result = await _fileService.GetFilesAsync();
                
                _logger.LogInformation("Список файлов успешно получен, количество: {FileCount}", 
                    result.Files.Count);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка файлов");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Ошибка получения списка файлов: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получает файл по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Содержимое файла</returns>
        /// <response code="200">Файл успешно получен</response>
        /// <response code="404">Файл не найден</response>
        /// <response code="500">Внутренняя ошибка сервера</response>
        [HttpGet("{fileId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFile(string fileId)
        {
            var validationResult = _validationService.ValidateFileId(fileId);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning($"File ID validation failed: {validationResult.ErrorMessage}");
                return BadRequest(validationResult.ErrorMessage);
            }

            _logger.LogInformation("Получен запрос на получение файла: {FileId}", fileId);
            
            try
            {
                var (fileStream, contentType, fileName) = await _fileService.GetFileAsync(fileId);
                
                _logger.LogInformation("Файл успешно получен: {FileId}, {FileName}", fileId, fileName);
                
                return File(fileStream, contentType, fileName);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Файл не найден: {FileId}", fileId);
                return NotFound(new { error = "Файл не найден" });
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning("Файл не найден на диске: {FileId}", fileId);
                return NotFound(new { error = "Файл не найден на диске" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении файла: {FileId}", fileId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Ошибка получения файла: {ex.Message}" });
            }
        }

        /// <summary>
        /// Получает метаданные файла по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Метаданные файла</returns>
        /// <response code="200">Метаданные успешно получены</response>
        /// <response code="404">Файл не найден</response>
        /// <response code="500">Внутренняя ошибка сервера</response>
        [HttpGet("{fileId}/metadata")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFileMetadata(string fileId)
        {
            _logger.LogInformation("Получен запрос на получение метаданных файла: {FileId}", fileId);
            
            try
            {
                var metadata = await _fileService.GetFileMetadataAsync(fileId);
                
                _logger.LogInformation("Метаданные файла успешно получены: {FileId}", fileId);
                
                return Ok(metadata);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Файл не найден: {FileId}", fileId);
                return NotFound(new { error = "Файл не найден" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении метаданных файла: {FileId}", fileId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Ошибка получения метаданных файла: {ex.Message}" });
            }
        }

        /// <summary>
        /// Удаляет файл по его идентификатору
        /// </summary>
        /// <param name="fileId">Идентификатор файла</param>
        /// <returns>Результат удаления</returns>
        /// <response code="200">Файл успешно удален</response>
        /// <response code="404">Файл не найден</response>
        /// <response code="500">Внутренняя ошибка сервера</response>
        [HttpDelete("{fileId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteFile(string fileId)
        {
            var validationResult = _validationService.ValidateFileId(fileId);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning($"File ID validation failed: {validationResult.ErrorMessage}");
                return BadRequest(validationResult.ErrorMessage);
            }

            _logger.LogInformation("Получен запрос на удаление файла: {FileId}", fileId);
            
            try
            {
                await _fileService.DeleteFileAsync(fileId);
                
                _logger.LogInformation("Файл успешно удален: {FileId}", fileId);
                
                return Ok(new { message = "Файл успешно удален", fileId });
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Файл не найден: {FileId}", fileId);
                return NotFound(new { error = "Файл не найден" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении файла: {FileId}", fileId);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = $"Ошибка удаления файла: {ex.Message}" });
            }
        }
    }
}
