public class Article {
    
    public int Id { get; set; } // Primary Key for the Article entity in the database.
    
    public string UniqueId { get; set; } = string.Empty; // Unique identifier for the article, used to avoid duplicates (can be the GUID from the RSS feed or the URL if no GUID is provided)
    
    public string Title { get; set; } = string.Empty; // Title of the article, as provided by the RSS feed
    public string? Author { get; set; } // Author of the article, if available
    public string Content { get; set; } = string.Empty;  // HTML COntent
    public string Url { get; set; } = string.Empty;      // Link to the original Article
    public DateTime PublishDate { get; set; } // The date when the article was published, as provided by the RSS feed. If not available, it can be set to the current date/time or the last updated time of the feed.

    // Estado de lectura
    public bool IsRead { get; set; } // Indicates whether the article has been read by the user
    public bool IsFavorite { get; set; } // Indicates whether the article has been marked as a favorite by the user

    // Relaciones
    public int FeedId { get; set; } // Foreign Key to Feed
    public Feed Feed { get; set; } = null!; // Navigation property to the Feed
}