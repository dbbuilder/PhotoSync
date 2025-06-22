-- =============================================
-- PhotoSync Database Migration Script
-- Adds comprehensive tracking fields for workflow management
-- Version: 2.0.0
-- =============================================

-- Check if we're in the right database context
IF DB_NAME() NOT LIKE '%Photo%'
BEGIN
    PRINT 'WARNING: Current database is ' + DB_NAME() + '. Make sure this is the correct database for PhotoSync!'
    PRINT 'Press Ctrl+C to cancel, or wait 10 seconds to continue...'
    WAITFOR DELAY '00:00:10'
END

PRINT 'Starting PhotoSync tracking fields migration...'
PRINT '============================================='

-- =============================================
-- Add new tracking columns to Photos table
-- =============================================
PRINT 'Adding tracking columns to PHOTOS.Photos table...'

-- Add ImageSource column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'ImageSource')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD ImageSource nvarchar(500) NULL
    PRINT '  ✓ Added ImageSource column'
END
ELSE
    PRINT '  - ImageSource column already exists'

-- Add SourceFileName column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'SourceFileName')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD SourceFileName nvarchar(255) NULL
    PRINT '  ✓ Added SourceFileName column'
END
ELSE
    PRINT '  - SourceFileName column already exists'

-- Add ImportedDate column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'ImportedDate')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD ImportedDate datetime2 NULL
    PRINT '  ✓ Added ImportedDate column'
END
ELSE
    PRINT '  - ImportedDate column already exists'

-- Add ExportedDate column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'ExportedDate')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD ExportedDate datetime2 NULL
    PRINT '  ✓ Added ExportedDate column'
END
ELSE
    PRINT '  - ExportedDate column already exists'

-- Add AzureUploadedDate column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'AzureUploadedDate')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD AzureUploadedDate datetime2 NULL
    PRINT '  ✓ Added AzureUploadedDate column'
END
ELSE
    PRINT '  - AzureUploadedDate column already exists'

-- Add PhotoModifiedDate column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'PhotoModifiedDate')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD PhotoModifiedDate datetime2 NULL
    PRINT '  ✓ Added PhotoModifiedDate column'
END
ELSE
    PRINT '  - PhotoModifiedDate column already exists'

-- Add AzureSyncRequired column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'AzureSyncRequired')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD AzureSyncRequired bit NOT NULL DEFAULT 0
    PRINT '  ✓ Added AzureSyncRequired column'
END
ELSE
    PRINT '  - AzureSyncRequired column already exists'

-- Add FileHash column for duplicate detection
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'FileHash')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD FileHash nvarchar(64) NULL
    PRINT '  ✓ Added FileHash column'
END
ELSE
    PRINT '  - FileHash column already exists'

-- Add FileSize column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'FileSize')
BEGIN
    ALTER TABLE PHOTOS.Photos ADD FileSize bigint NULL
    PRINT '  ✓ Added FileSize column'
END
ELSE
    PRINT '  - FileSize column already exists'

PRINT ''
PRINT 'Creating indexes for performance...'

-- =============================================
-- Create indexes for new columns
-- =============================================

-- Index for ExportedDate (for incremental exports)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'IX_Photos_ExportedDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Photos_ExportedDate ON PHOTOS.Photos (ExportedDate)
    PRINT '  ✓ Created index on ExportedDate'
END

-- Index for AzureSyncRequired (for finding photos needing sync)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'IX_Photos_AzureSyncRequired')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Photos_AzureSyncRequired ON PHOTOS.Photos (AzureSyncRequired) 
        INCLUDE (Code, AzureStoragePath) WHERE AzureSyncRequired = 1
    PRINT '  ✓ Created filtered index on AzureSyncRequired'
END

-- Index for FileHash (for duplicate detection)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'IX_Photos_FileHash')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Photos_FileHash ON PHOTOS.Photos (FileHash) WHERE FileHash IS NOT NULL
    PRINT '  ✓ Created index on FileHash'
END

