using PhotoSync.Configuration;
using PhotoSync.Services;
using Serilog;

namespace PhotoSync.Commands
{
    /// <summary>
    /// Command handler for exporting images from database to folder
    /// </summary>
    public class ExportCommand
    {
        private readonly IDatabaseService _databaseService;
        private readonly IFileService _fileService;
        private readonly PhotoSettings _photoSettings;
        private readonly ILogger _logger;

        public ExportCommand(
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
        /// Executes the export command to save images from database to folder
        /// </summary>
        /// <param name="exportFolder">Optional override for export folder from configuration</param>
        /// <returns>Export result with statistics</returns>
        public async Task<ExportResult> ExecuteAsync(string? exportFolder = null)
        {
            var folderPath = exportFolder ?? _photoSettings.ExportFolder;
            var result = new ExportResult { FolderPath = folderPath };

            try
            {
                _logger.Information("Starting export operation to folder: {FolderPath}", folderPath);

                // Validate folder path
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    var error = "Export folder path is not configured";
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

                // Ensure export directory exists
                try
                {
                    _fileService.EnsureDirectoryExists(folderPath);
                }
                catch (System.Exception ex)
                {
                    var error = $"Failed to create or access export directory: {folderPath}";
                    _logger.Error(ex, error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Get all images from database
                var images = await _databaseService.GetAllImagesAsync();

                if (!images.Any())
                {
                    _logger.Warning("No images found in database for export");
                    result.IsSuccess = true; // Not an error, just no work to do
                    return result;
                }

                _logger.Information("Found {ImageCount} images to export", images.Count);
                result.TotalImagesFound = images.Count;

                // Process each image
                foreach (var image in images)
                {
                    try
                    {
                        if (image.ImageData?.Length > 0)
                        {
                            // Save image to file
                            var savedPath = await _fileService.SaveImageToFolderAsync(
                                folderPath, 
                                image.Code, 
                                image.ImageData);

                            result.SuccessCount++;
                            result.ExportedFiles.Add(savedPath);
                            _logger.Information("Successfully exported image: {Code} to {SavedPath} ({FileSize})", 
                                image.Code, savedPath, image.ImageSizeFormatted);
                        }
                        else
                        {
                            result.FailureCount++;
                            result.FailedCodes.Add(image.Code);
                            _logger.Warning("Image with code {Code} has no data to export", image.Code);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        result.FailureCount++;
                        result.FailedCodes.Add(image.Code);
                        _logger.Error(ex, "Error exporting image: {Code}", image.Code);
                    }
                }

                result.IsSuccess = result.SuccessCount > 0;
                result.CompletedAt = DateTime.UtcNow;

                _logger.Information("Export operation completed. Success: {SuccessCount}, Failures: {FailureCount}", 
                    result.SuccessCount, result.FailureCount);
            }
            catch (System.Exception ex)
            {
                var error = $"Error during export operation to folder: {folderPath}";
                _logger.Error(ex, error);
                result.ErrorMessage = error;
                result.CompletedAt = DateTime.UtcNow;
            }

            return result;
        }
    }

    /// <summary>
    /// Result of an export operation
    /// </summary>
    public class ExportResult
    {
        public bool IsSuccess { get; set; }
        public string FolderPath { get; set; } = string.Empty;
        public int TotalImagesFound { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> ExportedFiles { get; set; } = new();
        public List<string> FailedCodes { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;
        
        public string Summary => $"Exported {SuccessCount}/{TotalImagesFound} images in {Duration:mm\\:ss}";
    }
}
