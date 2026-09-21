/*
 * Key aspects applied in this design
 * XML Security: DtdProcessing.Ignore prevents vulnerabilities associated with resolving external DTD entities.
 * HTTP Efficiency: Using HttpCompletionOption.ResponseHeadersRead with ReadAsStreamAsync processes the content directly in memory via streams without loading massive strings into memory.
 * CancellationToken: Allows canceling pending downloads if the user closes the application or changes views.
*/


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Avalonia;

// 1. Configure Serilog as the global logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/rss_reader_.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting RSS Reader application...");

    // 2. Create the service collection (Dependency Injection container)
    var services = new ServiceCollection();

    // 3. Configure HttpClient with a custom User-Agent
    services.AddHttpClient("RssClient", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Add("User-Agent", "Robin_Reader/1.0 (.NET C# Desktop App)");
    });

    // 4. Register DbContext and application services
    services.AddDbContext<AppDbContext>();
    services.AddTransient<IRssSyncService, RssSyncService>();

    // 5. Configure Microsoft Logging to use Serilog
    services.AddLogging(builder =>
    {
        builder.ClearProviders(); // Remove default logging providers
        builder.AddSerilog(Log.Logger); // Use Serilog for ILogger<T>
    });

    // 6. Build the service provider
    var provider = services.BuildServiceProvider();

    // 7. Execute automatic database migrations on startup (SQLite)
    using (var scope = provider.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
    }

    App.Services = provider;
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    provider.Dispose();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();