-- Index for ImportedDate
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'IX_Photos_ImportedDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Photos_ImportedDate ON PHOTOS.Photos (ImportedDate)
    PRINT '  ✓ Created index on ImportedDate'
END

PRINT ''
PRINT 'Updating existing data with sensible defaults...'

-- =============================================
-- Initialize tracking fields for existing data
-- =============================================

-- Set PhotoModifiedDate to CreatedDate for existing records
UPDATE PHOTOS.Photos 
SET PhotoModifiedDate = CreatedDate 
WHERE PhotoModifiedDate IS NULL AND ImageData IS NOT NULL

PRINT '  ✓ Set PhotoModifiedDate for ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' existing photos'

-- Set ImageSource for existing records based on what we know
UPDATE PHOTOS.Photos 
SET ImageSource = CASE 
    WHEN AzureStoragePath IS NOT NULL AND ImageData IS NULL THEN 'AZURE:' + AzureStoragePath
    WHEN AzureStoragePath IS NULL AND ImageData IS NOT NULL THEN 'BULK:Initial Import'
    WHEN AzureStoragePath IS NOT NULL AND ImageData IS NOT NULL THEN 'HYBRID:Local+Azure'
    ELSE 'UNKNOWN'
END
WHERE ImageSource IS NULL

PRINT '  ✓ Set ImageSource for ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' existing records'

-- Set ImportedDate to CreatedDate for existing records
UPDATE PHOTOS.Photos 
SET ImportedDate = CreatedDate 
WHERE ImportedDate IS NULL AND ImageData IS NOT NULL

PRINT '  ✓ Set ImportedDate for ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' existing records'

-- =============================================
-- Create new stored procedures for tracking
-- =============================================
PRINT ''
PRINT 'Creating new tracking stored procedures...'

-- Drop procedures if they exist
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PHOTOS.sp_GetPhotosForIncrementalExport') AND type in (N'P', N'PC'))
    DROP PROCEDURE PHOTOS.sp_GetPhotosForIncrementalExport

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PHOTOS.sp_GetPhotosNeedingAzureSync') AND type in (N'P', N'PC'))
    DROP PROCEDURE PHOTOS.sp_GetPhotosNeedingAzureSync

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PHOTOS.sp_UpdateExportTracking') AND type in (N'P', N'PC'))
    DROP PROCEDURE PHOTOS.sp_UpdateExportTracking

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PHOTOS.sp_UpdateImportTracking') AND type in (N'P', N'PC'))
    DROP PROCEDURE PHOTOS.sp_UpdateImportTracking

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PHOTOS.sp_CheckDuplicateByHash') AND type in (N'P', N'PC'))
    DROP PROCEDURE PHOTOS.sp_CheckDuplicateByHash

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'PHOTOS.sp_GetSyncStatus') AND type in (N'P', N'PC'))
    DROP PROCEDURE PHOTOS.sp_GetSyncStatus

-- =============================================
-- Procedure to get photos for incremental export
-- =============================================
CREATE PROCEDURE PHOTOS.sp_GetPhotosForIncrementalExport
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT 
        Code,
        ImageData,
        AzureStoragePath,
        CreatedDate,
        ModifiedDate,
        ImageSource,
        SourceFileName,
        ImportedDate,
        ExportedDate,
        AzureUploadedDate,
        PhotoModifiedDate,
        AzureSyncRequired,
        FileHash,
        FileSize
    FROM PHOTOS.Photos
    WHERE 
        -- Never exported
        (ExportedDate IS NULL) 
        OR 
        -- Modified after last export
        (ModifiedDate > ExportedDate)
        OR
        -- Photo data changed after last export
        (PhotoModifiedDate > ExportedDate)
    ORDER BY CreatedDate DESC
END

PRINT '  ✓ Created sp_GetPhotosForIncrementalExport'

