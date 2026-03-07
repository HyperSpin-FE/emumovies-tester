using EmuMoviesTester.Models;
using EmuMoviesTester.Services;
using Moq;
using Xunit;

namespace EmuMoviesTester.Tests;

public class MediaDownloaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IEmuMoviesApiClient> _mockClient;
    private readonly MediaDownloader _downloader;
    private readonly SystemInfo _system;
    private readonly List<MediaTypeInfo> _mediaTypes;

    public MediaDownloaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        Directory.SetCurrentDirectory(_tempDir);

        _mockClient = new Mock<IEmuMoviesApiClient>();
        _downloader = new MediaDownloader(_mockClient.Object);

        _system = new SystemInfo(Guid.NewGuid(), "Nintendo NES");
        _mediaTypes = new List<MediaTypeInfo>
        {
            new MediaTypeInfo("Logos - Game"),
            new MediaTypeInfo("Snapshots - Game")
        };
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task ExistingFiles_AreSkipped()
    {
        // Pre-create the output file
        var outputDir = Path.Combine(_tempDir, "media", "Logos - Game", "Nintendo NES");
        Directory.CreateDirectory(outputDir);
        var existingFile = Path.Combine(outputDir, "game.zip");
        await File.WriteAllTextAsync(existingFile, "existing");

        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile>());

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            new List<MediaTypeInfo> { new("Logos - Game") },
            new List<string> { "game.zip" },
            _ => { });

        Assert.Equal(1, stats.Skipped);
        Assert.Equal(0, stats.Downloaded);
    }

    [Fact]
    public async Task SuccessfulDownload_IncrementsDownloaded()
    {
        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile> { new("game.zip", null, null) });

        _mockClient.Setup(c => c.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, 200));

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            new List<MediaTypeInfo> { new("Logos - Game") },
            new List<string> { "game.zip" },
            _ => { });

        Assert.Equal(1, stats.Downloaded);
        Assert.Equal(0, stats.Failed);
    }

    [Fact]
    public async Task Response404_IncrementsNotAvailable()
    {
        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile> { new("game.zip", null, null) });

        _mockClient.Setup(c => c.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, 404));

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            new List<MediaTypeInfo> { new("Logos - Game") },
            new List<string> { "game.zip" },
            _ => { });

        Assert.Equal(1, stats.NotAvailable);
        Assert.Equal(0, stats.Failed);
    }

    [Fact]
    public async Task Response403_IncrementsNotAvailable()
    {
        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile> { new("game.zip", null, null) });

        _mockClient.Setup(c => c.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, 403));

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            new List<MediaTypeInfo> { new("Logos - Game") },
            new List<string> { "game.zip" },
            _ => { });

        Assert.Equal(1, stats.NotAvailable);
    }

    [Fact]
    public async Task FailedDownload_IncrementsFailed()
    {
        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile> { new("game.zip", null, null) });

        _mockClient.Setup(c => c.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, 500));

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            new List<MediaTypeInfo> { new("Logos - Game") },
            new List<string> { "game.zip" },
            _ => { });

        Assert.Equal(1, stats.Failed);
    }

    [Fact]
    public async Task AllMediaTypes_AreIterated()
    {
        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile>());

        // empty available set + available count = 0 => goes to download
        _mockClient.Setup(c => c.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, 200));

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            _mediaTypes,
            new List<string> { "game.zip" },
            _ => { });

        // Called once per media type
        _mockClient.Verify(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Exactly(_mediaTypes.Count));
    }

    [Fact]
    public async Task CancellationToken_StopsProcessing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile>());

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            _mediaTypes,
            new List<string> { "game1.zip", "game2.zip", "game3.zip" },
            _ => { },
            cts.Token);

        // No media types iterated because token was already cancelled
        _mockClient.Verify(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FileNotInAvailableSet_IncrementsNotAvailable()
    {
        // Available set has a different file
        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile> { new("other.zip", null, null) });

        var stats = await _downloader.DownloadMediaAsync(
            _system,
            new List<MediaTypeInfo> { new("Logos - Game") },
            new List<string> { "game.zip" },
            _ => { });

        // game.zip is not in the available set (which has 1 item), so NotAvailable
        Assert.Equal(1, stats.NotAvailable);
        _mockClient.Verify(c => c.DownloadFileAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TotalGames_SetCorrectly()
    {
        _mockClient.Setup(c => c.GetMediaFilesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<MediaFile>());

        var games = new List<string> { "a.zip", "b.zip", "c.zip" };
        var stats = await _downloader.DownloadMediaAsync(
            _system, new List<MediaTypeInfo>(), games, _ => { });

        Assert.Equal(3, stats.TotalGames);
    }
}
