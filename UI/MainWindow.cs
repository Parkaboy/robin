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

        Title = "Robin Reader";
        Width = 1280;
        Height = 760;
        MinWidth = 800;
        MinHeight = 500;
        Background = new SolidColorBrush(Color.Parse("#F5F5F5"));

        feedTree.ItemTemplate = new FuncDataTemplate<Feed>((feed, _) =>
            feed == null
                ? new TextBlock { Text = "Unknown feed" }
                : CreateFeedTreeItem(feed));
        articleList.ItemTemplate = new FuncDataTemplate<Article>((article, _) =>
            article == null
                ? new Border
                {
                    Padding = new Thickness(12, 10),
                    Child = new TextBlock { Text = "Untitled article" }
                }
                : new Border
                {
                    Padding = new Thickness(12, 10),
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(article.Title) ? "Untitled article" : article.Title,
                        TextWrapping = TextWrapping.Wrap,
                        MaxHeight = 44
                    }
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
        Opened += async (_, _) => await InitializeAsync();
    }

    private Control CreateFeedTreeItem(Feed feed)
    {
        var item = new Border
        {
            Padding = new Thickness(0),
            ContextMenu = new ContextMenu
            {
                ItemsSource = new object[]
                {
                    new MenuItem
                    {
                        Header = "Modify feed",
                        Command = new SimpleCommand(() => RunFeedActionAsync(feed, EditSelectedFeedAsync))
                    },
                    new MenuItem
                    {
                        Header = "Sync feed",
                        Command = new SimpleCommand(() => RunFeedActionAsync(feed, SyncSelectedFeedAsync))
                    },
                    new MenuItem
                    {
                        Header = "Delete feed",
                        Command = new SimpleCommand(() => RunFeedActionAsync(feed, RemoveSelectedFeedAsync))
                    }
                }
            },
            Child = new StackPanel
            {
                Margin = new Thickness(10, 7),
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = feed.Title, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock
                    {
                        Text = feed.LastSyncSuccess
                            ? $"{feed.Articles.Count} article(s)"
                            : "Sync failed",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.Parse("#A6A6A6")),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };

        item.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(item).Properties.PointerUpdateKind == Avalonia.Input.PointerUpdateKind.RightButtonPressed)
                selectedFeed = feed;
        };

        return item;
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

        var feedBrand = new TextBlock { Text = "ROBIN", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White, LetterSpacing = 2 };
        var feedSubtitle = new TextBlock { Text = "YOUR READING DESK", FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#A6A6A6")), Margin = new Thickness(0, 6, 0, 24) };
        Grid.SetRow(feedSubtitle, 1);
        Grid.SetRow(feedTree, 2);

        var feedsPanel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#202020")),
            Padding = new Thickness(18, 24),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,*"),
                Children =
                {
                    feedBrand,
                    feedSubtitle,
                    feedTree
                }
            }
        };

        var inboxTitle = new TextBlock { Text = "Articles", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#1A1A1A")) };
        var inboxSubtitle = new TextBlock { Text = "Stories from the selected feed", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#616161")), Margin = new Thickness(0, 5, 0, 18) };
        Grid.SetRow(inboxSubtitle, 1);
        Grid.SetRow(articleList, 2);
        var articlesPanel = new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(22, 24),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,*"),
                Children =
                {
                    inboxTitle,
                    inboxSubtitle,
                    articleList
                }
            }
        };

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
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            Background = Brushes.White,
            Margin = new Thickness(24, 14)
        };
        topBar.Children.Add(new TextBlock { Text = "Reading list", FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var feedMenu = new Menu
        {
            ItemsSource = new[]
            {
                new MenuItem
                {
                    Header = "Feeds",
                    ItemsSource = new[]
                    {
                        new MenuItem { Header = "Edit selected feed", Command = new SimpleCommand(async () => await EditSelectedFeedAsync()) },
                        new MenuItem { Header = "Remove selected feed", Command = new SimpleCommand(async () => await RemoveSelectedFeedAsync()) }
                    }
                }
            },
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(feedMenu, 1);
        topBar.Children.Add(feedMenu);
        Grid.SetColumn(addFeedPanel, 2);
        topBar.Children.Add(addFeedPanel);
        status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(status, 3);
        topBar.Children.Add(status);
        root.Children.Add(topBar);
        Grid.SetRow(columns, 1);
        root.Children.Add(columns);
        return root;
    }

    private async Task LoadFeedsAsync()
    {
        var feeds = await dbContext.Feeds
            .AsNoTracking()
            .Include(feed => feed.Articles)
            .OrderBy(feed => feed.Title)
            .ToListAsync();
        feedTree.ItemsSource = feeds;
        status.Text = $"{feeds.Count} feed(s)";
    }

    private async Task InitializeAsync()
    {
        try
        {
            var feeds = await dbContext.Feeds.OrderBy(feed => feed.Title).ToListAsync();
            status.Text = feeds.Count == 0 ? "No feeds yet" : "Syncing feeds...";

            foreach (var feed in feeds)
            {
                try
                {
                    await syncService.SyncFeedAsync(feed);
                }
                catch (Exception ex)
                {
                    status.Text = $"Could not sync {feed.Title}: {ex.Message}";
                }
            }

            await LoadFeedsAsync();
        }
        catch (Exception ex)
        {
            status.Text = $"Could not load feeds: {ex.Message}";
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
            status.Text = articleList.ItemsSource is IEnumerable<Article> articles
                ? $"{articles.Count()} article(s)"
                : "No articles";
            if (!selectedFeed.LastSyncSuccess && !string.IsNullOrWhiteSpace(selectedFeed.LastSyncError))
                status.Text = selectedFeed.LastSyncError;
        }
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
        articleContent.Text = string.IsNullOrWhiteSpace(article.Content)
            ? "This feed does not include the article text. Open the original article using the link above."
            : article.Content;
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
            var newArticles = await syncService.SyncFeedAsync(feed);
            urlInput.Text = string.Empty;
            await LoadFeedsAsync();
            feedTree.SelectedItem = feedTree.ItemsSource is IEnumerable<Feed> loadedFeeds
                ? loadedFeeds.FirstOrDefault(loadedFeed => loadedFeed.Id == feed.Id)
                : null;
            status.Text = feed.LastSyncSuccess
                ? $"Added {feed.Title}. {newArticles.Count} new article(s)."
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

    private async Task EditSelectedFeedAsync()
    {
        if (selectedFeed == null)
        {
            status.Text = "Select a feed first.";
            return;
        }

        var newUrl = await ShowFeedUrlDialogAsync("Edit feed", selectedFeed.Url);
        if (newUrl == null)
            return;

        var feedToUpdate = await dbContext.Feeds.FirstAsync(feed => feed.Id == selectedFeed.Id);
        feedToUpdate.Url = newUrl;
        feedToUpdate.LastSyncSuccess = false;
        feedToUpdate.LastSyncError = null;
        await dbContext.SaveChangesAsync();
        status.Text = "Feed updated. It will sync the next time the app starts.";
        await LoadFeedsAsync();
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
            status.Text = ex.Message;
        }
    }

    private async Task SyncSelectedFeedAsync()
    {
        if (selectedFeed == null)
            return;

        status.Text = $"Syncing {selectedFeed.Title}...";
        var feed = await dbContext.Feeds.FirstAsync(item => item.Id == selectedFeed.Id);
        var newArticles = await syncService.SyncFeedAsync(feed);
        await LoadFeedsAsync();

        var refreshedFeed = feedTree.ItemsSource is IEnumerable<Feed> feeds
            ? feeds.FirstOrDefault(item => item.Id == feed.Id)
            : null;
        feedTree.SelectedItem = refreshedFeed;
        status.Text = feed.LastSyncSuccess
            ? $"{newArticles.Count} new article(s)"
            : feed.LastSyncError ?? "The feed could not be synchronized.";
    }

    private async Task RemoveSelectedFeedAsync()
    {
        if (selectedFeed == null)
        {
            status.Text = "Select a feed first.";
            return;
        }

        var feedId = selectedFeed.Id;
        var feedTitle = selectedFeed.Title;
        var confirmed = await ShowConfirmationDialogAsync("Remove feed", $"Remove '{feedTitle}' and its articles?");
        if (!confirmed)
            return;

        var feed = await dbContext.Feeds.FirstOrDefaultAsync(item => item.Id == feedId);
        if (feed == null)
        {
            selectedFeed = null;
            await LoadFeedsAsync();
            status.Text = "Feed was already removed.";
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
        status.Text = "Feed removed.";
    }

    private async Task<string?> ShowFeedUrlDialogAsync(string title, string initialUrl)
    {
        var input = new TextBox { Text = initialUrl, MinWidth = 420 };
        var dialog = new Window { Title = title, Width = 520, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var save = new Button { Content = "Save", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        save.Click += (_, _) =>
        {
            if (Uri.TryCreate(input.Text?.Trim(), UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                dialog.Close(uri.ToString());
            }
        };
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Feed URL" },
                input,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { save, cancel } }
            }
        };
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var dialog = new Window { Title = title, Width = 420, Height = 160, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var remove = new Button { Content = "Remove" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        remove.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { remove, cancel } }
            }
        };
        return await dialog.ShowDialog<bool>(this);
    }

    private void ClearArticle()
    {
        articleTitle.Text = "Reading pane";
        articleMeta.Text = string.Empty;
        articleContent.Text = string.Empty;
    }
}

public sealed class SimpleCommand : System.Windows.Input.ICommand
{
    private readonly Func<Task> execute;

    public SimpleCommand(Func<Task> execute) => this.execute = execute;

    public bool CanExecute(object? parameter) => true;
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
    public async void Execute(object? parameter) => await execute();
}