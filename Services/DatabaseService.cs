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
                    using var command = new SqlCommand("sp_SaveImage", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 300 // 5 minutes for large images
                    };

                    // Add parameters for the stored procedure
                    command.Parameters.Add(new SqlParameter("@Code", SqlDbType.NVarChar, 100) 
                        { Value = imageRecord.Code });
                    command.Parameters.Add(new SqlParameter("@ImageData", SqlDbType.VarBinary, -1) 
                        { Value = imageRecord.ImageData });
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
                    using var command = new SqlCommand("sp_GetAllImages", connection)
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
                            ImageData = (byte[])reader["ImageData"],
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?
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
                    using var command = new SqlCommand("sp_GetImageByCode", connection)
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
                            ImageData = (byte[])reader["ImageData"],
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            ModifiedDate = reader["ModifiedDate"] as DateTime?
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
                    using var command = new SqlCommand("sp_GetImageCount", connection)
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
                    using var command = new SqlCommand("sp_DeleteImage", connection)
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
    }
}
