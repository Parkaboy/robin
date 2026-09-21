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
    private readonly TextBox urlInput = new() { Watermark = "Paste a feed URL" };
    private readonly Button addFeedButton = new() { Content = "Add feed", HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly TreeView feedTree = new() { Background = Brushes.Transparent };
    private readonly ListBox articleList = new() { Background = Brushes.Transparent };
    private readonly TextBlock articleTitle = new() { FontSize = 28, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#1A1A1A")) };
    private readonly TextBlock articleMeta = new() { Foreground = new SolidColorBrush(Color.Parse("#616161")), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock articleContent = new() { TextWrapping = TextWrapping.Wrap, LineHeight = 1.55, FontSize = 16, Foreground = new SolidColorBrush(Color.Parse("#292929")) };
    private readonly TextBlock status = new() { Foreground = new SolidColorBrush(Color.Parse("#616161")) };
    private Feed? selectedFeed;

    public MainWindow(AppDbContext dbContext, IRssSyncService syncService)
    {
        this.dbContext = dbContext;
        this.syncService = syncService;

        Title = "Robin RSS Reader";
        Width = 1280;
        Height = 760;
        MinWidth = 800;
        MinHeight = 500;
        Background = new SolidColorBrush(Color.Parse("#F5F5F5"));

        feedTree.ItemTemplate = new FuncDataTemplate<Feed>((feed, _) =>
            new TextBlock { Text = feed.Title, Margin = new Thickness(10, 8), TextTrimming = TextTrimming.CharacterEllipsis });
        articleList.ItemTemplate = new FuncDataTemplate<Article>((article, _) =>
            new Border
            {
                Padding = new Thickness(12, 10),
                Child = new TextBlock { Text = article.Title, TextWrapping = TextWrapping.Wrap, MaxHeight = 44 }
            });

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
            ColumnSpacing = 8,
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        addFeedPanel.Children.Add(urlInput);
        Grid.SetColumn(addFeedButton, 1);
        addFeedPanel.Children.Add(addFeedButton);

        var feedsPanel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#202020")),
            Padding = new Thickness(18, 24),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,*"),
                Children =
                {
                    new TextBlock { Text = "ROBIN", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White, LetterSpacing = 2 },
                    new TextBlock { Text = "YOUR READING DESK", FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#A6A6A6")), Margin = new Thickness(0, 6, 0, 24) },
                    feedTree
                }
            }
        };
        Grid.SetRow(feedTree, 2);

        var articlesPanel = new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(22, 24),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,*"),
                Children =
                {
                    new TextBlock { Text = "Inbox", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#1A1A1A")) },
                    new TextBlock { Text = "Recent stories", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#616161")), Margin = new Thickness(0, 5, 0, 18) },
                    articleList
                }
            }
        };
        Grid.SetRow(articleList, 2);

        var details = new StackPanel { Spacing = 14, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        details.Children.Add(articleTitle);
        details.Children.Add(articleMeta);
        details.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#E1E1E1")), Margin = new Thickness(0, 4, 0, 8) });
        details.Children.Add(articleContent);

        var detailScroll = new ScrollViewer
        {
            Padding = new Thickness(48, 42),
            Background = new SolidColorBrush(Color.Parse("#FAFAFA")),
            Content = details,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,340,*"),
            RowDefinitions = new RowDefinitions("*"),
            Background = new SolidColorBrush(Color.Parse("#F5F5F5"))
        };
        columns.Children.Add(feedsPanel);
        Grid.SetColumn(articlesPanel, 1);
        columns.Children.Add(articlesPanel);
        Grid.SetColumn(detailScroll, 2);
        columns.Children.Add(detailScroll);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var topBar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Background = Brushes.White,
            Margin = new Thickness(24, 14)
        };
        topBar.Children.Add(new TextBlock { Text = "Reading list", FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(addFeedPanel, 1);
        topBar.Children.Add(addFeedPanel);
        status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(status, 2);
        topBar.Children.Add(status);
        root.Children.Add(topBar);
        Grid.SetRow(columns, 1);
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