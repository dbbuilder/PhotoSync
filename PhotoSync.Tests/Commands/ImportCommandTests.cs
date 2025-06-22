using FluentAssertions;
using Moq;
using PhotoSync.Commands;
using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using PhotoSync.Tests.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PhotoSync.Tests.Commands
{
    public class ImportCommandTests : IDisposable
    {
        private readonly Mock<IDatabaseService> _mockDatabaseService;
        private readonly Mock<IFileService> _mockFileService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly PhotoSettings _photoSettings;
        private readonly ImportCommand _importCommand;
        private readonly string _testDirectory;

        public ImportCommandTests()
        {
            _mockDatabaseService = new Mock<IDatabaseService>();
            _mockFileService = new Mock<IFileService>();
            _mockLogger = new Mock<ILogger>();
            
            _photoSettings = new PhotoSettings
            {
                ImportFolder = "C:\\TestImport",
                ImportedArchiveFolder = "C:\\TestArchive",
                EnableAutoArchive = true,
                EnableDuplicateCheck = true
            };

            _importCommand = new ImportCommand(
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
                
            // Folder info returns default values
            _mockFileService.Setup(x => x.GetFolderInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(new FolderInfo { TotalFiles = 0, JpgFiles = 0 });
                
            // Get images from folder returns empty by default
            _mockFileService.Setup(x => x.GetImagesFromFolderAsync(It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, byte[]>());
        }

        [Fact]
        public async Task ExecuteAsync_WithValidFolder_ShouldImportFiles()
        {
            // Arrange
            var testImages = new Dictionary<string, byte[]>
            {
                { "file1", new byte[] { 1, 2, 3 } },
                { "file2", new byte[] { 4, 5, 6 } },
                { "file3", new byte[] { 7, 8, 9 } }
            };
            
            // Override the default empty result
            _mockFileService.Setup(x => x.GetImagesFromFolderAsync(It.IsAny<string>()))
                .ReturnsAsync(testImages);
                
            _mockFileService.Setup(x => x.GetFolderInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(new FolderInfo { TotalFiles = 3, JpgFiles = 3 });

            _mockDatabaseService.Setup(x => x.SaveImageAsync(It.IsAny<ImageRecord>()))
                .ReturnsAsync(true);

            // Act
            var result = await _importCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(3);
            result.ErrorMessage.Should().BeNull();

            _mockDatabaseService.Verify(x => x.SaveImageAsync(It.IsAny<ImageRecord>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteAsync_WithEmptyFolder_ShouldReturnEmptyResult()
        {
            // Arrange
            // No need to setup anything extra - default mocks return empty list

            // Act
            var result = await _importCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessCount.Should().Be(0);
            result.TotalFilesFound.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_WithInvalidFolder_ShouldReturnError()
        {
            // Arrange
            _mockFileService.Setup(x => x.DirectoryExists(It.IsAny<string>()))
                .Returns(false);

            // Act
            var result = await _importCommand.ExecuteAsync("C:\\NonExistentFolder");

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ExecuteAsync_WithDatabaseError_ShouldHandleGracefully()
        {
            // Arrange
            var testImages = new Dictionary<string, byte[]>
            {
                { "file1", new byte[] { 1, 2, 3 } }
            };
            
            _mockFileService.Setup(x => x.GetImagesFromFolderAsync(It.IsAny<string>()))
                .ReturnsAsync(testImages);
                
            _mockFileService.Setup(x => x.GetFolderInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(new FolderInfo { TotalFiles = 1, JpgFiles = 1 });

            _mockDatabaseService.Setup(x => x.SaveImageAsync(It.IsAny<ImageRecord>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _importCommand.ExecuteAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Database connection failed");
        }

        [Fact]
        public async Task ExecuteAsync_WithSkipArchive_ShouldNotArchiveFiles()
        {
            // Arrange
            var testImages = new Dictionary<string, byte[]>
            {
                { "file1", new byte[] { 1, 2, 3 } }
            };
            
            _mockFileService.Setup(x => x.GetImagesFromFolderAsync(It.IsAny<string>()))
                .ReturnsAsync(testImages);
                
            _mockFileService.Setup(x => x.GetFolderInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(new FolderInfo { TotalFiles = 1, JpgFiles = 1 });

            _mockDatabaseService.Setup(x => x.SaveImageAsync(It.IsAny<ImageRecord>()))
                .ReturnsAsync(true);

            // Act
            var result = await _importCommand.ExecuteAsync(null, skipArchive: true);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            
            _mockFileService.Verify(x => x.ArchiveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        public void Dispose()
        {
            TestDataHelper.CleanupTestDirectory(_testDirectory);
        }
    }
}