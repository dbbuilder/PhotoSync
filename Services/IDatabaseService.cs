using PhotoSync.Models;

namespace PhotoSync.Services
{
    /// <summary>
    /// Interface for database operations related to image storage
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// Inserts or updates an image record in the database
        /// </summary>
        /// <param name="imageRecord">Image record to save</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SaveImageAsync(ImageRecord imageRecord);

        /// <summary>
        /// Retrieves all image records from the database
        /// </summary>
        /// <returns>List of image records</returns>
        Task<List<ImageRecord>> GetAllImagesAsync();

        /// <summary>
        /// Retrieves a specific image by code
        /// </summary>
        /// <param name="code">Image code to search for</param>
        /// <returns>Image record if found, null otherwise</returns>
        Task<ImageRecord?> GetImageByCodeAsync(string code);

        /// <summary>
        /// Tests the database connection
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// Gets count of images in the database
        /// </summary>
        /// <returns>Number of image records</returns>
        Task<int> GetImageCountAsync();

        /// <summary>
        /// Deletes an image by code
        /// </summary>
        /// <param name="code">Image code to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteImageAsync(string code);
    }
}
