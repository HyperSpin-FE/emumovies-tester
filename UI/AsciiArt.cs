using Spectre.Console;

namespace EmuMoviesTester.UI;

public static class AsciiArt
{
    private static readonly string[] LogoLines =
    {
        @"███████╗███╗   ███╗██╗   ██╗███╗   ███╗ ██████╗ ██╗   ██╗██╗███████╗███████╗",
        @"██╔════╝████╗ ████║██║   ██║████╗ ████║██╔═══██╗██║   ██║██║██╔════╝██╔════╝",
        @"█████╗  ██╔████╔██║██║   ██║██╔████╔██║██║   ██║██║   ██║██║█████╗  ███████╗",
        @"██╔══╝  ██║╚██╔╝██║██║   ██║██║╚██╔╝██║██║   ██║╚██╗ ██╔╝██║██╔══╝  ╚════██║",
        @"███████╗██║ ╚═╝ ██║╚██████╔╝██║ ╚═╝ ██║╚██████╔╝ ╚████╔╝ ██║███████╗███████║",
        @"╚══════╝╚═╝     ╚═╝ ╚═════╝ ╚═╝     ╚═╝ ╚═════╝   ╚═══╝  ╚═╝╚══════╝╚══════╝",
        @"",
        @"████████╗███████╗███████╗████████╗███████╗██████╗ ",
        @"╚══██╔══╝██╔════╝██╔════╝╚══██╔══╝██╔════╝██╔══██╗",
        @"   ██║   █████╗  ███████╗   ██║   █████╗  ██████╔╝",
        @"   ██║   ██╔══╝  ╚════██║   ██║   ██╔══╝  ██╔══██╗",
        @"   ██║   ███████╗███████║   ██║   ███████╗██║  ██║",
        @"   ╚═╝   ╚══════╝╚══════╝   ╚═╝   ╚══════╝╚═╝  ╚═╝"
    };

    public static async Task DisplayLogoAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.WriteLine();

        foreach (var line in LogoLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                AnsiConsole.WriteLine();
                continue;
            }
            AnsiConsole.MarkupLine(CreateGradientLine(line));
            await Task.Delay(60);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]EmuMovies Media Download Tester v1.0[/]");
        AnsiConsole.WriteLine();
        await Task.Delay(300);
    }

    private static string CreateGradientLine(string line)
    {
        var result = new System.Text.StringBuilder();
        int length = line.Length;

        for (int i = 0; i < length; i++)
        {
            char c = line[i];
            if (c == ' ')
            {
                result.Append(c);
                continue;
            }

            float pos = (float)i / Math.Max(length - 1, 1);
            int r = (int)(pos * 255);
            int g = (int)((1 - pos) * 255);
            int b = 255;

            result.Append($"[rgb({r},{g},{b})]{EscapeMarkup(c)}[/]");
        }

        return result.ToString();
    }

    private static string EscapeMarkup(char c) => c switch
    {
        '[' => "[[",
        ']' => "]]",
        _ => c.ToString()
    };

    public static void DisplayError(string message) =>
        AnsiConsole.MarkupLine($"[red]✗ Error: {Markup.Escape(message)}[/]");

    public static void DisplaySuccess(string message) =>
        AnsiConsole.MarkupLine($"[cyan]✓ {Markup.Escape(message)}[/]");

    public static void DisplayInfo(string message) =>
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
}
