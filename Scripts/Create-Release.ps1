# PhotoSync GitHub Release Creation Script
# Automates the process of creating and publishing releases

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force = $false
)

Write-Host "🚀 PhotoSync Release Creator" -ForegroundColor Green
Write-Host "============================" -ForegroundColor Green
Write-Host ""

# Validate version format
if ($Version -notmatch '^v?\d+\.\d+\.\d+$') {
    Write-Host "❌ Error: Version must be in format 'X.Y.Z' or 'vX.Y.Z'" -ForegroundColor Red
    Write-Host "Examples: '1.0.0', 'v1.2.3', '2.1.0'" -ForegroundColor Gray
    exit 1
}

# Ensure version starts with 'v'
if (-not $Version.StartsWith('v')) {
    $Version = "v$Version"
}

Write-Host "📋 Release Information:" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor White
Write-Host "  Dry Run: $(if ($DryRun) { 'Yes (no changes will be made)' } else { 'No (will create real release)' })" -ForegroundColor White
Write-Host ""

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Host "❌ Error: This script must be run from the PhotoSync project root directory" -ForegroundColor Red
    Write-Host "Current directory: $(Get-Location)" -ForegroundColor Gray
    exit 1
}

# Check if git is available
try {
    $gitVersion = git --version
    Write-Host "✅ Git available: $gitVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: Git is not available in PATH" -ForegroundColor Red
    exit 1
}

# Check current branch
$currentBranch = git branch --show-current
Write-Host "📍 Current branch: $currentBranch" -ForegroundColor Gray

if ($currentBranch -ne "main" -and $currentBranch -ne "master" -and -not $Force) {
    Write-Host "⚠️  Warning: You're not on main/master branch" -ForegroundColor Yellow
    Write-Host "Current branch: $currentBranch" -ForegroundColor Yellow
    Write-Host "Use -Force to continue anyway, or switch to main/master branch" -ForegroundColor Yellow
    
    $confirm = Read-Host "Continue with release from $currentBranch? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Release cancelled" -ForegroundColor Yellow
        exit 0
    }
}

# Check if tag already exists
$existingTag = git tag -l $Version
if ($existingTag -and -not $Force) {
    Write-Host "❌ Error: Tag $Version already exists" -ForegroundColor Red
    Write-Host "Use -Force to overwrite, or choose a different version" -ForegroundColor Yellow
    exit 1
}

# Check for uncommitted changes
$status = git status --porcelain
if ($status -and -not $Force) {
    Write-Host "⚠️  Warning: You have uncommitted changes:" -ForegroundColor Yellow
    git status --short
    Write-Host ""
    
    $confirm = Read-Host "Continue with uncommitted changes? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Please commit your changes first" -ForegroundColor Yellow
        exit 0
    }
}

# Build and test the project first
Write-Host "🔨 Building project..." -ForegroundColor Cyan
try {
    dotnet clean --verbosity quiet
    dotnet restore --verbosity quiet
    dotnet build -c Release --verbosity quiet --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "✅ Build successful" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: Build failed" -ForegroundColor Red
    Write-Host "Please fix build errors before creating release" -ForegroundColor Yellow
    exit 1
}

# Update version in project file if needed
Write-Host "📝 Checking project version..." -ForegroundColor Cyan
$projectFile = "PhotoSync.csproj"
$projectContent = Get-Content $projectFile -Raw

# Extract version from tag (remove 'v' prefix)
$numericVersion = $Version -replace '^v', ''

# Check if project file has version
if ($projectContent -match '<Version>([^<]+)</Version>') {
    $currentProjectVersion = $matches[1]
    if ($currentProjectVersion -ne $numericVersion) {
        Write-Host "  Current project version: $currentProjectVersion" -ForegroundColor Gray
        Write-Host "  Updating to: $numericVersion" -ForegroundColor Gray
        
        if (-not $DryRun) {
            $projectContent = $projectContent -replace '<Version>[^<]+</Version>', "<Version>$numericVersion</Version>"
            $projectContent | Set-Content $projectFile -Encoding UTF8
            Write-Host "✅ Project version updated" -ForegroundColor Green
        } else {
            Write-Host "🔍 [DRY RUN] Would update project version" -ForegroundColor Yellow
        }
    } else {
        Write-Host "✅ Project version already correct: $numericVersion" -ForegroundColor Green
    }
} else {
    # Add version to project file
    if (-not $DryRun) {
        $versionProperty = "`n    <Version>$numericVersion</Version>"
        $projectContent = $projectContent -replace '(\s*<AssemblyName>.*</AssemblyName>)', "`$1$versionProperty"
        $projectContent | Set-Content $projectFile -Encoding UTF8
        Write-Host "✅ Added version to project file" -ForegroundColor Green
    } else {
        Write-Host "🔍 [DRY RUN] Would add version to project file" -ForegroundColor Yellow
    }
}

