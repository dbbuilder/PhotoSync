using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhotoSync.Commands;
using PhotoSync.Configuration;
using PhotoSync.Services;
using Serilog;
using System.Reflection;

namespace PhotoSync
{
    /// <summary>
    /// PhotoSync Console Application - Main Entry Point
    /// Handles photo import/export operations between file system and SQL Server database
    /// </summary>
    class Program
    {
        /// <summary>
        /// Main application entry point
        /// Processes command line arguments and routes to appropriate command handlers
        /// </summary>
        /// <param name="args">Command line arguments: [command] [optional path]</param>
        /// <returns>Exit code: 0 for success, 1 for error</returns>
        static async Task<int> Main(string[] args)
        {
            // Configure Serilog early for startup logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/photosync-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("PhotoSync application starting...");
                Log.Information("Command line arguments: {Args}", string.Join(" ", args));

                // Build configuration from appsettings.json files
                var configuration = BuildConfiguration();
                
                // Print connection string in development mode for debugging
                PrintDebugInfo(configuration);
                
                // Configure dependency injection container
                var serviceProvider = ConfigureServices(configuration);
                
                // Validate command line arguments
                if (args.Length == 0)
                {
                    ShowUsage();
                    return 1;
                }

                var command = args[0].ToLowerInvariant();
                var folderPath = args.Length > 1 ? args[1] : null;

                Log.Information("Executing command: {Command} with folder: {Folder}", command, folderPath ?? "[default from config]");

                // Route to appropriate command handler
                var exitCode = await ExecuteCommandAsync(serviceProvider, command, folderPath);
                
                Log.Information("PhotoSync application completed with exit code: {ExitCode}", exitCode);
                return exitCode;
            }
            catch (System.Exception ex)
            {
                Log.Fatal(ex, "PhotoSync application terminated unexpectedly");
                return 1;
            }
            finally
            {
                // Ensure all logs are flushed before application exit
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Builds application configuration from appsettings.json files and environment variables
        /// Supports Development, Production environment-specific configurations
        /// </summary>
        /// <returns>Built configuration instance</returns>
        private static IConfiguration BuildConfiguration()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("PHOTOSYNC_"); // Allow environment variable overrides with PHOTOSYNC_ prefix

            // Add Azure Key Vault in Production environment (requires proper Azure configuration)
            if (environment == "Production")
            {
                Log.Information("Production environment detected - Azure Key Vault integration available");
                // Azure Key Vault will be configured when deployed to Azure with Managed Identity
            }

            var configuration = configBuilder.Build();
            Log.Information("Configuration loaded for environment: {Environment}", environment);
            return configuration;
        }

