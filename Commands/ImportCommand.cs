using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using Serilog;

namespace PhotoSync.Commands
{
    /// <summary>
    /// Command handler for importing images from folder to database
    /// </summary>
    public class ImportCommand
    {
        private readonly IDatabaseService _databaseService;
        private readonly IFileService _fileService;
        private readonly PhotoSettings _photoSettings;
        private readonly ILogger _logger;

        public ImportCommand(
            IDatabaseService databaseService, 
            IFileService fileService, 
            PhotoSettings photoSettings, 
            ILogger logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _photoSettings = photoSettings ?? throw new ArgumentNullException(nameof(photoSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the import command to load images from folder to database
        /// </summary>
        /// <param name="importFolder">Optional override for import folder from configuration</param>
        /// <returns>Import result with statistics</returns>
        public async Task<ImportResult> ExecuteAsync(string? importFolder = null)
        {
            var folderPath = importFolder ?? _photoSettings.ImportFolder;
            var result = new ImportResult { FolderPath = folderPath };

            try
            {
                _logger.Information("Starting import operation from folder: {FolderPath}", folderPath);

                // Validate folder path
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    var error = "Import folder path is not configured";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Check if folder exists and is accessible
                if (!_fileService.ValidateFolderAccess(folderPath))
                {
                    var error = $"Import folder does not exist or is not accessible: {folderPath}";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Test database connection
                if (!await _databaseService.TestConnectionAsync())
                {
                    var error = "Database connection failed";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Get folder information
                var folderInfo = await _fileService.GetFolderInfoAsync(folderPath);
                _logger.Information("Folder analysis: {TotalFiles} total files, {JpgFiles} JPG files", 
                    folderInfo.TotalFiles, folderInfo.JpgFiles);

                // Get all images from the folder
                var images = await _fileService.GetImagesFromFolderAsync(folderPath);

                if (!images.Any())
                {
                    _logger.Warning("No images found in import folder: {FolderPath}", folderPath);
                    result.IsSuccess = true; // Not an error, just no work to do
                    return result;
                }

                _logger.Information("Found {ImageCount} images to import", images.Count);
                result.TotalFilesFound = images.Count;

                // Process each image
                foreach (var (fileName, imageData) in images)
                {
                    try
                    {
                        // Create image record
                        var imageRecord = new ImageRecord
                        {
                            Code = fileName,
                            ImageData = imageData,
                            CreatedDate = DateTime.UtcNow
                        };

                        // Save to database
                        var saveResult = await _databaseService.SaveImageAsync(imageRecord);

                        if (saveResult)
                        {
                            result.SuccessCount++;
                            _logger.Information("Successfully imported image: {FileName} ({FileSize})", 
                                fileName, imageRecord.ImageSizeFormatted);
                        }
                        else
                        {
                            result.FailureCount++;
                            result.FailedFiles.Add(fileName);
                            _logger.Error("Failed to import image: {FileName}", fileName);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        result.FailureCount++;
                        result.FailedFiles.Add(fileName);
                        _logger.Error(ex, "Error processing image: {FileName}", fileName);
                    }
                }

                result.IsSuccess = result.SuccessCount > 0;
                result.CompletedAt = DateTime.UtcNow;

                _logger.Information("Import operation completed. Success: {SuccessCount}, Failures: {FailureCount}", 
                    result.SuccessCount, result.FailureCount);
            }
            catch (System.Exception ex)
            {
                var error = $"Error during import operation from folder: {folderPath}";
                _logger.Error(ex, error);
                result.ErrorMessage = error;
                result.CompletedAt = DateTime.UtcNow;
            }

            return result;
        }
    }

    /// <summary>
    /// Result of an import operation
    /// </summary>
    public class ImportResult
    {
        public bool IsSuccess { get; set; }
        public string FolderPath { get; set; } = string.Empty;
        public int TotalFilesFound { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> FailedFiles { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;
        
        public string Summary => $"Imported {SuccessCount}/{TotalFilesFound} images in {Duration:mm\\:ss}";
    }
}
