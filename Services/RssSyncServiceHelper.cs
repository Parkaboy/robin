using System.ServiceModel.Syndication;
using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public class RssSyncServiceHelper
{
    public static int RemoveInvalidDateElements(XDocument document)
    {
        var dateElementNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "date",
            "issued",
            "lastBuildDate",
            "modified",
            "pubDate",
            "published",
            "updated"
        };
        var removedCount = 0;

        foreach (var element in document.Descendants().Where(element => dateElementNames.Contains(element.Name.LocalName)).ToList())
        {
            if (DateTimeOffset.TryParse(
                element.Value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out _))
            {
                continue;
            }

            element.Remove();
            removedCount++;
        }

        return removedCount;
    }

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
        var publishDate = item.PublishDate != default
            ? item.PublishDate.UtcDateTime
            : item.LastUpdatedTime != default
                ? item.LastUpdatedTime.UtcDateTime
                : DateTime.UtcNow;

        return new Article
        {
            FeedId = feedId,
            UniqueId = uniqueId,
            Title = item.Title?.Text ?? "Sin título",
            Content = ExtractContent(item),
            Url = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty,
            Author = item.Authors.FirstOrDefault()?.Name ?? item.Authors.FirstOrDefault()?.Email,
            PublishDate = publishDate,
            IsRead = false,
            IsFavorite = false
        };
    }

    private static string ExtractContent(SyndicationItem item)
    {
        string? encodedContent = null;
        foreach (var extension in item.ElementExtensions.Where(extension =>
                     extension.OuterName.Equals("encoded", StringComparison.OrdinalIgnoreCase) &&
                     extension.OuterNamespace.Equals("http://purl.org/rss/1.0/modules/content/", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var extensionReader = extension.GetReader();
                while (extensionReader.Read())
                {
                    if (extensionReader.NodeType is not XmlNodeType.Whitespace and not XmlNodeType.SignificantWhitespace)
                        break;
                }

                encodedContent = extensionReader.NodeType switch
                {
                    XmlNodeType.Element => WebUtility.HtmlDecode(extensionReader.ReadInnerXml()),
                    XmlNodeType.Text or XmlNodeType.CDATA => extensionReader.Value,
                    _ => null
                };
            }
            catch (Exception)
            {
                // Try the summary when an extension contains malformed markup.
            }

            if (!string.IsNullOrWhiteSpace(encodedContent))
                break;
        }

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
            catch (Exception)
            {
                // Fall back to the summary when the provider exposes non-text content.
            }
        }

        return item.Summary?.Text ?? string.Empty;
    }
}