        /// <summary>
        /// Prints debug information in development mode
        /// Shows connection string and configuration for troubleshooting
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        private static void PrintDebugInfo(IConfiguration configuration)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            
            // Only print debug info in Development mode
            if (environment == "Development")
            {
                Console.WriteLine("=== PhotoSync Debug Information ===");
                Console.WriteLine($"Environment: {environment}");
                Console.WriteLine();
                
                // Print connection string for debugging
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                Console.WriteLine("Connection String:");
                Console.WriteLine($"  {connectionString ?? "[NOT CONFIGURED]"}");
                Console.WriteLine();
                
                // Print photo settings
                Console.WriteLine("Photo Settings:");
                Console.WriteLine($"  TableName: {configuration["PhotoSettings:TableName"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine($"  ImageFieldName: {configuration["PhotoSettings:ImageFieldName"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine($"  CodeFieldName: {configuration["PhotoSettings:CodeFieldName"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine($"  ImportFolder: {configuration["PhotoSettings:ImportFolder"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine($"  ExportFolder: {configuration["PhotoSettings:ExportFolder"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine();
                
                // Print configuration files being used
                var configSources = ((IConfigurationRoot)configuration).Providers
                    .Where(p => p.GetType().Name.Contains("Json"))
                    .Select(p => p.ToString())
                    .ToArray();
                
                if (configSources.Any())
                {
                    Console.WriteLine("Configuration Sources:");
                    foreach (var source in configSources)
                    {
                        Console.WriteLine($"  {source}");
                    }
                    Console.WriteLine();
                }
                
                Console.WriteLine("====================================");
                Console.WriteLine();
                
                // Also log this information
                Log.Information("Debug mode - Connection string: {ConnectionString}", connectionString);
                Log.Information("Debug mode - Import folder: {ImportFolder}, Export folder: {ExportFolder}", 
                    configuration["PhotoSettings:ImportFolder"], 
                    configuration["PhotoSettings:ExportFolder"]);
            }
        }

        /// <summary>
        /// Configures dependency injection container with all required services
        /// Sets up interfaces, implementations, and configuration binding
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <returns>Configured service provider</returns>
        private static ServiceProvider ConfigureServices(IConfiguration configuration)
        {
            var services = new ServiceCollection();

            // Bind configuration sections to strongly-typed classes
            var appSettings = new AppSettings();
            configuration.Bind(appSettings);
            services.AddSingleton(appSettings);
            services.AddSingleton(appSettings.PhotoSettings);
            services.AddSingleton(appSettings.ConnectionStrings);

            // Register application services with their interfaces
            services.AddScoped<IDatabaseService, DatabaseService>();
            services.AddScoped<IFileService, FileService>();
            
            // Register command handlers
            services.AddScoped<ImportCommand>();
            services.AddScoped<ExportCommand>();

            // Configure Serilog with settings from configuration
            services.AddSingleton<ILogger>(provider =>
            {
                return new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();
            });

            var serviceProvider = services.BuildServiceProvider();
            Log.Information("Dependency injection container configured successfully");
            return serviceProvider;
        }

        /// <summary>
        /// Routes command line arguments to appropriate command handlers
        /// Supports: import, export, status, test commands
        /// </summary>
        /// <param name="serviceProvider">Dependency injection service provider</param>
        /// <param name="command">Command to execute (import, export, status, test)</param>
        /// <param name="folderPath">Optional folder path override</param>
        /// <returns>Exit code: 0 for success, 1 for error</returns>
        private static async Task<int> ExecuteCommandAsync(ServiceProvider serviceProvider, string command, string? folderPath)
        {
            try
            {
                switch (command)
                {
                    case "import":
                        Log.Information("Starting import command");
                        var importCommand = serviceProvider.GetRequiredService<ImportCommand>();
                        var importResult = await importCommand.ExecuteAsync(folderPath);
                        Console.WriteLine(importResult.Summary);
                        if (!string.IsNullOrEmpty(importResult.ErrorMessage))
                        {
                            Console.WriteLine($"Error: {importResult.ErrorMessage}");
                        }
                        return importResult.IsSuccess ? 0 : 1;

                    case "export":
                        Log.Information("Starting export command");
                        var exportCommand = serviceProvider.GetRequiredService<ExportCommand>();
                        var exportResult = await exportCommand.ExecuteAsync(folderPath);
                        Console.WriteLine($"Exported {exportResult.SuccessCount} images in {exportResult.Duration:mm\\:ss}");
                        if (!string.IsNullOrEmpty(exportResult.ErrorMessage))
                        {
                            Console.WriteLine($"Error: {exportResult.ErrorMessage}");
                        }
                        return exportResult.IsSuccess ? 0 : 1;

                    case "status":
                        Log.Information("Starting status command");
                        return await ExecuteStatusCommandAsync(serviceProvider);

                    case "test":
                        Log.Information("Starting test command");
                        return await ExecuteTestCommandAsync(serviceProvider);

                    case "diagnose":
                        Log.Information("Starting database diagnostic");
                        return await ExecuteDiagnoseCommandAsync(serviceProvider);

                    default:
                        Log.Error("Unknown command: {Command}", command);
                        Console.WriteLine($"Unknown command: {command}");
                        ShowUsage();
                        return 1;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error executing command: {Command}", command);
                Console.WriteLine($"Error executing command '{command}': {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Executes the status command to check database connectivity and image count
        /// </summary>
        /// <param name="serviceProvider">Dependency injection service provider</param>
        /// <returns>Exit code: 0 for success, 1 for error</returns>
        private static async Task<int> ExecuteStatusCommandAsync(ServiceProvider serviceProvider)
        {
            try
            {
                var databaseService = serviceProvider.GetRequiredService<IDatabaseService>();
                var photoSettings = serviceProvider.GetRequiredService<PhotoSettings>();

                Console.WriteLine("PhotoSync Status Check");
                Console.WriteLine("=====================");

                // Test database connection
                Console.Write("Database connection: ");
                var canConnect = await databaseService.TestConnectionAsync();
                Console.WriteLine(canConnect ? "✓ Connected" : "✗ Failed");

                if (canConnect)
                {
                    // Get image count from database
                    Console.Write("Image count: ");
                    var imageCount = await databaseService.GetImageCountAsync();
                    Console.WriteLine($"{imageCount} images");
                }

                // Check folder configurations
                Console.WriteLine($"Import folder: {photoSettings.ImportFolder}");
                Console.WriteLine($"Export folder: {photoSettings.ExportFolder}");
                Console.WriteLine($"Table name: {photoSettings.TableName}");

                Log.Information("Status command completed successfully");
                return canConnect ? 0 : 1;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error during status check");
                Console.WriteLine($"Error during status check: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Executes the test command to validate configuration and folder access
        /// </summary>
        /// <param name="serviceProvider">Dependency injection service provider</param>
        /// <returns>Exit code: 0 for success, 1 for error</returns>
        private static async Task<int> ExecuteTestCommandAsync(ServiceProvider serviceProvider)
        {
            try
            {
                var databaseService = serviceProvider.GetRequiredService<IDatabaseService>();
                var fileService = serviceProvider.GetRequiredService<IFileService>();
                var photoSettings = serviceProvider.GetRequiredService<PhotoSettings>();
                var connectionStrings = serviceProvider.GetRequiredService<ConnectionStrings>();

                Console.WriteLine("PhotoSync Configuration Test");
                Console.WriteLine("===========================");

                var allTestsPassed = true;

                // Test 1: Configuration validation
                Console.Write("Configuration validation: ");
                if (string.IsNullOrEmpty(connectionStrings.DefaultConnection))
                {
                    Console.WriteLine("✗ Connection string is missing");
                    allTestsPassed = false;
                }
                else if (string.IsNullOrEmpty(photoSettings.TableName))
                {
                    Console.WriteLine("✗ Table name is missing");
                    allTestsPassed = false;
                }
                else
                {
                    Console.WriteLine("✓ Valid");
                }

                // Test 2: Database connection
                Console.Write("Database connection: ");
                var canConnect = await databaseService.TestConnectionAsync();
                Console.WriteLine(canConnect ? "✓ Connected" : "✗ Failed");
                allTestsPassed = allTestsPassed && canConnect;

                // Test 3: Import folder access
                Console.Write("Import folder access: ");
                var importFolderExists = fileService.ValidateFolderAccess(photoSettings.ImportFolder);
                Console.WriteLine(importFolderExists ? "✓ Accessible" : "✗ Not accessible");
                allTestsPassed = allTestsPassed && importFolderExists;

                // Test 4: Export folder access (create if missing)
                Console.Write("Export folder access: ");
                try
                {
                    fileService.EnsureDirectoryExists(photoSettings.ExportFolder);
                    var exportFolderExists = fileService.ValidateFolderAccess(photoSettings.ExportFolder);
                    Console.WriteLine(exportFolderExists ? "✓ Accessible" : "✗ Cannot create/access");
                    allTestsPassed = allTestsPassed && exportFolderExists;
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine($"✗ Cannot create/access: {ex.Message}");
                    allTestsPassed = false;
                }

                Console.WriteLine($"Overall test result: {(allTestsPassed ? "✓ PASSED" : "✗ FAILED")}");

                Log.Information("Test command completed with result: {TestResult}", allTestsPassed ? "PASSED" : "FAILED");
                return allTestsPassed ? 0 : 1;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error during configuration test");
                Console.WriteLine($"Error during configuration test: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Executes the diagnose command to troubleshoot database connection issues
        /// </summary>
        /// <param name="serviceProvider">Dependency injection service provider</param>
        /// <returns>Exit code: 0 for success, 1 for error</returns>
        private static async Task<int> ExecuteDiagnoseCommandAsync(ServiceProvider serviceProvider)
        {
            try
            {
                var connectionStrings = serviceProvider.GetRequiredService<ConnectionStrings>();
                
                if (string.IsNullOrEmpty(connectionStrings.DefaultConnection))
                {
                    Console.WriteLine("ERROR: No connection string configured");
                    return 1;
                }
                
                await DatabaseDiagnostic.DiagnoseConnectionAsync(connectionStrings.DefaultConnection);
                
                Log.Information("Database diagnostic completed");
                return 0;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error during database diagnostic");
                Console.WriteLine($"Error during diagnostic: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Displays usage information and command examples
        /// </summary>
        private static void ShowUsage()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            
            Console.WriteLine($"PhotoSync v{version} - Photo Import/Export Tool");
            Console.WriteLine("===============================================");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  PhotoSync <command> [folder-path]");
            Console.WriteLine();
            Console.WriteLine("COMMANDS:");
            Console.WriteLine("  import [path]   Import JPG files from folder to database");
            Console.WriteLine("                  Uses configured ImportFolder if path not specified");
            Console.WriteLine();
            Console.WriteLine("  export [path]   Export images from database to folder as JPG files");
            Console.WriteLine("                  Uses configured ExportFolder if path not specified");
            Console.WriteLine();
            Console.WriteLine("  status          Check database connectivity and image count");
            Console.WriteLine();
            Console.WriteLine("  test            Validate configuration and folder access");
            Console.WriteLine();
            Console.WriteLine("  diagnose        Run detailed database connection diagnostic");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  PhotoSync import");
            Console.WriteLine("  PhotoSync import \"C:\\MyPhotos\"");
            Console.WriteLine("  PhotoSync export \"C:\\ExportedPhotos\"");
            Console.WriteLine("  PhotoSync status");
            Console.WriteLine("  PhotoSync test");
            Console.WriteLine("  PhotoSync diagnose");
            Console.WriteLine();
            Console.WriteLine("For more information, see README.md");

            Log.Information("Usage information displayed to user");
        }
    }
}
