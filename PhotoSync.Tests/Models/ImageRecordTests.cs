using FluentAssertions;
using PhotoSync.Models;
using System;
using Xunit;

namespace PhotoSync.Tests.Models
{
    public class ImageRecordTests
    {
        [Fact]
        public void ImageRecord_DefaultConstructor_ShouldInitializeProperties()
        {
            // Act
            var imageRecord = new ImageRecord();

            // Assert
            imageRecord.Code.Should().BeEmpty();
            imageRecord.ImageData.Should().BeEmpty();
            imageRecord.CreatedDate.Should().BeCloseTo(DateTime.MinValue, TimeSpan.FromSeconds(1));
            imageRecord.ModifiedDate.Should().BeNull();
            imageRecord.AzureStoragePath.Should().BeNull();
            imageRecord.ImageSource.Should().BeNull();
            imageRecord.SourceFileName.Should().BeNull();
            imageRecord.ImportedDate.Should().BeNull();
            imageRecord.ExportedDate.Should().BeNull();
            imageRecord.AzureUploadedDate.Should().BeNull();
            imageRecord.PhotoModifiedDate.Should().BeNull();
            imageRecord.AzureSyncRequired.Should().BeFalse();
            imageRecord.FileHash.Should().BeNull();
            imageRecord.FileSize.Should().BeNull();
        }

        [Fact]
        public void ImageRecord_WithData_ShouldSetAllProperties()
        {
            // Arrange
            var testDate = DateTime.UtcNow;
            var testData = new byte[] { 1, 2, 3, 4, 5 };

            // Act
            var imageRecord = new ImageRecord
            {
                Code = "TEST001",
                ImageData = testData,
                CreatedDate = testDate,
                ModifiedDate = testDate.AddHours(1),
                AzureStoragePath = "https://storage.blob.core.windows.net/photos/test.jpg",
                ImageSource = "FILE:C:\\Photos\\test.jpg",
                SourceFileName = "test.jpg",
                ImportedDate = testDate,
                ExportedDate = testDate.AddHours(2),
                AzureUploadedDate = testDate.AddHours(3),
                PhotoModifiedDate = testDate.AddHours(1),
                AzureSyncRequired = true,
                FileHash = "abc123",
                FileSize = 12345
            };

            // Assert
            imageRecord.Code.Should().Be("TEST001");
            imageRecord.ImageData.Should().BeEquivalentTo(testData);
            imageRecord.CreatedDate.Should().Be(testDate);
            imageRecord.ModifiedDate.Should().Be(testDate.AddHours(1));
            imageRecord.AzureStoragePath.Should().Be("https://storage.blob.core.windows.net/photos/test.jpg");
            imageRecord.ImageSource.Should().Be("FILE:C:\\Photos\\test.jpg");
            imageRecord.SourceFileName.Should().Be("test.jpg");
            imageRecord.ImportedDate.Should().Be(testDate);
            imageRecord.ExportedDate.Should().Be(testDate.AddHours(2));
            imageRecord.AzureUploadedDate.Should().Be(testDate.AddHours(3));
            imageRecord.PhotoModifiedDate.Should().Be(testDate.AddHours(1));
            imageRecord.AzureSyncRequired.Should().BeTrue();
            imageRecord.FileHash.Should().Be("abc123");
            imageRecord.FileSize.Should().Be(12345);
        }

        [Fact]
        public void ImageRecord_Equality_ShouldBeBasedOnCode()
        {
            // Arrange
            var record1 = new ImageRecord { Code = "IMG001", SourceFileName = "test1.jpg" };
            var record2 = new ImageRecord { Code = "IMG001", SourceFileName = "test2.jpg" };
            var record3 = new ImageRecord { Code = "IMG002", SourceFileName = "test1.jpg" };

            // Act & Assert
            record1.Code.Should().Be(record2.Code);
            record1.Code.Should().NotBe(record3.Code);
        }

