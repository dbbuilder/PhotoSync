# PhotoSync - Absolute Path Configuration Examples

## ✅ Recommended: Always Use Absolute Paths

### Windows Development Examples
```json
{
  "PhotoSettings": {
    "ImportFolder": "C:\\test\\import",
    "ExportFolder": "C:\\test\\export"
  }
}
```

### Alternative Windows Paths
```json
{
  "PhotoSettings": {
    "ImportFolder": "D:\\Photos\\ToImport",
    "ExportFolder": "D:\\Photos\\Exported"
  }
}
```

### Network Share Paths (if needed)
```json
{
  "PhotoSettings": {
    "ImportFolder": "\\\\server\\share\\photos\\import",
    "ExportFolder": "\\\\server\\share\\photos\\export"
  }
}
```

### Azure Linux App Service (Production)
```json
{
  "PhotoSettings": {
    "ImportFolder": "/home/site/wwwroot/import",
    "ExportFolder": "/home/site/wwwroot/export"
  }
}
```

## 🎯 Key Benefits of Absolute Paths

1. **Reliability**: No dependency on current working directory
2. **Clarity**: Exact location is always known
3. **Consistency**: Works the same from any execution context
4. **Debugging**: Easy to verify folder existence
5. **Security**: No ambiguity about where files are stored

## 🔧 Quick Setup for Testing

### 1. Create Test Folders
```cmd
mkdir C:\test\import
mkdir C:\test\export
```

### 2. Update appsettings.Development.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PhotoDB;Trusted_Connection=true;"
  },
  "PhotoSettings": {
    "TableName": "Photos",
    "ImageFieldName": "ImageData", 
    "CodeFieldName": "Code",
    "ImportFolder": "C:\\test\\import",
    "ExportFolder": "C:\\test\\export"
  }
}
```

### 3. Test the Configuration
```cmd
dotnet run test
```

## 📋 Path Format Notes

- **Windows**: Use double backslashes `"C:\\path\\to\\folder"` in JSON
- **Linux**: Use forward slashes `"/path/to/folder"`
- **Trailing slashes**: Optional - the application handles both formats
- **Spaces**: Fully supported in folder names
- **Unicode**: Supported for international folder names

## 🚀 Ready for Visual Studio Build

The complete project is now ready for manual build in Visual Studio:

1. Open `PhotoSync.sln` or `PhotoSync.csproj` in Visual Studio
2. Build -> Rebuild Solution
3. All compilation errors have been fixed
4. Ready for testing and deployment
