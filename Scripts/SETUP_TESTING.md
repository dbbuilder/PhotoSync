# PhotoSync Setup and Testing Guide

## Database Setup

### 1. Create Database
```sql
-- Run this in SQL Server Management Studio or sqlcmd
CREATE DATABASE PhotoDB
GO

USE PhotoDB
GO
```

### 2. Run Schema Creation
Execute the complete SQL script from `Database/StoredProcedures.sql`

### 3. Verify Database Setup
```sql
-- Check tables exist
SELECT name FROM sys.tables WHERE name = 'Photos'

-- Check stored procedures exist
SELECT name FROM sys.procedures WHERE name LIKE 'sp_%Image%'

-- Expected results:
-- Tables: Photos
-- Procedures: sp_SaveImage, sp_GetAllImages, sp_GetImageByCode, sp_GetImageCount, sp_DeleteImage
```

## Application Setup

### 1. Build the Application
```bash
cd d:\dev2\PhotoSync
dotnet restore
dotnet build --configuration Release
```

### 2. Configure Connection String
Update `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PhotoDB;Trusted_Connection=true;MultipleActiveResultSets=true;"
  }
}
```

For SQL Server instance, use:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=PhotoDB;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

### 3. Create Test Folders
```bash
mkdir C:\Temp\PhotoSync\Import
mkdir C:\Temp\PhotoSync\Export
```

## Testing Instructions

### 1. Test Configuration
```bash
dotnet run test
```

Expected output:
```
PhotoSync Configuration Test
===========================
✓ Database Connection: PASS
✓ Import Folder Access: PASS - C:\Temp\PhotoSync\Import
✓ Export Folder Access: PASS - C:\Temp\PhotoSync\Export
✓ Table Configuration: Photos.ImageData
```

### 2. Create Sample Images
Place some `.jpg` files in `C:\Temp\PhotoSync\Import\` with simple names like:
- `test1.jpg`
- `test2.jpg`
- `sample.jpg`

### 3. Test Import
```bash
dotnet run import
```

Expected output:
```
Import Results: Imported 3/3 images in 00:02
```

### 4. Verify Database Content
```sql
USE PhotoDB
SELECT Code, DATALENGTH(ImageData) as ImageSizeBytes, CreatedDate 
FROM Photos
```

### 5. Test Export
```bash
dotnet run export
```

Check that files appear in `C:\Temp\PhotoSync\Export\` with original names.

### 6. Test Status
```bash
dotnet run status
```

Expected output:
```
PhotoSync Status Check
=====================
Database Connection: OK
Images in Database: 3
```

## Command Examples

### Import from Custom Folder
```bash
dotnet run import "D:\MyPhotos"
```

### Export to Custom Folder
```bash
dotnet run export "D:\ExportedPhotos"
```

### Environment Variable Override
```bash
set PHOTOSYNC_ConnectionStrings__DefaultConnection="Server=myserver;Database=PhotoDB;..."
dotnet run status
```

## Troubleshooting

### Database Connection Issues
1. Verify SQL Server is running
2. Check connection string format
3. Ensure database exists
4. Test with `dotnet run test`

### File Access Issues
1. Check folder permissions
2. Verify paths exist
3. Use absolute paths
4. Check disk space

### Build Issues
1. Ensure .NET 6.0 SDK is installed
2. Clear obj/bin folders: `dotnet clean`
3. Restore packages: `dotnet restore`
4. Check for package version conflicts

## Performance Testing

### Large File Testing
1. Add images larger than 10MB to test folder
2. Monitor memory usage during import
3. Check database storage efficiency

### Batch Testing
1. Add 100+ small images to test folder
2. Time the import operation
3. Verify all images imported correctly

### Error Testing
1. Test with corrupt image files
2. Test with non-JPG files
3. Test with insufficient disk space
4. Test with invalid connection string

## Validation Checklist

- [ ] Database tables and procedures created
- [ ] Application builds successfully
- [ ] Configuration test passes
- [ ] Import command works with sample images
- [ ] Export command recreates original files
- [ ] Status command shows correct information
- [ ] Error handling works properly
- [ ] Logging outputs to console and files
- [ ] Custom folder paths work
- [ ] Environment variable overrides work

## Next Steps

After successful testing:
1. Set up automated testing framework
2. Configure Azure deployment
3. Implement performance optimizations
4. Add monitoring and alerting
