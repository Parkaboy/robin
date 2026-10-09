using Avalonia.Controls;

namespace Robin;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Title = UiText.Get("About");
        aboutTitle.Text = UiText.Get("About");
        aboutDescription.Text = UiText.Get("AboutDescription");
        var version = typeof(MainWindow).Assembly.GetName().Version
            ?? throw new InvalidOperationException("Robin's assembly version is unavailable.");
        versionText.Text = UiText.Format("VersionFormat", version.ToString(3));
        closeButton.Content = UiText.Get("Close");
    }

    private void CloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close();
}
