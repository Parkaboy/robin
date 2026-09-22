public class Category {

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Relación
    public List<Feed> Feeds { get; set; } = new();
}