# PhotoSync Windows Server Deployment Script
# Builds and packages PhotoSync for Windows Server deployment

param(
    [Parameter(Mandatory=$false)]
    [string]$DeploymentPath = "C:\Applications\PhotoSync",
    
    [Parameter(Mandatory=$false)]
    [string]$DataPath = "D:\PhotoSync",
    
    [Parameter(Mandatory=$false)]
    [switch]$SelfContained = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateFolders = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$InstallScheduledTasks = $false
)

Write-Host "PhotoSync Windows Server Deployment" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "WARNING: Not running as administrator. Some operations may fail." -ForegroundColor Yellow
    Write-Host "Consider running: PowerShell -ExecutionPolicy Bypass -File Deploy-WindowsServer.ps1" -ForegroundColor Yellow
    Write-Host ""
}

# Step 1: Build the application
Write-Host "Step 1: Building PhotoSync for production..." -ForegroundColor Cyan

$projectPath = (Get-Location).Path
$publishPath = Join-Path $projectPath "publish"

if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

if ($SelfContained) {
    Write-Host "Building self-contained deployment..." -ForegroundColor Gray
    dotnet publish -c Release -r win-x64 --self-contained true -o $publishPath
} else {
    Write-Host "Building framework-dependent deployment..." -ForegroundColor Gray
    dotnet publish -c Release -o $publishPath
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build completed successfully" -ForegroundColor Green
Write-Host ""

# Step 2: Create deployment directory
Write-Host "Step 2: Creating deployment directory..." -ForegroundColor Cyan

if (-not (Test-Path $DeploymentPath)) {
    New-Item -ItemType Directory -Path $DeploymentPath -Force | Out-Null
    Write-Host "✓ Created deployment directory: $DeploymentPath" -ForegroundColor Green
} else {
    Write-Host "✓ Deployment directory exists: $DeploymentPath" -ForegroundColor Green
}

# Step 3: Copy files
Write-Host "Step 3: Copying application files..." -ForegroundColor Cyan

# Copy all published files
Copy-Item "$publishPath\*" -Destination $DeploymentPath -Recurse -Force

# Copy additional files that might not be in publish folder
$additionalFiles = @(
    "Database\StoredProcedures.sql",
    "Scripts\*.ps1",
    "Scripts\*.bat",
    "*.md"
)

foreach ($pattern in $additionalFiles) {
    $files = Get-ChildItem $pattern -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring((Get-Location).Path.Length + 1)
        $destinationPath = Join-Path $DeploymentPath $relativePath
        $destinationDir = Split-Path $destinationPath -Parent
        
        if (-not (Test-Path $destinationDir)) {
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        }
        
        Copy-Item $file.FullName -Destination $destinationPath -Force
    }
}

Write-Host "✓ Application files copied" -ForegroundColor Green
Write-Host ""

# Step 4: Create data directories
if ($CreateFolders) {
    Write-Host "Step 4: Creating data directories..." -ForegroundColor Cyan
    
    $foldersToCreate = @(
        "$DataPath\Import",
        "$DataPath\Export", 
        "$DataPath\Logs"
    )
    
    foreach ($folder in $foldersToCreate) {
        if (-not (Test-Path $folder)) {
            New-Item -ItemType Directory -Path $folder -Force | Out-Null
            Write-Host "✓ Created: $folder" -ForegroundColor Green
        } else {
            Write-Host "✓ Exists: $folder" -ForegroundColor Green
        }
    }
    Write-Host ""
}

# Step 5: Update production configuration
Write-Host "Step 5: Updating production configuration..." -ForegroundColor Cyan

$prodConfigPath = Join-Path $DeploymentPath "appsettings.Production.json"
$prodConfig = @{
    "ConnectionStrings" = @{
        "DefaultConnection" = "Server=sqltest.schoolvision.net,14333;Database=PhotoDB;User Id=sa;Password=Gv51076!;Encrypt=true;TrustServerCertificate=true;MultipleActiveResultSets=true;"
    }
    "PhotoSettings" = @{
        "TableName" = "Photos"
        "ImageFieldName" = "ImageData"
        "CodeFieldName" = "Code"
        "ImportFolder" = "$DataPath\Import"
        "ExportFolder" = "$DataPath\Export"
    }
    "Serilog" = @{
        "MinimumLevel" = @{
            "Default" = "Information"
            "Override" = @{
                "Microsoft" = "Warning"
                "System" = "Warning"
            }
        }
        "WriteTo" = @(
            @{
                "Name" = "Console"
            },
            @{
                "Name" = "File"
                "Args" = @{
                    "path" = "$DataPath\Logs\app-.txt"
                    "rollingInterval" = "Day"
                    "retainedFileCountLimit" = 30
                }
            }
        )
    }
}

$prodConfig | ConvertTo-Json -Depth 10 | Set-Content $prodConfigPath -Encoding UTF8
Write-Host "✓ Production configuration updated" -ForegroundColor Green
Write-Host ""

# Step 6: Create batch scripts
Write-Host "Step 6: Creating operation scripts..." -ForegroundColor Cyan

