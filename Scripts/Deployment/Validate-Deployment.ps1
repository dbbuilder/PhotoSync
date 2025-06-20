# PhotoSync Deployment Validation Script
# Verifies that PhotoSync is properly deployed and configured on Windows Server

param(
    [Parameter(Mandatory=$false)]
    [string]$DeploymentPath = "C:\Applications\PhotoSync",
    
    [Parameter(Mandatory=$false)]
    [string]$DataPath = "D:\PhotoSync"
)

Write-Host "PhotoSync Deployment Validation" -ForegroundColor Green
Write-Host "===============================" -ForegroundColor Green
Write-Host ""

$allTestsPassed = $true

# Test 1: Check if deployment directory exists
Write-Host "1. Checking deployment directory..." -ForegroundColor Cyan
if (Test-Path $DeploymentPath) {
    Write-Host "   ✓ Deployment directory exists: $DeploymentPath" -ForegroundColor Green
} else {
    Write-Host "   ✗ Deployment directory not found: $DeploymentPath" -ForegroundColor Red
    $allTestsPassed = $false
}

# Test 2: Check if PhotoSync.exe exists
Write-Host "2. Checking PhotoSync executable..." -ForegroundColor Cyan
$exePath = Join-Path $DeploymentPath "PhotoSync.exe"
if (Test-Path $exePath) {
    Write-Host "   ✓ PhotoSync.exe found" -ForegroundColor Green
    
    # Check file version
    try {
        $version = (Get-ItemProperty $exePath).VersionInfo.FileVersion
        Write-Host "   ✓ Version: $version" -ForegroundColor Green
    } catch {
        Write-Host "   ⚠ Could not read version info" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ✗ PhotoSync.exe not found" -ForegroundColor Red
    $allTestsPassed = $false
}

# Test 3: Check configuration files
Write-Host "3. Checking configuration files..." -ForegroundColor Cyan
$configFiles = @(
    "appsettings.json",
    "appsettings.Production.json"
)

foreach ($configFile in $configFiles) {
    $configPath = Join-Path $DeploymentPath $configFile
    if (Test-Path $configPath) {
        Write-Host "   ✓ $configFile exists" -ForegroundColor Green
        
        # Validate JSON syntax
        try {
            $config = Get-Content $configPath -Raw | ConvertFrom-Json
            Write-Host "   ✓ $configFile is valid JSON" -ForegroundColor Green
        } catch {
            Write-Host "   ✗ $configFile has invalid JSON syntax" -ForegroundColor Red
            $allTestsPassed = $false
        }
    } else {
        Write-Host "   ✗ $configFile not found" -ForegroundColor Red
        $allTestsPassed = $false
    }
}

# Test 4: Check data directories
Write-Host "4. Checking data directories..." -ForegroundColor Cyan
$dataDirs = @(
    "$DataPath\Import",
    "$DataPath\Export",
    "$DataPath\Logs"
)

foreach ($dir in $dataDirs) {
    if (Test-Path $dir) {
        Write-Host "   ✓ $dir exists" -ForegroundColor Green
    } else {
        Write-Host "   ✗ $dir not found" -ForegroundColor Red
        $allTestsPassed = $false
    }
}

# Test 5: Check batch scripts
Write-Host "5. Checking operation scripts..." -ForegroundColor Cyan
$scripts = @(
    "Scripts\import.bat",
    "Scripts\export.bat",
    "Scripts\test.bat"
)

foreach ($script in $scripts) {
    $scriptPath = Join-Path $DeploymentPath $script
    if (Test-Path $scriptPath) {
        Write-Host "   ✓ $script exists" -ForegroundColor Green
    } else {
        Write-Host "   ✗ $script not found" -ForegroundColor Red
        $allTestsPassed = $false
    }
}

# Test 6: Check environment variable
Write-Host "6. Checking environment configuration..." -ForegroundColor Cyan
$aspnetEnv = [Environment]::GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", [EnvironmentVariableTarget]::Machine)
if ($aspnetEnv -eq "Production") {
    Write-Host "   ✓ ASPNETCORE_ENVIRONMENT is set to Production" -ForegroundColor Green
} elseif ($aspnetEnv) {
    Write-Host "   ⚠ ASPNETCORE_ENVIRONMENT is set to: $aspnetEnv" -ForegroundColor Yellow
} else {
    Write-Host "   ⚠ ASPNETCORE_ENVIRONMENT not set" -ForegroundColor Yellow
}

# Test 7: Check scheduled tasks
Write-Host "7. Checking scheduled tasks..." -ForegroundColor Cyan
try {
    $importTask = Get-ScheduledTask -TaskName "PhotoSync Import" -ErrorAction SilentlyContinue
    $exportTask = Get-ScheduledTask -TaskName "PhotoSync Export" -ErrorAction SilentlyContinue
    
    if ($importTask) {
        $importStatus = $importTask.State
        Write-Host "   ✓ PhotoSync Import task exists (Status: $importStatus)" -ForegroundColor Green
        
        # Check next run time
        $importInfo = Get-ScheduledTaskInfo -TaskName "PhotoSync Import"
        Write-Host "   ✓ Next run: $($importInfo.NextRunTime)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ PhotoSync Import scheduled task not found" -ForegroundColor Yellow
    }
    
    if ($exportTask) {
        $exportStatus = $exportTask.State
        Write-Host "   ✓ PhotoSync Export task exists (Status: $exportStatus)" -ForegroundColor Green
        
        # Check next run time
        $exportInfo = Get-ScheduledTaskInfo -TaskName "PhotoSync Export"
        Write-Host "   ✓ Next run: $($exportInfo.NextRunTime)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ PhotoSync Export scheduled task not found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ⚠ Could not check scheduled tasks (may require admin privileges)" -ForegroundColor Yellow
}

# Test 8: Run PhotoSync test command
Write-Host "8. Running PhotoSync configuration test..." -ForegroundColor Cyan
if (Test-Path $exePath) {
    Push-Location $DeploymentPath
    try {
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        
        # Run test command
        $testOutput = & ".\PhotoSync.exe" test 2>&1
        $testExitCode = $LASTEXITCODE
        
        if ($testExitCode -eq 0) {
            Write-Host "   ✓ PhotoSync configuration test passed" -ForegroundColor Green
        } else {
            Write-Host "   ⚠ PhotoSync configuration test failed (exit code: $testExitCode)" -ForegroundColor Yellow
            Write-Host "   Output: $testOutput" -ForegroundColor Gray
        }
        
    } catch {
        Write-Host "   ✗ Could not run PhotoSync test" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
        $allTestsPassed = $false
    } finally {
        Pop-Location
    }
} else {
    Write-Host "   ✗ Cannot run test - PhotoSync.exe not found" -ForegroundColor Red
    $allTestsPassed = $false
}

Write-Host ""

# Summary
if ($allTestsPassed) {
    Write-Host "Validation Summary: ✓ ALL TESTS PASSED" -ForegroundColor Green
    Write-Host "PhotoSync is properly deployed and ready for use!" -ForegroundColor Green
} else {
    Write-Host "Validation Summary: ⚠ SOME TESTS FAILED" -ForegroundColor Yellow
    Write-Host "Please review the failed tests above and fix any issues." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "1. Run database diagnostic: $DeploymentPath\PhotoSync.exe diagnose" -ForegroundColor White
Write-Host "2. Test import operation: $DeploymentPath\Scripts\import.bat" -ForegroundColor White  
Write-Host "3. Test export operation: $DeploymentPath\Scripts\export.bat" -ForegroundColor White
Write-Host "4. Monitor logs at: $DataPath\Logs\" -ForegroundColor White
Write-Host ""

# Return appropriate exit code
if ($allTestsPassed) {
    exit 0
} else {
    exit 1
}
