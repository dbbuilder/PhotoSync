@echo off
echo PhotoSync Test Environment Setup
echo ================================

REM Create base directory
set BASE_DIR=C:\Temp\PhotoSync
echo Creating base directory: %BASE_DIR%
if not exist "%BASE_DIR%" mkdir "%BASE_DIR%"

REM Create import directory
set IMPORT_DIR=%BASE_DIR%\Import
echo Creating import directory: %IMPORT_DIR%
if not exist "%IMPORT_DIR%" mkdir "%IMPORT_DIR%"

REM Create export directory  
set EXPORT_DIR=%BASE_DIR%\Export
echo Creating export directory: %EXPORT_DIR%
if not exist "%EXPORT_DIR%" mkdir "%EXPORT_DIR%"

REM Create logs directory in project
set PROJECT_LOGS=d:\dev2\PhotoSync\logs
echo Creating logs directory: %PROJECT_LOGS%
if not exist "%PROJECT_LOGS%" mkdir "%PROJECT_LOGS%"

echo.
echo Test environment setup completed!
echo.
echo Directory structure:
echo   %BASE_DIR%
echo   ├── Import\     (place JPG files here for testing)
echo   └── Export\     (exported files will appear here)
echo.
echo Project logs will be created in:
echo   %PROJECT_LOGS%
echo.
echo Next steps:
echo 1. Add some JPG files to %IMPORT_DIR%
echo 2. Update appsettings.Development.json with correct connection string
echo 3. Run database setup: Scripts\Setup-Database.ps1
echo 4. Test with: dotnet run test
echo.
pause
