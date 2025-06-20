# PhotoSync Settings Override Examples

## 🎯 **Overview**

The `-settings` command line argument allows you to override connection strings and photo settings for specific operations without modifying your main configuration files. This is perfect for:

- **Production deployments** with different database connections
- **Testing environments** with separate databases
- **Archive operations** using different folders
- **Multi-tenant scenarios** with different configurations per client
- **Development testing** with isolated environments

## ⚡ **Quick Usage**

```cmd
# Use production database and folders
PhotoSync import -settings "Examples\production-settings.json"

# Test with different database only
PhotoSync status -settings "Examples\test-database-only.json"

# Archive operation with different folders
PhotoSync export -settings "Examples\archive-folders-only.json"

# Local development environment
PhotoSync test -settings "Examples\local-development.json"
```

## 📁 **Example Files**

### **production-settings.json**
**Use Case:** Production environment with dedicated server and network shares
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-server.company.com,1433;Database=ProductionPhotoDB;..."
  },
  "PhotoSettings": {
    "TableName": "ProductionPhotos",
    "ImportFolder": "\\\\prod-share\\photos\\import",
    "ExportFolder": "\\\\prod-share\\photos\\export"
  }
}
```

**Usage:**
```cmd
PhotoSync import -settings "Examples\production-settings.json"
PhotoSync status -settings "Examples\production-settings.json"
```

### **test-database-only.json**
**Use Case:** Override database connection only, keep existing folder settings
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=test-db.company.com,1433;Database=TestPhotoDB;..."
  }
}
```

**Usage:**
```cmd
PhotoSync diagnose -settings "Examples\test-database-only.json"
PhotoSync status -settings "Examples\test-database-only.json"
```

### **archive-folders-only.json**
**Use Case:** Same database, different folders for archive operations
```json
{
  "PhotoSettings": {
    "TableName": "ArchivePhotos",
    "ImportFolder": "D:\\Archive\\Import",
    "ExportFolder": "D:\\Archive\\Export"
  }
}
```

**Usage:**
```cmd
PhotoSync export -settings "Examples\archive-folders-only.json"
PhotoSync import "D:\\OldPhotos" -settings "Examples\archive-folders-only.json"
```

### **local-development.json**
**Use Case:** Local development with LocalDB and development folders
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PhotoSyncDev;..."
  },
  "PhotoSettings": {
    "TableName": "DevPhotos",
    "ImportFolder": "C:\\Dev\\TestPhotos\\Import",
    "ExportFolder": "C:\\Dev\\TestPhotos\\Export"
  }
}
```

**Usage:**
```cmd
PhotoSync test -settings "Examples\local-development.json"
PhotoSync import -settings "Examples\local-development.json"
```

## 🔧 **Creating Custom Override Files**

### **Override Structure**
Settings override files can contain any combination of these sections:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string-here"
  },
  "PhotoSettings": {
    "TableName": "YourTableName",
    "ImageFieldName": "YourImageField",
    "CodeFieldName": "YourCodeField",
    "ImportFolder": "C:\\Your\\Import\\Path",
    "ExportFolder": "C:\\Your\\Export\\Path"
  }
}
```

### **Partial Overrides**
You can override only specific settings:

**Database Only:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=different-server;Database=DifferentDB;..."
  }
}
```

**Folders Only:**
```json
{
  "PhotoSettings": {
    "ImportFolder": "C:\\Different\\Import",
    "ExportFolder": "C:\\Different\\Export"
  }
}
```

**Table Only:**
```json
{
  "PhotoSettings": {
    "TableName": "AlternativeTable"
  }
}
```

## 🎯 **Common Scenarios**

### **Scenario 1: Multi-Environment Deployment**
```cmd
# Development
PhotoSync import -settings "config\\dev-settings.json"

# Staging  
PhotoSync import -settings "config\\staging-settings.json"

# Production
PhotoSync import -settings "config\\prod-settings.json"
```

### **Scenario 2: Client-Specific Operations**
```cmd
# Client A
PhotoSync export -settings "clients\\clientA-settings.json"

# Client B
PhotoSync export -settings "clients\\clientB-settings.json"
```

### **Scenario 3: Database Migration Testing**
```cmd
# Test connectivity to new database
PhotoSync diagnose -settings "migration\\new-db-settings.json"

# Import to new database
PhotoSync import -settings "migration\\new-db-settings.json"
```

### **Scenario 4: Archive and Backup Operations**
```cmd
# Export current photos to archive
PhotoSync export -settings "archive\\monthly-backup.json"

# Import historical photos to archive table
PhotoSync import "\\\\archive\\photos\\2023" -settings "archive\\historical-import.json"
```

## 🔐 **Security Considerations**

### **Protect Settings Files**
- Store settings files in secure locations
- Use environment variables for sensitive data
- Consider encryption for production passwords
- Restrict file permissions appropriately

### **Example with Environment Variables**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-server;Database=PhotoDB;User Id=${PHOTOSYNC_USER};Password=${PHOTOSYNC_PASS};..."
  }
}
```

## 📋 **Command Line Examples**

### **Basic Override Usage**
```cmd
# Override with absolute path
PhotoSync import -settings "C:\\Config\\production.json"

# Override with relative path
PhotoSync status -settings "..\\shared-config\\test-db.json"

# Combine with folder argument
PhotoSync import "D:\\NewPhotos" -settings "Examples\\production-settings.json"
```

### **Advanced Combinations**
```cmd
# Import from specific folder using production database
PhotoSync import "\\\\server\\photos\\batch1" -settings "Examples\\production-settings.json"

# Export to specific folder using archive settings
PhotoSync export "D:\\Backup\\Photos" -settings "Examples\\archive-folders-only.json"

# Test configuration with development settings
PhotoSync test -settings "Examples\\local-development.json"
```

## 🛠️ **Troubleshooting**

### **Common Issues**

**Settings file not found:**
```
ERROR: Settings override file not found: C:\\Config\\missing.json
```
**Solution:** Verify the file path and ensure the file exists.

**Invalid JSON format:**
```
ERROR: Invalid JSON in settings override file
```
**Solution:** Validate JSON syntax using a JSON validator.

**Connection string issues:**
```
ERROR: Database connection failed with override settings
```
**Solution:** Test connection string separately and verify server accessibility.

### **Debug Mode**
Use the debug information that PhotoSync displays in development mode to verify settings are being applied correctly:

```
=== PhotoSync Debug Information ===
Environment: Development
Connection String: [shows the overridden connection string]
...
```

## 💡 **Best Practices**

1. **Use descriptive filenames** that indicate the purpose (e.g., `production-settings.json`, `clientA-config.json`)

2. **Organize by environment** or purpose in folders:
   ```
   config/
   ├── environments/
   │   ├── dev-settings.json
   │   ├── staging-settings.json
   │   └── prod-settings.json
   ├── clients/
   │   ├── clientA-settings.json
   │   └── clientB-settings.json
   └── archive/
       └── backup-settings.json
   ```

3. **Test settings files** before use:
   ```cmd
   PhotoSync test -settings "your-settings.json"
   ```

4. **Keep settings files minimal** - only override what's necessary

5. **Document your settings files** with comments in accompanying README files

6. **Version control** settings files (excluding sensitive production files)

## 🎉 **Summary**

The `-settings` argument provides powerful configuration flexibility:

- ✅ **Environment-specific** deployments
- ✅ **Client-specific** configurations  
- ✅ **Testing and development** isolation
- ✅ **Archive and backup** operations
- ✅ **No modification** of main config files
- ✅ **Partial overrides** for maximum flexibility

**Transform any PhotoSync operation with custom settings in seconds!**
