public class Feed {
    public Int Id { get; set; }
    public String Title { get; set; } = string.Empty;
    public String Url { get; set; } = string.Empty;            // URL del XML RSS
    public String? WebsiteUrl { get; set; }                    // Sitio web principal
    public String? Description { get; set; }
    public String? FaviconUrl { get; set; }

    // Control de sincronización y caché
    public DateTime? LastUpdated { get; set; }
    public String? ETag { get; set; }                          // Para evitar descargas repetidas (HTTP ETag)
    public String? LastModified { get; set; }                   // Para HTTP Last-Modified

    // Relaciones
    public Int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public List<Article> Articles { get; set; } = new();
}