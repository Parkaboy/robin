using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.EntityFrameworkCore;

public class MainWindow : Window
{
    private readonly AppDbContext dbContext;
    private readonly IRssSyncService syncService;
    private readonly TextBox urlInput = new() { Watermark = "RSS or Atom URL" };
    private readonly Button addFeedButton = new() { Content = "Add feed" };
    private readonly TreeView feedTree = new();
    private readonly ListBox articleList = new();
    private readonly TextBlock articleTitle = new() { FontSize = 22, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock articleMeta = new() { Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock articleContent = new() { TextWrapping = TextWrapping.Wrap, LineHeight = 1.4 };
    private readonly TextBlock status = new() { Foreground = Brushes.Gray };
    private Feed? selectedFeed;

    public MainWindow(AppDbContext dbContext, IRssSyncService syncService)
    {
        this.dbContext = dbContext;
        this.syncService = syncService;

        Title = "Robin RSS Reader";
        Width = 1200;
        Height = 720;
        MinWidth = 800;
        MinHeight = 500;

        feedTree.ItemTemplate = new FuncDataTemplate<Feed>((feed, _) =>
            new TextBlock { Text = feed.Title, Margin = new Thickness(4) });
        articleList.ItemTemplate = new FuncDataTemplate<Article>((article, _) =>
            new TextBlock { Text = article.Title, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });

        feedTree.SelectionChanged += FeedTreeSelectionChanged;
        articleList.SelectionChanged += ArticleListSelectionChanged;
        addFeedButton.Click += AddFeedClicked;
        urlInput.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
                AddFeedClicked(this, new Avalonia.Interactivity.RoutedEventArgs());
        };

        Content = BuildLayout();
        Opened += async (_, _) => await LoadFeedsAsync();
    }

    private Control BuildLayout()
    {
        var addFeedPanel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(12)
        };
        addFeedPanel.Children.Add(urlInput);
        Grid.SetColumn(addFeedButton, 1);
        addFeedPanel.Children.Add(addFeedButton);

        var feedsPanel = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new DockPanel
            {
                Children =
                {
                    new TextBlock { Text = "Feeds", FontSize = 18, FontWeight = FontWeight.Bold, Margin = new Thickness(12, 12, 12, 4) },
                    feedTree
                }
            }
        };

        var articlesPanel = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(12),
            Child = new DockPanel
            {
                Children =
                {
                    new TextBlock { Text = "Articles", FontSize = 18, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 8) },
                    articleList
                }
            }
        };

        var details = new StackPanel { Spacing = 10 };
        details.Children.Add(articleTitle);
        details.Children.Add(articleMeta);
        details.Children.Add(new Separator());
        details.Children.Add(articleContent);

        var detailScroll = new ScrollViewer
        {
            Padding = new Thickness(20),
            Content = details,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("240,320,*"),
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        Grid.SetColumnSpan(addFeedPanel, 3);
        columns.Children.Add(addFeedPanel);
        Grid.SetRow(feedsPanel, 1);
        columns.Children.Add(feedsPanel);
        Grid.SetColumn(articlesPanel, 1);
        Grid.SetRow(articlesPanel, 1);
        columns.Children.Add(articlesPanel);
        Grid.SetColumn(detailScroll, 2);
        Grid.SetRow(detailScroll, 1);
        columns.Children.Add(detailScroll);

        var root = new DockPanel();
        DockPanel.SetDock(status, Dock.Bottom);
        status.Margin = new Thickness(12, 6);
        root.Children.Add(status);
        root.Children.Add(columns);
        return root;
    }

    private async Task LoadFeedsAsync()
    {
        var feeds = await dbContext.Feeds.AsNoTracking().OrderBy(feed => feed.Title).ToListAsync();
        feedTree.ItemsSource = feeds;
        status.Text = $"{feeds.Count} feed(s)";
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
    }

    private void ArticleListSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (articleList.SelectedItem is not Article article)
        {
            ClearArticle();
            return;
        }

        articleTitle.Text = article.Title;
        articleMeta.Text = $"{article.Author ?? "Unknown author"} | {article.PublishDate:g}\n{article.Url}";
        articleContent.Text = article.Content;
    }

    private async void AddFeedClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (!Uri.TryCreate(urlInput.Text?.Trim(), UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            status.Text = "Enter a valid HTTP or HTTPS URL.";
            return;
        }

        addFeedButton.IsEnabled = false;
        status.Text = "Syncing feed...";
        try
        {
            var feed = new Feed { Url = url.ToString(), Title = url.Host };
            dbContext.Feeds.Add(feed);
            await dbContext.SaveChangesAsync();
            await syncService.FetchAndProcessFeedAsync(feed);
            urlInput.Text = string.Empty;
            await LoadFeedsAsync();
            status.Text = feed.LastSyncSuccess
                ? $"Added {feed.Title}."
                : feed.LastSyncError ?? "The feed could not be synchronized.";
        }
        catch (Exception ex)
        {
            status.Text = ex.Message;
        }
        finally
        {
            addFeedButton.IsEnabled = true;
        }
    }

    private void ClearArticle()
    {
        articleTitle.Text = "Select an article";
        articleMeta.Text = string.Empty;
        articleContent.Text = string.Empty;
    }
}