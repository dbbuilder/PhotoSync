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
        public string TableName { get; set; } = string.Empty;
        public string ImageFieldName { get; set; } = string.Empty;
        public string CodeFieldName { get; set; } = string.Empty;
        public string ImportFolder { get; set; } = string.Empty;
        public string ExportFolder { get; set; } = string.Empty;
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
}
