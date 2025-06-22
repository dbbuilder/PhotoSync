-- =============================================
-- PhotoSync Database Setup Script
-- =============================================

-- Create PHOTOS schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'PHOTOS')
BEGIN
    EXEC('CREATE SCHEMA PHOTOS')
    PRINT 'PHOTOS schema created successfully'
END
ELSE
BEGIN
    PRINT 'PHOTOS schema already exists'
END

-- Create database table for storing images
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'PHOTOS' AND TABLE_NAME = 'Photos')
BEGIN
    CREATE TABLE PHOTOS.Photos (
        Id int IDENTITY(1,1) PRIMARY KEY,
        Code nvarchar(100) NOT NULL,
        ImageData varbinary(max) NULL,
        AzureStoragePath nvarchar(500) NULL,
        CreatedDate datetime2 NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDate datetime2 NULL
    )

    -- Create unique constraint on Code field
    ALTER TABLE PHOTOS.Photos ADD CONSTRAINT UK_Photos_Code UNIQUE (Code)

    -- Index for faster lookups by code
    CREATE NONCLUSTERED INDEX IX_Photos_Code ON PHOTOS.Photos (Code)
    
    -- Index for Azure operations
    CREATE NONCLUSTERED INDEX IX_Photos_AzureStoragePath ON PHOTOS.Photos (AzureStoragePath)

    PRINT 'Photos table created successfully'
END
ELSE
BEGIN
    -- Add AzureStoragePath column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'AzureStoragePath')
    BEGIN
        ALTER TABLE PHOTOS.Photos ADD AzureStoragePath nvarchar(500) NULL
        CREATE NONCLUSTERED INDEX IX_Photos_AzureStoragePath ON PHOTOS.Photos (AzureStoragePath)
        PRINT 'Added AzureStoragePath column to Photos table'
    END
    
    -- Make ImageData nullable if it isn't already
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PHOTOS.Photos') AND name = 'ImageData' AND is_nullable = 0)
    BEGIN
        ALTER TABLE PHOTOS.Photos ALTER COLUMN ImageData varbinary(max) NULL
        PRINT 'Made ImageData column nullable'
    END
    
    PRINT 'Photos table already exists and has been updated'
END

-- =============================================
-- Stored procedure to save (insert or update) an image
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_SaveImage
    @Code nvarchar(100),
    @ImageData varbinary(max) = NULL,
    @AzureStoragePath nvarchar(500) = NULL,
    @CreatedDate datetime2,
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
                ModifiedDate = GETUTCDATE()
            WHERE Code = @Code
            
            SET @Result = @@ROWCOUNT
            PRINT 'Updated existing image record for Code: ' + @Code
        END
        ELSE
        BEGIN
            -- Insert new record
            INSERT INTO PHOTOS.Photos (Code, ImageData, AzureStoragePath, CreatedDate)
            VALUES (@Code, @ImageData, @AzureStoragePath, @CreatedDate)
            
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

-- =============================================
-- Stored procedure to get all images
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_GetAllImages
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_GetAllImages - retrieving all image records'
    
    SELECT 
        Code,
        ImageData,
        AzureStoragePath,
        CreatedDate,
        ModifiedDate
    FROM PHOTOS.Photos
    ORDER BY CreatedDate DESC
    
    -- Print result summary as requested
    PRINT 'Retrieved ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' image records'
END

-- =============================================
-- Stored procedure to get image by code
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_GetImageByCode
    @Code nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_GetImageByCode for Code: ' + @Code
    
    SELECT 
        Code,
        ImageData,
        AzureStoragePath,
        CreatedDate,
        ModifiedDate
    FROM PHOTOS.Photos
    WHERE Code = @Code
    
    -- Print result summary as requested
    IF @@ROWCOUNT > 0
        PRINT 'Found image record for Code: ' + @Code
    ELSE
        PRINT 'No image record found for Code: ' + @Code
END

-- =============================================
-- Stored procedure to get image count
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_GetImageCount
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_GetImageCount'
    
    DECLARE @Count int
    SELECT @Count = COUNT(*) FROM PHOTOS.Photos
    
    -- Print result summary as requested
    PRINT 'Total image count: ' + CAST(@Count AS nvarchar(10))
    
    SELECT @Count AS ImageCount
END

