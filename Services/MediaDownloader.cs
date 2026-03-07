using EmuMoviesTester.Models;
using Spectre.Console;

namespace EmuMoviesTester.Services;

public class MediaDownloader
{
    private readonly IEmuMoviesApiClient _client;

    public MediaDownloader(IEmuMoviesApiClient client)
    {
        _client = client;
    }

    public async Task<DownloadStats> DownloadMediaAsync(
        SystemInfo system,
        List<MediaTypeInfo> mediaTypes,
        List<string> gameFiles,
        Action<DownloadStats> onProgress,
        CancellationToken cancellationToken = default)
    {
        var stats = new DownloadStats { TotalGames = gameFiles.Count };

        foreach (var mediaType in mediaTypes)
        {
            if (cancellationToken.IsCancellationRequested) break;

            stats.CurrentType = mediaType.Name;

            // Get available files for this system+type
            List<MediaFile> availableFiles;
            try
            {
                availableFiles = await _client.GetMediaFilesAsync(system.Id, mediaType.Name);
            }
            catch
            {
                availableFiles = new List<MediaFile>();
            }

            var availableSet = availableFiles
                .Select(f => f.Filename.ToLowerInvariant())
                .ToHashSet();

            foreach (var gameFile in gameFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var filename = Path.GetFileName(gameFile);
                var outputPath = Path.Combine("media", mediaType.Name, system.Name, filename);
                stats.CurrentFile = filename;

                // Skip if exists
                if (File.Exists(outputPath))
                {
                    stats.Skipped++;
                    onProgress(stats);
                    continue;
                }

                // Check if available
                if (!availableSet.Contains(filename.ToLowerInvariant()) && availableSet.Count > 0)
                {
                    stats.NotAvailable++;
                    onProgress(stats);
                    continue;
                }

                // Download
                var (success, statusCode) = await _client.DownloadFileAsync(
                    system.Id, mediaType.Name, filename, outputPath);

                if (success)
                    stats.Downloaded++;
                else if (statusCode == 404)
                    stats.NotAvailable++;
                else if (statusCode == 403)
                {
                    stats.NotAvailable++;
                    AnsiConsole.MarkupLine($"[yellow]403 Forbidden for {Markup.Escape(filename)} - skipping[/]");
                }
                else
                    stats.Failed++;

                onProgress(stats);
                await Task.Delay(50, cancellationToken); // small throttle
            }
        }

        return stats;
    }
}
