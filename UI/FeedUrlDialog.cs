using Avalonia.Automation;
using Avalonia.Controls;

namespace Robin;

public partial class FeedUrlDialog : Window
{
    public FeedUrlDialog(string title, string initialUrl)
    {
        InitializeComponent();
        Title = title;
        feedUrlLabel.Text = UiText.Get("FeedUrl");
        urlInput.Text = initialUrl;
        saveButton.Content = UiText.Get("Save");
        cancelButton.Content = UiText.Get("Cancel");
        AutomationProperties.SetName(urlInput, UiText.Get("FeedUrl"));
        AutomationProperties.SetLiveSetting(validation, Avalonia.Automation.AutomationLiveSetting.Polite);
    }

    private void SaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (Uri.TryCreate(urlInput.Text?.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            Close(uri.ToString());
            return;
        }

        validation.Text = UiText.Get("InvalidHttpUrl");
    }

    private void CancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(null);
}
