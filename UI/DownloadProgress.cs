using EmuMoviesTester.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace EmuMoviesTester.UI;

public static class DownloadProgress
{
    public static void DisplayStats(DownloadStats stats)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.DarkCyan)
            .AddColumn(new TableColumn("[grey]Metric[/]"))
            .AddColumn(new TableColumn("[grey]Count[/]").RightAligned());

        table.AddRow("[cyan]Downloaded[/]", $"[green]{stats.Downloaded}[/]");
        table.AddRow("[cyan]Skipped (existed)[/]", $"[grey]{stats.Skipped}[/]");
        table.AddRow("[cyan]Not Available[/]", $"[yellow]{stats.NotAvailable}[/]");
        table.AddRow("[cyan]Failed[/]", $"[red]{stats.Failed}[/]");
        table.AddRow("[white]Total Games[/]", $"[magenta]{stats.TotalGames}[/]");

        var panel = new Panel(table)
            .Header("[cyan] Download Summary [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Magenta1);

        AnsiConsole.Write(panel);
    }

    public static IRenderable BuildLivePanel(DownloadStats stats, int total)
    {
        var processed = stats.Downloaded + stats.Skipped + stats.NotAvailable + stats.Failed;
        var pct = total > 0 ? (double)processed / total * 100 : 0;

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn(new GridColumn());

        grid.AddRow("[grey]Type:[/]", $"[magenta]{Markup.Escape(stats.CurrentType)}[/]");
        grid.AddRow("[grey]File:[/]", $"[white]{Markup.Escape(TruncateFilename(stats.CurrentFile, 45))}[/]");
        grid.AddRow("", "");
        grid.AddRow("[cyan]Downloaded[/]", $"[green]{stats.Downloaded}[/]");
        grid.AddRow("[cyan]Skipped[/]", $"[grey]{stats.Skipped}[/]");
        grid.AddRow("[cyan]Not Available[/]", $"[yellow]{stats.NotAvailable}[/]");
        grid.AddRow("[cyan]Failed[/]", $"[red]{stats.Failed}[/]");
        grid.AddRow("", "");
        grid.AddRow("[grey]Progress[/]", $"[cyan]{processed}/{total} ({pct:F1}%)[/]");

        return new Panel(grid)
            .Header("[cyan] Downloading Media [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.DarkCyan)
            .Expand();
    }

    private static string TruncateFilename(string filename, int max)
    {
        if (filename.Length <= max) return filename;
        return "..." + filename[^(max - 3)..];
    }
}
