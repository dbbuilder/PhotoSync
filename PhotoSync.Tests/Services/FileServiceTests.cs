using FluentAssertions;
using PhotoSync.Configuration;
using PhotoSync.Services;
using PhotoSync.Tests.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PhotoSync.Tests.Services
{
    public class FileServiceTests : IDisposable
    {
        private readonly FileService _fileService;
        private readonly ILogger _logger;
        private readonly PhotoSettings _photoSettings;
        private readonly string _testDirectory;
        private readonly List<string> _createdDirectories;

        public FileServiceTests()
        {
            _logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            _photoSettings = new PhotoSettings
            {
                ImportFolder = Path.Combine(Path.GetTempPath(), "TestImport"),
                ExportFolder = Path.Combine(Path.GetTempPath(), "TestExport"),
                ImportedArchiveFolder = Path.Combine(Path.GetTempPath(), "TestArchive"),
                PreserveSourceStructure = false
            };

            _fileService = new FileService(_photoSettings, _logger);
            _testDirectory = Path.Combine(Path.GetTempPath(), $"PhotoSyncTest_{Guid.NewGuid()}");
            _createdDirectories = new List<string> { _testDirectory };
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public async Task ReadFileAsync_WithValidFile_ShouldReturnContent()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.jpg");
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            await File.WriteAllBytesAsync(testFile, testData);

            // Act
            var result = await _fileService.ReadFileAsync(testFile);

            // Assert
            result.Should().BeEquivalentTo(testData);
        }

        [Fact]
        public async Task ReadFileAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.jpg");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _fileService.ReadFileAsync(nonExistentFile));
        }

        [Fact]
        public async Task WriteFileAsync_ShouldCreateFile()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "output.jpg");
            var testData = new byte[] { 10, 20, 30, 40, 50 };

            // Act
            await _fileService.WriteFileAsync(testFile, testData);

            // Assert
            File.Exists(testFile).Should().BeTrue();
            var writtenData = await File.ReadAllBytesAsync(testFile);
            writtenData.Should().BeEquivalentTo(testData);
        }

        [Fact]
        public async Task WriteFileAsync_WithSubdirectory_ShouldCreateDirectoryStructure()
        {
            // Arrange
            var subDir = Path.Combine(_testDirectory, "subdir", "nested");
            var testFile = Path.Combine(subDir, "output.jpg");
            var testData = new byte[] { 1, 2, 3 };
            _createdDirectories.Add(subDir);

            // Act
            await _fileService.WriteFileAsync(testFile, testData);

            // Assert
            Directory.Exists(subDir).Should().BeTrue();
            File.Exists(testFile).Should().BeTrue();
        }

        [Fact]
        public void GetFiles_WithPattern_ShouldReturnMatchingFiles()
        {
            // Arrange
            var jpgFile1 = Path.Combine(_testDirectory, "image1.jpg");
            var jpgFile2 = Path.Combine(_testDirectory, "image2.jpg");
            var pngFile = Path.Combine(_testDirectory, "image.png");
            var txtFile = Path.Combine(_testDirectory, "document.txt");

            File.WriteAllBytes(jpgFile1, new byte[] { 1 });
            File.WriteAllBytes(jpgFile2, new byte[] { 2 });
            File.WriteAllBytes(pngFile, new byte[] { 3 });
            File.WriteAllBytes(txtFile, new byte[] { 4 });

            // Act
            var jpgFiles = _fileService.GetFiles(_testDirectory, "*.jpg");

            // Assert
            jpgFiles.Should().HaveCount(2);
            jpgFiles.Should().Contain(jpgFile1);
            jpgFiles.Should().Contain(jpgFile2);
            jpgFiles.Should().NotContain(pngFile);
            jpgFiles.Should().NotContain(txtFile);
        }

        [Fact]
        public void MoveFile_ShouldMoveFileToNewLocation()
        {
            // Arrange
            var sourceFile = Path.Combine(_testDirectory, "source.jpg");
            var destFile = Path.Combine(_testDirectory, "moved", "destination.jpg");
            _createdDirectories.Add(Path.GetDirectoryName(destFile));
            
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });

            // Act
            _fileService.MoveFile(sourceFile, destFile);

            // Assert
            File.Exists(sourceFile).Should().BeFalse();
            File.Exists(destFile).Should().BeTrue();
            var movedData = File.ReadAllBytes(destFile);
            movedData.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void CopyFile_ShouldCopyFileToNewLocation()
        {
            // Arrange
            var sourceFile = Path.Combine(_testDirectory, "source.jpg");
            var destFile = Path.Combine(_testDirectory, "copied", "destination.jpg");
            _createdDirectories.Add(Path.GetDirectoryName(destFile));
            
            var testData = new byte[] { 1, 2, 3 };
            File.WriteAllBytes(sourceFile, testData);

            // Act
            _fileService.CopyFile(sourceFile, destFile);

            // Assert
            File.Exists(sourceFile).Should().BeTrue();
            File.Exists(destFile).Should().BeTrue();
            File.ReadAllBytes(sourceFile).Should().BeEquivalentTo(testData);
            File.ReadAllBytes(destFile).Should().BeEquivalentTo(testData);
        }

        [Fact]
        public void DeleteFile_WithExistingFile_ShouldRemoveFile()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "delete-me.jpg");
            File.WriteAllBytes(testFile, new byte[] { 1 });

            // Act
            _fileService.DeleteFile(testFile);

            // Assert
            File.Exists(testFile).Should().BeFalse();
        }

        [Fact]
        public void DeleteFile_WithNonExistentFile_ShouldNotThrow()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "does-not-exist.jpg");

            // Act & Assert
            _fileService.Invoking(x => x.DeleteFile(nonExistentFile))
                .Should().NotThrow();
        }

        [Fact]
        public void DirectoryExists_ShouldReturnCorrectResult()
        {
            // Arrange
            var existingDir = _testDirectory;
            var nonExistentDir = Path.Combine(_testDirectory, "does-not-exist");

            // Act & Assert
            _fileService.DirectoryExists(existingDir).Should().BeTrue();
            _fileService.DirectoryExists(nonExistentDir).Should().BeFalse();
        }

        [Fact]
        public void EnsureDirectoryExists_ShouldCreateDirectory()
        {
            // Arrange
            var newDir = Path.Combine(_testDirectory, "new-directory");
            _createdDirectories.Add(newDir);

            // Act
            _fileService.EnsureDirectoryExists(newDir);

            // Assert
            Directory.Exists(newDir).Should().BeTrue();
        }

        [Fact]
        public void EnsureDirectoryExists_WithExistingDirectory_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            _fileService.Invoking(x => x.EnsureDirectoryExists(_testDirectory))
                .Should().NotThrow();
        }

        [Fact]
        public void GetFileSize_ShouldReturnCorrectSize()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "sized-file.jpg");
            var testData = new byte[1024]; // 1KB
            File.WriteAllBytes(testFile, testData);

            // Act
            var size = _fileService.GetFileSize(testFile);

            // Assert
            size.Should().Be(1024);
        }

        [Fact]
        public void FileExists_ShouldReturnCorrectResult()
        {
            // Arrange
            var existingFile = Path.Combine(_testDirectory, "exists.jpg");
            File.WriteAllBytes(existingFile, new byte[] { 1 });
            var nonExistentFile = Path.Combine(_testDirectory, "does-not-exist.jpg");

            // Act & Assert
            _fileService.FileExists(existingFile).Should().BeTrue();
            _fileService.FileExists(nonExistentFile).Should().BeFalse();
        }

        public void Dispose()
        {
            foreach (var dir in _createdDirectories.Where(Directory.Exists))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}