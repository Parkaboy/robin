using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Robin;

public partial class MainWindow : Window
{
    private readonly AppDbContext dbContext;
    private readonly IRssSyncService syncService;
    private string? currentStatusKey;
    private object?[] currentStatusArguments = Array.Empty<object?>();
    private Feed? selectedFeed;

    public MainWindow(AppDbContext dbContext, IRssSyncService syncService)
    {
        this.dbContext = dbContext;
        this.syncService = syncService;

        InitializeComponent();

        ApplyLocalization();
        ApplyPalette();
    }

    private async void WindowOpened(object? sender, EventArgs args) => await InitializeAsync();

    private async void UrlInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs args)
    {
        if (args.Key == Avalonia.Input.Key.Enter)
        {
            args.Handled = true;
            await AddFeedAsync();
        }
    }

    private void FeedItemPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs args)
    {
        if (sender is not Control item ||
            args.GetCurrentPoint(item).Properties.PointerUpdateKind != Avalonia.Input.PointerUpdateKind.RightButtonPressed ||
            item.DataContext is not Feed feed)
        {
            return;
        }

        selectedFeed = feed;
        feedTree.SelectedItem = feed;
        if (item.ContextMenu is { } contextMenu)
        {
            foreach (var menuItem in contextMenu.Items.OfType<MenuItem>())
            {
                if (menuItem.Tag is string key)
                    menuItem.Header = UiText.Get(key);
            }
        }
    }

    private async void FeedContextActionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (sender is not MenuItem { Tag: string action })
            return;

        Func<Task> operation = action switch
        {
            "ModifyFeed" => EditSelectedFeedAsync,
            "SyncFeed" => SyncSelectedFeedAsync,
            "DeleteFeed" => RemoveSelectedFeedAsync,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown feed action.")
        };
        await RunSelectedFeedActionAsync(operation);
    }

    private async void EditSelectedFeedClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        await RunSelectedFeedActionAsync(EditSelectedFeedAsync);

    private async void DeleteSelectedFeedClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        await RunSelectedFeedActionAsync(RemoveSelectedFeedAsync);

    private async void LightModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => await SetThemeAsync(false);
    private async void DarkModeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => await SetThemeAsync(true);
    private async void EnglishLanguageClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => await SetLanguageAsync(UiLanguage.English);
    private async void SpanishLanguageClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => await SetLanguageAsync(UiLanguage.Spanish);
    private async void GermanLanguageClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => await SetLanguageAsync(UiLanguage.German);
    private async void PortugueseLanguageClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => await SetLanguageAsync(UiLanguage.Portuguese);
    private async void ItalianLanguageClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => await SetLanguageAsync(UiLanguage.Italian);

    private void PaneSplitterKeyDown(object? sender, Avalonia.Input.KeyEventArgs args)
    {
        if (sender is not GridSplitter splitter || splitter.Parent is not Grid paneGrid)
            return;

        var delta = args.Key switch
        {
            Avalonia.Input.Key.Left => -12,
            Avalonia.Input.Key.Right => 12,
            _ => 0
        };
        if (delta == 0)
            return;

        var column = Grid.GetColumn(splitter);
        var previous = paneGrid.ColumnDefinitions[column - 1];
        var next = paneGrid.ColumnDefinitions[column + 1];
        var previousWidth = previous.Width.Value;
        var nextWidth = next.Width.IsAbsolute ? next.Width.Value : 0;
        if (delta > 0 && next.Width.IsAbsolute && nextWidth <= next.MinWidth ||
            delta < 0 && previousWidth <= previous.MinWidth)
            return;

        previous.Width = new GridLength(previousWidth + delta);
        if (next.Width.IsAbsolute)
            next.Width = new GridLength(nextWidth - delta);
        args.Handled = true;
    }
    private void RebuildMenus()
    {
        feedMenuItem.Header = UiText.Get("Feeds");
        editSelectedFeedMenuItem.Header = UiText.Get("EditSelectedFeed");
        deleteSelectedFeedMenuItem.Header = UiText.Get("DeleteSelectedFeed");
        settingsMenuItem.Header = UiText.Get("Settings");
        appearanceMenuItem.Header = UiText.Get("Appearance");
        lightModeMenuItem.Header = UiText.Get("LightMode");
        lightModeMenuItem.IsChecked = !App.Preferences.IsDarkMode;
        darkModeMenuItem.Header = UiText.Get("DarkMode");
        darkModeMenuItem.IsChecked = App.Preferences.IsDarkMode;
        languageMenuItem.Header = UiText.Get("Language");
        languageEnglishMenuItem.Header = UiText.LanguageName(UiLanguage.English);
        languageEnglishMenuItem.IsChecked = UiText.Language == UiLanguage.English;
        languageSpanishMenuItem.Header = UiText.LanguageName(UiLanguage.Spanish);
        languageSpanishMenuItem.IsChecked = UiText.Language == UiLanguage.Spanish;
        languageGermanMenuItem.Header = UiText.LanguageName(UiLanguage.German);
        languageGermanMenuItem.IsChecked = UiText.Language == UiLanguage.German;
        languagePortugueseMenuItem.Header = UiText.LanguageName(UiLanguage.Portuguese);
        languagePortugueseMenuItem.IsChecked = UiText.Language == UiLanguage.Portuguese;
        languageItalianMenuItem.Header = UiText.LanguageName(UiLanguage.Italian);
        languageItalianMenuItem.IsChecked = UiText.Language == UiLanguage.Italian;
    }

    private void ApplyLocalization()
    {
        Title = UiText.Get("WindowTitle");
        readingListTitle.Text = UiText.Get("ReadingList");
        feedSubtitle.Text = UiText.Get("FeedSubtitle");
        inboxTitle.Text = UiText.Get("Articles");
        inboxSubtitle.Text = UiText.Get("ArticlesSubtitle");
        urlInput.Watermark = UiText.Get("FeedUrlWatermark");
        addFeedButton.Content = UiText.Get("AddFeed");
        if (articleList.SelectedItem is Article selectedArticle)
            UpdateArticleDetails(selectedArticle);
        else
            articleTitle.Text = UiText.Get("ReadingPane");
        AutomationProperties.SetName(urlInput, UiText.Get("FeedUrlWatermark"));
        AutomationProperties.SetName(addFeedButton, UiText.Get("AddFeed"));
        AutomationProperties.SetName(feedMenu, UiText.Get("Feeds"));
        AutomationProperties.SetName(settingsMenu, UiText.Get("Settings"));
        AutomationProperties.SetName(feedsPanel, UiText.Get("FeedList"));
        AutomationProperties.SetName(articlesPanel, UiText.Get("Articles"));
        AutomationProperties.SetName(detailScroll, UiText.Get("ReadingPane"));
        AutomationProperties.SetName(feedTree, UiText.Get("FeedList"));
        AutomationProperties.SetName(articleList, UiText.Get("ArticleList"));
        AutomationProperties.SetName(status, UiText.Get("Status"));
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(feedPaneSplitter, UiText.Get("ResizeFeedPane"));
        AutomationProperties.SetName(articlesPaneSplitter, UiText.Get("ResizeArticlesPane"));
        RebuildMenus();
        var selectedFeedId = selectedFeed?.Id;
        var currentFeeds = feedTree.ItemsSource;
        feedTree.ItemsSource = null;
        feedTree.ItemsSource = currentFeeds;
        if (selectedFeedId.HasValue && currentFeeds is IEnumerable<Feed> feeds)
            feedTree.SelectedItem = feeds.FirstOrDefault(feed => feed.Id == selectedFeedId.Value);
        if (currentStatusKey != null)
            status.Text = currentStatusArguments.Length == 0
                ? UiText.Get(currentStatusKey)
                : UiText.Format(currentStatusKey, currentStatusArguments);
    }

    private void ApplyPalette()
    {
        var dark = App.Preferences.IsDarkMode;
        Background = Brush(dark ? "#12161C" : "#F3F5F8");
        topBar.Background = Brush(dark ? "#1B2028" : "#FFFFFF");
        feedsPanel.Background = Brush(dark ? "#202630" : "#E8EDF3");
        articlesPanel.Background = Brush(dark ? "#1B2028" : "#FFFFFF");
        detailScroll.Background = Brush(dark ? "#151A21" : "#FAFBFC");
        articleDivider.Background = Brush(dark ? "#3A424E" : "#D9DEE5");
        feedBrand.Foreground = Brush(dark ? "#F4F6F9" : "#17202A");
        feedSubtitle.Foreground = Brush(dark ? "#C2CAD4" : "#536170");
        inboxTitle.Foreground = Brush(dark ? "#F4F6F9" : "#1A1A1A");
        inboxSubtitle.Foreground = Brush(dark ? "#C2CAD4" : "#536170");
        articleTitle.Foreground = Brush(dark ? "#F4F6F9" : "#1A1A1A");
        articleMeta.Foreground = Brush(dark ? "#C2CAD4" : "#505B68");
        articleContent.Foreground = Brush(dark ? "#E4E8EE" : "#292929");
        status.Foreground = Brush(dark ? "#C2CAD4" : "#505B68");
        feedPaneSplitter.Background = Brush(dark ? "#505B68" : "#B9C2CD");
        articlesPaneSplitter.Background = Brush(dark ? "#505B68" : "#B9C2CD");
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));

    private string FormatArticleCount(int count) =>
        UiText.Format(count == 1 ? "ArticleCountOne" : "ArticleCountMany", count);

    private void SetStatus(string key, params object?[] arguments)
    {
        currentStatusKey = key;
        currentStatusArguments = arguments;
        status.Text = arguments.Length == 0 ? UiText.Get(key) : UiText.Format(key, arguments);
    }

    private void SetStatusMessage(string message)
    {
        currentStatusKey = null;
        currentStatusArguments = Array.Empty<object?>();
        status.Text = message;
    }

    private async Task LoadFeedsAsync()
    {
        var feeds = await dbContext.Feeds
            .AsNoTracking()
            .Include(feed => feed.Articles)
            .OrderBy(feed => feed.Title)
            .ToListAsync();
        feedTree.ItemsSource = feeds;
        SetStatus(feeds.Count == 1 ? "FeedCountOne" : "FeedCountMany", feeds.Count);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var feeds = await dbContext.Feeds.OrderBy(feed => feed.Title).ToListAsync();
            SetStatus(feeds.Count == 0 ? "NoFeeds" : "SyncingFeeds");

            foreach (var feed in feeds)
            {
                try
                {
                    await syncService.SyncFeedAsync(feed);
                }
                catch (Exception ex)
                {
                    SetStatus("CouldNotSyncFeed", feed.Title, ex.Message);
                }
            }

            await LoadFeedsAsync();
        }
        catch (Exception ex)
        {
            SetStatus("CouldNotLoadFeeds", ex.Message);
        }
    }

    private async void FeedTreeSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        selectedFeed = feedTree.SelectedItem as Feed;
        articleList.ItemsSource = selectedFeed == null
            ? null
            : await dbContext.Articles.AsNoTracking()
                .Where(article => article.FeedId == selectedFeed.Id)
                .OrderByDescending(article => article.PublishDate)
                .ToListAsync();
        ClearArticle();

        if (selectedFeed != null)
        {
            var articleCount = articleList.ItemsSource is IEnumerable<Article> articles
                ? articles.Count()
                : 0;
            SetStatus(articleCount == 1 ? "ArticleCountOne" : "ArticleCountMany", articleCount);
            if (!selectedFeed.LastSyncSuccess && !string.IsNullOrWhiteSpace(selectedFeed.LastSyncError))
                SetStatusMessage(selectedFeed.LastSyncError);
        }
    }

    private void ArticleListSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (articleList.SelectedItem is not Article article)
        {
            ClearArticle();
            return;
        }

        UpdateArticleDetails(article);
    }

    private void UpdateArticleDetails(Article article)
    {
        articleTitle.Text = article.Title;
        articleMeta.Text = UiText.Format(
            "ArticleMetadataFormat",
            article.Author ?? UiText.Get("UnknownAuthor"),
            article.PublishDate,
            article.Url);
        articleContent.Text = string.IsNullOrWhiteSpace(article.Content)
            ? UiText.Get("MissingArticleText")
            : ToReadableText(article.Content);
    }

    private async void AddFeedClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        await AddFeedAsync();

    private async Task AddFeedAsync()
    {
        if (!Uri.TryCreate(urlInput.Text?.Trim(), UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("InvalidHttpUrl");
            return;
        }

        addFeedButton.IsEnabled = false;
        SetStatus("SyncingFeed");
        try
        {
            var feed = new Feed { Url = url.ToString(), Title = url.Host };
            dbContext.Feeds.Add(feed);
            await dbContext.SaveChangesAsync();
            var newArticles = await syncService.SyncFeedAsync(feed);
            urlInput.Text = string.Empty;
            await LoadFeedsAsync();
            feedTree.SelectedItem = feedTree.ItemsSource is IEnumerable<Feed> loadedFeeds
                ? loadedFeeds.FirstOrDefault(loadedFeed => loadedFeed.Id == feed.Id)
                : null;
            if (feed.LastSyncSuccess)
                SetStatus("AddedFeed", feed.Title, newArticles.Count);
            else
                SetStatusMessage(feed.LastSyncError ?? UiText.Get("CouldNotSync"));
        }
        catch (Exception ex)
        {
            SetStatusMessage(ex.Message);
        }
        finally
        {
            addFeedButton.IsEnabled = true;
        }
    }

    private async Task EditSelectedFeedAsync()
    {
        if (selectedFeed == null)
        {
            SetStatus("SelectFeedFirst");
            return;
        }

        var newUrl = await ShowFeedUrlDialogAsync(UiText.Get("EditFeed"), selectedFeed.Url);
        if (newUrl == null)
            return;

        var feedToUpdate = await dbContext.Feeds.FirstAsync(feed => feed.Id == selectedFeed.Id);
        feedToUpdate.Url = newUrl;
        feedToUpdate.LastSyncSuccess = false;
        feedToUpdate.LastSyncError = null;
        await dbContext.SaveChangesAsync();
        SetStatus("FeedUpdated");
        await LoadFeedsAsync();
    }

    private async Task RunSelectedFeedActionAsync(Func<Task> action)
    {
        var feed = feedTree.SelectedItem as Feed ?? selectedFeed;
        if (feed == null)
        {
            SetStatus("SelectFeedFirst");
            return;
        }

        await RunFeedActionAsync(feed, action);
    }

    private async Task RunFeedActionAsync(Feed feed, Func<Task> action)
    {
        selectedFeed = feed;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatusMessage(ex.Message);
        }
    }

    private async Task SyncSelectedFeedAsync()
    {
        if (selectedFeed == null)
            return;

        SetStatus("SyncingFeedNamed", selectedFeed.Title);
        var feed = await dbContext.Feeds.FirstAsync(item => item.Id == selectedFeed.Id);
        var newArticles = await syncService.SyncFeedAsync(feed);
        await LoadFeedsAsync();

        var refreshedFeed = feedTree.ItemsSource is IEnumerable<Feed> feeds
            ? feeds.FirstOrDefault(item => item.Id == feed.Id)
            : null;
        feedTree.SelectedItem = refreshedFeed;
        if (feed.LastSyncSuccess)
            SetStatus(newArticles.Count == 1 ? "NewArticleCountOne" : "NewArticleCountMany", newArticles.Count);
        else
            SetStatusMessage(feed.LastSyncError ?? UiText.Get("CouldNotSync"));
    }

    private async Task RemoveSelectedFeedAsync()
    {
        if (selectedFeed == null)
        {
            SetStatus("SelectFeedFirst");
            return;
        }

        var feedId = selectedFeed.Id;
        var feedTitle = selectedFeed.Title;
        var confirmed = await ShowConfirmationDialogAsync(
            UiText.Get("RemoveFeed"),
            UiText.Format("RemoveFeedConfirmation", feedTitle));
        if (!confirmed)
            return;

        var feed = await dbContext.Feeds.FirstOrDefaultAsync(item => item.Id == feedId);
        if (feed == null)
        {
            selectedFeed = null;
            await LoadFeedsAsync();
            SetStatus("FeedAlreadyRemoved");
            return;
        }

        var articles = await dbContext.Articles
            .Where(article => article.FeedId == feedId)
            .ToListAsync();
        dbContext.Articles.RemoveRange(articles);
        dbContext.Feeds.Remove(feed);
        await dbContext.SaveChangesAsync();
        selectedFeed = null;
        articleList.ItemsSource = null;
        ClearArticle();
        await LoadFeedsAsync();
        SetStatus("FeedRemoved");
    }

    private Task SetLanguageAsync(UiLanguage language)
    {
        var previousLanguage = App.Preferences.Language;
        App.Preferences.Language = language;
        UiText.SetLanguage(language);
        ApplyLocalization();

        try
        {
            App.Preferences.Save();
        }
        catch (Exception ex)
        {
            App.Preferences.Language = previousLanguage;
            UiText.SetLanguage(previousLanguage);
            ApplyLocalization();
            SetStatus("CouldNotSaveSettings", ex.Message);
        }

        return Task.CompletedTask;
    }

    private Task SetThemeAsync(bool isDarkMode)
    {
        var previousTheme = App.Preferences.IsDarkMode;
        App.Preferences.IsDarkMode = isDarkMode;
        App.ApplyThemeVariant();
        ApplyPalette();
        RebuildMenus();

        try
        {
            App.Preferences.Save();
        }
        catch (Exception ex)
        {
            App.Preferences.IsDarkMode = previousTheme;
            App.ApplyThemeVariant();
            ApplyPalette();
            RebuildMenus();
            SetStatus("CouldNotSaveSettings", ex.Message);
        }

        return Task.CompletedTask;
    }

    private async Task<string?> ShowFeedUrlDialogAsync(string title, string initialUrl)
    {
        return await new FeedUrlDialog(title, initialUrl).ShowDialog<string?>(this);
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        return await new FeedConfirmationDialog(title, message).ShowDialog<bool>(this);
    }

    private void ClearArticle()
    {
        articleTitle.Text = UiText.Get("ReadingPane");
        articleMeta.Text = string.Empty;
        articleContent.Text = string.Empty;
    }

    private static string ToReadableText(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"<\s*(script|style)\b[^>]*>.*?<\s*/\s*\1\s*>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"<\s*(br|/p|/div|/li|/h[1-6])\s*/?\s*>",
            Environment.NewLine,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text) ?? string.Empty;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t\f\v]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(\r?\n\s*){3,}", Environment.NewLine + Environment.NewLine);

        return text.Trim();
    }
}

public sealed class FeedSummaryConverter : IValueConverter
{
    public static FeedSummaryConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Feed { LastSyncSuccess: false }
            ? UiText.Get("SyncFailed")
            : value is Feed feed
                ? UiText.Format(feed.Articles.Count == 1 ? "ArticleCountOne" : "ArticleCountMany", feed.Articles.Count)
                : UiText.Get("UnknownFeed");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ArticleTitleConverter : IValueConverter
{
    public static ArticleTitleConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Article article && !string.IsNullOrWhiteSpace(article.Title)
            ? article.Title
            : UiText.Get("UntitledArticle");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}