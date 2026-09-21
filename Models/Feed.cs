public class Feed {
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;            // URL del XML RSS
    public string? WebsiteUrl { get; set; }                    // Sitio web principal
    public string? Description { get; set; }
    public string? FaviconUrl { get; set; }

    // Control de sincronización y caché
    public DateTime? LastUpdated { get; set; }
    public string? ETag { get; set; }                          // Para evitar descargas repetidas (HTTP ETag)
    public string? LastModified { get; set; }                   // Para HTTP Last-Modified

    public DateTime? LastSyncTime { get; set; }                // Última vez que se sincronizó el feed
    public bool LastSyncSuccess { get; set; }                  // Indica si la última sincronización fue exitosa
    public string? LastSyncError { get; set; }                 // Mensaje de error

    // Relaciones
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public List<Article> Articles { get; set; } = new();
}