# PhotoSync - Windows Server Deployment Quick Start

## 🚀 **Quick Deployment (Recommended)**

### **Option 1: One-Click Deployment**
1. **Right-click** `Scripts\Deployment\Deploy-OneClick.bat`
2. **Select** "Run as administrator"
3. **Wait** for deployment to complete
4. **Run** validation: `Scripts\Deployment\Validate-Deployment.ps1`

### **Option 2: PowerShell Deployment**
```powershell
# Run as Administrator
cd D:\Dev2\PhotoSync
.\Scripts\Deployment\Deploy-WindowsServer.ps1 -CreateFolders -InstallScheduledTasks
```

## 📋 **What Gets Deployed**

### **Application Files** → `C:\Applications\PhotoSync\`
- PhotoSync.exe (main application)
- Configuration files (appsettings.*.json)
- Dependencies and libraries
- Database setup scripts
- Operation batch scripts

### **Data Folders** → `D:\PhotoSync\`
- `Import\` - Place JPG files here for import
- `Export\` - Exported images saved here  
- `Logs\` - Application log files

### **Scheduled Tasks** (Optional)
- **PhotoSync Import** - Daily at 2:00 AM
- **PhotoSync Export** - Daily at 6:00 AM

## 🧪 **Testing Your Deployment**

### **1. Validate Deployment**
```cmd
PowerShell -ExecutionPolicy Bypass -File "C:\Applications\PhotoSync\Scripts\Deployment\Validate-Deployment.ps1"
```

### **2. Test Database Connection**
```cmd
cd C:\Applications\PhotoSync
PhotoSync.exe diagnose
```

### **3. Test Configuration**
```cmd
PhotoSync.exe test
```

### **4. Manual Operations**
```cmd
# Test import (requires JPG files in D:\PhotoSync\Import\)
Scripts\import.bat

# Test export  
Scripts\export.bat
```

## 🔧 **Configuration**

### **Database Connection**
Edit `C:\Applications\PhotoSync\appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YourServer;Database=PhotoDB;User Id=YourUser;Password=YourPassword;Encrypt=true;TrustServerCertificate=true;"
  }
}
```

### **Folder Paths**
```json
{
  "PhotoSettings": {
    "ImportFolder": "D:\\PhotoSync\\Import",
    "ExportFolder": "D:\\PhotoSync\\Export"
  }
}
```

## 📊 **Monitoring**

### **Log Files**
- **Location**: `D:\PhotoSync\Logs\app-YYYYMMDD.txt`
- **Rotation**: Daily, kept for 30 days
- **Format**: Structured JSON logging

### **Scheduled Task History**
1. Open **Task Scheduler** (taskschd.msc)
2. Navigate to **Task Scheduler Library**
3. Find **PhotoSync Import** and **PhotoSync Export**
4. Check **History** tab

### **Event Logs** (Optional)
- **Location**: Windows Event Viewer → Application
- **Source**: PhotoSync

## ❗ **Common Issues & Solutions**

### **Database Connection Fails**
```cmd
# Run diagnostic
PhotoSync.exe diagnose

# Common fixes:
# 1. Verify server name and port
# 2. Check firewall settings  
# 3. Verify SQL Server authentication
# 4. Create PhotoDB database if missing
```

### **Permission Denied Errors**
```cmd
# Fix folder permissions
icacls "D:\PhotoSync" /grant "Everyone:(OI)(CI)F"
icacls "C:\Applications\PhotoSync" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F"
```

### **Scheduled Tasks Not Running**
1. **Check Task Scheduler History**
2. **Verify service account permissions**
3. **Test batch scripts manually**
4. **Check Windows Event Log**

## 🆘 **Support Commands**

### **Manual Operations**
```cmd
cd C:\Applications\PhotoSync

# Show help
PhotoSync.exe

# Test everything
PhotoSync.exe test

# Database diagnostic
PhotoSync.exe diagnose

# Import from specific folder
PhotoSync.exe import "C:\SpecificFolder"

# Export to specific folder  
PhotoSync.exe export "C:\SpecificFolder"

# Check status
PhotoSync.exe status
```

### **Batch Scripts**
```cmd
# Located in C:\Applications\PhotoSync\Scripts\

Scripts\test.bat     # Run configuration test
Scripts\import.bat   # Import operation
Scripts\export.bat   # Export operation
```

## 📞 **Troubleshooting Checklist**

- [ ] **Application deployed** to `C:\Applications\PhotoSync\`
- [ ] **Data folders created** at `D:\PhotoSync\`
- [ ] **Environment variable** set: `ASPNETCORE_ENVIRONMENT=Production`
- [ ] **Database connection** working (run `PhotoSync.exe diagnose`)
- [ ] **Folder permissions** allow read/write access
- [ ] **Scheduled tasks** created and enabled
- [ ] **Test operations** run successfully

## 🔄 **Updating PhotoSync**

### **To Deploy Updates:**
1. **Build new version** in Visual Studio
2. **Run deployment script** again
3. **Validate deployment** 
4. **Test functionality**

### **Zero-Downtime Update:**
1. **Stop scheduled tasks** temporarily
2. **Deploy to temporary folder**
3. **Test new version**
4. **Swap folders atomically**
5. **Re-enable scheduled tasks**

---

## 📋 **Deployment Checklist**

### **Pre-Deployment**
- [ ] Build succeeds in Visual Studio
- [ ] Database server accessible from target server
- [ ] Target server has appropriate permissions
- [ ] Folders D:\PhotoSync\ available

### **Deployment**
- [ ] Run deployment script as administrator
- [ ] Verify all files copied successfully
- [ ] Check configuration files updated
- [ ] Validate scheduled tasks created

### **Post-Deployment**
- [ ] Run `Validate-Deployment.ps1`
- [ ] Test `PhotoSync.exe diagnose`
- [ ] Test `PhotoSync.exe test`
- [ ] Verify logging works
- [ ] Test import/export operations

**Your PhotoSync application is now ready for Windows Server production use!**
