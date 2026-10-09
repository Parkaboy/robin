using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Robin;

public class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;
    public static UserPreferences Preferences { get; private set; } = new();

    public override void Initialize()
    {
        Preferences = UserPreferences.Load();
        UiText.SetLanguage(Preferences.Language);
        RequestedThemeVariant = Preferences.IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public static void ApplyThemeVariant()
    {
        if (Current is App app)
            app.RequestedThemeVariant = Preferences.IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var scope = Services.CreateScope();
            desktop.MainWindow = new MainWindow(
                scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                scope.ServiceProvider.GetRequiredService<IRssSyncService>());
            desktop.Exit += (_, _) => scope.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}