$scriptsPath = Join-Path $DeploymentPath "Scripts"
if (-not (Test-Path $scriptsPath)) {
    New-Item -ItemType Directory -Path $scriptsPath -Force | Out-Null
}

# Import script
$importScript = @"
@echo off
cd /d $DeploymentPath
set ASPNETCORE_ENVIRONMENT=Production

echo Starting PhotoSync Import at %date% %time%
PhotoSync.exe import

if %errorlevel% equ 0 (
    echo Import completed successfully at %date% %time%
) else (
    echo Import failed with error code %errorlevel% at %date% %time%
)
"@

$importScript | Set-Content (Join-Path $scriptsPath "import.bat") -Encoding ASCII

# Export script  
$exportScript = @"
@echo off
cd /d $DeploymentPath
set ASPNETCORE_ENVIRONMENT=Production

echo Starting PhotoSync Export at %date% %time%
PhotoSync.exe export

if %errorlevel% equ 0 (
    echo Export completed successfully at %date% %time%
) else (
    echo Export failed with error code %errorlevel% at %date% %time%
)
"@

$exportScript | Set-Content (Join-Path $scriptsPath "export.bat") -Encoding ASCII

# Test script
$testScript = @"
@echo off
cd /d $DeploymentPath
set ASPNETCORE_ENVIRONMENT=Production

echo Testing PhotoSync Configuration...
PhotoSync.exe test

echo.
echo Running Database Diagnostic...
PhotoSync.exe diagnose
"@

$testScript | Set-Content (Join-Path $scriptsPath "test.bat") -Encoding ASCII

Write-Host "✓ Operation scripts created" -ForegroundColor Green
Write-Host ""

# Step 7: Set environment variable
Write-Host "Step 7: Setting environment variable..." -ForegroundColor Cyan

try {
    [Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", [EnvironmentVariableTarget]::Machine)
    Write-Host "✓ ASPNETCORE_ENVIRONMENT set to Production" -ForegroundColor Green
} catch {
    Write-Host "WARNING: Could not set environment variable. Run as administrator or set manually." -ForegroundColor Yellow
}
Write-Host ""

# Step 8: Install scheduled tasks (optional)
if ($InstallScheduledTasks) {
    Write-Host "Step 8: Installing scheduled tasks..." -ForegroundColor Cyan
    
    try {
        # Import task - daily at 2 AM
        $importAction = New-ScheduledTaskAction -Execute (Join-Path $scriptsPath "import.bat")
        $importTrigger = New-ScheduledTaskTrigger -Daily -At 2:00AM
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
        $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
        
        Register-ScheduledTask -TaskName "PhotoSync Import" -Action $importAction -Trigger $importTrigger -Settings $settings -Principal $principal -Force
        Write-Host "✓ PhotoSync Import task scheduled for 2:00 AM daily" -ForegroundColor Green
        
        # Export task - daily at 6 AM
        $exportAction = New-ScheduledTaskAction -Execute (Join-Path $scriptsPath "export.bat")
        $exportTrigger = New-ScheduledTaskTrigger -Daily -At 6:00AM
        
        Register-ScheduledTask -TaskName "PhotoSync Export" -Action $exportAction -Trigger $exportTrigger -Settings $settings -Principal $principal -Force
        Write-Host "✓ PhotoSync Export task scheduled for 6:00 AM daily" -ForegroundColor Green
        
    } catch {
        Write-Host "WARNING: Could not create scheduled tasks. May need administrator privileges." -ForegroundColor Yellow
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

# Step 9: Test deployment
Write-Host "Step 9: Testing deployment..." -ForegroundColor Cyan

Push-Location $DeploymentPath
try {
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    
    Write-Host "Running configuration test..." -ForegroundColor Gray
    & ".\PhotoSync.exe" test
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Configuration test passed" -ForegroundColor Green
    } else {
        Write-Host "⚠ Configuration test failed - check settings" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "ERROR: Could not run test. Check deployment." -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Pop-Location
}

Write-Host ""

# Deployment Summary
Write-Host "Deployment Summary" -ForegroundColor Green
Write-Host "==================" -ForegroundColor Green
Write-Host "Application Path: $DeploymentPath" -ForegroundColor White
Write-Host "Data Path: $DataPath" -ForegroundColor White
Write-Host "Build Type: $(if ($SelfContained) { 'Self-Contained' } else { 'Framework-Dependent' })" -ForegroundColor White
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Verify database connectivity: $DeploymentPath\Scripts\test.bat" -ForegroundColor White
Write-Host "2. Run database diagnostic: $DeploymentPath\PhotoSync.exe diagnose" -ForegroundColor White
Write-Host "3. Test import: $DeploymentPath\Scripts\import.bat" -ForegroundColor White
Write-Host "4. Test export: $DeploymentPath\Scripts\export.bat" -ForegroundColor White
Write-Host ""

if (-not $InstallScheduledTasks) {
    Write-Host "To install scheduled tasks, re-run with -InstallScheduledTasks parameter" -ForegroundColor Cyan
}

Write-Host "Deployment completed!" -ForegroundColor Green
