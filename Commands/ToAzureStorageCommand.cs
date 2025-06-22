using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using Serilog;

namespace PhotoSync.Commands
{
    /// <summary>
    /// Command handler for uploading images to Azure Storage
    /// </summary>
    public class ToAzureStorageCommand
    {
        private readonly IDatabaseService _databaseService;
        private readonly IAzureStorageService _azureStorageService;
        private readonly PhotoSettings _photoSettings;
        private readonly ILogger _logger;

        public ToAzureStorageCommand(
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
        /// Executes the command to upload images to Azure Storage
        /// Uploads only images where AzureStoragePath is NULL and ImageData is NOT NULL
        /// Or where AzureSyncRequired is true when force is enabled
        /// </summary>
        /// <param name="force">Force upload even if already in Azure (for sync)</param>
        /// <returns>Command result with statistics</returns>
        public async Task<AzureOperationResult> ExecuteAsync(bool force = false)
        {
            var result = new AzureOperationResult
            {
                Operation = "ToAzureStorage",
                FieldName = _photoSettings.AzureStoragePathFieldName
            };

            try
            {
                _logger.Information("Starting ToAzureStorage operation");

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

                // Get images based on mode
                List<ImageRecord> images;
                if (force)
                {
                    // Get images that need sync
                    images = await _databaseService.GetPhotosNeedingAzureSyncAsync();
                    _logger.Information("Force mode: checking images needing Azure sync");
                }
                else
                {
                    // Get all images with NULL Azure path
                    images = await _databaseService.GetImagesWithNullAzurePathAsync();
                }
                
                result.IsForceSync = force;

                if (!images.Any())
                {
                    _logger.Warning("No images found with NULL {FieldName}", _photoSettings.AzureStoragePathFieldName);
                    result.IsSuccess = true; // Not an error, just no work to do
                    return result;
                }

                _logger.Information("Found {ImageCount} images to upload to Azure Storage", images.Count);
                result.TotalRecordsFound = images.Count;

                // Process each image
                foreach (var image in images)
                {
                    try
                    {
                        if (image.ImageData?.Length > 0)
                        {
                            // Check if we need to delete existing blob first (for updates)
                            if (force && !string.IsNullOrEmpty(image.AzureStoragePath))
                            {
                                try
                                {
                                    await _azureStorageService.DeleteBlobAsync(image.AzureStoragePath);
                                    _logger.Debug("Deleted existing blob for {Code} before re-upload", image.Code);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warning(ex, "Failed to delete existing blob for {Code}, continuing with upload", image.Code);
                                }
                            }
                            
                            // Upload to Azure Storage
                            var azureUrl = await _azureStorageService.UploadImageAsync(image.Code, image.ImageData);

                            // Update database with Azure URL and clear sync flag
                            var updateResult = await _databaseService.UpdateAzureStoragePathAsync(image.Code, azureUrl);

                            if (updateResult)
                            {
                                result.SuccessCount++;
                                _logger.Information("Successfully uploaded {Code} to Azure Storage", image.Code);
                            }
                            else
                            {
                                result.FailureCount++;
                                result.FailedRecords.Add(image.Code);
                                _logger.Error("Failed to update Azure path in database for {Code}", image.Code);
                            }
                        }
                        else
                        {
                            result.SkippedCount++;
                            result.SkippedRecords.Add($"{image.Code} (no image data)");
                            _logger.Warning("Image {Code} has no data to upload", image.Code);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        result.FailureCount++;
                        result.FailedRecords.Add(image.Code);
                        _logger.Error(ex, "Error uploading image {Code} to Azure Storage", image.Code);
                    }
                }

                result.IsSuccess = result.SuccessCount > 0 || result.SkippedCount == result.TotalRecordsFound;
                result.CompletedAt = DateTime.UtcNow;

                _logger.Information("ToAzureStorage operation completed. Mode: {Mode}, Success: {SuccessCount}, Failed: {FailureCount}, Skipped: {SkippedCount}",
                    force ? "Force Sync" : "Normal",
                    result.SuccessCount, result.FailureCount, result.SkippedCount);
            }
            catch (System.Exception ex)
            {
                var error = "Error during ToAzureStorage operation";
                _logger.Error(ex, error);
                result.ErrorMessage = error;
                result.CompletedAt = DateTime.UtcNow;
            }

            return result;
        }
    }

    /// <summary>
    /// Result of an Azure Storage operation
    /// </summary>
    public class AzureOperationResult
    {
        public bool IsSuccess { get; set; }
        public bool IsForceSync { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public int TotalRecordsFound { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> FailedRecords { get; set; } = new();
        public List<string> SkippedRecords { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;

        public string Summary => 
            $"{Operation}: Processed {SuccessCount}/{TotalRecordsFound} records in {Duration:mm\\:ss}" +
            (IsForceSync ? " (force sync)" : "") +
            $" (Failed: {FailureCount}, Skipped: {SkippedCount})";
    }
}