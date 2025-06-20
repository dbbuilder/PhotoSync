using PhotoSync.Services;
using Serilog;

namespace PhotoSync.Services
{
    /// <summary>
    /// Service for handling file operations related to image import/export
    /// </summary>
    public class FileService : IFileService
    {
        private readonly ILogger _logger;

        public FileService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all JPG files from the specified folder
        /// </summary>
        /// <param name="folderPath">Path to the folder containing images</param>
        /// <returns>Dictionary with filename (without extension) as key and file data as value</returns>
        public async Task<Dictionary<string, byte[]>> GetImagesFromFolderAsync(string folderPath)
        {
            var images = new Dictionary<string, byte[]>();

            try
            {
                _logger.Information("Starting to read images from folder: {FolderPath}", folderPath);

                if (!Directory.Exists(folderPath))
                {
                    _logger.Warning("Import folder does not exist: {FolderPath}", folderPath);
                    return images;
                }

                // Get all JPG files from the folder (case insensitive)
                var jpgFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => Path.GetExtension(file).ToLowerInvariant() == ".jpg" || 
                                  Path.GetExtension(file).ToLowerInvariant() == ".jpeg")
                    .ToArray();
                
                _logger.Information("Found {FileCount} JPG files in folder", jpgFiles.Length);

                // Process each image file
                foreach (var filePath in jpgFiles)
                {
                    try
                    {
                        // Get filename without extension to use as code
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        
                        // Skip files with empty names
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            _logger.Warning("Skipping file with empty name: {FilePath}", filePath);
                            continue;
                        }
                        
                        // Read file data
                        var fileData = await File.ReadAllBytesAsync(filePath);
                        
                        // Skip empty files
                        if (fileData.Length == 0)
                        {
                            _logger.Warning("Skipping empty file: {FileName}", fileName);
                            continue;
                        }
                        
                        images.Add(fileName, fileData);
                        _logger.Debug("Successfully read file: {FileName} ({FileSize} bytes)", fileName, fileData.Length);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.Error(ex, "Error reading file: {FilePath}", filePath);
                        // Continue processing other files
                    }
                }

                _logger.Information("Successfully processed {ProcessedCount} images from folder", images.Count);
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error accessing folder: {FolderPath}", folderPath);
                throw;
            }

            return images;
        }

        /// <summary>
        /// Saves image data to a file in the specified folder
        /// </summary>
        /// <param name="folderPath">Destination folder path</param>
        /// <param name="fileName">File name without extension</param>
        /// <param name="imageData">Binary image data</param>
        /// <returns>Full path of the saved file</returns>
        public async Task<string> SaveImageToFolderAsync(string folderPath, string fileName, byte[] imageData)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(folderPath))
                    throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));
                if (string.IsNullOrWhiteSpace(fileName))
                    throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
                if (imageData == null || imageData.Length == 0)
                    throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));

                // Ensure directory exists
                EnsureDirectoryExists(folderPath);

                // Sanitize filename to remove invalid characters
                var sanitizedFileName = SanitizeFileName(fileName);
                
                // Create full file path with JPG extension
                var fullPath = Path.Combine(folderPath, $"{sanitizedFileName}.jpg");
                
                // Write file data
                await File.WriteAllBytesAsync(fullPath, imageData);
                
                _logger.Debug("Successfully saved file: {FullPath} ({FileSize} bytes)", fullPath, imageData.Length);
                
                return fullPath;
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error saving file: {FileName} to folder: {FolderPath}", fileName, folderPath);
                throw;
            }
        }

        /// <summary>
        /// Ensures the specified directory exists
        /// </summary>
        /// <param name="folderPath">Path to ensure exists</param>
        public void EnsureDirectoryExists(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                    throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    _logger.Information("Created directory: {FolderPath}", folderPath);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error creating directory: {FolderPath}", folderPath);
                throw;
            }
        }

        /// <summary>
        /// Validates that a folder path exists and is accessible
        /// </summary>
        /// <param name="folderPath">Path to validate</param>
        /// <returns>True if folder exists and is accessible</returns>
        public bool ValidateFolderAccess(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                    return false;

                if (!Directory.Exists(folderPath))
                    return false;

                // Try to access the directory to check permissions
                Directory.GetFiles(folderPath);
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.Warning(ex, "Unable to access folder: {FolderPath}", folderPath);
                return false;
            }
        }

        /// <summary>
        /// Gets information about files in a folder
        /// </summary>
        /// <param name="folderPath">Path to analyze</param>
        /// <returns>Folder statistics</returns>
        public async Task<FolderInfo> GetFolderInfoAsync(string folderPath)
        {
            return await Task.Run(() =>
            {
                var info = new FolderInfo();

                try
                {
                    if (!Directory.Exists(folderPath))
                        return info;

                    var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
                    info.TotalFiles = allFiles.Length;

                    var jpgFiles = allFiles.Where(f => 
                        Path.GetExtension(f).ToLowerInvariant() == ".jpg" || 
                        Path.GetExtension(f).ToLowerInvariant() == ".jpeg").ToArray();
                    
                    info.JpgFiles = jpgFiles.Length;

                    // Calculate total size
                    foreach (var file in allFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            info.TotalSizeBytes += fileInfo.Length;
                            if (fileInfo.LastWriteTime > info.LastModified)
                                info.LastModified = fileInfo.LastWriteTime;
                        }
                        catch
                        {
                            // Skip files we can't access
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.Error(ex, "Error analyzing folder: {FolderPath}", folderPath);
                }

                return info;
            });
        }

        /// <summary>
        /// Sanitizes a filename by removing invalid characters
        /// </summary>
        /// <param name="fileName">Original filename</param>
        /// <returns>Sanitized filename safe for file system</returns>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "unnamed";

            // Get invalid characters for the current file system
            var invalidChars = Path.GetInvalidFileNameChars();
            
            // Replace invalid characters with underscores
            var sanitized = fileName;
            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            // Trim whitespace and periods from ends
            sanitized = sanitized.Trim(' ', '.');
            
            // Ensure we don't have an empty result
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "unnamed";

            return sanitized;
        }
    }
}
