using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using Serilog;

namespace PhotoSync.Commands
{
    /// <summary>
    /// Command handler for downloading images from Azure Storage
    /// </summary>
    public class FromAzureStorageCommand
    {
        private readonly IDatabaseService _databaseService;
        private readonly IAzureStorageService _azureStorageService;
        private readonly PhotoSettings _photoSettings;
        private readonly ILogger _logger;

        public FromAzureStorageCommand(
            IDatabaseService databaseService,
            IAzureStorageService azureStorageService,
            PhotoSettings photoSettings,
            ILogger logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _azureStorageService = azureStorageService ?? throw new ArgumentNullException(nameof(azureStorageService));
            _photoSettings = photoSettings ?? throw new ArgumentNullException(nameof(photoSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the command to download images from Azure Storage
        /// Downloads only images where ImageData is NULL and AzureStoragePath is NOT NULL
        /// </summary>
        /// <returns>Command result with statistics</returns>
        public async Task<AzureOperationResult> ExecuteAsync()
        {
            var result = new AzureOperationResult
            {
                Operation = "FromAzureStorage",
                FieldName = _photoSettings.PhotoFieldName
            };

            try
            {
                _logger.Information("Starting FromAzureStorage operation");

                // Test database connection
                if (!await _databaseService.TestConnectionAsync())
                {
                    var error = "Database connection failed";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Test Azure Storage connection
                if (!await _azureStorageService.TestConnectionAsync())
                {
                    var error = "Azure Storage connection failed";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Get all images with NULL photo data
                var images = await _databaseService.GetImagesWithNullPhotoDataAsync();

                if (!images.Any())
                {
                    _logger.Warning("No images found with NULL {FieldName}", _photoSettings.PhotoFieldName);
                    result.IsSuccess = true; // Not an error, just no work to do
                    return result;
                }

                _logger.Information("Found {ImageCount} images to download from Azure Storage", images.Count);
                result.TotalRecordsFound = images.Count;

                // Process each image
                foreach (var image in images)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(image.AzureStoragePath))
                        {
                            // Download from Azure Storage
                            var imageData = await _azureStorageService.DownloadImageAsync(image.AzureStoragePath);

                            // Update database with image data
                            var updateResult = await _databaseService.UpdateImageDataAsync(image.Code, imageData);

                            if (updateResult)
                            {
                                result.SuccessCount++;
                                _logger.Information("Successfully downloaded {Code} from Azure Storage ({Size} bytes)", 
                                    image.Code, imageData.Length);
                            }
                            else
                            {
                                result.FailureCount++;
                                result.FailedRecords.Add(image.Code);
                                _logger.Error("Failed to update image data in database for {Code}", image.Code);
                            }
                        }
                        else
                        {
                            result.SkippedCount++;
                            result.SkippedRecords.Add($"{image.Code} (no Azure path)");
                            _logger.Warning("Image {Code} has no Azure Storage path", image.Code);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        result.FailureCount++;
                        result.FailedRecords.Add(image.Code);
                        _logger.Error(ex, "Error downloading image {Code} from Azure Storage", image.Code);
                    }
                }

                result.IsSuccess = result.SuccessCount > 0 || result.SkippedCount == result.TotalRecordsFound;
                result.CompletedAt = DateTime.UtcNow;

                _logger.Information("FromAzureStorage operation completed. Success: {SuccessCount}, Failed: {FailureCount}, Skipped: {SkippedCount}",
                    result.SuccessCount, result.FailureCount, result.SkippedCount);
            }
            catch (System.Exception ex)
            {
                var error = "Error during FromAzureStorage operation";
                _logger.Error(ex, error);
                result.ErrorMessage = error;
                result.CompletedAt = DateTime.UtcNow;
            }

            return result;
        }
    }
}