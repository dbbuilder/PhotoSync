-- =============================================
-- PhotoSync Database Setup Script
-- =============================================

-- Create database table for storing images
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Photos' AND xtype='U')
BEGIN
    CREATE TABLE Photos (
        Id int IDENTITY(1,1) PRIMARY KEY,
        Code nvarchar(100) NOT NULL,
        ImageData varbinary(max) NOT NULL,
        CreatedDate datetime2 NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDate datetime2 NULL
    )

    -- Create unique constraint on Code field
    ALTER TABLE Photos ADD CONSTRAINT UK_Photos_Code UNIQUE (Code)

    -- Index for faster lookups by code
    CREATE NONCLUSTERED INDEX IX_Photos_Code ON Photos (Code)

    PRINT 'Photos table created successfully'
END
ELSE
BEGIN
    PRINT 'Photos table already exists'
END

-- =============================================
-- Stored procedure to save (insert or update) an image
-- =============================================
CREATE OR ALTER PROCEDURE sp_SaveImage
    @Code nvarchar(100),
    @ImageData varbinary(max),
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
        IF EXISTS (SELECT 1 FROM Photos WHERE Code = @Code)
        BEGIN
            -- Update existing record
            UPDATE Photos 
            SET ImageData = @ImageData,
                ModifiedDate = GETUTCDATE()
            WHERE Code = @Code
            
            SET @Result = @@ROWCOUNT
            PRINT 'Updated existing image record for Code: ' + @Code
        END
        ELSE
        BEGIN
            -- Insert new record
            INSERT INTO Photos (Code, ImageData, CreatedDate)
            VALUES (@Code, @ImageData, @CreatedDate)
            
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
CREATE OR ALTER PROCEDURE sp_GetAllImages
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_GetAllImages - retrieving all image records'
    
    SELECT 
        Code,
        ImageData,
        CreatedDate,
        ModifiedDate
    FROM Photos
    ORDER BY CreatedDate DESC
    
    -- Print result summary as requested
    PRINT 'Retrieved ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' image records'
END

-- =============================================
-- Stored procedure to get image by code
-- =============================================
CREATE OR ALTER PROCEDURE sp_GetImageByCode
    @Code nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_GetImageByCode for Code: ' + @Code
    
    SELECT 
        Code,
        ImageData,
        CreatedDate,
        ModifiedDate
    FROM Photos
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
CREATE OR ALTER PROCEDURE sp_GetImageCount
AS
BEGIN
    SET NOCOUNT ON
    
    -- Print debug information as requested
    PRINT 'Executing sp_GetImageCount'
    
    DECLARE @Count int
    SELECT @Count = COUNT(*) FROM Photos
    
    -- Print result summary as requested
    PRINT 'Total image count: ' + CAST(@Count AS nvarchar(10))
    
    SELECT @Count AS ImageCount
END

-- =============================================
-- Stored procedure to delete an image by code
-- =============================================
CREATE OR ALTER PROCEDURE sp_DeleteImage
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
        DELETE FROM Photos WHERE Code = @Code
        
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
-- Grant permissions (adjust as needed for your environment)
-- =============================================
-- GRANT EXECUTE ON sp_SaveImage TO [YourApplicationUser]
-- GRANT EXECUTE ON sp_GetAllImages TO [YourApplicationUser]
-- GRANT EXECUTE ON sp_GetImageByCode TO [YourApplicationUser]
-- GRANT EXECUTE ON sp_GetImageCount TO [YourApplicationUser]
-- GRANT EXECUTE ON sp_DeleteImage TO [YourApplicationUser]

PRINT 'All stored procedures created successfully!'
PRINT 'Database setup complete.'