-- =============================================
-- Procedure to get photos needing Azure sync
-- =============================================
CREATE PROCEDURE PHOTOS.sp_GetPhotosNeedingAzureSync
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT 
        Code,
        ImageData,
        AzureStoragePath,
        CreatedDate,
        ModifiedDate,
        AzureSyncRequired,
        PhotoModifiedDate,
        AzureUploadedDate
    FROM PHOTOS.Photos
    WHERE 
        AzureSyncRequired = 1 
        AND ImageData IS NOT NULL
    ORDER BY PhotoModifiedDate DESC
END

PRINT '  ✓ Created sp_GetPhotosNeedingAzureSync'

-- =============================================
-- Procedure to update export tracking
-- =============================================
CREATE PROCEDURE PHOTOS.sp_UpdateExportTracking
    @Code nvarchar(100),
    @ExportedDate datetime2,
    @Result int OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    SET @Result = 0
    
    UPDATE PHOTOS.Photos 
    SET ExportedDate = @ExportedDate
    WHERE Code = @Code
    
    SET @Result = @@ROWCOUNT
END

PRINT '  ✓ Created sp_UpdateExportTracking'

-- =============================================
-- Procedure to update import tracking
-- =============================================
CREATE PROCEDURE PHOTOS.sp_UpdateImportTracking
    @Code nvarchar(100),
    @ImportedDate datetime2,
    @ImageSource nvarchar(500),
    @SourceFileName nvarchar(255),
    @FileHash nvarchar(64),
    @FileSize bigint,
    @Result int OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    SET @Result = 0
    
    UPDATE PHOTOS.Photos 
    SET 
        ImportedDate = @ImportedDate,
        ImageSource = @ImageSource,
        SourceFileName = @SourceFileName,
        FileHash = @FileHash,
        FileSize = @FileSize,
        PhotoModifiedDate = @ImportedDate
    WHERE Code = @Code
    
    SET @Result = @@ROWCOUNT
END

PRINT '  ✓ Created sp_UpdateImportTracking'

-- =============================================
-- Procedure to check for duplicates by hash
-- =============================================
CREATE PROCEDURE PHOTOS.sp_CheckDuplicateByHash
    @FileHash nvarchar(64),
    @ExcludeCode nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT TOP 1
        Code,
        SourceFileName,
        ImportedDate,
        FileSize
    FROM PHOTOS.Photos
    WHERE 
        FileHash = @FileHash
        AND (@ExcludeCode IS NULL OR Code != @ExcludeCode)
END

PRINT '  ✓ Created sp_CheckDuplicateByHash'

-- =============================================
-- Procedure to get overall sync status
-- =============================================
CREATE PROCEDURE PHOTOS.sp_GetSyncStatus
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT 
        COUNT(*) as TotalPhotos,
        COUNT(CASE WHEN ImageData IS NOT NULL THEN 1 END) as PhotosWithData,
        COUNT(CASE WHEN AzureStoragePath IS NOT NULL THEN 1 END) as PhotosInAzure,
        COUNT(CASE WHEN ExportedDate IS NULL THEN 1 END) as PendingExport,
        COUNT(CASE WHEN ExportedDate < ModifiedDate THEN 1 END) as StaleExports,
        COUNT(CASE WHEN AzureSyncRequired = 1 THEN 1 END) as PendingAzureSync,
        COUNT(CASE WHEN FileHash IS NOT NULL THEN 1 END) as PhotosWithHash,
        COUNT(DISTINCT FileHash) as UniquePhotos,
        MIN(ImportedDate) as FirstImportDate,
        MAX(ImportedDate) as LastImportDate,
        MIN(ExportedDate) as FirstExportDate,
        MAX(ExportedDate) as LastExportDate,
        MIN(AzureUploadedDate) as FirstAzureUploadDate,
        MAX(AzureUploadedDate) as LastAzureUploadDate
    FROM PHOTOS.Photos
END

PRINT '  ✓ Created sp_GetSyncStatus'

-- =============================================
-- Update existing stored procedures
-- =============================================
PRINT ''
PRINT 'Updating existing stored procedures for tracking...'

