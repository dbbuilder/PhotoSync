using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PhotoSync.Configuration;
using Serilog;
using System.Text.RegularExpressions;

namespace PhotoSync.Services
{
    /// <summary>
    /// Service for Azure Storage Blob operations
    /// </summary>
    public class AzureStorageService : IAzureStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobContainerClient _containerClient;
        private readonly AzureStorageSettings _azureSettings;
        private readonly ILogger _logger;

        public AzureStorageService(AzureStorageSettings azureSettings, ILogger logger)
        {
            _azureSettings = azureSettings ?? throw new ArgumentNullException(nameof(azureSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                // Initialize BlobServiceClient based on configuration
                if (_azureSettings.UseDefaultAzureCredential && !string.IsNullOrEmpty(_azureSettings.StorageAccountName))
                {
                    // Use Managed Identity or Azure CLI credentials
                    var accountUri = new Uri($"https://{_azureSettings.StorageAccountName}.blob.core.windows.net");
                    _blobServiceClient = new BlobServiceClient(accountUri, new DefaultAzureCredential());
                    _logger.Information("Initialized Azure Storage with DefaultAzureCredential for account: {AccountName}", 
                        _azureSettings.StorageAccountName);
                }
                else if (!string.IsNullOrEmpty(_azureSettings.ConnectionString))
                {
                    // Use connection string
                    _blobServiceClient = new BlobServiceClient(_azureSettings.ConnectionString);
                    _logger.Information("Initialized Azure Storage with connection string");
                }
                else
                {
                    throw new InvalidOperationException("Azure Storage configuration is invalid. Provide either ConnectionString or StorageAccountName with UseDefaultAzureCredential=true");
                }

                // Get container client
                _containerClient = _blobServiceClient.GetBlobContainerClient(_azureSettings.ContainerName);
                _logger.Information("Using container: {ContainerName}", _azureSettings.ContainerName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize Azure Storage Service");
                throw;
            }
        }

        /// <summary>
        /// Uploads an image to Azure Storage
        /// </summary>
        /// <param name="blobName">Name of the blob (usually the image code)</param>
        /// <param name="imageData">Binary image data</param>
        /// <returns>The full Azure Storage URL of the uploaded blob</returns>
        public async Task<string> UploadImageAsync(string blobName, byte[] imageData)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(blobName))
                    throw new ArgumentException("Blob name cannot be null or empty", nameof(blobName));

                if (imageData == null || imageData.Length == 0)
                    throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));

                // Ensure container exists
                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                // Sanitize blob name
                var sanitizedBlobName = SanitizeBlobName(blobName);
                
                // Add .jpg extension if not present
                if (!sanitizedBlobName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    sanitizedBlobName += ".jpg";
                }

                // Get blob client
                var blobClient = _containerClient.GetBlobClient(sanitizedBlobName);

                // Upload image
                using var stream = new MemoryStream(imageData);
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "image/jpeg"
                    }
                };

                await blobClient.UploadAsync(stream, uploadOptions);

                var blobUrl = blobClient.Uri.ToString();
                _logger.Information("Successfully uploaded image {BlobName} to Azure Storage. URL: {BlobUrl}", 
                    sanitizedBlobName, blobUrl);

                return blobUrl;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error uploading image {BlobName} to Azure Storage", blobName);
                throw;
            }
        }

        /// <summary>
        /// Downloads an image from Azure Storage
        /// </summary>
        /// <param name="blobPath">The Azure Storage path or URL</param>
        /// <returns>Binary image data</returns>
        public async Task<byte[]> DownloadImageAsync(string blobPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(blobPath))
                    throw new ArgumentException("Blob path cannot be null or empty", nameof(blobPath));

                // Extract blob name from path or URL
                var blobName = ExtractBlobNameFromPath(blobPath);
                
                // Get blob client
                var blobClient = _containerClient.GetBlobClient(blobName);

                // Check if blob exists
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    _logger.Warning("Blob not found: {BlobName}", blobName);
                    throw new FileNotFoundException($"Blob not found: {blobName}");
                }

                // Download blob
                var downloadResponse = await blobClient.DownloadContentAsync();
                var imageData = downloadResponse.Value.Content.ToArray();

                _logger.Information("Successfully downloaded image {BlobName} from Azure Storage ({Size} bytes)", 
                    blobName, imageData.Length);

                return imageData;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error downloading image from Azure Storage: {BlobPath}", blobPath);
                throw;
            }
        }

        /// <summary>
        /// Deletes a blob from Azure Storage
        /// </summary>
        /// <param name="blobPath">The Azure Storage path or URL</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        public async Task<bool> DeleteBlobAsync(string blobPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(blobPath))
                    return false;

                // Extract blob name from path or URL
                var blobName = ExtractBlobNameFromPath(blobPath);
                
                // Get blob client
                var blobClient = _containerClient.GetBlobClient(blobName);

                // Delete blob
                var response = await blobClient.DeleteIfExistsAsync();
                
                if (response.Value)
                {
                    _logger.Information("Successfully deleted blob: {BlobName}", blobName);
                }
                else
                {
                    _logger.Warning("Blob not found for deletion: {BlobName}", blobName);
                }

                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting blob from Azure Storage: {BlobPath}", blobPath);
                return false;
            }
        }

        /// <summary>
        /// Checks if a blob exists in Azure Storage
        /// </summary>
        /// <param name="blobPath">The Azure Storage path or URL</param>
        /// <returns>True if blob exists, false otherwise</returns>
        public async Task<bool> BlobExistsAsync(string blobPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(blobPath))
                    return false;

                // Extract blob name from path or URL
                var blobName = ExtractBlobNameFromPath(blobPath);
                
                // Get blob client
                var blobClient = _containerClient.GetBlobClient(blobName);

                // Check existence
                var response = await blobClient.ExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking blob existence: {BlobPath}", blobPath);
                return false;
            }
        }

        /// <summary>
        /// Tests the Azure Storage connection
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Try to create container if it doesn't exist
                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                
                // Try to get container properties
                var properties = await _containerClient.GetPropertiesAsync();
                
                _logger.Information("Azure Storage connection test successful. Container: {ContainerName}", 
                    _azureSettings.ContainerName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Azure Storage connection test failed");
                return false;
            }
        }

        /// <summary>
        /// Sanitizes blob name to ensure it's valid for Azure Storage
        /// </summary>
        private string SanitizeBlobName(string blobName)
        {
            // Remove invalid characters and replace with underscore
            var sanitized = Regex.Replace(blobName, @"[^\w\-\.]", "_");
            
            // Remove leading/trailing dots and slashes
            sanitized = sanitized.Trim('.', '/', '\\');
            
            // Ensure it's not empty
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = Guid.NewGuid().ToString();
            }

            return sanitized;
        }

        /// <summary>
        /// Extracts blob name from a full path or URL
        /// </summary>
        private string ExtractBlobNameFromPath(string blobPath)
        {
            if (string.IsNullOrWhiteSpace(blobPath))
                throw new ArgumentException("Blob path cannot be null or empty", nameof(blobPath));

            // If it's a full URL, extract the blob name
            if (Uri.TryCreate(blobPath, UriKind.Absolute, out var uri))
            {
                // Remove container name from path
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 1 && segments[0].Equals(_azureSettings.ContainerName, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Join("/", segments.Skip(1));
                }
                else if (segments.Length > 0)
                {
                    return segments[segments.Length - 1];
                }
            }

            // If it's just a blob name, return as is
            return blobPath;
        }
    }
}