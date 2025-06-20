# PhotoSync Configuration Setup Script
# This script copies template configuration files and helps developers set up their local environment

param(
    [switch]$Force,
    [switch]$Help
)

function Show-Help {
    Write-Host "PhotoSync Configuration Setup Script" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\setup-config.ps1 [options]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -Force    Overwrite existing configuration files"
    Write-Host "  -Help     Show this help message"
    Write-Host ""
    Write-Host "Description:" -ForegroundColor Yellow
    Write-Host "  This script copies template configuration files to create working"
    Write-Host "  appsettings.json files for the PhotoSync application."
    Write-Host ""
    Write-Host "  Template files (.template) contain placeholder values that need"
    Write-Host "  to be updated with actual configuration values."
    Write-Host ""
    Write-Host "Security Note:" -ForegroundColor Red
    Write-Host "  Remember to update connection strings and other sensitive"
    Write-Host "  configuration values after running this script."
}

function Copy-ConfigFile {
    param(
        [string]$TemplateFile,
        [string]$TargetFile,
        [bool]$ForceOverwrite
    )
    
    if (Test-Path $TemplateFile) {
        if ((Test-Path $TargetFile) -and -not $ForceOverwrite) {
            Write-Warning "File $TargetFile already exists. Use -Force to overwrite."
            return $false
        }
        
        try {
            Copy-Item $TemplateFile $TargetFile -Force:$ForceOverwrite
            Write-Host "✅ Created $TargetFile" -ForegroundColor Green
            return $true
        }
        catch {
            Write-Error "❌ Failed to create $TargetFile`: $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-Error "❌ Template file $TemplateFile not found"
        return $false
    }
}

function Test-Prerequisites {
    # Check if we're in the correct directory
    if (-not (Test-Path "PhotoSync.csproj")) {
        Write-Error "❌ PhotoSync.csproj not found. Please run this script from the project root directory."
        return $false
    }
    
    # Check if template files exist
    $templateFiles = @(
        "appsettings.json.template",
        "appsettings.Development.json.template",
        "appsettings.Production.json.template"
    )
    
    $missingTemplates = @()
    foreach ($template in $templateFiles) {
        if (-not (Test-Path $template)) {
            $missingTemplates += $template
        }
    }
    
    if ($missingTemplates.Count -gt 0) {
        Write-Error "❌ Missing template files: $($missingTemplates -join ', ')"
        return $false
    }
    
    return $true
}

function Show-NextSteps {
    Write-Host ""
    Write-Host "🎉 Configuration setup complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Update connection strings in appsettings.json and appsettings.Development.json"
    Write-Host "2. Configure Azure Key Vault URL and Application Insights key"
    Write-Host "3. Verify import/export folder paths match your environment"
    Write-Host "4. Test the application with your configuration"
    Write-Host ""
    Write-Host "For detailed configuration instructions, see CONFIG_SETUP.md" -ForegroundColor Cyan
}

# Main script execution
try {
    if ($Help) {
        Show-Help
        exit 0
    }
    
    Write-Host "PhotoSync Configuration Setup" -ForegroundColor Green
    Write-Host "=============================" -ForegroundColor Green
    Write-Host ""
    
    # Check prerequisites
    if (-not (Test-Prerequisites)) {
        exit 1
    }
    
    Write-Host "Copying template files..." -ForegroundColor Yellow
    
    # Copy configuration files
    $results = @()
    $results += Copy-ConfigFile "appsettings.json.template" "appsettings.json" $Force
    $results += Copy-ConfigFile "appsettings.Development.json.template" "appsettings.Development.json" $Force
    $results += Copy-ConfigFile "appsettings.Production.json.template" "appsettings.Production.json" $Force
    
    # Check results
    $successCount = ($results | Where-Object { $_ -eq $true }).Count
    $totalCount = $results.Count
    
    Write-Host ""
    Write-Host "Results: $successCount of $totalCount files processed successfully" -ForegroundColor Cyan
    
    if ($successCount -eq $totalCount) {
        Show-NextSteps
        exit 0
    }
    else {
        Write-Host ""
        Write-Warning "Some files were not processed. Check the output above for details."
        exit 1
    }
}
catch {
    Write-Error "❌ An unexpected error occurred: $($_.Exception.Message)"
    exit 1
}