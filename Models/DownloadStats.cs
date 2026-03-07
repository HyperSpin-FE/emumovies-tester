namespace EmuMoviesTester.Models;

public class DownloadStats
{
    public int Downloaded { get; set; }
    public int Skipped { get; set; }
    public int NotAvailable { get; set; }
    public int Failed { get; set; }
    public int TotalGames { get; set; }
    public string CurrentFile { get; set; } = "";
    public string CurrentType { get; set; } = "";
}
