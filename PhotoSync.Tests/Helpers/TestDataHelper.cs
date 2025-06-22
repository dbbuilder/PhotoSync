using System;
using System.IO;
using System.Threading.Tasks;

namespace PhotoSync.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating test data
    /// </summary>
    public static class TestDataHelper
    {
        /// <summary>
        /// Creates a test image file with dummy content
        /// </summary>
        public static async Task<string> CreateTestImageFileAsync(string directory, string? fileName = null)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            fileName ??= $"test_{Guid.NewGuid()}.jpg";
            var filePath = Path.Combine(directory, fileName);

            // Create a simple JPEG header (minimal valid JPEG)
            byte[] jpegData = new byte[] 
            { 
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 
                0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
                0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9
            };

            await File.WriteAllBytesAsync(filePath, jpegData);
            return filePath;
        }

        /// <summary>
        /// Creates multiple test image files
        /// </summary>
        public static async Task<List<string>> CreateTestImageFilesAsync(string directory, int count)
        {
            var files = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var file = await CreateTestImageFileAsync(directory, $"test_image_{i:D3}.jpg");
                files.Add(file);
            }
            return files;
        }

        /// <summary>
        /// Cleans up test directory
        /// </summary>
        public static void CleanupTestDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Creates a test database connection string for in-memory SQLite
        /// </summary>
        public static string GetTestConnectionString()
        {
            return "Data Source=:memory:";
        }

        /// <summary>
        /// Generates random byte array for testing
        /// </summary>
        public static byte[] GenerateRandomBytes(int size)
        {
            var bytes = new byte[size];
            new Random().NextBytes(bytes);
            return bytes;
        }
    }
}