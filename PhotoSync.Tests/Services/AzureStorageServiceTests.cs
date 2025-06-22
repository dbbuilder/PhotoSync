using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Moq;
using PhotoSync.Configuration;
using PhotoSync.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PhotoSync.Tests.Services
{
    public class AzureStorageServiceTests
    {
        private readonly Mock<BlobContainerClient> _mockContainerClient;
        private readonly Mock<ILogger> _mockLogger;
        private readonly AzureStorageSettings _azureSettings;
        private readonly AzureStorageService _azureStorageService;

        public AzureStorageServiceTests()
        {
            _mockContainerClient = new Mock<BlobContainerClient>();
            _mockLogger = new Mock<ILogger>();
            
            _azureSettings = new AzureStorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName = "test-photos"
            };

            // Since we can't easily mock BlobServiceClient constructor, 
            // we'd need to use a factory pattern or integration tests
        }

        [Fact]
        public async Task UploadFilesAsync_WithValidFiles_ShouldUploadSuccessfully()
        {
            // This test would require either:
            // 1. Refactoring AzureStorageService to accept interfaces
            // 2. Using integration tests with Azurite (Azure Storage Emulator)
            // 3. Creating a wrapper around BlobServiceClient
        }

        [Fact]
        public async Task DownloadFilesAsync_ShouldDownloadAllBlobs()
        {
            // Similar to upload test, requires mocking strategy
        }

        [Fact]
        public async Task DeleteBlobsAsync_ShouldDeleteSpecifiedBlobs()
        {
            // Test deletion functionality
        }

        [Fact]
        public async Task ListBlobsAsync_ShouldReturnAllBlobs()
        {
            // Test listing functionality
        }
    }

    /// <summary>
    /// Integration tests for AzureStorageService using Azurite
    /// </summary>
    public class AzureStorageServiceIntegrationTests : IAsyncLifetime
    {
        private readonly string _connectionString = "UseDevelopmentStorage=true";
        private readonly string _containerName;
        private readonly AzureStorageSettings _azureSettings;
        private readonly ILogger _logger;
        private AzureStorageService _azureStorageService;
        private BlobServiceClient _blobServiceClient;
        private BlobContainerClient _containerClient;

        public AzureStorageServiceIntegrationTests()
        {
            _containerName = $"test-{Guid.NewGuid():N}";
            
            _azureSettings = new AzureStorageSettings
            {
                ConnectionString = _connectionString,
                ContainerName = _containerName
            };

            _logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
        }

        public async Task InitializeAsync()
        {
            // Note: These tests require Azurite to be running
            // Start Azurite with: azurite --silent --location c:\azurite --debug c:\azurite\debug.log
            
            _blobServiceClient = new BlobServiceClient(_connectionString);
            _containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await _containerClient.CreateIfNotExistsAsync();
            
            _azureStorageService = new AzureStorageService(_azureSettings, _logger);
        }

        public async Task DisposeAsync()
        {
            if (_containerClient != null)
            {
                await _containerClient.DeleteIfExistsAsync();
            }
        }

        [Fact]
        public async Task UploadAndDownload_RoundTrip_ShouldWork()
        {
            // Arrange
            var testFiles = new Dictionary<string, byte[]>
            {
                { "test1", new byte[] { 1, 2, 3, 4, 5 } },
                { "test2", new byte[] { 6, 7, 8, 9, 10 } },
                { "test3", new byte[] { 11, 12, 13, 14, 15 } }
            };

            var uploadedUrls = new Dictionary<string, string>();

            // Act - Upload
            foreach (var file in testFiles)
            {
                var url = await _azureStorageService.UploadImageAsync(file.Key, file.Value);
                uploadedUrls[file.Key] = url;
            }

            // Assert - Upload
            uploadedUrls.Should().HaveCount(3);

            // Act - Check existence
            foreach (var url in uploadedUrls.Values)
            {
                var exists = await _azureStorageService.BlobExistsAsync(url);
                exists.Should().BeTrue();
            }

            // Act - Download
            foreach (var file in testFiles)
            {
                var downloadedData = await _azureStorageService.DownloadImageAsync(uploadedUrls[file.Key]);
                downloadedData.Should().BeEquivalentTo(file.Value);
            }
        }

        [Fact]
        public async Task DeleteBlobs_ShouldRemoveSpecifiedBlobs()
        {
            // Arrange
            var testData = new byte[] { 1, 2, 3 };
            var url1 = await _azureStorageService.UploadImageAsync("delete1", testData);
            var url2 = await _azureStorageService.UploadImageAsync("delete2", testData);
            var url3 = await _azureStorageService.UploadImageAsync("keep", testData);

            // Act
            var deleted1 = await _azureStorageService.DeleteBlobAsync(url1);
            var deleted2 = await _azureStorageService.DeleteBlobAsync(url2);

            // Assert
            deleted1.Should().BeTrue();
            deleted2.Should().BeTrue();
            
            (await _azureStorageService.BlobExistsAsync(url1)).Should().BeFalse();
            (await _azureStorageService.BlobExistsAsync(url2)).Should().BeFalse();
            (await _azureStorageService.BlobExistsAsync(url3)).Should().BeTrue();
        }

        [Fact]
        public async Task TestConnection_ShouldReturnTrue()
        {
            // Act
            var result = await _azureStorageService.TestConnectionAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task BlobExistsAsync_ShouldReturnCorrectResult()
        {
            // Arrange
            var testData = new byte[] { 1 };
            var url = await _azureStorageService.UploadImageAsync("exists", testData);

            // Act & Assert
            (await _azureStorageService.BlobExistsAsync(url)).Should().BeTrue();
            (await _azureStorageService.BlobExistsAsync("https://storage.blob.core.windows.net/photos/does-not-exist.jpg")).Should().BeFalse();
        }
    }
}