@echo off
echo PhotoSync Windows Server Deployment
echo ====================================
echo.

REM Check for administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator
    echo Right-click and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

echo Running deployment script...
echo.

PowerShell -ExecutionPolicy Bypass -File "%~dp0Deploy-WindowsServer.ps1" -CreateFolders -InstallScheduledTasks

echo.
echo Deployment script completed.
echo Check the output above for any errors.
echo.

pause