-- =============================================
-- Stored procedure to delete an image by code
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_DeleteImage
    @Code nvarchar(100),
    @Result int OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_DeleteImage for Code: ' + @Code
    
    -- Initialize result
    SET @Result = 0
    
    BEGIN TRY
        BEGIN TRANSACTION
        
        -- Delete the record
        DELETE FROM PHOTOS.Photos WHERE Code = @Code
        
        SET @Result = @@ROWCOUNT
        
        -- Print result summary as requested
        IF @Result > 0
            PRINT 'Successfully deleted image record for Code: ' + @Code
        ELSE
            PRINT 'No image record found to delete for Code: ' + @Code
        
        COMMIT TRANSACTION
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION
        
        -- Log error details as requested
        PRINT 'Error in sp_DeleteImage: ' + ERROR_MESSAGE()
        PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS nvarchar(10))
        PRINT 'Error Severity: ' + CAST(ERROR_SEVERITY() AS nvarchar(10))
        PRINT 'Error State: ' + CAST(ERROR_STATE() AS nvarchar(10))
        
        SET @Result = 0
        
        -- Re-throw the error
        THROW
    END CATCH
END

-- =============================================
-- Stored procedure to get images with NULL Azure Storage Path
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_GetImagesWithNullAzurePath
AS
BEGIN
    SET NOCOUNT ON
    
    PRINT 'Executing sp_GetImagesWithNullAzurePath'
    
    SELECT 
        Code,
        ImageData,
        AzureStoragePath,
        CreatedDate,
        ModifiedDate
    FROM PHOTOS.Photos
    WHERE AzureStoragePath IS NULL AND ImageData IS NOT NULL
    ORDER BY CreatedDate DESC
    
    PRINT 'Retrieved ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' images with NULL Azure path'
END

-- =============================================
-- Stored procedure to get images with NULL photo data
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_GetImagesWithNullPhotoData
AS
BEGIN
    SET NOCOUNT ON
    
    PRINT 'Executing sp_GetImagesWithNullPhotoData'
    
    SELECT 
        Code,
        ImageData,
        AzureStoragePath,
        CreatedDate,
        ModifiedDate
    FROM PHOTOS.Photos
    WHERE ImageData IS NULL AND AzureStoragePath IS NOT NULL
    ORDER BY CreatedDate DESC
    
    PRINT 'Retrieved ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' images with NULL photo data'
END

-- =============================================
-- Stored procedure to update Azure Storage Path
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_UpdateAzureStoragePath
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
            ModifiedDate = GETUTCDATE()
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

-- =============================================
-- Stored procedure to update image data only
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_UpdateImageData
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
            ModifiedDate = GETUTCDATE()
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

-- =============================================
-- Stored procedure to nullify a specific field
-- =============================================
CREATE OR ALTER PROCEDURE PHOTOS.sp_NullifyField
    @FieldName nvarchar(50),
    @Result int OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    PRINT 'Executing sp_NullifyField for field: ' + @FieldName
    
    SET @Result = 0
    
    BEGIN TRY
        IF @FieldName = 'ImageData'
        BEGIN
            UPDATE PHOTOS.Photos 
            SET ImageData = NULL,
                ModifiedDate = GETUTCDATE()
            WHERE ImageData IS NOT NULL
            
            SET @Result = @@ROWCOUNT
            PRINT 'Nullified ImageData field for ' + CAST(@Result AS nvarchar(10)) + ' records'
        END
        ELSE IF @FieldName = 'AzureStoragePath'
        BEGIN
            UPDATE PHOTOS.Photos 
            SET AzureStoragePath = NULL,
                ModifiedDate = GETUTCDATE()
            WHERE AzureStoragePath IS NOT NULL
            
            SET @Result = @@ROWCOUNT
            PRINT 'Nullified AzureStoragePath field for ' + CAST(@Result AS nvarchar(10)) + ' records'
        END
        ELSE
        BEGIN
            PRINT 'Invalid field name: ' + @FieldName
            SET @Result = -1
        END
    END TRY
    BEGIN CATCH
        PRINT 'Error in sp_NullifyField: ' + ERROR_MESSAGE()
        SET @Result = 0
        THROW
    END CATCH
END

-- =============================================
-- Grant permissions (adjust as needed for your environment)
-- =============================================
-- GRANT EXECUTE ON PHOTOS.sp_SaveImage TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_GetAllImages TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_GetImageByCode TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_GetImageCount TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_DeleteImage TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_GetImagesWithNullAzurePath TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_GetImagesWithNullPhotoData TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_UpdateAzureStoragePath TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_UpdateImageData TO [YourApplicationUser]
-- GRANT EXECUTE ON PHOTOS.sp_NullifyField TO [YourApplicationUser]

PRINT 'All stored procedures created successfully!'
PRINT 'Database setup complete.'
