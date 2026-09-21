using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public class RssSyncServiceHelper
{
        public static string GetUniqueId(SyndicationItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Id))
            return item.Id;

        var link = item.Links.FirstOrDefault()?.Uri.ToString();
        if (!string.IsNullOrWhiteSpace(link))
            return link;

        return item.Title?.Text ?? Guid.NewGuid().ToString();
    }

    public static Article MapToArticle(SyndicationItem item, int feedId, string uniqueId)
    {
        return new Article
        {
            FeedId = feedId,
            UniqueId = uniqueId,
            Title = item.Title?.Text ?? "Sin título",
            Content = ExtractContent(item),
            Url = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty,
            Author = item.Authors.FirstOrDefault()?.Name ?? item.Authors.FirstOrDefault()?.Email,
            PublishDate = item.PublishDate != default 
                ? item.PublishDate.UtcDateTime 
                : item.LastUpdatedTime.UtcDateTime,
            IsRead = false,
            IsFavorite = false
        };
    }

    private static string ExtractContent(SyndicationItem item)
    {
        var encodedContent = item.ElementExtensions
            .Where(extension => extension.OuterName.Equals("encoded", StringComparison.OrdinalIgnoreCase) &&
                                extension.OuterNamespace.Equals("http://purl.org/rss/1.0/modules/content/", StringComparison.OrdinalIgnoreCase))
            .Select(extension => extension.GetObject<XElement>()?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(encodedContent))
            return encodedContent;

        if (item.Content is TextSyndicationContent textContent)
        {
            return textContent.Text;
        }

        if (item.Content != null)
        {
            try
            {
                var builder = new StringBuilder();
                using var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true });
                item.Content.WriteTo(writer, "content", string.Empty);
                writer.Flush();
                var content = builder.ToString();
                if (!string.IsNullOrWhiteSpace(content))
                    return content;
            }
            catch (InvalidOperationException)
            {
                // Fall back to the summary when the provider exposes non-text content.
            }
        }

        return item.Summary?.Text ?? string.Empty;
    }
}