        [Fact]
        public void ImageRecord_CalculatedProperties_ShouldWork()
        {
            // Arrange
            var imageRecord = new ImageRecord
            {
                Code = "IMG042",
                ImageData = new byte[2048],
                FileSize = 2048
            };

            // Act & Assert
            imageRecord.ImageSizeBytes.Should().Be(2048);
            imageRecord.ImageSizeFormatted.Should().Be("2.0 KB");
            imageRecord.HasLocalData.Should().BeTrue();
            imageRecord.IsInAzure.Should().BeFalse();
            imageRecord.StorageMode.Should().Be("LocalOnly");
        }

        [Theory]
        [InlineData("")]
        [InlineData("IMG001")]
        [InlineData("TEST_001")]
        [InlineData("photo_2023_12_25")]
        public void ImageRecord_Code_ShouldAcceptVariousValues(string code)
        {
            // Act
            var imageRecord = new ImageRecord { Code = code };

            // Assert
            imageRecord.Code.Should().Be(code);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(1024)]
        [InlineData(1048576)] // 1MB
        [InlineData(long.MaxValue)]
        public void ImageRecord_FileSize_ShouldAcceptVariousSizes(long fileSize)
        {
            // Act
            var imageRecord = new ImageRecord { FileSize = fileSize };

            // Assert
            imageRecord.FileSize.Should().Be(fileSize);
        }

        [Fact]
        public void ImageRecord_Dates_ShouldHandleNullValues()
        {
            // Arrange
            var imageRecord = new ImageRecord();

            // Act & Assert
            imageRecord.ImportedDate.Should().BeNull();
            imageRecord.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public void ImageRecord_WithLargeData_ShouldHandleCorrectly()
        {
            // Arrange
            var largeData = new byte[1024 * 1024]; // 1MB
            new Random().NextBytes(largeData);

            // Act
            var imageRecord = new ImageRecord
            {
                ImageData = largeData,
                FileSize = largeData.Length
            };

            // Assert
            imageRecord.ImageData.Should().HaveCount(1024 * 1024);
            imageRecord.FileSize.Should().Be(1024 * 1024);
            imageRecord.ImageSizeBytes.Should().Be(1024 * 1024);
            imageRecord.ImageSizeFormatted.Should().Be("1.0 MB");
        }

        [Fact]
        public void ImageRecord_NeedsExport_ShouldReturnCorrectValue()
        {
            // Arrange
            var now = DateTime.UtcNow;
            
            // Test case 1: Never exported
            var record1 = new ImageRecord { ExportedDate = null };
            record1.NeedsExport.Should().BeTrue();
            
            // Test case 2: Modified after export
            var record2 = new ImageRecord 
            { 
                ExportedDate = now.AddDays(-1),
                ModifiedDate = now 
            };
            record2.NeedsExport.Should().BeTrue();
            
            // Test case 3: Photo modified after export
            var record3 = new ImageRecord 
            { 
                ExportedDate = now.AddDays(-1),
                PhotoModifiedDate = now 
            };
            record3.NeedsExport.Should().BeTrue();
            
            // Test case 4: Recently exported, no modifications
            var record4 = new ImageRecord 
            { 
                ExportedDate = now,
                ModifiedDate = now.AddDays(-1),
                PhotoModifiedDate = now.AddDays(-1)
            };
            record4.NeedsExport.Should().BeFalse();
        }

        [Fact]
        public void ImageRecord_CalculateHash_ShouldWork()
        {
            // Arrange
            var imageData = System.Text.Encoding.UTF8.GetBytes("test image data");
            var record = new ImageRecord { ImageData = imageData };
            
            // Act
            var hash = record.CalculateHash();
            
            // Assert
            hash.Should().NotBeNullOrEmpty();
            hash.Should().HaveLength(44); // Base64 encoded SHA256 hash length
            
            // Empty data should return empty string
            var emptyRecord = new ImageRecord { ImageData = Array.Empty<byte>() };
            emptyRecord.CalculateHash().Should().BeEmpty();
        }
    }
}