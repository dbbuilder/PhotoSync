using PhotoSync.Models;

namespace PhotoSync.Services
{
    /// <summary>
    /// Interface for database operations related to image storage
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// Inserts or updates an image record in the database
        /// </summary>
        /// <param name="imageRecord">Image record to save</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SaveImageAsync(ImageRecord imageRecord);

        /// <summary>
        /// Retrieves all image records from the database
        /// </summary>
        /// <returns>List of image records</returns>
        Task<List<ImageRecord>> GetAllImagesAsync();

        /// <summary>
        /// Retrieves a specific image by code
        /// </summary>
        /// <param name="code">Image code to search for</param>
        /// <returns>Image record if found, null otherwise</returns>
        Task<ImageRecord?> GetImageByCodeAsync(string code);

        /// <summary>
        /// Tests the database connection
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// Gets count of images in the database
        /// </summary>
        /// <returns>Number of image records</returns>
        Task<int> GetImageCountAsync();

        /// <summary>
        /// Deletes an image by code
        /// </summary>
        /// <param name="code">Image code to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteImageAsync(string code);

        /// <summary>
        /// Gets all images that have NULL Azure Storage Path
        /// </summary>
        /// <returns>List of image records with null Azure path</returns>
        Task<List<ImageRecord>> GetImagesWithNullAzurePathAsync();

        /// <summary>
        /// Gets all images that have NULL photo data
        /// </summary>
        /// <returns>List of image records with null photo data</returns>
        Task<List<ImageRecord>> GetImagesWithNullPhotoDataAsync();

        /// <summary>
        /// Updates the Azure Storage Path for a specific image
        /// </summary>
        /// <param name="code">Image code to update</param>
        /// <param name="azurePath">Azure Storage path/URL</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateAzureStoragePathAsync(string code, string azurePath);

        /// <summary>
        /// Updates the image data for a specific image
        /// </summary>
        /// <param name="code">Image code to update</param>
        /// <param name="imageData">Binary image data</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateImageDataAsync(string code, byte[] imageData);

        /// <summary>
        /// Nullifies a specific field in all records
        /// </summary>
        /// <param name="fieldName">Field name to nullify (ImageData or AzureStoragePath)</param>
        /// <returns>Number of records affected</returns>
        Task<int> NullifyFieldAsync(string fieldName);

        /// <summary>
        /// Gets images that need incremental export
        /// </summary>
        /// <returns>List of images where ExportedDate is null or older than modified dates</returns>
        Task<List<ImageRecord>> GetPhotosForIncrementalExportAsync();

        /// <summary>
        /// Gets images that need Azure sync
        /// </summary>
        /// <returns>List of images where AzureSyncRequired is true</returns>
        Task<List<ImageRecord>> GetPhotosNeedingAzureSyncAsync();

        /// <summary>
        /// Updates export tracking information
        /// </summary>
        /// <param name="code">Image code</param>
        /// <param name="exportedDate">Export timestamp</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateExportTrackingAsync(string code, DateTime exportedDate);

        /// <summary>
        /// Updates import tracking information
        /// </summary>
        /// <param name="code">Image code</param>
        /// <param name="importedDate">Import timestamp</param>
        /// <param name="imageSource">Source information</param>
        /// <param name="sourceFileName">Original filename</param>
        /// <param name="fileHash">File hash</param>
        /// <param name="fileSize">File size in bytes</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateImportTrackingAsync(string code, DateTime importedDate, string imageSource, string sourceFileName, string fileHash, long fileSize);

        /// <summary>
        /// Checks for duplicate images by hash
        /// </summary>
        /// <param name="fileHash">SHA256 hash to check</param>
        /// <param name="excludeCode">Optional code to exclude from check</param>
        /// <returns>Existing image record if duplicate found, null otherwise</returns>
        Task<ImageRecord?> CheckDuplicateByHashAsync(string fileHash, string? excludeCode = null);

        /// <summary>
        /// Gets overall sync status statistics
        /// </summary>
        /// <returns>Dictionary of status metrics</returns>
        Task<Dictionary<string, object>> GetSyncStatusAsync();
    }
}
