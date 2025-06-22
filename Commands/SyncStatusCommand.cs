using PhotoSync.Services;
using Serilog;

namespace PhotoSync.Commands
{
    /// <summary>
    /// Command handler for displaying sync status and statistics
    /// </summary>
    public class SyncStatusCommand
    {
        private readonly IDatabaseService _databaseService;
        private readonly IFileService _fileService;
        private readonly ILogger _logger;

        public SyncStatusCommand(
            IDatabaseService databaseService,
            IFileService fileService,
            ILogger logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the sync status command
        /// </summary>
        /// <param name="detailed">Show detailed statistics</param>
        /// <returns>Exit code</returns>
        public async Task<int> ExecuteAsync(bool detailed = false)
        {
            try
            {
                _logger.Information("Retrieving sync status...");

                // Test database connection
                if (!await _databaseService.TestConnectionAsync())
                {
                    Console.WriteLine("❌ Database connection failed");
                    return 1;
                }

                // Get sync status from database
                var status = await _databaseService.GetSyncStatusAsync();

                Console.WriteLine();
                Console.WriteLine("PhotoSync Status Report");
                Console.WriteLine("======================");
                Console.WriteLine();

                // Basic statistics
                Console.WriteLine("📊 Overview:");
                Console.WriteLine($"  Total Photos: {status.GetValueOrDefault("TotalPhotos", 0):N0}");
                Console.WriteLine($"  Photos with Data: {status.GetValueOrDefault("PhotosWithData", 0):N0}");
                Console.WriteLine($"  Photos in Azure: {status.GetValueOrDefault("PhotosInAzure", 0):N0}");
                Console.WriteLine();

                // Export status
                var pendingExport = Convert.ToInt32(status.GetValueOrDefault("PendingExport", 0));
                var staleExports = Convert.ToInt32(status.GetValueOrDefault("StaleExports", 0));
                
                Console.WriteLine("📤 Export Status:");
                Console.WriteLine($"  Never Exported: {pendingExport:N0}");
                Console.WriteLine($"  Exports Out of Date: {staleExports:N0}");
                Console.WriteLine($"  Total Needing Export: {pendingExport + staleExports:N0}");
                Console.WriteLine();

                // Azure sync status
                var pendingSync = Convert.ToInt32(status.GetValueOrDefault("PendingAzureSync", 0));
                
                Console.WriteLine("☁️  Azure Sync Status:");
                Console.WriteLine($"  Pending Sync to Azure: {pendingSync:N0}");
                Console.WriteLine();

                // Duplicate detection
                var photosWithHash = Convert.ToInt32(status.GetValueOrDefault("PhotosWithHash", 0));
                var uniquePhotos = Convert.ToInt32(status.GetValueOrDefault("UniquePhotos", 0));
                var duplicates = photosWithHash > 0 ? photosWithHash - uniquePhotos : 0;

                Console.WriteLine("🔍 Duplicate Detection:");
                Console.WriteLine($"  Photos with Hash: {photosWithHash:N0}");
                Console.WriteLine($"  Unique Photos: {uniquePhotos:N0}");
                Console.WriteLine($"  Duplicates Found: {duplicates:N0}");
                Console.WriteLine();

                // Timeline information
                Console.WriteLine("📅 Timeline:");
                
                var firstImport = status.GetValueOrDefault("FirstImportDate", null) as DateTime?;
                var lastImport = status.GetValueOrDefault("LastImportDate", null) as DateTime?;
                var firstExport = status.GetValueOrDefault("FirstExportDate", null) as DateTime?;
                var lastExport = status.GetValueOrDefault("LastExportDate", null) as DateTime?;
                var firstAzure = status.GetValueOrDefault("FirstAzureUploadDate", null) as DateTime?;
                var lastAzure = status.GetValueOrDefault("LastAzureUploadDate", null) as DateTime?;

                if (firstImport.HasValue)
                    Console.WriteLine($"  First Import: {firstImport:yyyy-MM-dd HH:mm}");
                if (lastImport.HasValue)
                    Console.WriteLine($"  Last Import: {lastImport:yyyy-MM-dd HH:mm} ({GetTimeAgo(lastImport.Value)})");
                
                if (firstExport.HasValue)
                    Console.WriteLine($"  First Export: {firstExport:yyyy-MM-dd HH:mm}");
                if (lastExport.HasValue)
                    Console.WriteLine($"  Last Export: {lastExport:yyyy-MM-dd HH:mm} ({GetTimeAgo(lastExport.Value)})");
                
                if (firstAzure.HasValue)
                    Console.WriteLine($"  First Azure Upload: {firstAzure:yyyy-MM-dd HH:mm}");
                if (lastAzure.HasValue)
                    Console.WriteLine($"  Last Azure Upload: {lastAzure:yyyy-MM-dd HH:mm} ({GetTimeAgo(lastAzure.Value)})");

                Console.WriteLine();

                // Recommendations
                if (pendingExport + staleExports > 0 || pendingSync > 0 || duplicates > 0)
                {
                    Console.WriteLine("💡 Recommendations:");
                    
                    if (pendingExport + staleExports > 0)
                        Console.WriteLine($"  • Run 'photosync export -incremental' to export {pendingExport + staleExports:N0} photos");
                    
                    if (pendingSync > 0)
                        Console.WriteLine($"  • Run 'photosync toazurestorage -force' to sync {pendingSync:N0} photos to Azure");
                    
                    if (duplicates > 0)
                        Console.WriteLine($"  • {duplicates:N0} duplicate photos detected in the database");
                    
                    Console.WriteLine();
                }

                // Detailed view
                if (detailed)
                {
                    Console.WriteLine("📋 Detailed Statistics:");
                    Console.WriteLine("  (Use 'photosync status' for summary view)");
                    Console.WriteLine();
                    
                    foreach (var kvp in status.OrderBy(x => x.Key))
                    {
                        var value = kvp.Value?.ToString() ?? "null";
                        Console.WriteLine($"  {kvp.Key}: {value}");
                    }
                    Console.WriteLine();
                }

                _logger.Information("Sync status retrieved successfully");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving sync status");
                Console.WriteLine($"❌ Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Gets a human-readable time ago string
        /// </summary>
        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minutes ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hours ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays} days ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} months ago";
            
            return $"{(int)(timeSpan.TotalDays / 365)} years ago";
        }
    }
}