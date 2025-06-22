using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System;

namespace PhotoSync.Tests.Fixtures
{
    /// <summary>
    /// Base class for test fixtures providing common test infrastructure
    /// </summary>
    public abstract class TestFixtureBase : IDisposable
    {
        protected IServiceProvider ServiceProvider { get; }
        protected IConfiguration Configuration { get; }
        protected ILogger TestLogger { get; }

        protected TestFixtureBase()
        {
            // Create test logger
            TestLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug)
                .CreateLogger();

            // Build configuration
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.test.json", optional: false, reloadOnChange: true)
                .Build();

            // Build service provider
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Override to configure services for testing
        /// </summary>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(Configuration);
            services.AddSingleton<ILogger>(TestLogger);
        }

        public virtual void Dispose()
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}