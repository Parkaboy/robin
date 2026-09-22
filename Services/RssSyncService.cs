using System.Net;
using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class RssSyncService : IRssSyncService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<RssSyncService> _logger;

    public RssSyncService(
        IHttpClientFactory httpClientFactory,
        AppDbContext dbContext,
        ILogger<RssSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<Article>> SyncFeedAsync(Feed feed, CancellationToken cancellationToken = default)
    {
        var actualFeedUrl = await ResolveActualFeedUrlAsync(feed.Url, cancellationToken);

        if (!string.Equals(feed.Url, actualFeedUrl, StringComparison.OrdinalIgnoreCase))
        {
            feed.Url = actualFeedUrl;
            feed.ETag = null;
            feed.LastModified = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var hasBlankArticles = await _dbContext.Articles
            .AnyAsync(article => article.FeedId == feed.Id && string.IsNullOrWhiteSpace(article.Content), cancellationToken);

        if (hasBlankArticles)
        {
            feed.ETag = null;
            feed.LastModified = null;
        }

        return await ProcessFeedAsync(feed, cancellationToken);
    }

    private async Task<List<Article>> ProcessFeedAsync(Feed feed, CancellationToken cancellationToken = default)
    {
        var newArticles = new List<Article>();
        var client = _httpClientFactory.CreateClient("RssClient");

        // 1. Configurar encabezados condicionales para ahorro de ancho de banda
        var request = new HttpRequestMessage(HttpMethod.Get, feed.Url);

        if (!string.IsNullOrEmpty(feed.ETag))
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(feed.ETag));
        }

        if (!string.IsNullOrEmpty(feed.LastModified) && DateTimeOffset.TryParse(feed.LastModified, out var lastModifiedDate))
        {
            request.Headers.IfModifiedSince = lastModifiedDate;
        }

        try
        {
            _logger.LogInformation("Descargando feed: {FeedUrl}", feed.Url);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // 2. Manejo de respuesta HTTP 304 (Not Modified)
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                _logger.LogInformation("El feed {FeedUrl} no tiene cambios desde la última consulta.", feed.Url);

                feed.LastSyncTime = DateTime.UtcNow;
                feed.LastSyncSuccess = true;
                feed.LastSyncError = null;

                await _dbContext.SaveChangesAsync(cancellationToken);
                return newArticles;
            }

            response.EnsureSuccessStatusCode();

            // Guardar/Actualizar metadatos de sincronización HTTP
            if (response.Headers.ETag != null) {
                feed.ETag = response.Headers.ETag.Tag;
            }


            if (response.Content.Headers.LastModified.HasValue)
            {
                feed.LastModified = response.Content.Headers.LastModified.Value.ToString("r");                
            }


            // 3. Procesamiento del Stream XML
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Ignore
            });

            var document = await System.Xml.Linq.XDocument.LoadAsync(
                xmlReader,
                System.Xml.Linq.LoadOptions.PreserveWhitespace,
                cancellationToken);
            var removedDateCount = RssSyncServiceHelper.RemoveInvalidDateElements(document);
            if (removedDateCount > 0)
            {
                _logger.LogWarning(
                    "Se omitieron {Count} fechas inválidas del feed {FeedUrl}",
                    removedDateCount,
                    feed.Url);
            }

            using var feedReader = document.CreateReader();
            var syndicationFeed = SyndicationFeed.Load(feedReader);

            if (!string.IsNullOrWhiteSpace(syndicationFeed.Title?.Text))
                feed.Title = syndicationFeed.Title.Text;

            feed.Description = syndicationFeed.Description?.Text;
            feed.WebsiteUrl = syndicationFeed.Links.FirstOrDefault()?.Uri.ToString();

            // 4. Cargar IDs existentes en memoria (Optimizando N+1)
            var existingArticles = await _dbContext.Articles
                .Where(a => a.FeedId == feed.Id)
                .ToDictionaryAsync(a => a.UniqueId, cancellationToken);

            // 5. Mapeo y filtrado
            foreach (var item in syndicationFeed.Items)
            {
                string uniqueId = RssSyncServiceHelper.GetUniqueId(item);

                if (!existingArticles.TryGetValue(uniqueId, out var existingArticle))
                {
                    var article = RssSyncServiceHelper.MapToArticle(item, feed.Id, uniqueId);
                    newArticles.Add(article);
                    existingArticles.Add(uniqueId, article); // Prevent duplicates inside the same XML document.
                }
                else
                {
                    var refreshedArticle = RssSyncServiceHelper.MapToArticle(item, feed.Id, uniqueId);

                    existingArticle.Title = refreshedArticle.Title;
                    existingArticle.Author = refreshedArticle.Author;
                    existingArticle.Url = refreshedArticle.Url;
                    existingArticle.PublishDate = refreshedArticle.PublishDate;
                    if (!string.IsNullOrWhiteSpace(refreshedArticle.Content))
                        existingArticle.Content = refreshedArticle.Content;
                }
            }

            // 6. Persistencia
            if (newArticles.Any())
            {
                _dbContext.Articles.AddRange(newArticles);
            }

            feed.LastSyncTime = DateTime.UtcNow;
            feed.LastSyncSuccess = true;
            feed.LastSyncError = null;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Se agregaron {Count} artículos nuevos desde {FeedUrl}", newArticles.Count, feed.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar el feed: {FeedUrl}", feed.Url);

            feed.LastSyncTime = DateTime.UtcNow;
            feed.LastSyncSuccess = false;
            feed.LastSyncError = ex.Message;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return newArticles;
    }

    public async Task<string> ResolveActualFeedUrlAsync(string inputUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(inputUrl, UriKind.Absolute, out var targetUri))
        {
            return inputUrl;
        }

        var client = _httpClientFactory.CreateClient("RssClient");

        try
        {
            using var response = await client.GetAsync(inputUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType;

            // Si la respuesta es directamente XML o RSS/Atom, la URL ya es la correcta
            if (contentType != null && (contentType.Contains("xml") || contentType.Contains("rss") || contentType.Contains("atom")))
            {
                return inputUrl;
            }

            // Si la respuesta es HTML, inspeccionar el contenido para buscar el tag <link> del RSS
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var linkTags = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<link\b[^>]*>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match linkTag in linkTags)
            {
                var tag = linkTag.Value;
                var typeMatch = System.Text.RegularExpressions.Regex.Match(
                    tag,
                    @"\btype\s*=\s*[""']application/(?:rss|atom)\+xml[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var relMatch = System.Text.RegularExpressions.Regex.Match(
                    tag,
                    @"\brel\s*=\s*[""'][^""']*alternate[^""']*[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var hrefMatch = System.Text.RegularExpressions.Regex.Match(
                    tag,
                    @"\bhref\s*=\s*[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if ((!typeMatch.Success && !relMatch.Success) || !hrefMatch.Success)
                    continue;

                string discoveredUrl = WebUtility.HtmlDecode(hrefMatch.Groups[1].Value);

                // Convertir URLs relativas ("/feed/") a URLs absolutas ("https://ejemplo.com/feed/")
                if (Uri.TryCreate(targetUri, discoveredUrl, out var absoluteUri))
                {
                    _logger.LogInformation("Feed detectado automáticamente: {DiscoveredUrl} para la web {InputUrl}", absoluteUri, inputUrl);
                    return absoluteUri.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo realizar el Auto-Discovery para {InputUrl}", inputUrl);
        }

        return inputUrl; // Si falla o no encuentra nada, retorna la URL original
    }



}