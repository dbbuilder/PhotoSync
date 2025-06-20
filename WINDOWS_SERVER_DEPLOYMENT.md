# PhotoSync - Windows Server Deployment Guide

## 🚀 **Deployment Options Overview**

Choose the deployment method that best fits your needs:

1. **Manual Console Execution** - Run commands manually
2. **Scheduled Tasks** - Automated execution on schedule
3. **Windows Service** - Always-running background service
4. **IIS Application** - Web-based interface (advanced)

---

## 🎯 **Option 1: Manual Console Deployment (Simplest)**

### **Step 1: Prepare the Application**

#### **Build for Production**
```cmd
# In Visual Studio or command line
cd D:\Dev2\PhotoSync
dotnet publish -c Release -r win-x64 --self-contained true -o "publish"
```

#### **Alternative: Framework-Dependent Build** (requires .NET runtime on server)
```cmd
dotnet publish -c Release -o "publish"
```

### **Step 2: Copy Files to Server**

Copy the entire `publish` folder to your Windows server, for example:
```
C:\Applications\PhotoSync\
├── PhotoSync.exe
├── appsettings.json
├── appsettings.Production.json
├── Database\StoredProcedures.sql
└── [all other files]
```

### **Step 3: Configure for Production**

#### **Update appsettings.Production.json:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqltest.schoolvision.net,14333;Database=PhotoDB;User Id=sa;Password=Gv51076!;Encrypt=true;TrustServerCertificate=true;MultipleActiveResultSets=true;"
  },
  "PhotoSettings": {
    "TableName": "Photos",
    "ImageFieldName": "ImageData",
    "CodeFieldName": "Code",
    "ImportFolder": "D:\\PhotoSync\\Import",
    "ExportFolder": "D:\\PhotoSync\\Export"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "D:\\PhotoSync\\Logs\\app-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### **Step 4: Set Environment Variable**
```cmd
setx ASPNETCORE_ENVIRONMENT "Production" /M
```

### **Step 5: Create Folders**
```cmd
mkdir D:\PhotoSync\Import
mkdir D:\PhotoSync\Export
mkdir D:\PhotoSync\Logs
```

### **Step 6: Test Deployment**
```cmd
cd C:\Applications\PhotoSync
PhotoSync.exe diagnose
PhotoSync.exe test
```

---

## ⏰ **Option 2: Windows Scheduled Tasks (Recommended for Automation)**

### **Step 1: Complete Manual Deployment Above**

### **Step 2: Create Batch Scripts**

#### **Import Script: `C:\Applications\PhotoSync\Scripts\import.bat`**
```batch
@echo off
cd /d C:\Applications\PhotoSync
set ASPNETCORE_ENVIRONMENT=Production

echo Starting PhotoSync Import at %date% %time%
PhotoSync.exe import

if %errorlevel% equ 0 (
    echo Import completed successfully at %date% %time%
) else (
    echo Import failed with error code %errorlevel% at %date% %time%
)
```

#### **Export Script: `C:\Applications\PhotoSync\Scripts\export.bat`**
```batch
@echo off
cd /d C:\Applications\PhotoSync
set ASPNETCORE_ENVIRONMENT=Production

echo Starting PhotoSync Export at %date% %time%
PhotoSync.exe export

if %errorlevel% equ 0 (
    echo Export completed successfully at %date% %time%
) else (
    echo Export failed with error code %errorlevel% at %date% %time%
)
```

### **Step 3: Create Scheduled Tasks**

#### **Using Task Scheduler GUI:**
1. Open **Task Scheduler** (taskschd.msc)
2. Create Basic Task...
3. **Name**: "PhotoSync Import"
4. **Trigger**: Daily at desired time
5. **Action**: Start a program
   - **Program**: `C:\Applications\PhotoSync\Scripts\import.bat`
   - **Start in**: `C:\Applications\PhotoSync`
6. **Settings**: 
   - ✓ Run whether user is logged on or not
   - ✓ Run with highest privileges

#### **Using PowerShell:**
```powershell
# Import task - runs daily at 2 AM
$action = New-ScheduledTaskAction -Execute "C:\Applications\PhotoSync\Scripts\import.bat"
$trigger = New-ScheduledTaskTrigger -Daily -At 2:00AM
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName "PhotoSync Import" -Action $action -Trigger $trigger -Settings $settings -Principal $principal

# Export task - runs daily at 6 AM  
$action = New-ScheduledTaskAction -Execute "C:\Applications\PhotoSync\Scripts\export.bat"
$trigger = New-ScheduledTaskTrigger -Daily -At 6:00AM

Register-ScheduledTask -TaskName "PhotoSync Export" -Action $action -Trigger $trigger -Settings $settings -Principal $principal
```

---

## 🛠️ **Option 3: Windows Service Deployment (Advanced)**

### **Step 1: Install Windows Service Framework**

Add to PhotoSync.csproj:
```xml
<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.0.0" />
```

### **Step 2: Modify Program.cs for Service**
```csharp
// Add this method to Program.cs
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHostedService<PhotoSyncWorkerService>();
        });
```

### **Step 3: Create Worker Service Class**
```csharp
// Create PhotoSyncWorkerService.cs
public class PhotoSyncWorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Import every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            // Run import logic here
        }
    }
}
```

### **Step 4: Install as Windows Service**
```cmd
sc create PhotoSyncService binPath= "C:\Applications\PhotoSync\PhotoSync.exe"
sc start PhotoSyncService
```

---

## 🗄️ **Database Setup on Server**

### **Step 1: Ensure SQL Server Access**
```cmd
# Test connection from server
sqlcmd -S sqltest.schoolvision.net,14333 -U sa -P Gv51076!
```

### **Step 2: Create Database and Tables**
```sql
-- Connect to master database first
USE master;
GO

-- Create PhotoDB database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'PhotoDB')
BEGIN
    CREATE DATABASE PhotoDB;
END
GO

USE PhotoDB;
GO

-- Run the stored procedures script
-- Copy content from Database\StoredProcedures.sql
```

### **Step 3: Test Database Setup**
```cmd
cd C:\Applications\PhotoSync
PhotoSync.exe diagnose
```

---

## 🔐 **Security Considerations**

### **File Permissions**
```cmd
# Give appropriate permissions to PhotoSync folders
icacls "C:\Applications\PhotoSync" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F"
icacls "D:\PhotoSync" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F"
```

### **Service Account** (for Scheduled Tasks)
- Create dedicated service account for PhotoSync
- Grant minimal required permissions
- Use Group Managed Service Accounts if available

### **Connection String Security**
- Store connection strings in Windows credentials or Azure Key Vault
- Use environment variables for sensitive data
- Encrypt configuration files if needed

---

## 📊 **Monitoring and Logging**

### **Log File Locations**
```
D:\PhotoSync\Logs\app-20241220.txt
```

### **Event Log Integration** (Optional)
Add to appsettings.Production.json:
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "EventLog",
        "Args": {
          "source": "PhotoSync",
          "logName": "Application"
        }
      }
    ]
  }
}
```

### **Performance Counters** (Optional)
Monitor:
- Import/Export success rates
- Processing times
- Error frequencies

---

## 🚀 **Deployment Checklist**

### **Pre-Deployment**
- [ ] Build application in Release mode
- [ ] Test locally with Production configuration
- [ ] Verify database connectivity from development machine
- [ ] Create deployment folders on server

### **Deployment**
- [ ] Copy application files to server
- [ ] Update appsettings.Production.json with server paths
- [ ] Set ASPNETCORE_ENVIRONMENT=Production
- [ ] Create import/export/log folders
- [ ] Set appropriate file permissions

### **Database Setup**
- [ ] Test SQL Server connectivity from server
- [ ] Create PhotoDB database
- [ ] Run stored procedures script
- [ ] Test with `PhotoSync.exe diagnose`

### **Automation Setup**
- [ ] Create batch scripts for import/export
- [ ] Configure scheduled tasks
- [ ] Test scheduled task execution
- [ ] Verify logging is working

### **Testing**
- [ ] Run `PhotoSync.exe test`
- [ ] Test import with sample files
- [ ] Test export functionality
- [ ] Verify logs are being written
- [ ] Test error handling scenarios

### **Production Readiness**
- [ ] Configure monitoring/alerting
- [ ] Document operational procedures
- [ ] Set up backup procedures for logs
- [ ] Train operations team

---

## 🆘 **Troubleshooting Common Issues**

### **Permission Denied Errors**
```cmd
# Run as administrator or fix permissions
icacls "D:\PhotoSync" /grant "Everyone:(OI)(CI)F"
```

### **Database Connection Issues**
```cmd
# Test connection
PhotoSync.exe diagnose

# Check SQL Server configuration
sqlcmd -S sqltest.schoolvision.net,14333 -U sa -P Gv51076!
```

### **Scheduled Task Not Running**
- Check Task Scheduler History
- Verify service account permissions
- Check event logs for errors
- Test batch script manually

### **Performance Issues**
- Monitor CPU/Memory usage during operations
- Check database performance
- Review log files for bottlenecks
- Consider parallel processing for large datasets

---

## 📞 **Support Information**

### **Log Locations**
- Application Logs: `D:\PhotoSync\Logs\`
- Windows Event Log: Application → PhotoSync
- Task Scheduler History

### **Key Configuration Files**
- `C:\Applications\PhotoSync\appsettings.Production.json`
- `C:\Applications\PhotoSync\Scripts\*.bat`

### **Common Commands**
```cmd
# Test configuration
PhotoSync.exe test

# Database diagnostic
PhotoSync.exe diagnose

# Manual import
PhotoSync.exe import "D:\SpecificFolder"

# Manual export  
PhotoSync.exe export "D:\SpecificFolder"
```

This deployment guide covers all major Windows Server deployment scenarios. Choose the option that best fits your operational requirements!
