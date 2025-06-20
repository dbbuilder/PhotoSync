# PhotoSync Installation Script
# Downloads and installs PhotoSync from GitHub releases

param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "latest",
    
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Applications\PhotoSync",
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubRepo = "yourusername/photosync",  # Replace with your actual GitHub repo
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("win-x64", "framework-dependent")]
    [string]$PackageType = "win-x64",
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateShortcuts = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$AddToPath = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force = $false
)

# Set TLS version for GitHub API compatibility
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Write-Host "🚀 PhotoSync Installer" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host ""

# Check for administrator privileges for system-wide installation
$isAdmin = $false
try {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
} catch {
    # Continue without admin check
}

if ($InstallPath.StartsWith("C:\Program Files") -and -not $isAdmin) {
    Write-Host "⚠️  Warning: Installing to Program Files requires administrator privileges" -ForegroundColor Yellow
    Write-Host "Consider running as administrator or choosing a different install path" -ForegroundColor Yellow
    
    $userPath = "$env:LOCALAPPDATA\PhotoSync"
    $continue = Read-Host "Install to user directory instead? ($userPath) [Y/n]"
    if ($continue -ne 'n' -and $continue -ne 'N') {
        $InstallPath = $userPath
    }
}

Write-Host "📋 Installation Details:" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor White
Write-Host "  Install Path: $InstallPath" -ForegroundColor White
Write-Host "  Package Type: $PackageType" -ForegroundColor White
Write-Host "  GitHub Repo: $GitHubRepo" -ForegroundColor White
Write-Host "  Administrator: $(if ($isAdmin) { 'Yes' } else { 'No' })" -ForegroundColor White
Write-Host ""

# Get release information from GitHub API
Write-Host "🔍 Fetching release information..." -ForegroundColor Cyan

try {
    if ($Version -eq "latest") {
        $apiUrl = "https://api.github.com/repos/$GitHubRepo/releases/latest"
        Write-Host "  Checking for latest release..." -ForegroundColor Gray
    } else {
        # Ensure version starts with 'v' for API call
        $versionTag = if ($Version.StartsWith('v')) { $Version } else { "v$Version" }
        $apiUrl = "https://api.github.com/repos/$GitHubRepo/releases/tags/$versionTag"
        Write-Host "  Checking for version $versionTag..." -ForegroundColor Gray
    }
    
    $release = Invoke-RestMethod -Uri $apiUrl -ErrorAction Stop
    $actualVersion = $release.tag_name
    Write-Host "✅ Found release: $actualVersion" -ForegroundColor Green
    
} catch {
    Write-Host "❌ Error: Could not fetch release information" -ForegroundColor Red
    Write-Host "  Repository: $GitHubRepo" -ForegroundColor Gray
    Write-Host "  Version: $Version" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Please check:" -ForegroundColor Yellow
    Write-Host "  - Repository name is correct" -ForegroundColor Yellow
    Write-Host "  - Release exists and is public" -ForegroundColor Yellow
    Write-Host "  - Internet connection is working" -ForegroundColor Yellow
    exit 1
}

# Find the appropriate asset
Write-Host "📦 Finding download package..." -ForegroundColor Cyan

$assetPattern = if ($PackageType -eq "win-x64") {
    "*-win-x64.zip"
} else {
    "*-framework-dependent.zip"
}

$asset = $release.assets | Where-Object { $_.name -like $assetPattern } | Select-Object -First 1

if (-not $asset) {
    Write-Host "❌ Error: Could not find $PackageType package in release $actualVersion" -ForegroundColor Red
    Write-Host "Available assets:" -ForegroundColor Gray
    $release.assets | ForEach-Object { Write-Host "  - $($_.name)" -ForegroundColor Gray }
    exit 1
}

$downloadUrl = $asset.browser_download_url
$fileName = $asset.name
$fileSize = [math]::Round($asset.size / 1MB, 2)

Write-Host "✅ Found package: $fileName ($fileSize MB)" -ForegroundColor Green
Write-Host ""

