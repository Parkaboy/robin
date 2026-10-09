using Avalonia.Controls;

namespace Robin;

public partial class FeedConfirmationDialog : Window
{
    public FeedConfirmationDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        message.Text = prompt;
        confirmButton.Content = UiText.Get("ConfirmRemove");
        cancelButton.Content = UiText.Get("Cancel");
    }

    private void ConfirmClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(true);
    private void CancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(false);
}
