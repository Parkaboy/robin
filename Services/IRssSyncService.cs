// Defining the interface helps decouple the layers of the application (for example, to consume it from the graphical interface or from a background process).
public interface IRssSyncService {
    
    Task<List<Article>> FetchAndProcessFeedAsync(Feed feed, CancellationToken cancellationToken = default);
}