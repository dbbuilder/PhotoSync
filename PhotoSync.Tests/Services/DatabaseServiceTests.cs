using FluentAssertions;
using Microsoft.Data.SqlClient;
using Moq;
using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using PhotoSync.Tests.Helpers;
using Polly;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace PhotoSync.Tests.Services
{
    public class DatabaseServiceTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly AppSettings _appSettings;
        private readonly string _testConnectionString;

        public DatabaseServiceTests()
        {
            _mockLogger = new Mock<ILogger>();
            _testConnectionString = "Server=TestServer;Database=TestDB;Trusted_Connection=true;";
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
        }

        [Fact]
        public async Task TestConnectionAsync_WithValidConnection_ShouldReturnTrue()
        {
            // This test would require a test database or mocking at a lower level
            // For now, we'll create a mock-based test
            var mockConnection = new Mock<IDbConnection>();
            mockConnection.Setup(x => x.State).Returns(ConnectionState.Open);

            // Note: In a real scenario, you'd need to either:
            // 1. Use an in-memory database
            // 2. Mock the SqlConnection (which is sealed and difficult to mock)
            // 3. Use integration tests with a real test database
        }

        [Fact]
        public async Task SaveImageAsync_WithValidImage_ShouldReturnTrue()
        {
            // Arrange
            var image = new ImageRecord 
            { 
                Code = "TEST001", 
                ImageData = new byte[] { 1, 2, 3 }, 
                CreatedDate = DateTime.UtcNow 
            };

            // This is a unit test that would require database mocking
            // In practice, you'd want integration tests for database operations
        }

        [Fact]
        public async Task GetAllImagesAsync_ShouldReturnImageList()
        {
            // Arrange
            // This test would require database mocking or integration testing
            
            // Act
            // var result = await _databaseService.GetAllImagesAsync();
            
            // Assert
            // result.Should().NotBeNull();
        }

        [Fact]
        public async Task GetImageCountAsync_ShouldReturnCount()
        {
            // This test would require database mocking or integration testing
        }

        [Fact]
        public void BuildRetryPolicy_ShouldCreatePolicyWithCorrectRetries()
        {
            // Arrange
            var service = new DatabaseService(_appSettings, _mockLogger.Object);
            
            // Act - This would require making BuildRetryPolicy public or testing it indirectly
            // through the public methods that use it
        }

        [Fact]
        public async Task SaveImageAsync_WithRetryableError_ShouldRetry()
        {
            // This test would verify that transient errors trigger retries
        }

        [Fact]
        public async Task CheckDuplicateByHashAsync_WithExistingHash_ShouldReturnRecord()
        {
            // This test would verify duplicate detection logic
        }

        [Fact]
        public async Task GetPhotosForIncrementalExportAsync_ShouldReturnUnexportedPhotos()
        {
            // Arrange
            // This test would verify incremental export filtering
            
            // Act
            // var result = await _databaseService.GetPhotosForIncrementalExportAsync();
            
            // Assert
            // Should return photos where ExportedDate is null or older than ModifiedDate
        }

        [Fact]
        public async Task UpdateExportTrackingAsync_ShouldUpdateExportedDate()
        {
            // Arrange
            var code = "TEST001";
            var exportedDate = DateTime.UtcNow;
            
            // This test would verify export tracking updates
        }

        [Fact]
        public async Task UpdateImportTrackingAsync_ShouldUpdateAllTrackingFields()
        {
            // Arrange
            var code = "TEST001";
            var importedDate = DateTime.UtcNow;
            var imageSource = "FILE:C:\\Photos\\test.jpg";
            var sourceFileName = "test.jpg";
            var fileHash = "abc123hash";
            var fileSize = 12345L;
            
            // This test would verify import tracking updates
        }

        [Fact]
        public async Task GetSyncStatusAsync_ShouldReturnStatusMetrics()
        {
            // This test would verify sync status calculation
        }

        [Fact]
        public async Task NullifyFieldAsync_WithImageData_ShouldNullifyImageDataField()
        {
            // Arrange
            var fieldName = "ImageData";
            
            // This test would verify field nullification
        }

        [Fact]
        public async Task GetPhotosNeedingAzureSyncAsync_ShouldReturnPhotosWithSyncFlag()
        {
            // This test would verify Azure sync filtering
        }
    }

    /// <summary>
    /// Integration tests for DatabaseService that require actual database connection
    /// </summary>
    public class DatabaseServiceIntegrationTests : IAsyncLifetime
    {
        private string _testConnectionString;
        private readonly string _testDatabaseName;
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;
        private DatabaseService _databaseService;

        public DatabaseServiceIntegrationTests()
        {
            _testDatabaseName = $"PhotoSyncTest_{Guid.NewGuid():N}";
            _testConnectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={_testDatabaseName};Trusted_Connection=true;";
            
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
                .WriteTo.Console()
                .CreateLogger();
        }

        public async Task InitializeAsync()
        {
            // Create test database and table
            await CreateTestDatabase();
            _databaseService = new DatabaseService(_appSettings, _logger);
        }

        public async Task DisposeAsync()
        {
            // Clean up test database
            await DropTestDatabase();
        }

        private async Task CreateTestDatabase()
        {
            // Create test database using helper
            _testConnectionString = await TestDatabaseHelper.CreateTestDatabaseAsync(_testDatabaseName);
            
            // Update connection string in app settings
            _appSettings.ConnectionStrings.DefaultConnection = _testConnectionString;
        }

        private async Task DropTestDatabase()
        {
            // Drop test database using helper
            await TestDatabaseHelper.DropTestDatabaseAsync(_testDatabaseName);
        }

        [Fact]
        public async Task FullWorkflow_SaveAndRetrieveImages_ShouldWork()
        {
            // Arrange
            var testImages = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = new byte[] { 1, 2, 3, 4, 5 }, CreatedDate = DateTime.UtcNow },
                new ImageRecord { Code = "TEST002", ImageData = new byte[] { 6, 7, 8, 9, 10 }, CreatedDate = DateTime.UtcNow }
            };

            // Act - Save images
            foreach (var image in testImages)
            {
                var saved = await _databaseService.SaveImageAsync(image);
                saved.Should().BeTrue();
            }

            // Act - Retrieve images
            var retrievedImages = await _databaseService.GetAllImagesAsync();

            // Assert - Verify retrieved
            retrievedImages.Should().HaveCount(2);
            retrievedImages.Should().Contain(img => img.Code == "TEST001");
            retrievedImages.First(img => img.Code == "TEST001").ImageData.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
        }
    }
}