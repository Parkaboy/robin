# Robin

Robin is an open-source desktop RSS reader based on Owl Reader. It helps you
collect feeds, synchronize their articles, and read them in one place.

## Features

- Add RSS and Atom feeds using HTTP or HTTPS URLs.
- Automatically discover a feed URL when a website URL is entered.
- Synchronize saved feeds when the application starts, and synchronize
  individual feeds on demand.
- Browse feeds, articles, and article text in three resizable panes.
- Add, edit, and remove feeds, with confirmation before deleting a feed and its
  articles.
- Switch between light and dark themes.
- Use the interface in English, Spanish, German, Portuguese, or Italian.
- Use keyboard-accessible pane splitters and accessible names for key controls.
- View application information and version from the About menu.

## Requirements

- .NET 10 SDK
- An operating system supported by Avalonia Desktop

## Run

From the repository root, restore dependencies and start the application:

```sh
dotnet restore
dotnet run --project robin.csproj
```

The application applies its Entity Framework Core database migrations at
startup. The SQLite database is named `rss_reader.db` in the application's
working directory. Application logs are written to the `logs` directory.

Robin saves theme and language preferences to
`%APPDATA%/Robin/settings.json` on Windows, or the equivalent application-data
directory on other platforms.

## Tests

Run the unit tests from the repository root:

```sh
dotnet test Tests/Robin.Tests.csproj
```

The test project currently covers RSS helper behavior and localized UI text.

## Project layout

| Path | Purpose |
| --- | --- |
| `UI/` | Avalonia AXAML views, code-behind, and user preferences |
| `Resources/` | Neutral and culture-specific `.resx` UI strings |
| `Models/` | Feed, article, and category entities |
| `Data/` | Entity Framework Core database context |
| `Migrations/` | Database schema migrations |
| `Services/` | RSS synchronization and feed parsing |
| `Tests/` | Unit tests |

## Technology

- C# and .NET 10
- Avalonia UI with the Fluent theme
- Entity Framework Core with SQLite
- `System.ServiceModel.Syndication` for RSS/Atom feed parsing
- Serilog for application logging
