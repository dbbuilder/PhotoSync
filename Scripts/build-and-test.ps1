# PhotoSync Build and Test Script
# This script builds the solution and runs all tests with detailed output

param(
    [switch]$SkipIntegration,
    [switch]$Coverage,
    [switch]$Verbose
)

Write-Host "PhotoSync Build and Test Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Set error action preference
$ErrorActionPreference = "Stop"

try {
    # Clean previous build artifacts
    Write-Host "Cleaning previous build artifacts..." -ForegroundColor Yellow
    dotnet clean --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    Write-Host "✓ Clean completed" -ForegroundColor Green
    Write-Host ""

    # Restore NuGet packages
    Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    Write-Host "✓ Restore completed" -ForegroundColor Green
    Write-Host ""

    # Build the solution
    Write-Host "Building solution..." -ForegroundColor Yellow
    if ($Verbose) {
        dotnet build --configuration Release --no-restore
    } else {
        dotnet build --configuration Release --no-restore --verbosity minimal
    }
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "✓ Build completed successfully" -ForegroundColor Green
    Write-Host ""

    # Run tests
    Write-Host "Running tests..." -ForegroundColor Yellow
    
    $testArgs = @()
    
    # Add filter for skipping integration tests if requested
    if ($SkipIntegration) {
        $testArgs += "--filter"
        $testArgs += "Category!=Integration"
        Write-Host "  (Skipping integration tests)" -ForegroundColor DarkGray
    }
    
    # Add code coverage if requested
    if ($Coverage) {
        $testArgs += "--collect:`"XPlat Code Coverage`""
        $testArgs += "--results-directory"
        $testArgs += "TestResults"
        Write-Host "  (Collecting code coverage)" -ForegroundColor DarkGray
    }
    
    # Add verbosity
    if ($Verbose) {
        $testArgs += "--logger"
        $testArgs += "console;verbosity=detailed"
    } else {
        $testArgs += "--logger"
        $testArgs += "console;verbosity=normal"
    }
    
    # Execute tests
    dotnet test $testArgs
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    Write-Host "✓ All tests passed" -ForegroundColor Green
    Write-Host ""

    # Generate coverage report if coverage was collected
    if ($Coverage) {
        Write-Host "Generating coverage report..." -ForegroundColor Yellow
        $coverageFile = Get-ChildItem -Path "TestResults" -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1
        if ($coverageFile) {
            Write-Host "Coverage file found: $($coverageFile.FullName)" -ForegroundColor DarkGray
            # You can use ReportGenerator here if installed
            # reportgenerator -reports:$($coverageFile.FullName) -targetdir:TestResults\CoverageReport -reporttypes:Html
        }
    }

    Write-Host ""
    Write-Host "Build and test completed successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Show summary
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "--------" -ForegroundColor Cyan
    
    # Count test projects
    $testProjects = Get-ChildItem -Filter "*.Tests.csproj" -Recurse
    Write-Host "Test Projects: $($testProjects.Count)" -ForegroundColor White
    
    # Show build output location
    Write-Host "Build Output: bin\Release\net8.0\" -ForegroundColor White
    
    if ($Coverage) {
        Write-Host "Coverage Results: TestResults\" -ForegroundColor White
    }

} catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Build and test failed!" -ForegroundColor Red
    exit 1
}

# Prompt to run specific test categories
Write-Host ""
Write-Host "Additional test options:" -ForegroundColor Cyan
Write-Host "  Run only unit tests:        .\build-and-test.ps1 -SkipIntegration" -ForegroundColor DarkGray
Write-Host "  Run with code coverage:     .\build-and-test.ps1 -Coverage" -ForegroundColor DarkGray
Write-Host "  Run with verbose output:    .\build-and-test.ps1 -Verbose" -ForegroundColor DarkGray
Write-Host "  Run specific test:          dotnet test --filter `"FullyQualifiedName~ImportCommand`"" -ForegroundColor DarkGray
Write-Host ""