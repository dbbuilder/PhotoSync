using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PhotoSync.Configuration;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PhotoSync.Tests.Configuration
{
    public class AppSettingsTests
    {
        [Fact]
        public void AppSettings_DefaultValues_ShouldBeSet()
        {
            // Arrange & Act
            var appSettings = new AppSettings();

            // Assert
            appSettings.PhotoSettings.Should().NotBeNull();
            appSettings.PhotoSettings.TableName.Should().NotBeNullOrEmpty();
            appSettings.PhotoSettings.ImageFieldName.Should().NotBeNullOrEmpty();
            appSettings.PhotoSettings.CodeFieldName.Should().NotBeNullOrEmpty();
            appSettings.PhotoSettings.ImportFolder.Should().BeEmpty();
            appSettings.PhotoSettings.ExportFolder.Should().BeEmpty();
            appSettings.PhotoSettings.ExportFileNameFormat.Should().Be("{Code}.jpg");
            appSettings.PhotoSettings.MaxParallelOperations.Should().Be(4);
            appSettings.PhotoSettings.EnableAutoArchive.Should().BeTrue();
            appSettings.PhotoSettings.EnableDuplicateCheck.Should().BeTrue();
            appSettings.PhotoSettings.UseIncrementalExport.Should().BeTrue();
            appSettings.PhotoSettings.TrackFileHash.Should().BeTrue();
            appSettings.PhotoSettings.PreserveSourceStructure.Should().BeFalse();
            
            appSettings.ConnectionStrings.Should().NotBeNull();
            appSettings.ConnectionStrings.DefaultConnection.Should().BeEmpty();
            
            appSettings.AzureStorage.Should().NotBeNull();
            appSettings.AzureStorage.ContainerName.Should().Be("photos");
            appSettings.AzureStorage.UseDefaultAzureCredential.Should().BeFalse();
        }

        [Fact]
        public void AppSettings_FromConfiguration_ShouldLoadCorrectly()
        {
            // Arrange
            var configDict = new Dictionary<string, string?>
            {
                {"ConnectionStrings:DefaultConnection", "Server=TestServer;Database=TestDB;"},
                {"PhotoSettings:TableName", "TestPhotos"},
                {"PhotoSettings:ImageFieldName", "TestImageData"},
                {"PhotoSettings:CodeFieldName", "TestCode"},
                {"PhotoSettings:ImportFolder", "C:\\TestImport"},
                {"PhotoSettings:ExportFolder", "C:\\TestExport"},
                {"PhotoSettings:ImportedArchiveFolder", "C:\\TestArchive"},
                {"PhotoSettings:ExportFileNameFormat", "IMG_{Code}.jpg"},
                {"PhotoSettings:MaxParallelOperations", "8"},
                {"PhotoSettings:EnableAutoArchive", "false"},
                {"PhotoSettings:EnableDuplicateCheck", "false"},
                {"PhotoSettings:UseIncrementalExport", "false"},
                {"PhotoSettings:TrackFileHash", "false"},
                {"PhotoSettings:PreserveSourceStructure", "true"},
                {"AzureStorage:ConnectionString", "DefaultEndpointsProtocol=https;AccountName=test;"},
                {"AzureStorage:ContainerName", "test-container"},
                {"AzureStorage:UseDefaultAzureCredential", "true"},
                {"AzureStorage:StorageAccountName", "testaccount"}
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict)
                .Build();

            // Act
            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            // Assert
            appSettings.ConnectionStrings.DefaultConnection.Should().Be("Server=TestServer;Database=TestDB;");
            
            appSettings.PhotoSettings.TableName.Should().Be("TestPhotos");
            appSettings.PhotoSettings.ImageFieldName.Should().Be("TestImageData");
            appSettings.PhotoSettings.CodeFieldName.Should().Be("TestCode");
            appSettings.PhotoSettings.ImportFolder.Should().Be("C:\\TestImport");
            appSettings.PhotoSettings.ExportFolder.Should().Be("C:\\TestExport");
            appSettings.PhotoSettings.ImportedArchiveFolder.Should().Be("C:\\TestArchive");
            appSettings.PhotoSettings.ExportFileNameFormat.Should().Be("IMG_{Code}.jpg");
            appSettings.PhotoSettings.MaxParallelOperations.Should().Be(8);
            appSettings.PhotoSettings.EnableAutoArchive.Should().BeFalse();
            appSettings.PhotoSettings.EnableDuplicateCheck.Should().BeFalse();
            appSettings.PhotoSettings.UseIncrementalExport.Should().BeFalse();
            appSettings.PhotoSettings.TrackFileHash.Should().BeFalse();
            appSettings.PhotoSettings.PreserveSourceStructure.Should().BeTrue();
            
            appSettings.AzureStorage.ConnectionString.Should().Be("DefaultEndpointsProtocol=https;AccountName=test;");
            appSettings.AzureStorage.ContainerName.Should().Be("test-container");
            appSettings.AzureStorage.UseDefaultAzureCredential.Should().BeTrue();
            appSettings.AzureStorage.StorageAccountName.Should().Be("testaccount");
        }

        [Fact]
        public void PhotoSettings_RequiredFields_ShouldNotBeNull()
        {
            // Arrange
            var photoSettings = new PhotoSettings
            {
                TableName = "Photos",
                ImageFieldName = "ImageData",
                CodeFieldName = "Code"
            };

            // Act & Assert
            photoSettings.TableName.Should().NotBeNullOrEmpty();
            photoSettings.ImageFieldName.Should().NotBeNullOrEmpty();
            photoSettings.CodeFieldName.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void AzureStorageSettings_DefaultValues_ShouldBeSet()
        {
            // Arrange & Act
            var azureSettings = new AzureStorageSettings();

            // Assert
            azureSettings.ConnectionString.Should().BeEmpty();
            azureSettings.ContainerName.Should().Be("photos");
            azureSettings.UseDefaultAzureCredential.Should().BeFalse();
            azureSettings.StorageAccountName.Should().BeEmpty();
        }

        [Fact]
        public void PhotoSettings_WorkflowSettings_ShouldHaveCorrectDefaults()
        {
            // Arrange & Act
            var photoSettings = new PhotoSettings();

            // Assert - Workflow settings
            photoSettings.EnableAutoArchive.Should().BeTrue();
            photoSettings.EnableDuplicateCheck.Should().BeTrue();
            photoSettings.UseIncrementalExport.Should().BeTrue();
            photoSettings.TrackFileHash.Should().BeTrue();
            photoSettings.PreserveSourceStructure.Should().BeFalse();
            photoSettings.MaxParallelOperations.Should().Be(4);
            photoSettings.ExportFileNameFormat.Should().Be("{Code}.jpg");
        }

        [Fact]
        public void AppSettings_ValidationScenarios_ShouldWork()
        {
            // Arrange
            var appSettings = new AppSettings();

            // Act - Test various validation scenarios
            
            // Scenario 1: Empty connection string
            appSettings.ConnectionStrings.DefaultConnection = "";
            appSettings.ConnectionStrings.DefaultConnection.Should().BeEmpty();

            // Scenario 2: Invalid table name
            appSettings.PhotoSettings.TableName = "";
            appSettings.PhotoSettings.TableName.Should().BeEmpty();

            // Scenario 3: Azure settings without connection string
            appSettings.AzureStorage.UseDefaultAzureCredential = true;
            appSettings.AzureStorage.StorageAccountName = "myaccount";
            appSettings.AzureStorage.UseDefaultAzureCredential.Should().BeTrue();
            appSettings.AzureStorage.StorageAccountName.Should().Be("myaccount");
        }
    }
}