public class Category {

    public Int Id { get; set; }
    public String Name { get; set; } = string.Empty;

    // Relación
    public List<Feed> Feeds { get; set; } = new();
}