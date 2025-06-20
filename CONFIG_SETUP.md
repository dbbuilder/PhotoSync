# Configuration Setup Guide

## Overview
This project uses appsettings.json files for configuration. The actual configuration files are excluded from Git for security reasons, but template files are provided to show the required structure.

## Quick Setup

### Option 1: Copy Template Files
Copy the template files and remove the `.template` extension:

```cmd
copy appsettings.json.template appsettings.json
copy appsettings.Development.json.template appsettings.Development.json
copy appsettings.Production.json.template appsettings.Production.json
```

### Option 2: Use PowerShell Script
Run the provided setup script:
```powershell
.\Scripts\setup-config.ps1
```

## Required Configuration Values

### Connection Strings
- **DefaultConnection**: Primary database connection string
  - Replace `YOUR_SERVER` with your SQL Server instance
  - Replace `YOUR_DATABASE` with your database name
  - Replace `YOUR_USERNAME` and `YOUR_PASSWORD` with credentials
- **TestConnection** (Development only): Used for testing database operations

### Photo Settings
- **TableName**: Database table containing photo data (default: "Photos")
- **ImageFieldName**: Column name for image binary data (default: "ImageData")
- **CodeFieldName**: Column name for photo identifier (default: "Code")
- **ImportFolder**: Local folder for importing images
- **ExportFolder**: Local folder for exporting images

### Azure Configuration
- **KeyVault.VaultUrl**: Azure Key Vault URL for secure configuration storage
- **ApplicationInsights.InstrumentationKey**: Azure Application Insights key for telemetry

### Logging Configuration
- **Serilog**: Logging framework configuration
  - Development: Debug level logging
  - Production: Information level with filtered Microsoft/System logs

## Security Notes

⚠️ **IMPORTANT**: Never commit actual appsettings files to Git. They contain sensitive information including:
- Database connection strings with passwords
- Azure service credentials
- API keys and secrets

The .gitignore file is configured to prevent accidental commits of these files.

## Environment-Specific Configuration

### Development (appsettings.Development.json)
- Includes test database connection
- Debug-level logging enabled
- Local file paths for import/export folders

### Production (appsettings.Production.json)
- Production database connection
- Information-level logging
- Linux container paths for Azure deployment
- Azure services integration

## Troubleshooting

### Missing Configuration File Error
If you see "Configuration file not found" errors:
1. Ensure you've copied the template files as described above
2. Verify the files are in the project root directory
3. Check that file names match exactly (case-sensitive on Linux)

### Connection String Issues
If database connections fail:
1. Verify server name and port (if using non-standard port)
2. Ensure database exists and user has appropriate permissions
3. Check firewall settings for database server access
4. Validate Encrypt and TrustServerCertificate settings for your environment

### Azure Service Issues
If Azure services fail to connect:
1. Verify Key Vault URL format: `https://YOUR_KEYVAULT_NAME.vault.azure.net/`
2. Ensure Application Insights instrumentation key is correct
3. Check Azure service permissions and authentication
