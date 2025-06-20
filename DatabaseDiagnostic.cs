using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace PhotoSync
{
    /// <summary>
    /// Quick database connection diagnostic tool
    /// Tests both master database and PhotoDB to identify issues
    /// </summary>
    public class DatabaseDiagnostic
    {
        /// <summary>
        /// Tests database connectivity and diagnoses common issues
        /// </summary>
        /// <param name="baseConnectionString">Connection string to test</param>
        public static async Task DiagnoseConnectionAsync(string baseConnectionString)
        {
            Console.WriteLine("=== Database Connection Diagnostic ===");
            Console.WriteLine();

            // Test 1: Can we connect to master database with these credentials?
            var masterConnectionString = baseConnectionString.Replace("Database=PhotoDB", "Database=master");
            
            Console.Write("1. Testing SA credentials against master database: ");
            try
            {
                using var masterConnection = new SqlConnection(masterConnectionString);
                await masterConnection.OpenAsync();
                Console.WriteLine("✓ SUCCESS - SA credentials are valid");
                
                // Test SQL Server version
                using var versionCommand = new SqlCommand("SELECT @@VERSION", masterConnection);
                var version = (string)await versionCommand.ExecuteScalarAsync();
                Console.WriteLine($"   SQL Server Version: {version.Split('\n')[0]}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗ FAILED - SA credentials are invalid");
                Console.WriteLine($"   Error: {ex.Message}");
                return; // No point testing PhotoDB if SA credentials don't work
            }
            
            Console.WriteLine();
            
            // Test 2: Does PhotoDB database exist?
            Console.Write("2. Checking if PhotoDB database exists: ");
            try
            {
                using var masterConnection = new SqlConnection(masterConnectionString);
                await masterConnection.OpenAsync();
                
                using var checkDbCommand = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.databases WHERE name = 'PhotoDB'", 
                    masterConnection);
                var dbExists = (int)await checkDbCommand.ExecuteScalarAsync() > 0;
                
                if (dbExists)
                {
                    Console.WriteLine("✓ PhotoDB database exists");
                }
                else
                {
                    Console.WriteLine("✗ PhotoDB database does NOT exist");
                    Console.WriteLine("   SOLUTION: Create PhotoDB database first");
                    Console.WriteLine("   Run: Scripts\\Setup-Database.ps1 -CreateDatabase");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗ FAILED to check database existence");
                Console.WriteLine($"   Error: {ex.Message}");
                return;
            }
            
            Console.WriteLine();
            
            // Test 3: Can we connect to PhotoDB specifically?
            Console.Write("3. Testing connection to PhotoDB database: ");
            try
            {
                using var photoDbConnection = new SqlConnection(baseConnectionString);
                await photoDbConnection.OpenAsync();
                Console.WriteLine("✓ SUCCESS - Can connect to PhotoDB");
                
                // Test if Photos table exists
                using var checkTableCommand = new SqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Photos'", 
                    photoDbConnection);
                var tableExists = (int)await checkTableCommand.ExecuteScalarAsync() > 0;
                
                if (tableExists)
                {
                    Console.WriteLine("   ✓ Photos table exists");
                    
                    // Count existing records
                    using var countCommand = new SqlCommand("SELECT COUNT(*) FROM Photos", photoDbConnection);
                    var recordCount = (int)await countCommand.ExecuteScalarAsync();
                    Console.WriteLine($"   ✓ Found {recordCount} existing photo records");
                }
                else
                {
                    Console.WriteLine("   ✗ Photos table does NOT exist");
                    Console.WriteLine("   SOLUTION: Run database setup script");
                    Console.WriteLine("   Run: Scripts\\Setup-Database.ps1");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗ FAILED to connect to PhotoDB");
                Console.WriteLine($"   Error: {ex.Message}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== End Diagnostic ===");
        }
    }
}
