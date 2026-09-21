/*
 * Key aspects applied in this design
 * XML Security: DtdProcessing.Ignore prevents vulnerabilities associated with resolving external DTD entities.
 * HTTP Efficiency: Using HttpCompletionOption.ResponseHeadersRead with ReadAsStreamAsync processes the content directly in memory via streams without loading massive strings into memory.
 * CancellationToken: Allows canceling pending downloads if the user closes the application or changes views.
*/


var services = new ServiceCollection();

// Configure httpClient with a custom User-Agent (required by many RSS servers)
services.AddHttpClient("RssClient", client => {
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "MiLectorRSS/1.0 (.NET C# Desktop App)");
});


// Register the DbContext and services
services.AddDbContext<AppDbContext>();
services.AddTransient<IRssSyncService, RssSyncService>();

// Configure Serilog or another ILogger provider
services.AddLogging(builder => builder.AddConsole());

var provider = services.BuildServiceProvider();