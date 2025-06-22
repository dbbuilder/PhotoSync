# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build and Run
```bash
# Build the project
dotnet build

# Run with various commands
dotnet run -- import
dotnet run -- export
dotnet run -- status
dotnet run -- test
dotnet run -- diagnose
dotnet run -- toazurestorage
dotnet run -- fromazurestorage
dotnet run -- writeall [fieldname]

# Run with settings override
dotnet run -- import -settings "C:\\Config\\prod-settings.json"
```

### Database Setup
```powershell
# Run the database setup script (creates PHOTOS schema and stored procedures)
sqlcmd -S YOUR_SERVER -d YOUR_DATABASE -i Database/StoredProcedures.sql
```

### Testing
```bash
# Run tests (when implemented)
dotnet test

# Test database connection
dotnet run -- diagnose

# Test configuration
dotnet run -- test
```

## Architecture

### Overview
PhotoSync is a .NET 8 console application for synchronizing photos between a SQL Server database and file system/Azure Storage. It uses the PHOTOS schema to avoid conflicts with existing database objects.

### Key Components

1. **Commands** - Command pattern implementation for operations:
   - `ImportCommand` - Import photos from file system to database
   - `ExportCommand` - Export photos from database to file system
   - `ToAzureStorageCommand` - Upload photos to Azure Storage
   - `FromAzureStorageCommand` - Download photos from Azure Storage
   - `WriteAllCommand` - Batch process NULL values

2. **Services**:
   - `DatabaseService` - SQL Server operations using stored procedures in PHOTOS schema
   - `FileService` - File system operations
   - `AzureStorageService` - Azure Blob Storage operations

3. **Configuration**:
   - Uses appsettings.json with environment-specific overrides
   - Supports -settings flag for custom configuration files
   - Azure Storage can use connection string or DefaultAzureCredential

### Database Schema
All database objects are in the PHOTOS schema:
- Table: `PHOTOS.Photos` with fields:
  - `Code` (unique identifier)
  - `ImageData` (binary photo data, nullable)
  - `AzureStoragePath` (Azure Storage URL, nullable)
  - `CreatedDate`, `ModifiedDate`

### Dependency Flow
```
Program.cs 
  → Commands (ImportCommand, ExportCommand, etc.)
    → Services (DatabaseService, FileService, AzureStorageService)
      → SQL Server (via PHOTOS schema stored procedures)
      → File System
      → Azure Storage
```

### Key Design Decisions
- All database operations use stored procedures for security and performance
- PHOTOS schema isolation prevents conflicts with existing database objects
- Nullable ImageData allows hybrid storage (local binary or Azure reference)
- Polly retry policies for resilient database operations
- Supports both connection string and managed identity for Azure Storage