using PhotoSync.Configuration;
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
        private readonly PhotoSettings _photoSettings;

        public FileService(PhotoSettings photoSettings, ILogger logger)
        {
            _photoSettings = photoSettings ?? throw new ArgumentNullException(nameof(photoSettings));
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

        /// <summary>
        /// Archives a file by moving it to an archive folder
        /// </summary>
        public async Task<string> ArchiveFileAsync(string sourceFilePath, string archiveFolder, bool preserveStructure = false)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");

            try
            {
                var fileName = Path.GetFileName(sourceFilePath);
                var targetPath = archiveFolder;

                // Preserve folder structure if requested
                if (preserveStructure)
                {
                    var sourceFolder = Path.GetDirectoryName(sourceFilePath);
                    var relativePath = Path.GetRelativePath(_photoSettings.ImportFolder, sourceFolder);
                    if (!relativePath.StartsWith(".."))
                    {
                        targetPath = Path.Combine(archiveFolder, relativePath);
                    }
                }

                // Ensure target directory exists
                EnsureDirectoryExists(targetPath);

                // Handle duplicate file names
                var targetFilePath = Path.Combine(targetPath, fileName);
                if (File.Exists(targetFilePath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var extension = Path.GetExtension(fileName);
                    targetFilePath = Path.Combine(targetPath, $"{nameWithoutExt}_{timestamp}{extension}");
                }

                // Move the file
                await Task.Run(() => File.Move(sourceFilePath, targetFilePath));
                
                _logger.Information("Archived file from {Source} to {Target}", sourceFilePath, targetFilePath);
                return targetFilePath;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error archiving file: {FilePath}", sourceFilePath);
                throw;
            }
        }

        /// <summary>
        /// Calculates SHA256 hash of a file
        /// </summary>
        public async Task<string> CalculateFileHashAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                
                var hash = await sha256.ComputeHashAsync(stream);
                return Convert.ToBase64String(hash);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calculating hash for file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Calculates SHA256 hash of byte array
        /// </summary>
        public string CalculateHash(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Archives multiple files in parallel
        /// </summary>
        public async Task<Dictionary<string, string>> ArchiveFilesAsync(IEnumerable<string> filePaths, string archiveFolder, int maxParallel = 4)
        {
            var results = new Dictionary<string, string>();
            var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            var tasks = new List<Task>();

            foreach (var filePath in filePaths)
            {
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var archivedPath = await ArchiveFileAsync(filePath, archiveFolder, _photoSettings.PreserveSourceStructure);
                        lock (results)
                        {
                            results[filePath] = archivedPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to archive file: {FilePath}", filePath);
                        lock (results)
                        {
                            results[filePath] = string.Empty; // Empty string indicates failure
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// Gets all JPG files from the specified folder
        /// </summary>
        public List<string> GetJpgFiles(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                _logger.Warning("Folder does not exist: {FolderPath}", folderPath);
                return new List<string>();
            }

            return Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => Path.GetExtension(file).ToLowerInvariant() == ".jpg" || 
                              Path.GetExtension(file).ToLowerInvariant() == ".jpeg")
                .ToList();
        }

        /// <summary>
        /// Reads file contents asynchronously
        /// </summary>
        public async Task<byte[]> ReadFileAsync(string filePath)
        {
            return await File.ReadAllBytesAsync(filePath);
        }

        /// <summary>
        /// Gets filename without extension
        /// </summary>
        public string GetFileNameWithoutExtension(string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        /// <summary>
        /// Writes data to file asynchronously
        /// </summary>
        public async Task WriteFileAsync(string filePath, byte[] data)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(filePath, data);
        }

        /// <summary>
        /// Archives a file synchronously (for compatibility)
        /// </summary>
        public void ArchiveFile(string sourceFilePath, string archiveFolder)
        {
            var task = ArchiveFileAsync(sourceFilePath, archiveFolder);
            task.Wait();
        }

        /// <summary>
        /// Gets files matching pattern
        /// </summary>
        public List<string> GetFiles(string folderPath, string pattern)
        {
            if (!Directory.Exists(folderPath))
            {
                _logger.Warning("Folder does not exist: {FolderPath}", folderPath);
                return new List<string>();
            }

            return Directory.GetFiles(folderPath, pattern, SearchOption.TopDirectoryOnly).ToList();
        }

        /// <summary>
        /// Moves a file from source to destination
        /// </summary>
        public void MoveFile(string sourceFilePath, string destinationFilePath)
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            File.Move(sourceFilePath, destinationFilePath);
        }

        /// <summary>
        /// Copies a file from source to destination
        /// </summary>
        public void CopyFile(string sourceFilePath, string destinationFilePath)
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            File.Copy(sourceFilePath, destinationFilePath);
        }

        /// <summary>
        /// Deletes a file
        /// </summary>
        public void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Checks if a directory exists
        /// </summary>
        public bool DirectoryExists(string directoryPath)
        {
            return Directory.Exists(directoryPath);
        }

        /// <summary>
        /// Gets the size of a file in bytes
        /// </summary>
        public long GetFileSize(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Exists ? fileInfo.Length : 0;
        }

        /// <summary>
        /// Checks if a file exists
        /// </summary>
        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }
        
        /// <summary>
        /// Creates output directory if it doesn't exist
        /// </summary>
        public string CreateOutputDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                _logger.Information("Created output directory: {DirectoryPath}", directoryPath);
            }
            return directoryPath;
        }
    }
}
