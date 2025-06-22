using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using Serilog;
using System.Collections.Concurrent;

namespace PhotoSync.Commands
{
    /// <summary>
    /// Command handler for importing images from folder to database
    /// </summary>
    public class ImportCommand
    {
        private readonly IDatabaseService _databaseService;
        private readonly IFileService _fileService;
        private readonly PhotoSettings _photoSettings;
        private readonly ILogger _logger;

        public ImportCommand(
            IDatabaseService databaseService, 
            IFileService fileService, 
            PhotoSettings photoSettings, 
            ILogger logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _photoSettings = photoSettings ?? throw new ArgumentNullException(nameof(photoSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the import command to load images from folder to database
        /// </summary>
        /// <param name="importFolder">Optional override for import folder from configuration</param>
        /// <returns>Import result with statistics</returns>
        public async Task<ImportResult> ExecuteAsync(string? importFolder = null, bool skipArchive = false)
        {
            var folderPath = importFolder ?? _photoSettings.ImportFolder;
            var result = new ImportResult { FolderPath = folderPath };

            try
            {
                _logger.Information("Starting import operation from folder: {FolderPath}", folderPath);

                // Validate folder path
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    var error = "Import folder path is not configured";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Check if folder exists and is accessible
                if (!_fileService.ValidateFolderAccess(folderPath))
                {
                    var error = $"Import folder does not exist or is not accessible: {folderPath}";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Test database connection
                if (!await _databaseService.TestConnectionAsync())
                {
                    var error = "Database connection failed";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Get folder information
                var folderInfo = await _fileService.GetFolderInfoAsync(folderPath);
                _logger.Information("Folder analysis: {TotalFiles} total files, {JpgFiles} JPG files", 
                    folderInfo.TotalFiles, folderInfo.JpgFiles);

                // Get all images from the folder
                var images = await _fileService.GetImagesFromFolderAsync(folderPath);

                if (!images.Any())
                {
                    _logger.Warning("No images found in import folder: {FolderPath}", folderPath);
                    result.IsSuccess = true; // Not an error, just no work to do
                    return result;
                }

                _logger.Information("Found {ImageCount} images to import", images.Count);
                result.TotalFilesFound = images.Count;

                // Track successfully imported files for archiving
                var successfulImports = new ConcurrentBag<string>();
                var duplicateFiles = new ConcurrentBag<string>();

                // Get full file paths for archiving
                var imagePaths = Directory.GetFiles(folderPath, "*.jpg", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(folderPath, "*.jpeg", SearchOption.TopDirectoryOnly))
                    .ToDictionary(Path.GetFileNameWithoutExtension, p => p, StringComparer.OrdinalIgnoreCase);

                // Process each image
                foreach (var (fileName, imageData) in images)
                {
                    try
                    {
                        var importDate = DateTime.UtcNow;
                        var filePath = imagePaths.ContainsKey(fileName) ? imagePaths[fileName] : null;
                        var sourceFileName = filePath != null ? Path.GetFileName(filePath) : $"{fileName}.jpg";
                        
                        // Calculate hash if enabled
                        string? fileHash = null;
                        if (_photoSettings.TrackFileHash)
                        {
                            fileHash = _fileService.CalculateHash(imageData);
                            
                            // Check for duplicates if enabled
                            if (_photoSettings.EnableDuplicateCheck && !string.IsNullOrEmpty(fileHash))
                            {
                                var duplicate = await _databaseService.CheckDuplicateByHashAsync(fileHash);
                                if (duplicate != null)
                                {
                                    result.DuplicateCount++;
                                    result.DuplicateFiles.Add($"{fileName} (duplicate of {duplicate.Code})");
                                    _logger.Warning("Skipping duplicate file: {FileName} (matches {ExistingCode})", 
                                        fileName, duplicate.Code);
                                    
                                    if (filePath != null)
                                        duplicateFiles.Add(filePath);
                                    
                                    continue;
                                }
                            }
                        }

                        // Create image record with tracking info
                        var imageRecord = new ImageRecord
                        {
                            Code = fileName,
                            ImageData = imageData,
                            CreatedDate = importDate,
                            ImageSource = $"FILE:{filePath ?? fileName}",
                            SourceFileName = sourceFileName,
                            ImportedDate = importDate,
                            PhotoModifiedDate = importDate,
                            FileHash = fileHash,
                            FileSize = imageData.Length
                        };

                        // Save to database
                        var saveResult = await _databaseService.SaveImageAsync(imageRecord);

                        if (saveResult)
                        {
                            result.SuccessCount++;
                            _logger.Information("Successfully imported image: {FileName} ({FileSize})", 
                                fileName, imageRecord.ImageSizeFormatted);
                            
                            // Track successful import for archiving
                            if (filePath != null)
                                successfulImports.Add(filePath);
                            
                            // Update import tracking
                            await _databaseService.UpdateImportTrackingAsync(
                                fileName, 
                                importDate, 
                                imageRecord.ImageSource!, 
                                sourceFileName, 
                                fileHash ?? string.Empty, 
                                imageData.Length);
                        }
                        else
                        {
                            result.FailureCount++;
                            result.FailedFiles.Add(fileName);
                            _logger.Error("Failed to import image: {FileName}", fileName);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        result.FailureCount++;
                        result.FailedFiles.Add(fileName);
                        _logger.Error(ex, "Error processing image: {FileName}", fileName);
                    }
                }

                // Archive successfully imported files if enabled
                if (_photoSettings.EnableAutoArchive && !skipArchive && 
                    !string.IsNullOrEmpty(_photoSettings.ImportedArchiveFolder) && 
                    (successfulImports.Any() || duplicateFiles.Any()))
                {
                    try
                    {
                        _logger.Information("Archiving {Count} successfully imported files", 
                            successfulImports.Count + duplicateFiles.Count);
                        
                        var allFilesToArchive = successfulImports.Concat(duplicateFiles);
                        var archiveResults = await _fileService.ArchiveFilesAsync(
                            allFilesToArchive, 
                            _photoSettings.ImportedArchiveFolder, 
                            _photoSettings.MaxParallelOperations);
                        
                        var successfulArchives = archiveResults.Count(r => !string.IsNullOrEmpty(r.Value));
                        result.ArchivedCount = successfulArchives;
                        
                        _logger.Information("Archived {Count} files to {ArchiveFolder}", 
                            successfulArchives, _photoSettings.ImportedArchiveFolder);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error archiving imported files");
                        // Don't fail the import if archiving fails
                    }
                }

                result.IsSuccess = result.SuccessCount > 0;
                result.CompletedAt = DateTime.UtcNow;

                _logger.Information("Import completed. Success: {Success}, Failed: {Failed}, Duplicates: {Duplicates}, Archived: {Archived}", 
                    result.SuccessCount, result.FailureCount, result.DuplicateCount, result.ArchivedCount);
            }
            catch (System.Exception ex)
            {
                var error = $"Error during import operation from folder: {folderPath}";
                _logger.Error(ex, error);
                result.ErrorMessage = error;
                result.CompletedAt = DateTime.UtcNow;
            }

            return result;
        }
    }

    /// <summary>
    /// Result of an import operation
    /// </summary>
    public class ImportResult
    {
        public bool IsSuccess { get; set; }
        public string FolderPath { get; set; } = string.Empty;
        public int TotalFilesFound { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ArchivedCount { get; set; }
        public List<string> FailedFiles { get; set; } = new();
        public List<string> DuplicateFiles { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;
        
        public string Summary => $"Imported {SuccessCount}/{TotalFilesFound} images in {Duration:mm\\:ss}" +
            (DuplicateCount > 0 ? $" ({DuplicateCount} duplicates skipped)" : "") +
            (ArchivedCount > 0 ? $" ({ArchivedCount} files archived)" : "");
    }
}
