-- Test Database Creation Script for PhotoSync
-- This script creates the test database schema and stored procedures

-- Create PHOTOS schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'PHOTOS')
BEGIN
    EXEC('CREATE SCHEMA PHOTOS')
END
GO

-- Create Photos table with all tracking fields
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PHOTOS].[Photos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [PHOTOS].[Photos](
        [Code] [nvarchar](50) NOT NULL,
        [ImageData] [varbinary](max) NULL,
        [CreatedDate] [datetime2](7) NOT NULL,
        [ModifiedDate] [datetime2](7) NULL,
        [AzureStoragePath] [nvarchar](500) NULL,
        [ImageSource] [nvarchar](500) NULL,
        [SourceFileName] [nvarchar](255) NULL,
        [ImportedDate] [datetime2](7) NULL,
        [ExportedDate] [datetime2](7) NULL,
        [AzureUploadedDate] [datetime2](7) NULL,
        [PhotoModifiedDate] [datetime2](7) NULL,
        [AzureSyncRequired] [bit] NOT NULL DEFAULT 0,
        [FileHash] [nvarchar](100) NULL,
        [FileSize] [bigint] NULL,
        CONSTRAINT [PK_PHOTOS_Photos] PRIMARY KEY CLUSTERED ([Code] ASC)
    )
END
GO

-- Create index on FileHash for duplicate detection
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PHOTOS_Photos_FileHash')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PHOTOS_Photos_FileHash] ON [PHOTOS].[Photos]
    (
        [FileHash] ASC
    ) WHERE [FileHash] IS NOT NULL
END
GO

-- Create index on tracking fields for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PHOTOS_Photos_Tracking')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PHOTOS_Photos_Tracking] ON [PHOTOS].[Photos]
    (
        [ExportedDate] ASC,
        [AzureSyncRequired] ASC,
        [ImportedDate] ASC
    )
END
GO

-- Create InsertOrUpdatePhoto stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[InsertOrUpdatePhoto]
    @Code nvarchar(50),
    @ImageData varbinary(max) = NULL,
    @CreatedDate datetime2(7),
    @ModifiedDate datetime2(7) = NULL,
    @AzureStoragePath nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    IF EXISTS (SELECT 1 FROM [PHOTOS].[Photos] WHERE [Code] = @Code)
    BEGIN
        UPDATE [PHOTOS].[Photos]
        SET [ImageData] = @ImageData,
            [ModifiedDate] = @ModifiedDate,
            [AzureStoragePath] = @AzureStoragePath
        WHERE [Code] = @Code;
    END
    ELSE
    BEGIN
        INSERT INTO [PHOTOS].[Photos] ([Code], [ImageData], [CreatedDate], [ModifiedDate], [AzureStoragePath])
        VALUES (@Code, @ImageData, @CreatedDate, @ModifiedDate, @AzureStoragePath);
    END
END
GO

-- Create GetAllPhotos stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[GetAllPhotos]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT [Code], [ImageData], [CreatedDate], [ModifiedDate], [AzureStoragePath],
           [ImageSource], [SourceFileName], [ImportedDate], [ExportedDate],
           [AzureUploadedDate], [PhotoModifiedDate], [AzureSyncRequired],
           [FileHash], [FileSize]
    FROM [PHOTOS].[Photos]
    ORDER BY [CreatedDate] DESC;
END
GO

-- Create GetPhotoByCode stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[GetPhotoByCode]
    @Code nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT [Code], [ImageData], [CreatedDate], [ModifiedDate], [AzureStoragePath],
           [ImageSource], [SourceFileName], [ImportedDate], [ExportedDate],
           [AzureUploadedDate], [PhotoModifiedDate], [AzureSyncRequired],
           [FileHash], [FileSize]
    FROM [PHOTOS].[Photos]
    WHERE [Code] = @Code;
END
GO

-- Create DeletePhoto stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[DeletePhoto]
    @Code nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM [PHOTOS].[Photos]
    WHERE [Code] = @Code;
    
    RETURN @@ROWCOUNT;
END
GO

-- Create GetPhotoCount stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[GetPhotoCount]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT COUNT(*) AS PhotoCount
    FROM [PHOTOS].[Photos];
END
GO

-- Create GetPhotosWithNullAzurePath stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[GetPhotosWithNullAzurePath]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT [Code], [ImageData], [CreatedDate], [ModifiedDate], [AzureStoragePath],
           [ImageSource], [SourceFileName], [ImportedDate], [ExportedDate],
           [AzureUploadedDate], [PhotoModifiedDate], [AzureSyncRequired],
           [FileHash], [FileSize]
    FROM [PHOTOS].[Photos]
    WHERE [AzureStoragePath] IS NULL
      AND [ImageData] IS NOT NULL;
END
GO

-- Create GetPhotosWithNullImageData stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[GetPhotosWithNullImageData]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT [Code], [ImageData], [CreatedDate], [ModifiedDate], [AzureStoragePath],
           [ImageSource], [SourceFileName], [ImportedDate], [ExportedDate],
           [AzureUploadedDate], [PhotoModifiedDate], [AzureSyncRequired],
           [FileHash], [FileSize]
    FROM [PHOTOS].[Photos]
    WHERE [ImageData] IS NULL
      AND [AzureStoragePath] IS NOT NULL;
END
GO

-- Create UpdateAzureStoragePath stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[UpdateAzureStoragePath]
    @Code nvarchar(50),
    @AzureStoragePath nvarchar(500),
    @AzureUploadedDate datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE [PHOTOS].[Photos]
    SET [AzureStoragePath] = @AzureStoragePath,
        [AzureUploadedDate] = @AzureUploadedDate,
        [AzureSyncRequired] = 0,
        [ModifiedDate] = SYSDATETIME()
    WHERE [Code] = @Code;
    
    RETURN @@ROWCOUNT;
END
GO

-- Create NullifyField stored procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[NullifyField]
    @FieldName nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @FieldName = 'ImageData'
    BEGIN
        UPDATE [PHOTOS].[Photos]
        SET [ImageData] = NULL,
            [ModifiedDate] = SYSDATETIME()
        WHERE [ImageData] IS NOT NULL;
        
        RETURN @@ROWCOUNT;
    END
    ELSE IF @FieldName = 'AzureStoragePath'
    BEGIN
        UPDATE [PHOTOS].[Photos]
        SET [AzureStoragePath] = NULL,
            [AzureUploadedDate] = NULL,
            [AzureSyncRequired] = 1,
            [ModifiedDate] = SYSDATETIME()
        WHERE [AzureStoragePath] IS NOT NULL;
        
        RETURN @@ROWCOUNT;
    END
    
    RETURN 0;
END
GO

-- Create test data insertion procedure
CREATE OR ALTER PROCEDURE [PHOTOS].[InsertTestData]
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Insert test records with various states
    INSERT INTO [PHOTOS].[Photos] 
        ([Code], [ImageData], [CreatedDate], [ModifiedDate], [AzureStoragePath], 
         [ImageSource], [SourceFileName], [ImportedDate], [ExportedDate],
         [AzureUploadedDate], [PhotoModifiedDate], [AzureSyncRequired],
         [FileHash], [FileSize])
    VALUES
        -- Record with both local and Azure data
        ('TEST001', 0x0102030405, DATEADD(DAY, -7, SYSDATETIME()), NULL, 
         'https://storage.blob.core.windows.net/photos/test001.jpg',
         'FILE:C:\TestPhotos\test001.jpg', 'test001.jpg', DATEADD(DAY, -7, SYSDATETIME()),
         DATEADD(DAY, -6, SYSDATETIME()), DATEADD(DAY, -5, SYSDATETIME()), NULL, 0,
         'hash001', 12345),
         
        -- Record with only local data
        ('TEST002', 0x0607080910, DATEADD(DAY, -5, SYSDATETIME()), NULL, NULL,
         'FILE:C:\TestPhotos\test002.jpg', 'test002.jpg', DATEADD(DAY, -5, SYSDATETIME()),
         NULL, NULL, NULL, 0, 'hash002', 23456),
         
        -- Record with only Azure data
        ('TEST003', NULL, DATEADD(DAY, -3, SYSDATETIME()), NULL,
         'https://storage.blob.core.windows.net/photos/test003.jpg',
         'FILE:C:\TestPhotos\test003.jpg', 'test003.jpg', DATEADD(DAY, -3, SYSDATETIME()),
         DATEADD(DAY, -2, SYSDATETIME()), DATEADD(DAY, -2, SYSDATETIME()), NULL, 0,
         'hash003', 34567),
         
        -- Record needing export
        ('TEST004', 0x1112131415, DATEADD(DAY, -1, SYSDATETIME()), SYSDATETIME(), NULL,
         'FILE:C:\TestPhotos\test004.jpg', 'test004.jpg', DATEADD(DAY, -1, SYSDATETIME()),
         DATEADD(DAY, -2, SYSDATETIME()), NULL, SYSDATETIME(), 1,
         'hash004', 45678),
         
        -- Record needing Azure sync
        ('TEST005', 0x1617181920, SYSDATETIME(), NULL, NULL,
         'FILE:C:\TestPhotos\test005.jpg', 'test005.jpg', SYSDATETIME(),
         NULL, NULL, NULL, 1, 'hash005', 56789);
END
GO

-- Create cleanup procedure for tests
CREATE OR ALTER PROCEDURE [PHOTOS].[CleanupTestData]
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM [PHOTOS].[Photos]
    WHERE [Code] LIKE 'TEST%';
END
GO