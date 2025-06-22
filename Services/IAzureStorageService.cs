namespace PhotoSync.Services
{
    /// <summary>
    /// Interface for Azure Storage Blob operations
    /// </summary>
    public interface IAzureStorageService
    {
        /// <summary>
        /// Uploads an image to Azure Storage
        /// </summary>
        /// <param name="blobName">Name of the blob (usually the image code)</param>
        /// <param name="imageData">Binary image data</param>
        /// <returns>The full Azure Storage URL of the uploaded blob</returns>
        Task<string> UploadImageAsync(string blobName, byte[] imageData);

        /// <summary>
        /// Downloads an image from Azure Storage
        /// </summary>
        /// <param name="blobPath">The Azure Storage path or URL</param>
        /// <returns>Binary image data</returns>
        Task<byte[]> DownloadImageAsync(string blobPath);

        /// <summary>
        /// Deletes a blob from Azure Storage
        /// </summary>
        /// <param name="blobPath">The Azure Storage path or URL</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteBlobAsync(string blobPath);

        /// <summary>
        /// Checks if a blob exists in Azure Storage
        /// </summary>
        /// <param name="blobPath">The Azure Storage path or URL</param>
        /// <returns>True if blob exists, false otherwise</returns>
        Task<bool> BlobExistsAsync(string blobPath);

        /// <summary>
        /// Tests the Azure Storage connection
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        Task<bool> TestConnectionAsync();
    }
}