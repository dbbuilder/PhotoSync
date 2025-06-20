# PhotoSync - Photo Import/Export Console Application

A .NET Core console application for importing JPG images from folders into a SQL Server database and exporting them back to folders. Designed for Azure deployment with enterprise-grade reliability and monitoring.

## Features

- **Import**: Load JPG files from a folder into a database table with configurable field names
- **Export**: Save database images to a folder as JPG files named by their code
- **Status**: Check database connectivity and image count
- **Test**: Validate configuration and folder access
- **Configuration**: Flexible configuration via appsettings.json
- **Resilience**: Built-in retry policies and error handling
- **Logging**: Comprehensive structured logging with Serilog
- **Azure Ready**: Designed for deployment to Azure Linux App Services

## Prerequisites

- .NET 6.0 or later
- SQL Server (local or Azure SQL Database)
- Visual Studio 2022 or VS Code

## Quick Start

1. **Clone and setup**:
   ```bash
   git clone <repository-url>
   cd PhotoSync
   dotnet restore
   ```

2. **Setup configuration**:
   ```powershell
   # Use the automated setup script
   .\Scripts\setup-config.ps1
   
   # OR manually copy template files
   copy appsettings.json.template appsettings.json
   copy appsettings.Development.json.template appsettings.Development.json
   ```
   
   ⚠️ **Important**: Update the configuration files with your actual values:
   - Database connection strings
   - Azure Key Vault URL (if using Azure)
   - Application Insights key (if using Azure)
   
   📋 **For detailed configuration instructions, see [CONFIG_SETUP.md](CONFIG_SETUP.md)**

3. **Configure database**:
   - Verify connection string in your appsettings files
   - Run `Database/StoredProcedures.sql` in your SQL Server

3. **Configure folders**:
   ```json
   {
     "PhotoSettings": {
       "ImportFolder": "C:\\MyPhotos",
       "ExportFolder": "C:\\ExportedPhotos"
     }
   }
   ```

4. **Test setup**:
   ```bash
   dotnet run test
   ```

5. **Import photos**:
   ```bash
   dotnet run import
   ```

## Usage

### Commands

```bash
# Import from configured folder
PhotoSync.exe import

# Import from specific folder
PhotoSync.exe import "C:\MyPhotos"

# Export to configured folder
PhotoSync.exe export

# Export to specific folder
PhotoSync.exe export "C:\ExportedPhotos"

# Check database status
PhotoSync.exe status

# Test configuration
PhotoSync.exe test
```

### Configuration

The application uses appsettings.json files for configuration. For security, actual configuration files are excluded from Git.

**Setup Process:**
1. Copy template files: `.\Scripts\setup-config.ps1` (or manually copy .template files)
2. Update configuration values with your environment-specific settings
3. For detailed instructions, see [CONFIG_SETUP.md](CONFIG_SETUP.md)

**Template Structure:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "YOUR_CONNECTION_STRING_HERE"
  },
  "PhotoSettings": {
    "TableName": "Photos",
    "ImageFieldName": "ImageData", 
    "CodeFieldName": "Code",
    "ImportFolder": "C:\\Temp\\PhotoSync\\Import",
    "ExportFolder": "C:\\Temp\\PhotoSync\\Export"
  },
  "Azure": {
    "KeyVault": {
      "VaultUrl": "https://YOUR_KEYVAULT_NAME.vault.azure.net/"
    },
    "ApplicationInsights": {
      "InstrumentationKey": "YOUR_INSTRUMENTATION_KEY"
    }
  }
}
```

**Template Files Available:**
- `appsettings.json.template` - Base configuration
- `appsettings.Development.json.template` - Development overrides 
- `appsettings.Production.json.template` - Production overrides

### Environment Variables

Override settings with `PHOTOSYNC_` prefixed environment variables:

```bash
export PHOTOSYNC_ConnectionStrings__DefaultConnection="Server=..."
export PHOTOSYNC_PhotoSettings__ImportFolder="/custom/import/path"
```

## Database Schema

The application expects a table with:
- `Code` (nvarchar): Unique identifier for the image
- `ImageData` (varbinary(max)): Binary image data  
- `CreatedDate` (datetime2): Creation timestamp
- `ModifiedDate` (datetime2): Last modification timestamp

## Architecture

- **Services**: Database and file operations with interfaces
- **Commands**: Import and export command handlers
- **Configuration**: Strongly-typed configuration classes
- **Models**: Data transfer objects
- **Logging**: Structured logging with Serilog
- **Resilience**: Polly retry policies for database operations

## Deployment

### Local Development
```bash
dotnet build
dotnet run import
```

### Azure App Service
1. Configure connection strings in Azure portal
2. Set application settings for folder paths
3. Deploy using Azure DevOps or GitHub Actions

### Docker
```bash
docker build -t photosync .
docker run -e ConnectionStrings__DefaultConnection="..." photosync import
```

## Logging

Logs are written to:
- Console output
- Rolling file logs in `logs/` directory
- Application Insights (when configured)

Configure log levels in `appsettings.json`.

## Error Handling

- Comprehensive exception handling at all levels
- Retry policies for transient database failures
- Detailed logging for troubleshooting
- Graceful degradation for individual file failures

## Development

### Building
```bash
dotnet build --configuration Release
```

### Testing
```bash
dotnet test
```

### Publishing
```bash
dotnet publish --configuration Release --runtime linux-x64 --self-contained
```

## Troubleshooting

1. **Database connection issues**: 
   - Check connection string
   - Verify SQL Server is accessible
   - Run `PhotoSync.exe test`

2. **File access issues**:
   - Check folder permissions
   - Verify paths exist
   - Use absolute paths

3. **Import failures**:
   - Check image file formats (JPG only)
   - Verify files aren't corrupted
   - Check available disk space

## Support

For issues:
1. Check logs in `logs/` directory
2. Run `PhotoSync.exe test` to validate configuration
3. Verify database connectivity and stored procedures
4. Review folder permissions for import/export paths

## License

This project is licensed under the MIT License - see the LICENSE file for details.
