using System.ServiceModel.Syndication;

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
        if (item.Content is TextSyndicationContent textContent)
        {
            return textContent.Text;
        }

        return item.Summary?.Text ?? string.Empty;
    }
}