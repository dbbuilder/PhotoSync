using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PhotoSync.Tests.Helpers
{
    /// <summary>
    /// Helper class for managing test database creation and cleanup
    /// </summary>
    public static class TestDatabaseHelper
    {
        private const string MasterConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=true;";
        
        /// <summary>
        /// Creates a test database with the specified name
        /// </summary>
        public static async Task<string> CreateTestDatabaseAsync(string databaseName)
        {
            var connectionString = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=true;";
            
            // Create the database
            using (var connection = new SqlConnection(MasterConnectionString))
            {
                await connection.OpenAsync();
                
                // Drop if exists
                var dropCommand = new SqlCommand($@"
                    IF EXISTS (SELECT name FROM master.dbo.sysdatabases WHERE name = '{databaseName}')
                    BEGIN
                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                        DROP DATABASE [{databaseName}]
                    END", connection);
                await dropCommand.ExecuteNonQueryAsync();
                
                // Create new database
                var createCommand = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
                await createCommand.ExecuteNonQueryAsync();
            }
            
            // Initialize schema and procedures
            await InitializeDatabaseSchemaAsync(connectionString);
            
            return connectionString;
        }
        
        /// <summary>
        /// Initializes the database schema using the SQL script
        /// </summary>
        private static async Task InitializeDatabaseSchemaAsync(string connectionString)
        {
            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDatabase", "CreateTestDatabase.sql");
            if (!File.Exists(scriptPath))
            {
                // If running from different directory, try to find it
                scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "PhotoSync.Tests", "TestDatabase", "CreateTestDatabase.sql");
                if (!File.Exists(scriptPath))
                {
                    throw new FileNotFoundException($"Test database script not found at: {scriptPath}");
                }
            }
            
            var script = await File.ReadAllTextAsync(scriptPath);
            
            // Split by GO statements
            var batches = script.Split(new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO", "\nGO", "GO\r\n", "GO\n" }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                
                using var command = new SqlCommand(batch, connection);
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (SqlException ex)
                {
                    throw new Exception($"Error executing batch: {batch.Substring(0, Math.Min(100, batch.Length))}...", ex);
                }
            }
        }
        
        /// <summary>
        /// Drops the test database
        /// </summary>
        public static async Task DropTestDatabaseAsync(string databaseName)
        {
            using var connection = new SqlConnection(MasterConnectionString);
            await connection.OpenAsync();
            
            var command = new SqlCommand($@"
                IF EXISTS (SELECT name FROM master.dbo.sysdatabases WHERE name = '{databaseName}')
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                    DROP DATABASE [{databaseName}]
                END", connection);
            
            await command.ExecuteNonQueryAsync();
        }
        
        /// <summary>
        /// Inserts test data into the database
        /// </summary>
        public static async Task InsertTestDataAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand("[PHOTOS].[InsertTestData]", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            await command.ExecuteNonQueryAsync();
        }
        
        /// <summary>
        /// Cleans up test data from the database
        /// </summary>
        public static async Task CleanupTestDataAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand("[PHOTOS].[CleanupTestData]", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            await command.ExecuteNonQueryAsync();
        }
        
        /// <summary>
        /// Checks if LocalDB is available
        /// </summary>
        public static async Task<bool> IsLocalDbAvailableAsync()
        {
            try
            {
                using var connection = new SqlConnection(MasterConnectionString);
                await connection.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}