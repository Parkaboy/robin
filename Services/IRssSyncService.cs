public interface IRssSyncService
{
    Task<List<Article>> SyncFeedAsync(Feed feed, CancellationToken cancellationToken = default);

    // Método para resolver y validar URLs ingresadas por el usuario
    Task<string> ResolveActualFeedUrlAsync(string inputUrl, CancellationToken cancellationToken = default);
}