using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext {

    // Database tables
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Feed> Feeds { get; set; } = null!;
    public DbSet<Article> Articles { get; set; } = null!;

    // SQLite Conexion configuration
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        if (!optionsBuilder.IsConfigured) {
            // Saves the database file as "rss_reader.db" in the executable folder
            optionsBuilder.UseSqlite("Data Source=rss_reader.db");
        }
    }

    // Configuration of constraints and indexes
    protected override void OnModelCreating(ModelBuilder modelBuilder) {

        // Avoid duplicate articles by ensuring that each article is unique within its feed based on the combination of FeedId and UniqueId.
        modelBuilder.Entity<Article>().HasIndex(a => new { a.FeedId, a.UniqueId }).IsUnique();

        // Index on IsRead to quickly filter read/unread articles
        modelBuilder.Entity<Article>().HasIndex(a => a.IsRead);
        modelBuilder.Entity<Article>().HasIndex(a => a.PublishDate);

    }
}