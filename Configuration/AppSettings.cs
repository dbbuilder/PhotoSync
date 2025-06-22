namespace PhotoSync.Configuration
{
    /// <summary>
    /// Application configuration settings for photo import/export operations
    /// </summary>
    public class AppSettings
    {
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public PhotoSettings PhotoSettings { get; set; } = new();
        public AzureSettings Azure { get; set; } = new();
        public AzureStorageSettings AzureStorage { get; set; } = new();
    }

    /// <summary>
    /// Database connection string configuration
    /// </summary>
    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; } = string.Empty;
    }

    /// <summary>
    /// Photo-specific configuration settings
    /// </summary>
    public class PhotoSettings
    {
        public string TableName { get; set; } = "Photos";
        public string ImageFieldName { get; set; } = "ImageData";
        public string CodeFieldName { get; set; } = "Code";
        public string ImportFolder { get; set; } = string.Empty;
        public string ExportFolder { get; set; } = string.Empty;
        public string PhotoFieldName { get; set; } = "ImageData";
        public string AzureStoragePathFieldName { get; set; } = "AzureStoragePath";
        
        // Workflow settings
        public string ImportedArchiveFolder { get; set; } = string.Empty;
        public bool EnableAutoArchive { get; set; } = true;
        public bool EnableDuplicateCheck { get; set; } = true;
        public bool UseIncrementalExport { get; set; } = true;
        public string ExportFileNameFormat { get; set; } = "{Code}.jpg";
        public bool TrackFileHash { get; set; } = true;
        public bool PreserveSourceStructure { get; set; } = false;
        public int MaxParallelOperations { get; set; } = 4;
    }

    /// <summary>
    /// Azure-specific configuration settings
    /// </summary>
    public class AzureSettings
    {
        public KeyVaultSettings KeyVault { get; set; } = new();
        public ApplicationInsightsSettings ApplicationInsights { get; set; } = new();
    }

    /// <summary>
    /// Azure Key Vault configuration
    /// </summary>
    public class KeyVaultSettings
    {
        public string VaultUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Application Insights configuration
    /// </summary>
    public class ApplicationInsightsSettings
    {
        public string InstrumentationKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Azure Storage configuration settings
    /// </summary>
    public class AzureStorageSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string ContainerName { get; set; } = "photos";
        public bool UseDefaultAzureCredential { get; set; } = false;
        public string StorageAccountName { get; set; } = string.Empty;
    }
}