# Generate release notes if not provided
if (-not $ReleaseNotes) {
    Write-Host "📝 Generating release notes..." -ForegroundColor Cyan
    
    # Get commits since last tag
    $lastTag = git describe --tags --abbrev=0 2>$null
    if ($lastTag) {
        $commitRange = "$lastTag..HEAD"
        Write-Host "  Comparing against last tag: $lastTag" -ForegroundColor Gray
    } else {
        $commitRange = "HEAD"
        Write-Host "  No previous tags found, including all commits" -ForegroundColor Gray
    }
    
    $commits = git log $commitRange --oneline --no-merges
    if ($commits) {
        $ReleaseNotes = @"
## What's Changed

### 🔧 Changes in this release:
$($commits | ForEach-Object { "- $_" } | Out-String)

### 📦 Installation
``````powershell
# One-line installation
iex (iwr "https://raw.githubusercontent.com/yourusername/photosync/main/Scripts/Install-PhotoSync.ps1").Content -Version "$Version"
``````

**Full Changelog**: https://github.com/yourusername/photosync/compare/$lastTag...$Version
"@
    } else {
        $ReleaseNotes = @"
## PhotoSync $Version

### 📦 Installation
``````powershell
# One-line installation  
iex (iwr "https://raw.githubusercontent.com/yourusername/photosync/main/Scripts/Install-PhotoSync.ps1").Content -Version "$Version"
``````
"@
    }
}

Write-Host "📋 Release Notes Preview:" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Gray
Write-Host $ReleaseNotes -ForegroundColor Gray
Write-Host "========================" -ForegroundColor Gray
Write-Host ""

if (-not $DryRun) {
    $confirm = Read-Host "Create release $Version? (Y/n)"
    if ($confirm -eq 'n' -or $confirm -eq 'N') {
        Write-Host "Release cancelled" -ForegroundColor Yellow
        exit 0
    }
}

# Commit version changes if any
if (-not $DryRun) {
    $statusAfterVersion = git status --porcelain
    if ($statusAfterVersion) {
        Write-Host "📝 Committing version changes..." -ForegroundColor Cyan
        git add .
        git commit -m "chore: bump version to $Version"
        Write-Host "✅ Version changes committed" -ForegroundColor Green
    }
} else {
    Write-Host "🔍 [DRY RUN] Would commit any version changes" -ForegroundColor Yellow
}

# Create and push tag
Write-Host "🏷️  Creating git tag..." -ForegroundColor Cyan
if (-not $DryRun) {
    if ($existingTag) {
        git tag -d $Version  # Delete existing tag locally
        git push origin --delete $Version 2>$null  # Delete remote tag (ignore errors)
    }
    
    git tag -a $Version -m "Release $Version"
    
    Write-Host "📤 Pushing tag to origin..." -ForegroundColor Cyan
    git push origin $Version
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Tag pushed successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Error: Failed to push tag" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "🔍 [DRY RUN] Would create and push tag: $Version" -ForegroundColor Yellow
}

# Final success message
Write-Host ""
Write-Host "🎉 Release process completed!" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green

if (-not $DryRun) {
    Write-Host "✅ Tag $Version created and pushed" -ForegroundColor Green
    Write-Host "🔄 GitHub Actions will now build and create the release automatically" -ForegroundColor Green
    Write-Host ""
    Write-Host "🔗 Monitor the build progress at:" -ForegroundColor Cyan
    Write-Host "   https://github.com/yourusername/photosync/actions" -ForegroundColor Blue
    Write-Host ""
    Write-Host "📦 Release will be available at:" -ForegroundColor Cyan  
    Write-Host "   https://github.com/yourusername/photosync/releases/tag/$Version" -ForegroundColor Blue
    Write-Host ""
    Write-Host "⏱️  The build typically takes 2-3 minutes to complete" -ForegroundColor Gray
} else {
    Write-Host "🔍 DRY RUN completed - no changes were made" -ForegroundColor Yellow
    Write-Host "Run without -DryRun to create the actual release" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🚀 Users will be able to install with:" -ForegroundColor Green
Write-Host "   iex (iwr `"https://raw.githubusercontent.com/yourusername/photosync/main/Scripts/Install-PhotoSync.ps1`").Content" -ForegroundColor Blue
