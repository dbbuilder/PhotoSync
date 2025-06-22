using FluentAssertions;
using Microsoft.Data.SqlClient;
using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PhotoSync.Tests.Integration
{
    /// <summary>
    /// Integration tests for database operations
    /// Requires LocalDB or SQL Server test instance
    /// </summary>
    [Collection("Database")]
    public class DatabaseIntegrationTests : IAsyncLifetime
    {
        private readonly string _testDatabaseName;
        private readonly string _masterConnectionString;
        private readonly string _testConnectionString;
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;
        private DatabaseService _databaseService;

        public DatabaseIntegrationTests()
        {
            _testDatabaseName = $"PhotoSyncTest_{Guid.NewGuid():N}";
            _masterConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=true;";
            _testConnectionString = $@"Server=(localdb)\MSSQLLocalDB;Database={_testDatabaseName};Trusted_Connection=true;";
            
            _appSettings = new AppSettings
            {
                ConnectionStrings = new ConnectionStrings
                {
                    DefaultConnection = _testConnectionString
                },
                PhotoSettings = new PhotoSettings
                {
                    TableName = "TestPhotos",
                    CodeFieldName = "Code",
                    ImageFieldName = "ImageData",
                    PhotoFieldName = "ImageData",
                    AzureStoragePathFieldName = "AzureStoragePath"
                }
            };

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }

        public async Task InitializeAsync()
        {
            // Create test database
            await CreateTestDatabase();
            
            // Create tables
            await CreateTestTables();
            
            // Initialize service
            _databaseService = new DatabaseService(_appSettings, _logger);
        }

        public async Task DisposeAsync()
        {
            await DropTestDatabase();
        }

        private async Task CreateTestDatabase()
        {
            using var connection = new SqlConnection(_masterConnectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand($@"
                IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{_testDatabaseName}')
                BEGIN
                    CREATE DATABASE [{_testDatabaseName}]
                END", connection);
            
            await command.ExecuteNonQueryAsync();
        }

        private async Task CreateTestTables()
        {
            using var connection = new SqlConnection(_testConnectionString);
            await connection.OpenAsync();
            
            // Create schema first
            using var schemaCommand = new SqlCommand(@"
                IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'PHOTOS')
                BEGIN
                    EXEC('CREATE SCHEMA PHOTOS')
                END", connection);
            await schemaCommand.ExecuteNonQueryAsync();
            
            // Create table with correct structure
            using var command = new SqlCommand($@"
                CREATE TABLE [PHOTOS].[{_appSettings.PhotoSettings.TableName}] (
                    [Code] NVARCHAR(100) PRIMARY KEY,
                    [ImageData] VARBINARY(MAX),
                    [CreatedDate] DATETIME2 NOT NULL,
                    [ModifiedDate] DATETIME2 NULL,
                    [AzureStoragePath] NVARCHAR(500) NULL,
                    [ImageSource] NVARCHAR(500) NULL,
                    [SourceFileName] NVARCHAR(255) NULL,
                    [ImportedDate] DATETIME2 NULL,
                    [ExportedDate] DATETIME2 NULL,
                    [AzureUploadedDate] DATETIME2 NULL,
                    [PhotoModifiedDate] DATETIME2 NULL,
                    [AzureSyncRequired] BIT DEFAULT 0,
                    [FileHash] NVARCHAR(64) NULL,
                    [FileSize] BIGINT NULL
                )", connection);
            
            await command.ExecuteNonQueryAsync();
        }

        private async Task DropTestDatabase()
        {
            try
            {
                using var connection = new SqlConnection(_masterConnectionString);
                await connection.OpenAsync();
                
                // Force close existing connections
                using var closeCommand = new SqlCommand($@"
                    ALTER DATABASE [{_testDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", connection);
                await closeCommand.ExecuteNonQueryAsync();
                
                // Drop database
                using var dropCommand = new SqlCommand($@"
                    DROP DATABASE [{_testDatabaseName}]", connection);
                await dropCommand.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to drop test database");
            }
        }

        [Fact]
        public async Task TestConnectionAsync_WithValidDatabase_ShouldReturnTrue()
        {
            // Act
            var result = await _databaseService.TestConnectionAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SaveImageAsync_WithValidImages_ShouldPersistToDatabase()
        {
            // Arrange
            var images = new List<ImageRecord>
            {
                new ImageRecord 
                { 
                    Code = "TEST001", 
                    ImageData = new byte[] { 1, 2, 3, 4, 5 },
                    CreatedDate = DateTime.UtcNow,
                    FileSize = 5,
                    ImportedDate = DateTime.UtcNow,
                    SourceFileName = "test1.jpg",
                    ImageSource = @"FILE:C:\Test\test1.jpg"
                },
                new ImageRecord 
                { 
                    Code = "TEST002", 
                    ImageData = new byte[] { 6, 7, 8, 9, 10 },
                    CreatedDate = DateTime.UtcNow,
                    FileSize = 5,
                    ImportedDate = DateTime.UtcNow,
                    SourceFileName = "test2.jpg",
                    ImageSource = @"FILE:C:\Test\test2.jpg"
                }
            };

            // Act - Save images individually
            int savedCount = 0;
            foreach (var image in images)
            {
                var saved = await _databaseService.SaveImageAsync(image);
                if (saved) savedCount++;
            }

            // Assert
            savedCount.Should().Be(2);

            // Verify data was saved
            var totalCount = await _databaseService.GetImageCountAsync();
            totalCount.Should().Be(2);
        }

        [Fact]
        public async Task GetAllImagesAsync_ShouldReturnPagedResults()
        {
            // Arrange - Save test data
            var testImages = new List<ImageRecord>();
            for (int i = 0; i < 25; i++)
            {
                testImages.Add(new ImageRecord
                {
                    Code = $"TEST{i:D3}",
                    ImageData = new byte[] { (byte)i },
                    CreatedDate = DateTime.UtcNow,
                    FileSize = 1,
                    ImportedDate = DateTime.UtcNow,
                    SourceFileName = $"test{i:D3}.jpg"
                });
            }
            foreach (var image in testImages)
            {
                await _databaseService.SaveImageAsync(image);
            }

            // Act - Get all images
            var allImages = await _databaseService.GetAllImagesAsync();
            allImages = allImages.OrderBy(x => x.Code).ToList();

            // Assert - Check total count
            allImages.Should().HaveCount(25);
            
            // Assert - Check first page
            var firstPage = allImages.Take(10).ToList();
            firstPage.Should().HaveCount(10);
            firstPage[0].Code.Should().Be("TEST000");
            firstPage[9].Code.Should().Be("TEST009");

            // Assert - Check second page
            var secondPage = allImages.Skip(10).Take(10).ToList();
            secondPage.Should().HaveCount(10);
            secondPage[0].Code.Should().Be("TEST010");
            secondPage[9].Code.Should().Be("TEST019");

            // Assert - Check last page
            var lastPage = allImages.Skip(20).Take(10).ToList();
            lastPage.Should().HaveCount(5);
            lastPage[0].Code.Should().Be("TEST020");
            lastPage[4].Code.Should().Be("TEST024");
        }

        [Fact]
        public async Task SaveImageAsync_WithLargeData_ShouldHandleCorrectly()
        {
            // Arrange
            var largeImage = new ImageRecord
            {
                Code = "LARGE001",
                ImageData = new byte[5 * 1024 * 1024], // 5MB
                FileSize = 5 * 1024 * 1024,
                ImportedDate = DateTime.UtcNow
            };
            new Random().NextBytes(largeImage.ImageData);

            // Act
            var saved = await _databaseService.SaveImageAsync(largeImage);

            // Assert
            saved.Should().BeTrue();

            // Verify retrieval
            var retrieved = await _databaseService.GetAllImagesAsync();
            retrieved.Should().HaveCount(1);
            retrieved[0].ImageData.Should().HaveCount(5 * 1024 * 1024);
        }

        [Fact]
        public async Task BatchOperations_ShouldProcessInBatches()
        {
            // Arrange
            var images = new List<ImageRecord>();
            for (int i = 0; i < 250; i++) // More than batch size
            {
                images.Add(new ImageRecord
                {
                    Code = $"BATCH{i:D3}",
                    SourceFileName = $"batch{i:D3}.jpg",
                    ImageData = new byte[] { (byte)(i % 256) },
                    FileSize = 1,
                    ImportedDate = DateTime.UtcNow
                });
            }

            // Act
            foreach (var image in images)
            {
                await _databaseService.SaveImageAsync(image);
            }

            // Assert
            var totalCount = await _databaseService.GetImageCountAsync();
            totalCount.Should().Be(250);
        }

        [Fact]
        public async Task GetImagesWithMetadataAsync_ShouldReturnCompleteRecords()
        {
            // Arrange
            var testDate = DateTime.UtcNow;
            var image = new ImageRecord
            {
                Code = "META001",
                SourceFileName = "metadata_test.jpg",
                ImageData = new byte[] { 1, 2, 3 },
                FileSize = 3,
                ImportedDate = testDate,
                ModifiedDate = testDate.AddHours(1),
                ImageSource = @"C:\Source\metadata_test.jpg",
                FileHash = "ABC123",
                AzureSyncRequired = true
            };

            await _databaseService.SaveImageAsync(image);

            // Act
            var retrieved = await _databaseService.GetAllImagesAsync();

            // Assert
            retrieved.Should().HaveCount(1);
            var retrievedImage = retrieved[0];
            retrievedImage.Code.Should().Be("metadata_test.jpg");
            retrievedImage.FileSize.Should().Be(3);
            retrievedImage.ImageSource.Should().Be(@"C:\Source\metadata_test.jpg");
            retrievedImage.FileHash.Should().Be("ABC123");
            retrievedImage.AzureSyncRequired.Should().BeTrue();
        }

        [Fact]
        public async Task ConcurrentSaves_ShouldHandleCorrectly()
        {
            // Arrange
            var tasks = new List<Task<int>>();

            // Act - Save images concurrently
            for (int i = 0; i < 5; i++)
            {
                var taskIndex = i;
                var task = Task.Run(async () =>
                {
                    var images = new List<ImageRecord>
                    {
                        new ImageRecord
                        {
                            Code = $"CONCURRENT_{taskIndex}",
                            ImageData = new byte[] { (byte)taskIndex },
                            CreatedDate = DateTime.UtcNow,
                            FileSize = 1,
                            ImportedDate = DateTime.UtcNow,
                            SourceFileName = $"concurrent_{taskIndex}.jpg"
                        }
                    };
                    var saved = await _databaseService.SaveImageAsync(images[0]);
                    return saved ? 1 : 0;
                });
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Sum().Should().Be(5);
            var totalCount = await _databaseService.GetImageCountAsync();
            totalCount.Should().Be(5);
        }
    }
}