using System.ServiceModel.Syndication;
using System.Xml.Linq;
using Xunit;

public sealed class RssSyncServiceHelperTests
{
    [Fact]
    public void RemoveInvalidDateElementsRemovesOnlyUnparseableDateFields()
    {
        var document = XDocument.Parse(
            "<feed><pubDate>not a date</pubDate><updated>2026-10-09T12:00:00Z</updated><title>Story</title></feed>");

        var removedCount = RssSyncServiceHelper.RemoveInvalidDateElements(document);

        Assert.Equal(1, removedCount);
        Assert.Null(document.Descendants("pubDate").SingleOrDefault());
        Assert.Equal("2026-10-09T12:00:00Z", document.Descendants("updated").Single().Value);
        Assert.Equal("Story", document.Descendants("title").Single().Value);
    }

    [Fact]
    public void GetUniqueIdPrefersFeedIdThenLinkThenTitle()
    {
        var itemWithId = CreateItem();
        itemWithId.Id = "feed-id";
        itemWithId.Links.Add(new SyndicationLink(new Uri("https://example.com/article")));
        var itemWithLink = CreateItem();
        itemWithLink.Links.Add(new SyndicationLink(new Uri("https://example.com/article")));
        var itemWithTitle = new SyndicationItem
        {
            Title = new TextSyndicationContent("Sample article")
        };

        Assert.Equal("feed-id", RssSyncServiceHelper.GetUniqueId(itemWithId));
        Assert.Equal("https://example.com/article", RssSyncServiceHelper.GetUniqueId(itemWithLink));
        Assert.Equal("Sample article", RssSyncServiceHelper.GetUniqueId(itemWithTitle));
    }

    [Fact]
    public void MapToArticleCopiesFeedMetadataAndContent()
    {
        var item = CreateItem();
        item.PublishDate = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        item.Authors.Add(new SyndicationPerson { Name = "Robin Author" });
        item.Content = new TextSyndicationContent("<p>Article body</p>");

        var article = RssSyncServiceHelper.MapToArticle(item, 42, "article-42");

        Assert.Equal(42, article.FeedId);
        Assert.Equal("article-42", article.UniqueId);
        Assert.Equal("Sample article", article.Title);
        Assert.Equal("https://example.com/article", article.Url);
        Assert.Equal("Robin Author", article.Author);
        Assert.Equal("<p>Article body</p>", article.Content);
        Assert.Equal(item.PublishDate.UtcDateTime, article.PublishDate);
        Assert.False(article.IsRead);
        Assert.False(article.IsFavorite);
    }

    private static SyndicationItem CreateItem() =>
        new("Sample article", "Summary", new Uri("https://example.com/article"));
}