# Check if already installed
if (Test-Path $InstallPath) {
    if (-not $Force) {
        Write-Host "⚠️  PhotoSync is already installed at: $InstallPath" -ForegroundColor Yellow
        
        # Try to get current version
        $currentExe = Join-Path $InstallPath "PhotoSync.exe"
        if (Test-Path $currentExe) {
            try {
                $currentVersion = (Get-ItemProperty $currentExe).VersionInfo.FileVersion
                Write-Host "  Current version: $currentVersion" -ForegroundColor Gray
            } catch {
                Write-Host "  Current version: Unknown" -ForegroundColor Gray
            }
        }
        
        $overwrite = Read-Host "Overwrite existing installation? [Y/n]"
        if ($overwrite -eq 'n' -or $overwrite -eq 'N') {
            Write-Host "Installation cancelled" -ForegroundColor Yellow
            exit 0
        }
    }
    
    Write-Host "🗑️  Removing existing installation..." -ForegroundColor Yellow
    try {
        Remove-Item $InstallPath -Recurse -Force -ErrorAction Stop
        Write-Host "✅ Existing installation removed" -ForegroundColor Green
    } catch {
        Write-Host "❌ Error: Could not remove existing installation" -ForegroundColor Red
        Write-Host "  Path: $InstallPath" -ForegroundColor Gray
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
        exit 1
    }
}

# Create installation directory
Write-Host "📁 Creating installation directory..." -ForegroundColor Cyan
try {
    New-Item -ItemType Directory -Path $InstallPath -Force -ErrorAction Stop | Out-Null
    Write-Host "✅ Directory created: $InstallPath" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: Could not create installation directory" -ForegroundColor Red
    Write-Host "  Path: $InstallPath" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    exit 1
}

# Download the package
Write-Host "⬇️  Downloading PhotoSync $actualVersion..." -ForegroundColor Cyan
$tempPath = Join-Path $env:TEMP $fileName

try {
    # Show progress
    $ProgressPreference = 'Continue'
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempPath -UseBasicParsing -ErrorAction Stop
    Write-Host "✅ Download completed" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: Download failed" -ForegroundColor Red
    Write-Host "  URL: $downloadUrl" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    exit 1
}

# Verify download
Write-Host "🔍 Verifying download..." -ForegroundColor Cyan
$downloadedSize = [math]::Round((Get-Item $tempPath).Length / 1MB, 2)
Write-Host "✅ Downloaded $downloadedSize MB" -ForegroundColor Green

# Extract the package
Write-Host "📦 Extracting PhotoSync..." -ForegroundColor Cyan
try {
    Expand-Archive -Path $tempPath -DestinationPath $InstallPath -Force -ErrorAction Stop
    Write-Host "✅ Extraction completed" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: Extraction failed" -ForegroundColor Red
    Write-Host "  Archive: $tempPath" -ForegroundColor Gray
    Write-Host "  Destination: $InstallPath" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    exit 1
} finally {
    # Clean up temp file
    if (Test-Path $tempPath) {
        Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
    }
}

