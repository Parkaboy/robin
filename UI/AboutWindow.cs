/// <summary>Displays application information and project links.</summary>
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Interactivity;

namespace Robin;

public partial class AboutWindow : Window
{
    /// <summary>Initializes the application information window.</summary>
    public AboutWindow()
    {
        InitializeComponent();
        Title = UiText.Get("AboutRobinReader");
        aboutTitle.Text = UiText.Get("RobinReader");
        var version = typeof(AboutWindow).Assembly.GetName().Version
            ?? throw new InvalidOperationException("Robin's assembly version is unavailable.");
        aboutVersion.Text = UiText.Format("VersionFormat", version.ToString(3));
        aboutDescription.Text = UiText.Get("AboutDescription");
        aboutTechnology.Text = UiText.Get("AboutTechnology");
        aboutCopyright.Text = UiText.Get("AboutCopyright");
        aboutLicense.Text = UiText.Get("AboutLicense");
        buyMeACoffeeButton.Content = UiText.Get("BuyMeACoffeeMessage");
        closeButton.Content = UiText.Get("Close");
        AutomationProperties.SetName(buyMeACoffeeButton, UiText.Get("BuyMeACoffeeMessage"));
        AutomationProperties.SetName(closeButton, UiText.Get("Close"));
    }

    /// <summary>Opens the configured donation page in the default browser.</summary>
    private void BuyMeACoffee_OnClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = UiText.Get("BuyMeACoffeeUrl"),
            UseShellExecute = true
        });
    }

    /// <summary>Closes the application information window.</summary>
    private void Close_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}