-- Update sp_SaveImage to handle tracking fields
ALTER PROCEDURE PHOTOS.sp_SaveImage
    @Code nvarchar(100),
    @ImageData varbinary(max) = NULL,
    @AzureStoragePath nvarchar(500) = NULL,
    @CreatedDate datetime2,
    @ImageSource nvarchar(500) = NULL,
    @SourceFileName nvarchar(255) = NULL,
    @FileHash nvarchar(64) = NULL,
    @FileSize bigint = NULL,
    @Result int OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_SaveImage for Code: ' + @Code + ', Image Size: ' + CAST(DATALENGTH(@ImageData) AS nvarchar(20)) + ' bytes'
    
    -- Initialize result
    SET @Result = 0
    
    BEGIN TRY
        BEGIN TRANSACTION
        
        -- Check if record already exists
        IF EXISTS (SELECT 1 FROM PHOTOS.Photos WHERE Code = @Code)
        BEGIN
            -- Update existing record
            UPDATE PHOTOS.Photos 
            SET ImageData = ISNULL(@ImageData, ImageData),
                AzureStoragePath = ISNULL(@AzureStoragePath, AzureStoragePath),
                ModifiedDate = GETUTCDATE(),
                PhotoModifiedDate = CASE WHEN @ImageData IS NOT NULL THEN GETUTCDATE() ELSE PhotoModifiedDate END,
                AzureSyncRequired = CASE WHEN @ImageData IS NOT NULL AND AzureStoragePath IS NOT NULL THEN 1 ELSE AzureSyncRequired END,
                ImageSource = ISNULL(@ImageSource, ImageSource),
                SourceFileName = ISNULL(@SourceFileName, SourceFileName),
                FileHash = ISNULL(@FileHash, FileHash),
                FileSize = ISNULL(@FileSize, FileSize)
            WHERE Code = @Code
            
            SET @Result = @@ROWCOUNT
            PRINT 'Updated existing image record for Code: ' + @Code
        END
        ELSE
        BEGIN
            -- Insert new record
            INSERT INTO PHOTOS.Photos (Code, ImageData, AzureStoragePath, CreatedDate, ImportedDate, 
                PhotoModifiedDate, ImageSource, SourceFileName, FileHash, FileSize)
            VALUES (@Code, @ImageData, @AzureStoragePath, @CreatedDate, 
                CASE WHEN @ImageData IS NOT NULL THEN @CreatedDate ELSE NULL END,
                CASE WHEN @ImageData IS NOT NULL THEN @CreatedDate ELSE NULL END,
                @ImageSource, @SourceFileName, @FileHash, @FileSize)
            
            SET @Result = @@ROWCOUNT
            PRINT 'Inserted new image record for Code: ' + @Code
        END
        
        COMMIT TRANSACTION
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION
        
        -- Log error details as requested
        PRINT 'Error in sp_SaveImage: ' + ERROR_MESSAGE()
        PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS nvarchar(10))
        PRINT 'Error Severity: ' + CAST(ERROR_SEVERITY() AS nvarchar(10))
        PRINT 'Error State: ' + CAST(ERROR_STATE() AS nvarchar(10))
        
        SET @Result = 0
        
        -- Re-throw the error
        THROW
    END CATCH
END

PRINT '  ✓ Updated sp_SaveImage with tracking support'

-- Update sp_UpdateAzureStoragePath to include tracking
ALTER PROCEDURE PHOTOS.sp_UpdateAzureStoragePath
    @Code nvarchar(100),
    @AzureStoragePath nvarchar(500),
    @Result int OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    PRINT 'Executing sp_UpdateAzureStoragePath for Code: ' + @Code
    
    SET @Result = 0
    
    BEGIN TRY
        UPDATE PHOTOS.Photos 
        SET AzureStoragePath = @AzureStoragePath,
            ModifiedDate = GETUTCDATE(),
            AzureUploadedDate = GETUTCDATE(),
            AzureSyncRequired = 0
        WHERE Code = @Code
        
        SET @Result = @@ROWCOUNT
        
        IF @Result > 0
            PRINT 'Successfully updated Azure path for Code: ' + @Code
        ELSE
            PRINT 'No record found for Code: ' + @Code
            
    END TRY
    BEGIN CATCH
        PRINT 'Error in sp_UpdateAzureStoragePath: ' + ERROR_MESSAGE()
        SET @Result = 0
        THROW
    END CATCH
