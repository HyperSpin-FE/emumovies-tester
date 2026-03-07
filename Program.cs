using DotNetEnv;
using EmuMoviesTester.Models;
using EmuMoviesTester.Services;
using EmuMoviesTester.UI;
using Spectre.Console;

namespace EmuMoviesTester;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            await AsciiArt.DisplayLogoAsync();

            using var apiClient = new EmuMoviesApiClient();

            // Auth
            bool authenticated = false;
            while (!authenticated)
            {
                // Try cached token first
                if (apiClient.TryLoadCachedToken())
                {
                    AsciiArt.DisplaySuccess("Using cached token");
                    authenticated = true;
                    break;
                }

                // Try .env credentials
                string? username = null;
                string? password = null;

                if (File.Exists(".env"))
                {
                    Env.Load(".env");
                    username = Environment.GetEnvironmentVariable("EMUMOVIES_USERNAME");
                    password = Environment.GetEnvironmentVariable("EMUMOVIES_PASSWORD");
                }

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    AnsiConsole.MarkupLine("[cyan]EmuMovies Login[/]");
                    AnsiConsole.WriteLine();
                    username = AnsiConsole.Ask<string>("[grey]Username:[/]");
                    password = AnsiConsole.Prompt(
                        new TextPrompt<string>("[grey]Password:[/]").Secret());
                }

                AnsiConsole.WriteLine();

                bool success = false;
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Authenticating...[/]", async ctx =>
                    {
                        success = await apiClient.AuthenticateAsync(username!, password!);
                    });

                if (success)
                {
                    // Save to .env
                    await File.WriteAllTextAsync(".env",
                        $"EMUMOVIES_USERNAME={username}\nEMUMOVIES_PASSWORD={password}\n");
                    AsciiArt.DisplaySuccess("Authenticated successfully");
                    authenticated = true;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Authentication failed. Please try again.[/]");
                    AnsiConsole.WriteLine();
                    // Clear .env so we re-prompt
                    if (File.Exists(".env")) File.Delete(".env");
                }
            }

            AnsiConsole.WriteLine();

            // Load systems
            List<SystemInfo>? systems = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Loading systems...[/]", async ctx =>
                {
                    systems = await apiClient.GetSystemsAsync();
                });

            if (systems == null || systems.Count == 0)
            {
                AsciiArt.DisplayError("Failed to load systems from API");
                WaitForExit();
                return;
            }

            AnsiConsole.Clear();

            // Select system
            var selector = new SystemSelector(systems);
            var selectedSystem = selector.Run();

            if (selectedSystem == null)
            {
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return;
            }

            AnsiConsole.Clear();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]Selected:[/] [magenta]{Markup.Escape(selectedSystem.Name)}[/]");
            AnsiConsole.WriteLine();

            // Load media types
            List<MediaTypeInfo>? mediaTypes = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Loading media types...[/]", async ctx =>
                {
                    mediaTypes = await apiClient.GetMediaTypesAsync();
                });

            if (mediaTypes == null || mediaTypes.Count == 0)
            {
                AsciiArt.DisplayError("Failed to load media types");
                WaitForExit();
                return;
            }

            // Multi-select media types
            var selectedTypes = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("[cyan]Select media types to download:[/]")
                    .PageSize(15)
                    .MoreChoicesText("[grey]Scroll for more[/]")
                    .InstructionsText("[grey]Space[/] to toggle, [grey]Enter[/] to confirm")
                    .AddChoices(mediaTypes.Select(t => t.Name)));

            if (selectedTypes.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No media types selected. Exiting.[/]");
                WaitForExit();
                return;
            }

            var selectedTypeInfos = mediaTypes
                .Where(t => selectedTypes.Contains(t.Name))
                .ToList();

            AnsiConsole.WriteLine();

            // Scan games directory
            var gamesDir = Path.Combine("games", selectedSystem.Name);
            List<string> gameFiles = new();

            if (Directory.Exists(gamesDir))
            {
                gameFiles = Directory.GetFiles(gamesDir, "*", SearchOption.TopDirectoryOnly).ToList();
            }

            AnsiConsole.MarkupLine($"[grey]Games directory:[/] [white]{Markup.Escape(gamesDir)}[/]");
            AnsiConsole.MarkupLine($"[grey]ROM files found:[/] [cyan]{gameFiles.Count}[/]");
            AnsiConsole.WriteLine();

            if (gameFiles.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No ROM files found in {Markup.Escape(gamesDir)}[/]");
                AnsiConsole.MarkupLine("[grey]Place ROM files there and re-run.[/]");
                WaitForExit();
                return;
            }

            var confirm = AnsiConsole.Confirm(
                $"[cyan]Download {selectedTypes.Count} media type(s) for {gameFiles.Count} game(s)?[/]",
                defaultValue: true);

            if (!confirm)
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return;
            }

            AnsiConsole.WriteLine();

            // Download
            var downloader = new MediaDownloader(apiClient);
            DownloadStats? finalStats = null;
            var totalOps = gameFiles.Count * selectedTypeInfos.Count;

            await AnsiConsole.Live(DownloadProgress.BuildLivePanel(new DownloadStats { TotalGames = gameFiles.Count }, totalOps))
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    finalStats = await downloader.DownloadMediaAsync(
                        selectedSystem,
                        selectedTypeInfos,
                        gameFiles,
                        stats =>
                        {
                            ctx.UpdateTarget(DownloadProgress.BuildLivePanel(stats, totalOps));
                            ctx.Refresh();
                        });
                });

            // Summary
            AnsiConsole.Clear();
            AnsiConsole.WriteLine();
            await AsciiArt.DisplayLogoAsync();
            AnsiConsole.WriteLine();
            AsciiArt.DisplaySuccess("Download Complete!");
            AnsiConsole.WriteLine();
            DownloadProgress.DisplayStats(finalStats!);
            AnsiConsole.WriteLine();

            WaitForExit();
        }
        catch (Exception ex)
        {
            AsciiArt.DisplayError(ex.Message);
            WaitForExit();
        }
    }

    private static void WaitForExit()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
        Console.ReadKey(true);
    }
}
