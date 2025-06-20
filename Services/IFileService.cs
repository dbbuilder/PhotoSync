namespace PhotoSync.Services
{
    /// <summary>
    /// Interface for file operations related to image handling
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Gets all JPG files from the specified folder
        /// </summary>
        /// <param name="folderPath">Path to the folder containing images</param>
        /// <returns>Dictionary with filename (without extension) as key and file data as value</returns>
        Task<Dictionary<string, byte[]>> GetImagesFromFolderAsync(string folderPath);

        /// <summary>
        /// Saves image data to a file in the specified folder
        /// </summary>
        /// <param name="folderPath">Destination folder path</param>
        /// <param name="fileName">File name without extension</param>
        /// <param name="imageData">Binary image data</param>
        /// <returns>Full path of the saved file</returns>
        Task<string> SaveImageToFolderAsync(string folderPath, string fileName, byte[] imageData);

        /// <summary>
        /// Ensures the specified directory exists
        /// </summary>
        /// <param name="folderPath">Path to ensure exists</param>
        void EnsureDirectoryExists(string folderPath);

        /// <summary>
        /// Validates that a folder path exists and is accessible
        /// </summary>
        /// <param name="folderPath">Path to validate</param>
        /// <returns>True if folder exists and is accessible</returns>
        bool ValidateFolderAccess(string folderPath);

        /// <summary>
        /// Gets information about files in a folder
        /// </summary>
        /// <param name="folderPath">Path to analyze</param>
        /// <returns>Folder statistics</returns>
        Task<FolderInfo> GetFolderInfoAsync(string folderPath);
    }

    /// <summary>
    /// Information about a folder's contents
    /// </summary>
    public class FolderInfo
    {
        public int TotalFiles { get; set; }
        public int JpgFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public DateTime LastModified { get; set; }
    }
}
