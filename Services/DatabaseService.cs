using System.Data;
using Microsoft.Data.SqlClient;
using PhotoSync.Configuration;
using PhotoSync.Models;
using PhotoSync.Services;
using Polly;
using Serilog;

namespace PhotoSync.Services
{
    /// <summary>
    /// Service for database operations using stored procedures only
    /// </summary>
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;
        private readonly PhotoSettings _photoSettings;
        private readonly ILogger _logger;
        private readonly IAsyncPolicy _retryPolicy;

        public DatabaseService(AppSettings appSettings, ILogger logger)
        {
            _connectionString = appSettings.ConnectionStrings.DefaultConnection ?? 
                throw new ArgumentNullException(nameof(appSettings.ConnectionStrings.DefaultConnection));
            _photoSettings = appSettings.PhotoSettings ?? 
                throw new ArgumentNullException(nameof(appSettings.PhotoSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Configure Polly retry policy for database operations
            _retryPolicy = Policy
                .Handle<SqlException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.Warning("Database operation retry {RetryCount} after {Delay}ms", 
                            retryCount, timespan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Inserts or updates an image record in the database using stored procedure
        /// </summary>
        /// <param name="imageRecord">Image record to save</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SaveImageAsync(ImageRecord imageRecord)
        {
            try
            {
                if (imageRecord == null)
                    throw new ArgumentNullException(nameof(imageRecord));

                if (string.IsNullOrWhiteSpace(imageRecord.Code))
                    throw new ArgumentException("Image code cannot be null or empty", nameof(imageRecord));

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_SaveImage", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300 // 5 minutes for large images
                    };

                    // Add parameters for the stored procedure
                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) 
                        { Value = imageRecord.Code });
                    command.Parameters.Add(new SqlParameter("@ImageData", SqlDbType.VarBinary, -1) 
                        { Value = (object)imageRecord.ImageData ?? DBNull.Value });
                    command.Parameters.Add(new SqlParameter("@AzureStoragePath", SqlDbType.NVarChar, 500) 
                        { Value = (object)imageRecord.AzureStoragePath ?? DBNull.Value });
                    command.Parameters.Add(new SqlParameter("@CreatedDate", SqlDbType.DateTime2) 
                        { Value = imageRecord.CreatedDate });
                    
                    // Add output parameter to get result
                    var resultParam = new SqlParameter("@Result", SqlDbType.Int) 
                        { Direction = ParameterDirection.Output };
                    command.Parameters.Add(resultParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var result = (int)resultParam.Value;
                    
                    if (result > 0)
                    {
                        _logger.Information("Successfully saved image with code: {Code} ({ImageSize} bytes)", 
                            imageRecord.Code, imageRecord.ImageData.Length);
                        return true;
                    }
                    else
                    {
                        _logger.Warning("Failed to save image with code: {Code}", imageRecord.Code);
                        return false;
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error saving image with code: {Code}", imageRecord.Code);
                return false;
            }
        }

        /// <summary>
        /// Retrieves all image records from the database using stored procedure
        /// </summary>
        /// <returns>List of image records</returns>
        public async Task<List<ImageRecord>> GetAllImagesAsync()
        {
            var images = new List<ImageRecord>();

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetAllImages", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300 // 5 minutes for large result sets
                    };

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var image = new ImageRecord
                        {
                            Code = reader["Code"].ToString() ?? string.Empty,
                            ImageData = reader["ImageData"] as byte[] ?? Array.Empty<byte>(),
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?,
                            AzureStoragePath = reader["AzureStoragePath"]?.ToString()
                        };

                        images.Add(image);
                    }

                    _logger.Information("Successfully retrieved {ImageCount} images from database", images.Count);
                    return true; // Required for Polly policy
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error retrieving all images from database");
                throw;
            }

            return images;
        }

