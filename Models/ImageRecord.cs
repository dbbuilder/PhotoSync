namespace PhotoSync.Models
{
    /// <summary>
    /// Represents an image record in the database
    /// </summary>
    public class ImageRecord
    {
        /// <summary>
        /// Unique identifier for the image (filename without extension)
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Binary image data
        /// </summary>
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Date and time when the record was created
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Date and time when the record was last modified
        /// </summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Azure Storage path/URL for the image
        /// </summary>
        public string? AzureStoragePath { get; set; }

        /// <summary>
        /// Source of the image (FILE:path, AZURE:url, BULK:info, API:source)
        /// </summary>
        public string? ImageSource { get; set; }

        /// <summary>
        /// Original file name when imported
        /// </summary>
        public string? SourceFileName { get; set; }

        /// <summary>
        /// Date when image was imported from file system
        /// </summary>
        public DateTime? ImportedDate { get; set; }

        /// <summary>
        /// Date when image was last exported to client folder
        /// </summary>
        public DateTime? ExportedDate { get; set; }

        /// <summary>
        /// Date when image was uploaded to Azure Storage
        /// </summary>
        public DateTime? AzureUploadedDate { get; set; }

        /// <summary>
        /// Date when photo data was last modified
        /// </summary>
        public DateTime? PhotoModifiedDate { get; set; }

        /// <summary>
        /// Flag indicating Azure Storage needs to be synced with local changes
        /// </summary>
        public bool AzureSyncRequired { get; set; }

        /// <summary>
        /// SHA256 hash of the image data for duplicate detection
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// File size in bytes (stored separately for quick access)
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// Gets the size of the image data in bytes
        /// </summary>
        public long ImageSizeBytes => ImageData?.Length ?? 0;

        /// <summary>
        /// Gets a human-readable representation of the image size
        /// </summary>
        public string ImageSizeFormatted
        {
            get
            {
                var bytes = ImageSizeBytes;
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
                return $"{bytes / (1024 * 1024):F1} MB";
            }
        }

        /// <summary>
        /// Determines if the image needs to be exported
        /// </summary>
        public bool NeedsExport => ExportedDate == null || 
            (ModifiedDate.HasValue && ModifiedDate > ExportedDate) ||
            (PhotoModifiedDate.HasValue && PhotoModifiedDate > ExportedDate);

        /// <summary>
        /// Determines if the image exists in Azure
        /// </summary>
        public bool IsInAzure => !string.IsNullOrEmpty(AzureStoragePath);

        /// <summary>
        /// Determines if the image has local data
        /// </summary>
        public bool HasLocalData => ImageData?.Length > 0;

        /// <summary>
        /// Gets the storage mode for this image
        /// </summary>
        public string StorageMode
        {
            get
            {
                if (!HasLocalData && !IsInAzure) return "Empty";
                if (HasLocalData && !IsInAzure) return "LocalOnly";
                if (!HasLocalData && IsInAzure) return "AzureOnly";
                return "Hybrid";
            }
        }

        /// <summary>
        /// Gets the source type from ImageSource
        /// </summary>
        public string SourceType => ImageSource?.Split(':')[0] ?? "UNKNOWN";

        /// <summary>
        /// Calculates SHA256 hash of the image data
        /// </summary>
        public string CalculateHash()
        {
            if (ImageData == null || ImageData.Length == 0)
                return string.Empty;

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(ImageData);
            return Convert.ToBase64String(hashBytes);
        }
    }
}
