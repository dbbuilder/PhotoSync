using FluentAssertions;
using Moq;
using PhotoSync.Commands;
using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PhotoSync.Tests.Commands
{
    public class ToAzureStorageCommandTests
    {
        private readonly Mock<IAzureStorageService> _mockAzureService;
        private readonly Mock<IDatabaseService> _mockDatabaseService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly PhotoSettings _photoSettings;
        private readonly ToAzureStorageCommand _toAzureCommand;

        public ToAzureStorageCommandTests()
        {
            _mockAzureService = new Mock<IAzureStorageService>();
            _mockDatabaseService = new Mock<IDatabaseService>();
            _mockLogger = new Mock<ILogger>();
            
            _photoSettings = new PhotoSettings
            {
                ImportFolder = @"C:\TestSource",
                ExportFolder = @"C:\TestExport"
            };

            _toAzureCommand = new ToAzureStorageCommand(
                _mockDatabaseService.Object,
                _mockAzureService.Object,
                _photoSettings,
                _mockLogger.Object
            );
            
            // Setup common mocks
            SetupCommonMocks();
        }
        
        private void SetupCommonMocks()
        {
            // Database connection test always succeeds by default
            _mockDatabaseService.Setup(x => x.TestConnectionAsync())
                .ReturnsAsync(true);
                
            // Azure connection test succeeds by default
            _mockAzureService.Setup(x => x.TestConnectionAsync())
                .ReturnsAsync(true);
        }

        [Fact]
        public async Task ExecuteAsync_WithValidFiles_ShouldUploadToAzure()
        {
            // Arrange
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = new byte[] { 1, 2, 3 }, AzureStoragePath = null },
                new ImageRecord { Code = "TEST002", ImageData = new byte[] { 4, 5, 6 }, AzureStoragePath = null },
                new ImageRecord { Code = "TEST003", ImageData = new byte[] { 7, 8, 9 }, AzureStoragePath = null }
            };

            _mockDatabaseService.Setup(x => x.GetImagesWithNullAzurePathAsync())
                .ReturnsAsync(imageRecords);

            _mockAzureService.Setup(x => x.UploadImageAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .ReturnsAsync((string code, byte[] data) => $"https://storage.blob.core.windows.net/photos/{code}.jpg");

            _mockDatabaseService.Setup(x => x.UpdateAzureStoragePathAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _toAzureCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(3);
            result.ErrorMessage.Should().BeNull();

            _mockAzureService.Verify(x => x.UploadImageAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Exactly(3));
            _mockDatabaseService.Verify(x => x.UpdateAzureStoragePathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteAsync_WithNoData_ShouldReturnEmptyResult()
        {
            // Arrange
            _mockDatabaseService.Setup(x => x.GetImagesWithNullAzurePathAsync())
                .ReturnsAsync(new List<ImageRecord>());

            // Act
            var result = await _toAzureCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(0);
            result.TotalRecordsFound.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_WithUploadError_ShouldHandleGracefully()
        {
            // Arrange
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = new byte[] { 1, 2, 3 }, AzureStoragePath = null }
            };

            _mockDatabaseService.Setup(x => x.GetImagesWithNullAzurePathAsync())
                .ReturnsAsync(imageRecords);

            _mockAzureService.Setup(x => x.UploadImageAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Azure connection failed"));

            // Act
            var result = await _toAzureCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue(); // Individual failures don't fail the whole operation
            result.SuccessCount.Should().Be(0);
            result.FailureCount.Should().Be(1);
        }

        [Fact]
        public async Task ExecuteAsync_WithForceFlag_ShouldProcessAzureSyncRequired()
        {
            // Arrange
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = new byte[] { 1, 2, 3 }, AzureSyncRequired = true }
            };

            _mockDatabaseService.Setup(x => x.GetPhotosNeedingAzureSyncAsync())
                .ReturnsAsync(imageRecords);

            _mockAzureService.Setup(x => x.UploadImageAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .ReturnsAsync((string code, byte[] data) => $"https://storage.blob.core.windows.net/photos/{code}.jpg");

            _mockDatabaseService.Setup(x => x.UpdateAzureStoragePathAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _toAzureCommand.ExecuteAsync(force: true);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(1);
        }
    }

    public class FromAzureStorageCommandTests
    {
        private readonly Mock<IAzureStorageService> _mockAzureService;
        private readonly Mock<IDatabaseService> _mockDatabaseService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly PhotoSettings _photoSettings;
        private readonly FromAzureStorageCommand _fromAzureCommand;

        public FromAzureStorageCommandTests()
        {
            _mockAzureService = new Mock<IAzureStorageService>();
            _mockDatabaseService = new Mock<IDatabaseService>();
            _mockLogger = new Mock<ILogger>();
            
            _photoSettings = new PhotoSettings
            {
                ImportFolder = @"C:\TestSource",
                ExportFolder = @"C:\TestDestination"
            };

            _fromAzureCommand = new FromAzureStorageCommand(
                _mockDatabaseService.Object,
                _mockAzureService.Object,
                _photoSettings,
                _mockLogger.Object
            );
            
            // Setup common mocks
            SetupFromAzureCommonMocks();
        }
        
        private void SetupFromAzureCommonMocks()
        {
            // Database connection test always succeeds by default
            _mockDatabaseService.Setup(x => x.TestConnectionAsync())
                .ReturnsAsync(true);
                
            // Azure connection test succeeds by default
            _mockAzureService.Setup(x => x.TestConnectionAsync())
                .ReturnsAsync(true);
        }

        [Fact]
        public async Task ExecuteAsync_WithBlobsInContainer_ShouldDownloadAll()
        {
            // Arrange
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = null, AzureStoragePath = "https://storage.blob.core.windows.net/photos/TEST001.jpg" },
                new ImageRecord { Code = "TEST002", ImageData = null, AzureStoragePath = "https://storage.blob.core.windows.net/photos/TEST002.jpg" },
                new ImageRecord { Code = "TEST003", ImageData = null, AzureStoragePath = "https://storage.blob.core.windows.net/photos/TEST003.jpg" }
            };

            _mockDatabaseService.Setup(x => x.GetImagesWithNullPhotoDataAsync())
                .ReturnsAsync(imageRecords);

            _mockAzureService.Setup(x => x.DownloadImageAsync(It.IsAny<string>()))
                .ReturnsAsync(new byte[] { 1, 2, 3 });

            _mockDatabaseService.Setup(x => x.UpdateImageDataAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _fromAzureCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(3);
            result.TotalRecordsFound.Should().Be(3);

            _mockAzureService.Verify(x => x.DownloadImageAsync(It.IsAny<string>()), Times.Exactly(3));
            _mockDatabaseService.Verify(x => x.UpdateImageDataAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteAsync_WithEmptyContainer_ShouldReturnEmptyResult()
        {
            // Arrange
            _mockDatabaseService.Setup(x => x.GetImagesWithNullPhotoDataAsync())
                .ReturnsAsync(new List<ImageRecord>());

            // Act
            var result = await _fromAzureCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(0);
            result.TotalRecordsFound.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_WithDownloadError_ShouldHandleGracefully()
        {
            // Arrange
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = null, AzureStoragePath = "https://storage.blob.core.windows.net/photos/TEST001.jpg" }
            };

            _mockDatabaseService.Setup(x => x.GetImagesWithNullPhotoDataAsync())
                .ReturnsAsync(imageRecords);

            _mockAzureService.Setup(x => x.DownloadImageAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Blob not found"));

            // Act
            var result = await _fromAzureCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue(); // Individual failures don't fail the whole operation
            result.SuccessCount.Should().Be(0);
            result.FailureCount.Should().Be(1);
        }
    }
}