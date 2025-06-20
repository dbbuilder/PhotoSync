@echo off
echo 🚀 PhotoSync Release Creator
echo ===========================
echo.

if "%1"=="" (
    echo ERROR: Version number required
    echo.
    echo Usage: %0 ^<version^>
    echo Example: %0 1.0.1
    echo.
    echo This will:
    echo   1. Build and test PhotoSync
    echo   2. Update project version
    echo   3. Create git tag
    echo   4. Trigger GitHub Actions build
    echo   5. Create GitHub release automatically
    echo.
    pause
    exit /b 1
)

set VERSION=%1

echo Creating PhotoSync release %VERSION%...
echo.

REM Run the PowerShell release script
PowerShell -ExecutionPolicy Bypass -Command "& '%~dp0Create-Release.ps1' -Version '%VERSION%'"

if %errorlevel% equ 0 (
    echo.
    echo ✅ Release %VERSION% created successfully!
    echo.
    echo 🔗 Monitor build progress:
    echo    https://github.com/yourusername/photosync/actions
    echo.
    echo 📦 Release will be available at:
    echo    https://github.com/yourusername/photosync/releases
    echo.
    echo 🚀 Users can install with:
    echo    iex (iwr "https://raw.githubusercontent.com/yourusername/photosync/main/Scripts/Install-PhotoSync.ps1"^).Content
) else (
    echo.
    echo ❌ Release creation failed. Check the output above for details.
)

echo.
pause
