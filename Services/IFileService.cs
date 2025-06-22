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

        /// <summary>
        /// Archives a file by moving it to an archive folder
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="archiveFolder">Archive folder path</param>
        /// <param name="preserveStructure">Whether to preserve folder structure</param>
        /// <returns>New file path after archiving</returns>
        Task<string> ArchiveFileAsync(string sourceFilePath, string archiveFolder, bool preserveStructure = false);

        /// <summary>
        /// Calculates SHA256 hash of a file
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Base64 encoded hash string</returns>
        Task<string> CalculateFileHashAsync(string filePath);

        /// <summary>
        /// Calculates SHA256 hash of byte array
        /// </summary>
        /// <param name="data">Byte array data</param>
        /// <returns>Base64 encoded hash string</returns>
        string CalculateHash(byte[] data);

        /// <summary>
        /// Archives multiple files in parallel
        /// </summary>
        /// <param name="filePaths">List of file paths to archive</param>
        /// <param name="archiveFolder">Archive folder path</param>
        /// <param name="maxParallel">Maximum parallel operations</param>
        /// <returns>Dictionary of original path to archived path</returns>
        Task<Dictionary<string, string>> ArchiveFilesAsync(IEnumerable<string> filePaths, string archiveFolder, int maxParallel = 4);

        /// <summary>
        /// Gets all JPG files from the specified folder
        /// </summary>
        /// <param name="folderPath">Folder to search</param>
        /// <returns>List of JPG file paths</returns>
        List<string> GetJpgFiles(string folderPath);

        /// <summary>
        /// Reads file contents asynchronously
        /// </summary>
        /// <param name="filePath">File path to read</param>
        /// <returns>File contents as byte array</returns>
        Task<byte[]> ReadFileAsync(string filePath);

        /// <summary>
        /// Gets filename without extension
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Filename without extension</returns>
        string GetFileNameWithoutExtension(string filePath);

        /// <summary>
        /// Writes data to file asynchronously
        /// </summary>
        /// <param name="filePath">File path to write</param>
        /// <param name="data">Data to write</param>
        /// <returns>Task</returns>
        Task WriteFileAsync(string filePath, byte[] data);

        /// <summary>
        /// Archives a file synchronously (for compatibility)
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="archiveFolder">Archive folder path</param>
        void ArchiveFile(string sourceFilePath, string archiveFolder);

        /// <summary>
        /// Gets files matching pattern
        /// </summary>
        /// <param name="folderPath">Folder to search</param>
        /// <param name="pattern">Search pattern</param>
        /// <returns>List of matching file paths</returns>
        List<string> GetFiles(string folderPath, string pattern);

        /// <summary>
        /// Moves a file from source to destination
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="destinationFilePath">Destination file path</param>
        void MoveFile(string sourceFilePath, string destinationFilePath);

        /// <summary>
        /// Copies a file from source to destination
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="destinationFilePath">Destination file path</param>
        void CopyFile(string sourceFilePath, string destinationFilePath);

        /// <summary>
        /// Deletes a file
        /// </summary>
        /// <param name="filePath">File path to delete</param>
        void DeleteFile(string filePath);

        /// <summary>
        /// Checks if a directory exists
        /// </summary>
        /// <param name="directoryPath">Directory path to check</param>
        /// <returns>True if directory exists</returns>
        bool DirectoryExists(string directoryPath);

        /// <summary>
        /// Gets the size of a file in bytes
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>File size in bytes</returns>
        long GetFileSize(string filePath);

        /// <summary>
        /// Checks if a file exists
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <returns>True if file exists</returns>
        bool FileExists(string filePath);
        
        /// <summary>
        /// Creates output directory if it doesn't exist
        /// </summary>
        /// <param name="directoryPath">Directory path to create</param>
        /// <returns>The directory path</returns>
        string CreateOutputDirectory(string directoryPath);
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