        /// <summary>
        /// Retrieves a specific image by code using stored procedure
        /// </summary>
        /// <param name="code">Image code to search for</param>
        /// <returns>Image record if found, null otherwise</returns>
        public async Task<ImageRecord?> GetImageByCodeAsync(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                    throw new ArgumentException("Code cannot be null or empty", nameof(code));

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetImageByCode", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 60
                    };

                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) { Value = code });

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        var image = new ImageRecord
                        {
                            Code = reader["Code"].ToString() ?? string.Empty,
                            ImageData = reader["ImageData"] as byte[] ?? Array.Empty<byte>(),
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?,
                            AzureStoragePath = reader["AzureStoragePath"]?.ToString()
                        };

                        _logger.Debug("Successfully retrieved image with code: {Code}", code);
                        return image;
                    }

                    _logger.Debug("No image found with code: {Code}", code);
                    return null;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error retrieving image with code: {Code}", code);
                return null;
            }
        }

        /// <summary>
        /// Tests the database connection
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                _logger.Information("Database connection test successful");
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Database connection test failed");
                return false;
            }
        }

        /// <summary>
        /// Gets count of images in the database
        /// </summary>
        /// <returns>Number of image records</returns>
        public async Task<int> GetImageCountAsync()
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetImageCount", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 30
                    };

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    var count = Convert.ToInt32(result);
                    
                    _logger.Debug("Database contains {ImageCount} images", count);
                    return count;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error getting image count from database");
                return 0;
            }
        }

        /// <summary>
        /// Deletes an image by code
        /// </summary>
        /// <param name="code">Image code to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> DeleteImageAsync(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                    throw new ArgumentException("Code cannot be null or empty", nameof(code));

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_DeleteImage", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 60
                    };

                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) { Value = code });
                    var resultParam = new SqlParameter("@Result", SqlDbType.Int) 
                        { Direction = ParameterDirection.Output };
                    command.Parameters.Add(resultParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var result = (int)resultParam.Value;
                    
                    if (result > 0)
                    {
                        _logger.Information("Successfully deleted image with code: {Code}", code);
                        return true;
                    }
                    else
                    {
                        _logger.Warning("No image found to delete with code: {Code}", code);
                        return false;
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error deleting image with code: {Code}", code);
                return false;
            }
        }

        /// <summary>
        /// Gets all images that have NULL Azure Storage Path
        /// </summary>
        /// <returns>List of image records with null Azure path</returns>
        public async Task<List<ImageRecord>> GetImagesWithNullAzurePathAsync()
        {
            var images = new List<ImageRecord>();

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetImagesWithNullAzurePath", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300
                    };

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var image = new ImageRecord
                        {
                            Code = reader["Code"].ToString() ?? string.Empty,
                            ImageData = reader["ImageData"] as byte[] ?? Array.Empty<byte>(),
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?,
                            AzureStoragePath = reader["AzureStoragePath"]?.ToString()
                        };

                        images.Add(image);
                    }

                    _logger.Information("Retrieved {ImageCount} images with NULL Azure path", images.Count);
                    return true;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error retrieving images with NULL Azure path");
                throw;
            }

            return images;
        }

        /// <summary>
        /// Gets all images that have NULL photo data
        /// </summary>
        /// <returns>List of image records with null photo data</returns>
        public async Task<List<ImageRecord>> GetImagesWithNullPhotoDataAsync()
        {
            var images = new List<ImageRecord>();

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetImagesWithNullPhotoData", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300
                    };

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var image = new ImageRecord
                        {
                            Code = reader["Code"].ToString() ?? string.Empty,
                            ImageData = reader["ImageData"] as byte[] ?? Array.Empty<byte>(),
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?,
                            AzureStoragePath = reader["AzureStoragePath"]?.ToString()
                        };

                        images.Add(image);
                    }

                    _logger.Information("Retrieved {ImageCount} images with NULL photo data", images.Count);
                    return true;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error retrieving images with NULL photo data");
                throw;
            }

            return images;
        }

        /// <summary>
        /// Updates the Azure Storage Path for a specific image
        /// </summary>
        /// <param name="code">Image code to update</param>
        /// <param name="azurePath">Azure Storage path/URL</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> UpdateAzureStoragePathAsync(string code, string azurePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                    throw new ArgumentException("Code cannot be null or empty", nameof(code));

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_UpdateAzureStoragePath", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 60
                    };

                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) { Value = code });
                    command.Parameters.Add(new SqlParameter("@AzureStoragePath", SqlDbType.NVarChar, 500) 
                        { Value = (object)azurePath ?? DBNull.Value });
                    
                    var resultParam = new SqlParameter("@Result", SqlDbType.Int) 
                        { Direction = ParameterDirection.Output };
                    command.Parameters.Add(resultParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var result = (int)resultParam.Value;
                    
                    if (result > 0)
                    {
                        _logger.Information("Successfully updated Azure path for code: {Code}", code);
                        return true;
                    }
                    else
                    {
                        _logger.Warning("Failed to update Azure path for code: {Code}", code);
                        return false;
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error updating Azure path for code: {Code}", code);
                return false;
            }
        }

        /// <summary>
        /// Updates the image data for a specific image
        /// </summary>
        /// <param name="code">Image code to update</param>
        /// <param name="imageData">Binary image data</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> UpdateImageDataAsync(string code, byte[] imageData)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                    throw new ArgumentException("Code cannot be null or empty", nameof(code));

                if (imageData == null || imageData.Length == 0)
                    throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_UpdateImageData", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300
                    };

                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) { Value = code });
                    command.Parameters.Add(new SqlParameter("@ImageData", SqlDbType.VarBinary, -1) { Value = imageData });
                    
                    var resultParam = new SqlParameter("@Result", SqlDbType.Int) 
                        { Direction = ParameterDirection.Output };
                    command.Parameters.Add(resultParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var result = (int)resultParam.Value;
                    
                    if (result > 0)
                    {
                        _logger.Information("Successfully updated image data for code: {Code} ({Size} bytes)", 
                            code, imageData.Length);
                        return true;
                    }
                    else
                    {
                        _logger.Warning("Failed to update image data for code: {Code}", code);
                        return false;
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error updating image data for code: {Code}", code);
                return false;
            }
        }

        /// <summary>
        /// Nullifies a specific field in all records
        /// </summary>
        /// <param name="fieldName">Field name to nullify (ImageData or AzureStoragePath)</param>
        /// <returns>Number of records affected</returns>
        public async Task<int> NullifyFieldAsync(string fieldName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fieldName))
                    throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_NullifyField", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300
                    };

                    command.Parameters.Add(new SqlParameter("@FieldName", SqlDbType.NVarChar, 50) { Value = fieldName });
                    
                    var resultParam = new SqlParameter("@Result", SqlDbType.Int) 
                        { Direction = ParameterDirection.Output };
                    command.Parameters.Add(resultParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var result = (int)resultParam.Value;
                    
                    if (result >= 0)
                    {
                        _logger.Information("Successfully nullified field {FieldName} for {Count} records", 
                            fieldName, result);
                        return result;
                    }
                    else
                    {
                        _logger.Warning("Invalid field name: {FieldName}", fieldName);
                        return 0;
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error nullifying field: {FieldName}", fieldName);
                return 0;
            }
        }

        /// <summary>
        /// Gets images that need incremental export
        /// </summary>
        /// <returns>List of images where ExportedDate is null or older than modified dates</returns>
        public async Task<List<ImageRecord>> GetPhotosForIncrementalExportAsync()
        {
            var images = new List<ImageRecord>();

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetPhotosForIncrementalExport", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300
                    };

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var image = new ImageRecord
                        {
                            Code = reader["Code"].ToString() ?? string.Empty,
                            ImageData = reader["ImageData"] as byte[] ?? Array.Empty<byte>(),
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?,
                            AzureStoragePath = reader["AzureStoragePath"]?.ToString(),
                            ImageSource = reader["ImageSource"]?.ToString(),
                            SourceFileName = reader["SourceFileName"]?.ToString(),
                            ImportedDate = reader["ImportedDate"] as DateTime?,
                            ExportedDate = reader["ExportedDate"] as DateTime?,
                            AzureUploadedDate = reader["AzureUploadedDate"] as DateTime?,
                            PhotoModifiedDate = reader["PhotoModifiedDate"] as DateTime?,
                            AzureSyncRequired = reader["AzureSyncRequired"] as bool? ?? false,
                            FileHash = reader["FileHash"]?.ToString(),
                            FileSize = reader["FileSize"] as long?
                        };

                        images.Add(image);
                    }

                    _logger.Information("Retrieved {ImageCount} images for incremental export", images.Count);
                    return true;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error retrieving images for incremental export");
                throw;
            }

            return images;
        }

        /// <summary>
        /// Gets images that need Azure sync
        /// </summary>
        /// <returns>List of images where AzureSyncRequired is true</returns>
        public async Task<List<ImageRecord>> GetPhotosNeedingAzureSyncAsync()
        {
            var images = new List<ImageRecord>();

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetPhotosNeedingAzureSync", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300
                    };

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var image = new ImageRecord
                        {
                            Code = reader["Code"].ToString() ?? string.Empty,
                            ImageData = reader["ImageData"] as byte[] ?? Array.Empty<byte>(),
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?,
                            AzureStoragePath = reader["AzureStoragePath"]?.ToString(),
                            AzureSyncRequired = reader["AzureSyncRequired"] as bool? ?? false,
                            PhotoModifiedDate = reader["PhotoModifiedDate"] as DateTime?,
                            AzureUploadedDate = reader["AzureUploadedDate"] as DateTime?
                        };

                        images.Add(image);
                    }

                    _logger.Information("Retrieved {ImageCount} images needing Azure sync", images.Count);
                    return true;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error retrieving images needing Azure sync");
                throw;
            }

            return images;
        }

        /// <summary>
        /// Updates export tracking information
        /// </summary>
        /// <param name="code">Image code</param>
        /// <param name="exportedDate">Export timestamp</param>
        /// <returns>True if successful</returns>
        public async Task<bool> UpdateExportTrackingAsync(string code, DateTime exportedDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                    throw new ArgumentException("Code cannot be null or empty", nameof(code));

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_UpdateExportTracking", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 60
                    };

                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) { Value = code });
                    command.Parameters.Add(new SqlParameter("@ExportedDate", SqlDbType.DateTime2) { Value = exportedDate });
                    
                    var resultParam = new SqlParameter("@Result", SqlDbType.Int) 
                        { Direction = ParameterDirection.Output };
                    command.Parameters.Add(resultParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var result = (int)resultParam.Value;
                    
                    if (result > 0)
                    {
                        _logger.Debug("Updated export tracking for code: {Code}", code);
                        return true;
                    }
                    else
                    {
                        _logger.Warning("Failed to update export tracking for code: {Code}", code);
                        return false;
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error updating export tracking for code: {Code}", code);
                return false;
            }
        }

        /// <summary>
        /// Updates import tracking information
        /// </summary>
        public async Task<bool> UpdateImportTrackingAsync(string code, DateTime importedDate, string imageSource, string sourceFileName, string fileHash, long fileSize)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_UpdateImportTracking", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 60
                    };

                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) { Value = code });
                    command.Parameters.Add(new SqlParameter("@ImportedDate", SqlDbType.DateTime2) { Value = importedDate });
                    command.Parameters.Add(new SqlParameter("@ImageSource", SqlDbType.NVarChar, 500) { Value = imageSource });
                    command.Parameters.Add(new SqlParameter("@SourceFileName", SqlDbType.NVarChar, 255) { Value = sourceFileName });
                    command.Parameters.Add(new SqlParameter("@FileHash", SqlDbType.NVarChar, 64) { Value = fileHash });
                    command.Parameters.Add(new SqlParameter("@FileSize", SqlDbType.BigInt) { Value = fileSize });
                    
                    var resultParam = new SqlParameter("@Result", SqlDbType.Int) 
                        { Direction = ParameterDirection.Output };
                    command.Parameters.Add(resultParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var result = (int)resultParam.Value;
                    
                    if (result > 0)
                    {
                        _logger.Debug("Updated import tracking for code: {Code}", code);
                        return true;
                    }
                    else
                    {
                        _logger.Warning("Failed to update import tracking for code: {Code}", code);
                        return false;
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error updating import tracking for code: {Code}", code);
                return false;
            }
        }

        /// <summary>
        /// Checks for duplicate images by hash
        /// </summary>
        public async Task<ImageRecord?> CheckDuplicateByHashAsync(string fileHash, string? excludeCode = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileHash))
                    return null;

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_CheckDuplicateByHash", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 60
                    };

                    command.Parameters.Add(new SqlParameter("@FileHash", SqlDbType.NVarChar, 64) { Value = fileHash });
                    command.Parameters.Add(new SqlParameter("@ExcludeCode", SqlDbType.NVarChar, 100) 
                        { Value = (object)excludeCode ?? DBNull.Value });

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        var duplicate = new ImageRecord
                        {
                            Code = reader["Code"].ToString() ?? string.Empty,
                            SourceFileName = reader["SourceFileName"]?.ToString(),
                            ImportedDate = reader["ImportedDate"] as DateTime?,
                            FileSize = reader["FileSize"] as long?
                        };

                        _logger.Warning("Found duplicate image by hash. Existing: {Code}, Hash: {Hash}", 
                            duplicate.Code, fileHash);
                        return duplicate;
                    }

                    return null;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error checking duplicate by hash: {Hash}", fileHash);
                return null;
            }
        }

        /// <summary>
        /// Gets overall sync status statistics
        /// </summary>
        public async Task<Dictionary<string, object>> GetSyncStatusAsync()
        {
            var status = new Dictionary<string, object>();

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    using var command = new SqlCommand("PHOTOS.sp_GetSyncStatus", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 60
                    };

                    await connection.OpenAsync();
                    using var reader = await command.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        // Read all columns into dictionary
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);
                            var value = reader.GetValue(i);
                            status[name] = value == DBNull.Value ? null : value;
                        }
                    }

                    _logger.Information("Retrieved sync status with {MetricCount} metrics", status.Count);
                    return true;
                });
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error retrieving sync status");
                throw;
            }

            return status;
        }
    }
}
