# PhotoSync Database Setup Script
# Run this script to create and configure the PhotoSync database

param(
    [string]$ServerName = "(localdb)\mssqllocaldb",
    [string]$DatabaseName = "PhotoDB",
    [switch]$CreateDatabase = $false
)

Write-Host "PhotoSync Database Setup" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host ""

# Connection strings
$masterConnectionString = "Server=$ServerName;Database=master;Integrated Security=true;TrustServerCertificate=true;"
$dbConnectionString = "Server=$ServerName;Database=$DatabaseName;Integrated Security=true;TrustServerCertificate=true;"

Write-Host "Server: $ServerName" -ForegroundColor Yellow
Write-Host "Database: $DatabaseName" -ForegroundColor Yellow
Write-Host ""

try {
    # Test SQL Server connection
    Write-Host "Testing SQL Server connection..." -ForegroundColor Cyan
    
    $masterConnection = New-Object System.Data.SqlClient.SqlConnection($masterConnectionString)
    $masterConnection.Open()
    Write-Host "✓ SQL Server connection successful" -ForegroundColor Green
    $masterConnection.Close()

    # Create database if requested
    if ($CreateDatabase) {
        Write-Host "Creating database '$DatabaseName'..." -ForegroundColor Cyan
        
        $createDbQuery = @"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '$DatabaseName')
BEGIN
    CREATE DATABASE [$DatabaseName]
    PRINT 'Database $DatabaseName created successfully'
END
ELSE
BEGIN
    PRINT 'Database $DatabaseName already exists'
END
"@

        $masterConnection = New-Object System.Data.SqlClient.SqlConnection($masterConnectionString)
        $masterConnection.Open()
        $command = New-Object System.Data.SqlClient.SqlCommand($createDbQuery, $masterConnection)
        $command.ExecuteNonQuery() | Out-Null
        $masterConnection.Close()
        
        Write-Host "✓ Database creation completed" -ForegroundColor Green
    }

    # Test database connection
    Write-Host "Testing database connection..." -ForegroundColor Cyan
    
    $dbConnection = New-Object System.Data.SqlClient.SqlConnection($dbConnectionString)
    $dbConnection.Open()
    Write-Host "✓ Database connection successful" -ForegroundColor Green
    $dbConnection.Close()

    # Read and execute stored procedures script
    Write-Host "Reading stored procedures script..." -ForegroundColor Cyan
    
    $scriptPath = Join-Path $PSScriptRoot "..\Database\StoredProcedures.sql"
    if (-not (Test-Path $scriptPath)) {
        throw "Stored procedures script not found: $scriptPath"
    }

    $sqlScript = Get-Content $scriptPath -Raw
    Write-Host "✓ SQL script loaded successfully" -ForegroundColor Green

    # Execute SQL script
    Write-Host "Executing stored procedures script..." -ForegroundColor Cyan
    
    $dbConnection = New-Object System.Data.SqlClient.SqlConnection($dbConnectionString)
    $dbConnection.Open()
    
    # Split script by GO statements and execute each batch
    $batches = $sqlScript -split '\r?\nGO\r?\n'
    $batchCount = 0
    
    foreach ($batch in $batches) {
        $batch = $batch.Trim()
        if ($batch.Length -gt 0) {
            $batchCount++
            try {
                $command = New-Object System.Data.SqlClient.SqlCommand($batch, $dbConnection)
                $command.CommandTimeout = 300
                $command.ExecuteNonQuery() | Out-Null
                Write-Host "  ✓ Batch $batchCount executed" -ForegroundColor Gray
            }
            catch {
                Write-Host "  ✗ Batch $batchCount failed: $($_.Exception.Message)" -ForegroundColor Red
                throw
            }
        }
    }
    
    $dbConnection.Close()
    Write-Host "✓ All SQL batches executed successfully" -ForegroundColor Green

    # Verify setup
    Write-Host "Verifying database setup..." -ForegroundColor Cyan
    
    $verificationQuery = @"
SELECT 
    (SELECT COUNT(*) FROM sys.tables WHERE name = 'Photos') as TablesCreated,
    (SELECT COUNT(*) FROM sys.procedures WHERE name LIKE 'sp_%Image%') as ProceduresCreated
"@

    $dbConnection = New-Object System.Data.SqlClient.SqlConnection($dbConnectionString)
    $dbConnection.Open()
    $command = New-Object System.Data.SqlClient.SqlCommand($verificationQuery, $dbConnection)
    $reader = $command.ExecuteReader()
    
    if ($reader.Read()) {
        $tablesCreated = $reader["TablesCreated"]
        $proceduresCreated = $reader["ProceduresCreated"]
        
        Write-Host "  Tables created: $tablesCreated" -ForegroundColor $(if ($tablesCreated -eq 1) { "Green" } else { "Red" })
        Write-Host "  Procedures created: $proceduresCreated" -ForegroundColor $(if ($proceduresCreated -eq 5) { "Green" } else { "Red" })
        
        if ($tablesCreated -eq 1 -and $proceduresCreated -eq 5) {
            Write-Host "✓ Database setup verification successful" -ForegroundColor Green
        } else {
            Write-Host "✗ Database setup verification failed" -ForegroundColor Red
            exit 1
        }
    }
    
    $reader.Close()
    $dbConnection.Close()

    Write-Host ""
    Write-Host "PhotoSync database setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Connection string for appsettings.json:" -ForegroundColor Yellow
    Write-Host $dbConnectionString -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Update appsettings.Development.json with the connection string above" -ForegroundColor Gray
    Write-Host "2. Create test folders: C:\Temp\PhotoSync\Import and C:\Temp\PhotoSync\Export" -ForegroundColor Gray
    Write-Host "3. Run 'dotnet run test' to verify configuration" -ForegroundColor Gray
    Write-Host "4. Add sample JPG files to import folder" -ForegroundColor Gray
    Write-Host "5. Run 'dotnet run import' to test import functionality" -ForegroundColor Gray

} catch {
    Write-Host ""
    Write-Host "✗ Error during database setup: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting tips:" -ForegroundColor Yellow
    Write-Host "1. Ensure SQL Server is running" -ForegroundColor Gray
    Write-Host "2. Check if you have admin rights" -ForegroundColor Gray
    Write-Host "3. Verify server name is correct" -ForegroundColor Gray
    Write-Host "4. Try using -CreateDatabase switch if database doesn't exist" -ForegroundColor Gray
    exit 1
}