END

PRINT '  ✓ Updated sp_UpdateAzureStoragePath with tracking support'

-- Update sp_UpdateImageData to set sync flag
ALTER PROCEDURE PHOTOS.sp_UpdateImageData
    @Code nvarchar(100),
    @ImageData varbinary(max),
    @Result int OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    PRINT 'Executing sp_UpdateImageData for Code: ' + @Code
    
    SET @Result = 0
    
    BEGIN TRY
        UPDATE PHOTOS.Photos 
        SET ImageData = @ImageData,
            ModifiedDate = GETUTCDATE(),
            PhotoModifiedDate = GETUTCDATE(),
            AzureSyncRequired = CASE WHEN AzureStoragePath IS NOT NULL THEN 1 ELSE 0 END
        WHERE Code = @Code
        
        SET @Result = @@ROWCOUNT
        
        IF @Result > 0
            PRINT 'Successfully updated image data for Code: ' + @Code
        ELSE
            PRINT 'No record found for Code: ' + @Code
            
    END TRY
    BEGIN CATCH
        PRINT 'Error in sp_UpdateImageData: ' + ERROR_MESSAGE()
        SET @Result = 0
        THROW
    END CATCH
END

PRINT '  ✓ Updated sp_UpdateImageData with sync tracking'

-- =============================================
-- Create a summary view for easy monitoring
-- =============================================
IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'PHOTOS.vw_PhotoSyncStatus'))
    DROP VIEW PHOTOS.vw_PhotoSyncStatus

CREATE VIEW PHOTOS.vw_PhotoSyncStatus
AS
SELECT 
    Code,
    CASE 
        WHEN ImageData IS NULL AND AzureStoragePath IS NULL THEN 'Empty'
        WHEN ImageData IS NOT NULL AND AzureStoragePath IS NULL THEN 'LocalOnly'
        WHEN ImageData IS NULL AND AzureStoragePath IS NOT NULL THEN 'AzureOnly'
        WHEN ImageData IS NOT NULL AND AzureStoragePath IS NOT NULL THEN 'Hybrid'
    END as StorageMode,
    CASE
        WHEN ExportedDate IS NULL THEN 'NeverExported'
        WHEN ExportedDate < ISNULL(PhotoModifiedDate, ModifiedDate) THEN 'ExportNeeded'
        ELSE 'ExportCurrent'
    END as ExportStatus,
    CASE
        WHEN AzureSyncRequired = 1 THEN 'SyncNeeded'
        WHEN AzureStoragePath IS NULL THEN 'NotInAzure'
        ELSE 'Synced'
    END as AzureStatus,
    ImportedDate,
    ExportedDate,
    AzureUploadedDate,
    PhotoModifiedDate,
    FileSize,
    LEFT(ImageSource, 50) as ImageSourceType
FROM PHOTOS.Photos

PRINT ''
PRINT '✓ Created vw_PhotoSyncStatus view for monitoring'

-- =============================================
-- Final summary
-- =============================================
PRINT ''
PRINT '============================================='
PRINT 'Migration completed successfully!'
PRINT ''
PRINT 'Summary of changes:'
PRINT '  - Added 9 new tracking columns'
PRINT '  - Created 4 performance indexes'
PRINT '  - Added 6 new stored procedures'
PRINT '  - Updated 3 existing stored procedures'
PRINT '  - Created 1 monitoring view'
PRINT ''
PRINT 'Next steps:'
PRINT '  1. Update application code to use new fields'
PRINT '  2. Test incremental export functionality'
PRINT '  3. Configure auto-archive settings'
PRINT '  4. Run PhotoSync status command to verify'
PRINT '============================================='