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
    public class ExportCommandTests : IDisposable
    {
        private readonly Mock<IDatabaseService> _mockDatabaseService;
        private readonly Mock<IFileService> _mockFileService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly PhotoSettings _photoSettings;
        private readonly ExportCommand _exportCommand;
        private readonly string _testDirectory;

        public ExportCommandTests()
        {
            _mockDatabaseService = new Mock<IDatabaseService>();
            _mockFileService = new Mock<IFileService>();
            _mockLogger = new Mock<ILogger>();
            
            _photoSettings = new PhotoSettings
            {
                ExportFolder = "C:\\TestExport",
                ExportFileNameFormat = "{Code}.jpg",
                UseIncrementalExport = false,
                MaxParallelOperations = 4
            };

            _exportCommand = new ExportCommand(
                _mockDatabaseService.Object,
                _mockFileService.Object,
                _photoSettings,
                _mockLogger.Object
            );

            _testDirectory = Path.Combine(Path.GetTempPath(), $"PhotoSyncTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            
            // Setup common mocks
            SetupCommonMocks();
        }
        
        private void SetupCommonMocks()
        {
            // Database connection test always succeeds by default
            _mockDatabaseService.Setup(x => x.TestConnectionAsync())
                .ReturnsAsync(true);
                
            // Folder validation succeeds by default
            _mockFileService.Setup(x => x.ValidateFolderAccess(It.IsAny<string>()))
                .Returns(true);
                
            // Create output directory by default
            _mockFileService.Setup(x => x.CreateOutputDirectory(It.IsAny<string>()))
                .Returns<string>(dir => dir);
        }

        [Fact]
        public async Task ExecuteAsync_WithValidData_ShouldExportFiles()
        {
            // Arrange
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = new byte[] { 1, 2, 3 }, CreatedDate = DateTime.UtcNow },
                new ImageRecord { Code = "TEST002", ImageData = new byte[] { 4, 5, 6 }, CreatedDate = DateTime.UtcNow },
                new ImageRecord { Code = "TEST003", ImageData = new byte[] { 7, 8, 9 }, CreatedDate = DateTime.UtcNow }
            };

            _mockDatabaseService.Setup(x => x.GetAllImagesAsync())
                .ReturnsAsync(imageRecords);

            _mockFileService.Setup(x => x.SaveImageToFolderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()))
                .ReturnsAsync("path");

            // Act
            var result = await _exportCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(3);
            result.ErrorMessage.Should().BeNull();

            _mockFileService.Verify(x => x.SaveImageToFolderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteAsync_WithNoData_ShouldReturnEmptyResult()
        {
            // Arrange
            _mockDatabaseService.Setup(x => x.GetAllImagesAsync())
                .ReturnsAsync(new List<ImageRecord>());

            // Act
            var result = await _exportCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(0);
            result.TotalImagesFound.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_WithWriteError_ShouldContinueProcessing()
        {
            // Arrange
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = new byte[] { 1, 2, 3 }, CreatedDate = DateTime.UtcNow },
                new ImageRecord { Code = "TEST002", ImageData = new byte[] { 4, 5, 6 }, CreatedDate = DateTime.UtcNow }
            };

            _mockDatabaseService.Setup(x => x.GetAllImagesAsync())
                .ReturnsAsync(imageRecords);

            var callCount = 0;
            _mockFileService.Setup(x => x.SaveImageToFolderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                        throw new IOException("Disk full");
                    return Task.FromResult("path");
                });

            // Act
            var result = await _exportCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(1);
            result.FailureCount.Should().Be(1);
        }

        [Fact]
        public async Task ExecuteAsync_WithCustomExportFolder_ShouldUseProvidedPath()
        {
            // Arrange
            var customPath = "C:\\CustomExport";
            var imageRecords = new List<ImageRecord>
            {
                new ImageRecord { Code = "TEST001", ImageData = new byte[] { 1, 2, 3 }, CreatedDate = DateTime.UtcNow }
            };

            _mockDatabaseService.Setup(x => x.GetAllImagesAsync())
                .ReturnsAsync(imageRecords);

            string capturedPath = null;
            _mockFileService.Setup(x => x.SaveImageToFolderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()))
                .Callback<string, string, byte[]>((folder, fileName, data) => capturedPath = folder)
                .ReturnsAsync((string folder, string fileName, byte[] data) => Path.Combine(folder, fileName + ".jpg"));

            // Act
            var result = await _exportCommand.ExecuteAsync(customPath);

            // Assert
            capturedPath.Should().StartWith(customPath);
        }

        [Fact]
        public async Task ExecuteAsync_WithDatabaseError_ShouldReturnError()
        {
            // Arrange
            _mockDatabaseService.Setup(x => x.GetAllImagesAsync())
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _exportCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Database connection failed");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}