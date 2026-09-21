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

    public async Task<List<Article>> FetchAndProcessFeedAsync(Feed feed, CancellationToken cancellationToken = default)
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
            if (response.Headers.ETag != null)
                feed.ETag = response.Headers.ETag.Tag;

            if (response.Content.Headers.LastModified.HasValue)
                feed.LastModified = response.Content.Headers.LastModified.Value.ToString("r");

            // 3. Procesamiento del Stream XML
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings 
            { 
                Async = true,
                DtdProcessing = DtdProcessing.Ignore
            });

            var syndicationFeed = SyndicationFeed.Load(xmlReader);

            // 4. Cargar IDs existentes en memoria (Optimizando N+1)
            var existingIds = await _dbContext.Articles
                .Where(a => a.FeedId == feed.Id)
                .Select(a => a.UniqueId)
                .ToHashSetAsync(cancellationToken);

            // 5. Mapeo y filtrado
            foreach (var item in syndicationFeed.Items)
            {
                string uniqueId = GetUniqueId(item);

                if (!existingIds.Contains(uniqueId))
                {
                    var article = MapToArticle(item, feed.Id, uniqueId);
                    newArticles.Add(article);
                    existingIds.Add(uniqueId); // Previene duplicados internos del propio XML
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

    // Métodos auxiliares
    private static string GetUniqueId(SyndicationItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Id))
            return item.Id;

        var link = item.Links.FirstOrDefault()?.Uri.ToString();
        if (!string.IsNullOrWhiteSpace(link))
            return link;

        return item.Title?.Text ?? Guid.NewGuid().ToString();
    }

    private static Article MapToArticle(SyndicationItem item, int feedId, string uniqueId)
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