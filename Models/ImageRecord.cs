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
    }
}
