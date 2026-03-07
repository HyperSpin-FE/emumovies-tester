using EmuMoviesTester.Models;
using Spectre.Console;

namespace EmuMoviesTester.UI;

public class SystemSelector
{
    private readonly List<SystemInfo> _allSystems;
    private List<SystemInfo> _filteredSystems;
    private string _filter = "";
    private int _selectedIndex = 0;
    private int _scrollOffset = 0;
    private const int VisibleItems = 16;

    public SystemSelector(List<SystemInfo> systems)
    {
        _allSystems = systems;
        _filteredSystems = systems;
    }

    public SystemInfo? Run()
    {
        SystemInfo? result = null;
        var done = false;

        AnsiConsole.Live(BuildLayout())
            .AutoClear(false)
            .Start(ctx =>
            {
                while (!done)
                {
                    ctx.UpdateTarget(BuildLayout());
                    ctx.Refresh();

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.UpArrow:
                                MoveSelection(-1);
                                break;
                            case ConsoleKey.DownArrow:
                                MoveSelection(1);
                                break;
                            case ConsoleKey.PageUp:
                                MoveSelection(-VisibleItems);
                                break;
                            case ConsoleKey.PageDown:
                                MoveSelection(VisibleItems);
                                break;
                            case ConsoleKey.Enter:
                                if (_filteredSystems.Count > 0)
                                {
                                    result = _filteredSystems[_selectedIndex];
                                    done = true;
                                }
                                break;
                            case ConsoleKey.Escape:
                                done = true;
                                break;
                            case ConsoleKey.Backspace:
                                if (_filter.Length > 0)
                                {
                                    _filter = _filter[..^1];
                                    ApplyFilter();
                                }
                                break;
                            default:
                                if (!char.IsControl(key.KeyChar))
                                {
                                    _filter += key.KeyChar;
                                    ApplyFilter();
                                }
                                break;
                        }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            });

        return result;
    }

    private Layout BuildLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Main"),
                new Layout("Footer").Size(3)
            );

        layout["Main"].Update(BuildMainPanel());
        layout["Footer"].Update(BuildFooter());

        return layout;
    }

    private Panel BuildMainPanel()
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Systems").NoWrap());

        var filterText = string.IsNullOrEmpty(_filter)
            ? "[grey]Type to filter...[/]"
            : $"[magenta]{_filter.EscapeMarkup()}[/]";
        table.AddRow($"  > {filterText}");
        table.AddRow("");

        if (_scrollOffset > 0)
            table.AddRow("[grey]    ▲ more above[/]");
        else
            table.AddRow("");

        var visibleEnd = Math.Min(_scrollOffset + VisibleItems, _filteredSystems.Count);
        for (int i = _scrollOffset; i < visibleEnd; i++)
        {
            var system = _filteredSystems[i];
            var name = system.Name.EscapeMarkup();
            if (name.Length > 50) name = name[..48] + "..";

            if (i == _selectedIndex)
                table.AddRow($"[cyan]  ▶ {name}[/]");
            else
                table.AddRow($"[white]    {name}[/]");
        }

        for (int i = visibleEnd - _scrollOffset; i < VisibleItems; i++)
            table.AddRow("");

        if (visibleEnd < _filteredSystems.Count)
            table.AddRow("[grey]    ▼ more below[/]");
        else
            table.AddRow("");

        table.AddRow("");
        var status = _filteredSystems.Count == 0
            ? "[yellow]No systems match filter[/]"
            : $"[grey]{_filteredSystems.Count} systems[/]";
        table.AddRow($"  {status}");

        return new Panel(table)
            .Header("[cyan] Select a System [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.DarkCyan)
            .Expand();
    }

    private Panel BuildFooter()
    {
        return new Panel("[grey]↑↓/PgUp/PgDn[/] Navigate  [darkcyan]|[/]  [grey]Enter[/] Select  [darkcyan]|[/]  [grey]Esc[/] Cancel  [darkcyan]|[/]  [grey]Type[/] Filter")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.DarkCyan)
            .Expand();
    }

    private void MoveSelection(int delta)
    {
        if (_filteredSystems.Count == 0) return;
        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _filteredSystems.Count - 1);
        if (_selectedIndex < _scrollOffset) _scrollOffset = _selectedIndex;
        else if (_selectedIndex >= _scrollOffset + VisibleItems) _scrollOffset = _selectedIndex - VisibleItems + 1;
    }

    private void ApplyFilter()
    {
        _filteredSystems = string.IsNullOrEmpty(_filter)
            ? _allSystems
            : _allSystems.Where(s => s.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToList();
        _selectedIndex = 0;
        _scrollOffset = 0;
    }
}
