using PhotoSync.Configuration;
using PhotoSync.Services;
using Serilog;

namespace PhotoSync.Commands
{
    /// <summary>
    /// Command handler for nullifying a field and processing all NULL values
    /// </summary>
    public class WriteAllCommand
    {
        private readonly IDatabaseService _databaseService;
        private readonly IFileService _fileService;
        private readonly ImportCommand _importCommand;
        private readonly ExportCommand _exportCommand;
        private readonly ToAzureStorageCommand _toAzureCommand;
        private readonly FromAzureStorageCommand _fromAzureCommand;
        private readonly PhotoSettings _photoSettings;
        private readonly ILogger _logger;

        public WriteAllCommand(
            IDatabaseService databaseService,
            IFileService fileService,
            ImportCommand importCommand,
            ExportCommand exportCommand,
            ToAzureStorageCommand toAzureCommand,
            FromAzureStorageCommand fromAzureCommand,
            PhotoSettings photoSettings,
            ILogger logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _importCommand = importCommand ?? throw new ArgumentNullException(nameof(importCommand));
            _exportCommand = exportCommand ?? throw new ArgumentNullException(nameof(exportCommand));
            _toAzureCommand = toAzureCommand ?? throw new ArgumentNullException(nameof(toAzureCommand));
            _fromAzureCommand = fromAzureCommand ?? throw new ArgumentNullException(nameof(fromAzureCommand));
            _photoSettings = photoSettings ?? throw new ArgumentNullException(nameof(photoSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the WriteAll command with optional field nullification
        /// </summary>
        /// <param name="fieldToNullify">Optional field name to nullify before processing (ImageData or AzureStoragePath)</param>
        /// <param name="workflow">Comma-separated list of operations: import,azure,export (default: all)</param>
        /// <param name="skipArchive">Skip archiving imported files</param>
        /// <param name="dryRun">Preview operations without executing</param>
        /// <returns>Command result with statistics</returns>
        public async Task<WriteAllResult> ExecuteAsync(
            string? fieldToNullify = null, 
            string workflow = "import,azure,export",
            bool skipArchive = false,
            bool dryRun = false)
        {
            var result = new WriteAllResult
            {
                FieldNullified = fieldToNullify
            };

            try
            {
                _logger.Information("Starting WriteAll operation");

                // Test database connection
                if (!await _databaseService.TestConnectionAsync())
                {
                    var error = "Database connection failed";
                    _logger.Error(error);
                    result.ErrorMessage = error;
                    return result;
                }

                // Parse workflow steps
                var steps = workflow.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries);
                result.WorkflowSteps = steps.ToList();

                if (dryRun)
                {
                    _logger.Information("DRY RUN MODE - No changes will be made");
                    Console.WriteLine("🔍 DRY RUN MODE - Preview of operations:");
                    Console.WriteLine($"  Workflow: {string.Join(" → ", steps)}");
                    if (!string.IsNullOrWhiteSpace(fieldToNullify))
                        Console.WriteLine($"  Nullify Field: {fieldToNullify}");
                    Console.WriteLine();
                }

                // Nullify field if specified
                if (!string.IsNullOrWhiteSpace(fieldToNullify) && !dryRun)
                {
                    _logger.Information("Nullifying field: {FieldName}", fieldToNullify);
                    var nullifiedCount = await _databaseService.NullifyFieldAsync(fieldToNullify);
                    result.RecordsNullified = nullifiedCount;
                    _logger.Information("Nullified {Count} records in field {FieldName}", nullifiedCount, fieldToNullify);

                    if (nullifiedCount == 0)
                    {
                        _logger.Warning("No records were nullified. Field may be invalid or already NULL.");
                    }
                }

                // Run Import operation if requested
                if (steps.Contains("import"))
                {
                    _logger.Information("Running Import operation...");
                    
                    if (dryRun)
                    {
                        var folderInfo = await _fileService.GetFolderInfoAsync(_photoSettings.ImportFolder);
                        Console.WriteLine($"  📥 Would import {folderInfo.JpgFiles} photos from {_photoSettings.ImportFolder}");
                    }
                    else
                    {
                        var importResult = await _importCommand.ExecuteAsync(null, skipArchive);
                        result.ImportResult = importResult;
                        
                        if (!string.IsNullOrEmpty(importResult.ErrorMessage))
                        {
                            _logger.Warning("Import operation encountered errors: {Error}", importResult.ErrorMessage);
                        }
                        else
                        {
                            _logger.Information("Import operation completed: {Summary}", importResult.Summary);
                        }
                    }
                }

                // Run Azure operations if requested
                if (steps.Contains("azure") || steps.Contains("toazure") || steps.Contains("fromazure"))
                {
                    // ToAzureStorage operation
                    if (steps.Contains("azure") || steps.Contains("toazure"))
                    {
                        _logger.Information("Running ToAzureStorage operation...");
                        
                        if (dryRun)
                        {
                            var pendingUploads = await _databaseService.GetImagesWithNullAzurePathAsync();
                            Console.WriteLine($"  ☁️  Would upload {pendingUploads.Count} photos to Azure Storage");
                        }
                        else
                        {
                            var toAzureResult = await _toAzureCommand.ExecuteAsync();
                            result.ToAzureStorageResult = toAzureResult;

                            if (!string.IsNullOrEmpty(toAzureResult.ErrorMessage))
                            {
                                _logger.Warning("ToAzureStorage operation encountered errors: {Error}", toAzureResult.ErrorMessage);
                            }
                            else
                            {
                                _logger.Information("ToAzureStorage operation completed: {Summary}", toAzureResult.Summary);
                            }
                        }
                    }

                    // FromAzureStorage operation
                    if (steps.Contains("azure") || steps.Contains("fromazure"))
                    {
                        _logger.Information("Running FromAzureStorage operation...");
                        
                        if (dryRun)
                        {
                            var pendingDownloads = await _databaseService.GetImagesWithNullPhotoDataAsync();
                            Console.WriteLine($"  ☁️  Would download {pendingDownloads.Count} photos from Azure Storage");
                        }
                        else
                        {
                            var fromAzureResult = await _fromAzureCommand.ExecuteAsync();
                            result.FromAzureStorageResult = fromAzureResult;

                            if (!string.IsNullOrEmpty(fromAzureResult.ErrorMessage))
                            {
                                _logger.Warning("FromAzureStorage operation encountered errors: {Error}", fromAzureResult.ErrorMessage);
                            }
                            else
                            {
                                _logger.Information("FromAzureStorage operation completed: {Summary}", fromAzureResult.Summary);
                            }
                        }
                    }
                }

                // Run Export operation if requested
                if (steps.Contains("export"))
                {
                    _logger.Information("Running Export operation...");
                    
                    if (dryRun)
                    {
                        var pendingExports = await _databaseService.GetPhotosForIncrementalExportAsync();
                        Console.WriteLine($"  📤 Would export {pendingExports.Count} photos to {_photoSettings.ExportFolder}");
                    }
                    else
                    {
                        var exportResult = await _exportCommand.ExecuteAsync(null, true, false);
                        result.ExportResult = exportResult;
                        
                        if (!string.IsNullOrEmpty(exportResult.ErrorMessage))
                        {
                            _logger.Warning("Export operation encountered errors: {Error}", exportResult.ErrorMessage);
                        }
                        else
                        {
                            _logger.Information("Export operation completed: {Summary}", exportResult.Summary);
                        }
                    }
                }

                // Determine overall success
                result.IsSuccess = !dryRun && DetermineOverallSuccess(result);
                result.IsDryRun = dryRun;

                result.CompletedAt = DateTime.UtcNow;

                _logger.Information("WriteAll operation completed. Total processed: {TotalProcessed}, Total failed: {TotalFailed}",
                    result.TotalProcessed, result.TotalFailed);
            }
            catch (System.Exception ex)
            {
                var error = "Error during WriteAll operation";
                _logger.Error(ex, error);
                result.ErrorMessage = error;
                result.CompletedAt = DateTime.UtcNow;
            }

            return result;
        }

        /// <summary>
        /// Determines overall success based on individual operation results
        /// </summary>
        private bool DetermineOverallSuccess(WriteAllResult result)
        {
            var success = true;

            if (result.ImportResult != null)
                success &= result.ImportResult.IsSuccess || result.ImportResult.TotalFilesFound == 0;

            if (result.ToAzureStorageResult != null)
                success &= result.ToAzureStorageResult.IsSuccess || result.ToAzureStorageResult.TotalRecordsFound == 0;

            if (result.FromAzureStorageResult != null)
                success &= result.FromAzureStorageResult.IsSuccess || result.FromAzureStorageResult.TotalRecordsFound == 0;

            if (result.ExportResult != null)
                success &= result.ExportResult.IsSuccess || result.ExportResult.TotalImagesFound == 0;

            return success;
        }
    }

    /// <summary>
    /// Result of a WriteAll operation
    /// </summary>
    public class WriteAllResult
    {
        public bool IsSuccess { get; set; }
        public bool IsDryRun { get; set; }
        public List<string> WorkflowSteps { get; set; } = new();
        public string? FieldNullified { get; set; }
        public int RecordsNullified { get; set; }
        public ImportResult? ImportResult { get; set; }
        public ExportResult? ExportResult { get; set; }
        public AzureOperationResult? ToAzureStorageResult { get; set; }
        public AzureOperationResult? FromAzureStorageResult { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;

        public int TotalProcessed => 
            (ImportResult?.SuccessCount ?? 0) +
            (ExportResult?.SuccessCount ?? 0) +
            (ToAzureStorageResult?.SuccessCount ?? 0) + 
            (FromAzureStorageResult?.SuccessCount ?? 0);

        public int TotalFailed => 
            (ImportResult?.FailureCount ?? 0) +
            (ExportResult?.FailureCount ?? 0) +
            (ToAzureStorageResult?.FailureCount ?? 0) + 
            (FromAzureStorageResult?.FailureCount ?? 0);

        public string Summary
        {
            get
            {
                if (IsDryRun)
                {
                    return $"WriteAll DRY RUN completed in {Duration:mm\\:ss} - No changes made";
                }

                var parts = new List<string>();
                
                if (!string.IsNullOrEmpty(FieldNullified) && RecordsNullified > 0)
                {
                    parts.Add($"Nullified {RecordsNullified} records in {FieldNullified}");
                }

                if (ImportResult != null && ImportResult.TotalFilesFound > 0)
                {
                    parts.Add($"Imported {ImportResult.SuccessCount}/{ImportResult.TotalFilesFound} files");
                }

                if (ToAzureStorageResult != null && ToAzureStorageResult.TotalRecordsFound > 0)
                {
                    parts.Add($"Uploaded {ToAzureStorageResult.SuccessCount}/{ToAzureStorageResult.TotalRecordsFound} to Azure");
                }

                if (FromAzureStorageResult != null && FromAzureStorageResult.TotalRecordsFound > 0)
                {
                    parts.Add($"Downloaded {FromAzureStorageResult.SuccessCount}/{FromAzureStorageResult.TotalRecordsFound} from Azure");
                }

                if (ExportResult != null && ExportResult.TotalImagesFound > 0)
                {
                    parts.Add($"Exported {ExportResult.SuccessCount}/{ExportResult.TotalImagesFound} files");
                }

                if (parts.Any())
                {
                    return $"WriteAll completed in {Duration:mm\\:ss}: {string.Join(", ", parts)}";
                }
                else
                {
                    return $"WriteAll completed in {Duration:mm\\:ss}: No operations performed";
                }
            }
        }
    }
}