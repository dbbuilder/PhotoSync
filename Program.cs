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
        /// <param name="args">Command line arguments: [command] [optional path] [-settings path]</param>
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

                // Parse command line arguments
                var parsedArgs = ParseCommandLineArguments(args);
                if (!parsedArgs.IsValid)
                {
                    ShowUsage();
                    return 1;
                }

                // Build configuration from appsettings.json files and optional settings override
                var configuration = BuildConfiguration(parsedArgs.SettingsOverridePath);
                
                // Print connection string in development mode for debugging
                PrintDebugInfo(configuration);
                
                // Configure dependency injection container
                var serviceProvider = ConfigureServices(configuration);
                
                Log.Information("Executing command: {Command} with folder: {Folder}", 
                    parsedArgs.Command, parsedArgs.FolderPath ?? "[default from config]");

                // Route to appropriate command handler
                var exitCode = await ExecuteCommandAsync(serviceProvider, parsedArgs);
                
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
        /// Parses command line arguments into a structured format
        /// Supports: [command] [optional folder path] [-settings override-file-path] [additional flags]
        /// </summary>
        /// <param name="args">Raw command line arguments</param>
        /// <returns>Parsed argument structure</returns>
        private static ParsedArguments ParseCommandLineArguments(string[] args)
        {
            var result = new ParsedArguments();
            
            if (args.Length == 0)
            {
                result.IsValid = false;
                return result;
            }

            // First argument is always the command
            result.Command = args[0].ToLowerInvariant();
            
            // Process remaining arguments
            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                
                if (arg.ToLowerInvariant() == "-settings" || arg.ToLowerInvariant() == "--settings")
                {
                    // Next argument should be the settings file path
                    if (i + 1 < args.Length)
                    {
                        i++; // Move to next argument
                        result.SettingsOverridePath = args[i];
                        Log.Information("Settings override file specified: {SettingsPath}", result.SettingsOverridePath);
                    }
                    else
                    {
                        Log.Error("Settings argument specified but no file path provided");
                        result.IsValid = false;
                        return result;
                    }
                }
                else if (arg.ToLowerInvariant() == "-workflow" || arg.ToLowerInvariant() == "--workflow")
                {
                    // Next argument should be the workflow steps
                    if (i + 1 < args.Length)
                    {
                        i++; // Move to next argument
                        result.AdditionalArgs["workflow"] = args[i];
                        Log.Information("Workflow specified: {Workflow}", args[i]);
                    }
                }
                else if (arg.ToLowerInvariant() == "-skiparchive" || arg.ToLowerInvariant() == "--skiparchive")
                {
                    result.Flags.Add("skiparchive");
                    Log.Information("Skip archive flag set");
                }
                else if (arg.ToLowerInvariant() == "-dryrun" || arg.ToLowerInvariant() == "--dryrun")
                {
                    result.Flags.Add("dryrun");
                    Log.Information("Dry run flag set");
                }
                else if (arg.ToLowerInvariant() == "-incremental" || arg.ToLowerInvariant() == "--incremental")
                {
                    result.Flags.Add("incremental");
                    Log.Information("Incremental flag set");
                }
                else if (arg.ToLowerInvariant() == "-force" || arg.ToLowerInvariant() == "--force")
                {
                    result.Flags.Add("force");
                    Log.Information("Force flag set");
                }
                else if (arg.ToLowerInvariant() == "-detailed" || arg.ToLowerInvariant() == "--detailed")
                {
                    result.Flags.Add("detailed");
                    Log.Information("Detailed flag set");
                }
                else if (!arg.StartsWith("-") && string.IsNullOrEmpty(result.FolderPath))
                {
                    // Non-flag argument is treated as folder path (if not already set)
                    result.FolderPath = arg;
                }
                else if (arg.StartsWith("-"))
                {
                    Log.Warning("Unknown argument: {Argument}", arg);
                    // Continue processing - don't fail on unknown arguments
                }
            }

            result.IsValid = !string.IsNullOrEmpty(result.Command);
            
            Log.Information("Parsed arguments - Command: {Command}, Folder: {Folder}, Settings: {Settings}, Flags: {Flags}", 
                result.Command, 
                result.FolderPath ?? "[none]", 
                result.SettingsOverridePath ?? "[none]",
                string.Join(", ", result.Flags));
                
            return result;
        }

        /// <summary>
        /// Structure to hold parsed command line arguments
        /// </summary>
        private class ParsedArguments
        {
            public bool IsValid { get; set; } = true;
            public string Command { get; set; } = string.Empty;
            public string? FolderPath { get; set; }
            public string? SettingsOverridePath { get; set; }
            public HashSet<string> Flags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> AdditionalArgs { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds application configuration from appsettings.json files and environment variables
        /// Supports Development, Production environment-specific configurations
        /// Optionally applies settings overrides from a specified file
        /// </summary>
        /// <param name="settingsOverridePath">Optional path to settings override file</param>
        /// <returns>Built configuration instance</returns>
        private static IConfiguration BuildConfiguration(string? settingsOverridePath = null)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("PHOTOSYNC_"); // Allow environment variable overrides with PHOTOSYNC_ prefix

            // Add settings override file if specified
            if (!string.IsNullOrEmpty(settingsOverridePath))
            {
                if (File.Exists(settingsOverridePath))
                {
                    Log.Information("Applying settings override from: {SettingsPath}", settingsOverridePath);
                    configBuilder.AddJsonFile(settingsOverridePath, optional: false, reloadOnChange: false);
                }
                else
                {
                    Log.Error("Settings override file not found: {SettingsPath}", settingsOverridePath);
                    throw new FileNotFoundException($"Settings override file not found: {settingsOverridePath}");
                }
            }

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
                Console.WriteLine($"  PhotoFieldName: {configuration["PhotoSettings:PhotoFieldName"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine($"  AzureStoragePathFieldName: {configuration["PhotoSettings:AzureStoragePathFieldName"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine();
                
                // Print Azure Storage settings
                Console.WriteLine("Azure Storage Settings:");
                Console.WriteLine($"  ContainerName: {configuration["AzureStorage:ContainerName"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine($"  UseDefaultAzureCredential: {configuration["AzureStorage:UseDefaultAzureCredential"] ?? "[NOT CONFIGURED]"}");
                Console.WriteLine($"  StorageAccountName: {configuration["AzureStorage:StorageAccountName"] ?? "[NOT CONFIGURED]"}");
                var hasConnString = !string.IsNullOrEmpty(configuration["AzureStorage:ConnectionString"]);
                Console.WriteLine($"  ConnectionString: {(hasConnString ? "[CONFIGURED]" : "[NOT CONFIGURED]")}");
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
            services.AddSingleton(appSettings.AzureStorage);

            // Register application services with their interfaces
            services.AddScoped<IDatabaseService, DatabaseService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IAzureStorageService, AzureStorageService>();
            
            // Register command handlers
            services.AddScoped<ImportCommand>();
            services.AddScoped<ExportCommand>();
            services.AddScoped<ToAzureStorageCommand>();
            services.AddScoped<FromAzureStorageCommand>();
            services.AddScoped<WriteAllCommand>();
            services.AddScoped<SyncStatusCommand>();

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
        /// Supports: import, export, status, test, syncstatus, and Azure commands
        /// </summary>
        /// <param name="serviceProvider">Dependency injection service provider</param>
        /// <param name="parsedArgs">Parsed command line arguments</param>
        /// <returns>Exit code: 0 for success, 1 for error</returns>
        private static async Task<int> ExecuteCommandAsync(ServiceProvider serviceProvider, ParsedArguments parsedArgs)
        {
            var command = parsedArgs.Command;
            try
            {
                var folderPath = parsedArgs.FolderPath;
                
                switch (command)
                {
                    case "import":
                        Log.Information("Starting import command");
                        var importCommand = serviceProvider.GetRequiredService<ImportCommand>();
                        var skipArchive = parsedArgs.Flags.Contains("skiparchive");
                        var importResult = await importCommand.ExecuteAsync(folderPath, skipArchive);
                        Console.WriteLine(importResult.Summary);
                        if (!string.IsNullOrEmpty(importResult.ErrorMessage))
                        {
                            Console.WriteLine($"Error: {importResult.ErrorMessage}");
                        }
                        return importResult.IsSuccess ? 0 : 1;

                    case "export":
                        Log.Information("Starting export command");
                        var exportCommand = serviceProvider.GetRequiredService<ExportCommand>();
                        var incremental = parsedArgs.Flags.Contains("incremental");
                        var force = parsedArgs.Flags.Contains("force");
                        var exportResult = await exportCommand.ExecuteAsync(folderPath, incremental, force);
                        Console.WriteLine(exportResult.Summary);
                        if (!string.IsNullOrEmpty(exportResult.ErrorMessage))
                        {
                            Console.WriteLine($"Error: {exportResult.ErrorMessage}");
                        }
                        return exportResult.IsSuccess ? 0 : 1;

                    case "status":
                        Log.Information("Starting status command");
                        return await ExecuteStatusCommandAsync(serviceProvider);

                    case "syncstatus":
                        Log.Information("Starting syncstatus command");
                        var syncStatusCommand = serviceProvider.GetRequiredService<SyncStatusCommand>();
                        var detailed = parsedArgs.Flags.Contains("detailed");
                        return await syncStatusCommand.ExecuteAsync(detailed);

                    case "test":
                        Log.Information("Starting test command");
                        return await ExecuteTestCommandAsync(serviceProvider);

                    case "diagnose":
                        Log.Information("Starting database diagnostic");
                        return await ExecuteDiagnoseCommandAsync(serviceProvider);

                    case "toazurestorage":
                    case "-toazurestorage":
                        Log.Information("Starting toazurestorage command");
                        var toAzureCommand = serviceProvider.GetRequiredService<ToAzureStorageCommand>();
                        var toAzureForce = parsedArgs.Flags.Contains("force");
                        var toAzureResult = await toAzureCommand.ExecuteAsync(toAzureForce);
                        Console.WriteLine(toAzureResult.Summary);
                        if (!string.IsNullOrEmpty(toAzureResult.ErrorMessage))
                        {
                            Console.WriteLine($"Error: {toAzureResult.ErrorMessage}");
                        }
                        return toAzureResult.IsSuccess ? 0 : 1;

                    case "fromazurestorage":
                    case "-fromazurestorage":
                        Log.Information("Starting fromazurestorage command");
                        var fromAzureCommand = serviceProvider.GetRequiredService<FromAzureStorageCommand>();
                        var fromAzureResult = await fromAzureCommand.ExecuteAsync();
                        Console.WriteLine(fromAzureResult.Summary);
                        if (!string.IsNullOrEmpty(fromAzureResult.ErrorMessage))
                        {
                            Console.WriteLine($"Error: {fromAzureResult.ErrorMessage}");
                        }
                        return fromAzureResult.IsSuccess ? 0 : 1;

                    case "writeall":
                    case "-writeall":
                        Log.Information("Starting writeall command");
                        var writeAllCommand = serviceProvider.GetRequiredService<WriteAllCommand>();
                        // Check if there's an additional argument for field to nullify
                        string? fieldToNullify = null;
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            // In this case, folderPath is actually the field name
                            fieldToNullify = folderPath;
                        }
                        var workflow = parsedArgs.AdditionalArgs.ContainsKey("workflow") ? parsedArgs.AdditionalArgs["workflow"] : "import,azure,export";
                        var skipArchiveFlag = parsedArgs.Flags.Contains("skiparchive");
                        var dryRun = parsedArgs.Flags.Contains("dryrun");
                        var writeAllResult = await writeAllCommand.ExecuteAsync(fieldToNullify, workflow, skipArchiveFlag, dryRun);
                        Console.WriteLine(writeAllResult.Summary);
                        if (!string.IsNullOrEmpty(writeAllResult.ErrorMessage))
                        {
                            Console.WriteLine($"Error: {writeAllResult.ErrorMessage}");
                        }
                        return writeAllResult.IsSuccess ? 0 : 1;

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
            Console.WriteLine("  PhotoSync <command> [folder-path] [-settings override-file]");
            Console.WriteLine();
            Console.WriteLine("COMMANDS:");
            Console.WriteLine("  import [path]        Import JPG files from folder to database");
            Console.WriteLine("                       Uses configured ImportFolder if path not specified");
            Console.WriteLine("                       Options: -skiparchive");
            Console.WriteLine();
            Console.WriteLine("  export [path]        Export images from database to folder as JPG files");
            Console.WriteLine("                       Uses configured ExportFolder if path not specified");
            Console.WriteLine("                       Options: -incremental, -force");
            Console.WriteLine();
            Console.WriteLine("  status               Check database connectivity and image count");
            Console.WriteLine();
            Console.WriteLine("  syncstatus           Display comprehensive sync status and statistics");
            Console.WriteLine("                       Options: -detailed");
            Console.WriteLine();
            Console.WriteLine("  test                 Validate configuration and folder access");
            Console.WriteLine();
            Console.WriteLine("  diagnose             Run detailed database connection diagnostic");
            Console.WriteLine();
            Console.WriteLine("  toazurestorage       Upload images to Azure Storage where AzureStoragePath is NULL");
            Console.WriteLine("                       Options: -force (sync images with AzureSyncRequired=1)");
            Console.WriteLine();
            Console.WriteLine("  fromazurestorage     Download images from Azure Storage where ImageData is NULL");
            Console.WriteLine();
            Console.WriteLine("  writeall [field]     Process all NULL values that can be fixed");
            Console.WriteLine("                       Optional: specify field to nullify first (ImageData or AzureStoragePath)");
            Console.WriteLine("                       Options: -workflow <steps>, -skiparchive, -dryrun");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  -settings <file>     Override connection and photo settings from file");
            Console.WriteLine("                       File should contain ConnectionStrings and/or PhotoSettings");
            Console.WriteLine();
            Console.WriteLine("  -workflow <steps>    Specify workflow steps (default: import,azure,export)");
            Console.WriteLine("                       Examples: \"import\", \"import,azure\", \"azure,export\"");
            Console.WriteLine();
            Console.WriteLine("  -incremental         Only process new/changed items");
            Console.WriteLine("  -force               Force operation even if already processed");
            Console.WriteLine("  -skiparchive         Skip archiving imported files");
            Console.WriteLine("  -dryrun              Preview operations without making changes");
            Console.WriteLine("  -detailed            Show detailed information");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  PhotoSync import");
            Console.WriteLine("  PhotoSync import \"C:\\MyPhotos\" -skiparchive");
            Console.WriteLine("  PhotoSync export \"C:\\ExportedPhotos\" -incremental");
            Console.WriteLine("  PhotoSync status");
            Console.WriteLine("  PhotoSync syncstatus");
            Console.WriteLine("  PhotoSync syncstatus -detailed");
            Console.WriteLine("  PhotoSync test");
            Console.WriteLine("  PhotoSync diagnose");
            Console.WriteLine("  PhotoSync import -settings \"C:\\Config\\prod-settings.json\"");
            Console.WriteLine("  PhotoSync toazurestorage");
            Console.WriteLine("  PhotoSync toazurestorage -force");
            Console.WriteLine("  PhotoSync fromazurestorage");
            Console.WriteLine("  PhotoSync writeall");
            Console.WriteLine("  PhotoSync writeall ImageData");
            Console.WriteLine("  PhotoSync writeall -workflow \"import,export\" -dryrun");
            Console.WriteLine("  PhotoSync writeall AzureStoragePath -workflow \"azure\"");
            Console.WriteLine();
            Console.WriteLine("For more information, see README.md");

            Log.Information("Usage information displayed to user");
        }
    }
}
