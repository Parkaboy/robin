public class Article {
    
    public Int Id { get; set; } // Primary Key for the Article entity in the database.
    
    public String UniqueId { get; set; } = string.Empty; // Unique identifier for the article, used to avoid duplicates (can be the GUID from the RSS feed or the URL if no GUID is provided)
    
    public String Title { get; set; } = string.Empty; // Title of the article, as provided by the RSS feed
    public String? Author { get; set; } // Author of the article, if available
    public String Content { get; set; } = string.Empty;  // HTML COntent
    public String Url { get; set; } = string.Empty;      // Link to the original Article
    public DateTime PublishDate { get; set; } // The date when the article was published, as provided by the RSS feed. If not available, it can be set to the current date/time or the last updated time of the feed.

    // Estado de lectura
    public Bool IsRead { get; set; } // Indicates whether the article has been read by the user
    public Bool IsFavorite { get; set; } // Indicates whether the article has been marked as a favorite by the user

    // Relaciones
    public Int FeedId { get; set; } // Foreign Key to Feed
    public Feed Feed { get; set; } = null!; // Navigation property to the Feed
}