# Verify installation
Write-Host "✅ Verifying installation..." -ForegroundColor Cyan
$exePath = Join-Path $InstallPath "PhotoSync.exe"
if (Test-Path $exePath) {
    try {
        $installedVersion = (Get-ItemProperty $exePath).VersionInfo.FileVersion
        Write-Host "✅ PhotoSync $installedVersion installed successfully" -ForegroundColor Green
    } catch {
        Write-Host "✅ PhotoSync installed successfully" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  Warning: PhotoSync.exe not found after extraction" -ForegroundColor Yellow
    Write-Host "Installation may be incomplete" -ForegroundColor Yellow
}

# Add to PATH if requested
if ($AddToPath) {
    Write-Host "🔧 Adding to PATH..." -ForegroundColor Cyan
    try {
        $currentPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::User)
        if ($currentPath -notlike "*$InstallPath*") {
            $newPath = "$currentPath;$InstallPath"
            [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::User)
            Write-Host "✅ Added to user PATH" -ForegroundColor Green
            Write-Host "  Restart your terminal to use 'PhotoSync' command" -ForegroundColor Gray
        } else {
            Write-Host "✅ Already in PATH" -ForegroundColor Green
        }
    } catch {
        Write-Host "⚠️  Warning: Could not add to PATH" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
}

# Create desktop shortcut if requested
if ($CreateShortcuts) {
    Write-Host "🔗 Creating shortcuts..." -ForegroundColor Cyan
    try {
        $WScript = New-Object -ComObject WScript.Shell
        
        # Desktop shortcut
        $desktopPath = [Environment]::GetFolderPath("Desktop")
        $shortcutPath = Join-Path $desktopPath "PhotoSync.lnk"
        $shortcut = $WScript.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $exePath
        $shortcut.WorkingDirectory = $InstallPath
        $shortcut.Description = "PhotoSync - Photo Import/Export Tool"
        $shortcut.Save()
        
        Write-Host "✅ Desktop shortcut created" -ForegroundColor Green
    } catch {
        Write-Host "⚠️  Warning: Could not create shortcuts" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
}

# Create data directories
Write-Host "📁 Setting up data directories..." -ForegroundColor Cyan
$dataPath = if ($InstallPath.StartsWith("C:\Program Files")) {
    "C:\ProgramData\PhotoSync"
} else {
    Join-Path (Split-Path $InstallPath -Parent) "PhotoSyncData"
}

$dataDirs = @(
    "$dataPath\Import",
    "$dataPath\Export", 
    "$dataPath\Logs"
)

foreach ($dir in $dataDirs) {
    try {
        New-Item -ItemType Directory -Path $dir -Force -ErrorAction Stop | Out-Null
    } catch {
        Write-Host "⚠️  Warning: Could not create directory: $dir" -ForegroundColor Yellow
    }
}

Write-Host "✅ Data directories created at: $dataPath" -ForegroundColor Green

# Show completion summary
Write-Host ""
Write-Host "🎉 Installation Completed Successfully!" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
Write-Host ""
Write-Host "📍 Installation Path: $InstallPath" -ForegroundColor White
Write-Host "📍 Data Path: $dataPath" -ForegroundColor White
Write-Host "📍 Version: $actualVersion" -ForegroundColor White
Write-Host ""

Write-Host "🚀 Next Steps:" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. Configure your database connection:" -ForegroundColor Yellow
Write-Host "   Edit: $InstallPath\appsettings.Production.json" -ForegroundColor White
Write-Host ""

Write-Host "2. Test the installation:" -ForegroundColor Yellow
if ($AddToPath) {
    Write-Host "   PhotoSync test" -ForegroundColor White
    Write-Host "   PhotoSync diagnose" -ForegroundColor White
} else {
    Write-Host "   cd `"$InstallPath`"" -ForegroundColor White
    Write-Host "   .\PhotoSync.exe test" -ForegroundColor White
    Write-Host "   .\PhotoSync.exe diagnose" -ForegroundColor White
}
Write-Host ""

Write-Host "3. Set up your import/export folders:" -ForegroundColor Yellow
Write-Host "   Import folder: $dataPath\Import" -ForegroundColor White
Write-Host "   Export folder: $dataPath\Export" -ForegroundColor White
Write-Host ""

Write-Host "📚 Documentation:" -ForegroundColor Cyan
Write-Host "   Windows Server Deployment: https://github.com/$GitHubRepo/blob/main/WINDOWS_SERVER_DEPLOYMENT.md" -ForegroundColor Blue
Write-Host "   Project README: https://github.com/$GitHubRepo/blob/main/README.md" -ForegroundColor Blue
Write-Host ""

if (-not $AddToPath) {
    Write-Host "💡 Tip: Add -AddToPath parameter to make 'PhotoSync' command available globally" -ForegroundColor Gray
    Write-Host "    iex (iwr `"URL`").Content -AddToPath" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✅ PhotoSync is ready to use!" -ForegroundColor Green
