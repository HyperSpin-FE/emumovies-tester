using EmuMoviesTester.Models;
using Xunit;

namespace EmuMoviesTester.Tests;

public class ModelsTests
{
    [Fact]
    public void SystemInfo_ConstructsCorrectly()
    {
        var id = Guid.NewGuid();
        var si = new SystemInfo(id, "Nintendo NES", "8-bit console", "Nintendo", "Console");
        Assert.Equal(id, si.Id);
        Assert.Equal("Nintendo NES", si.Name);
        Assert.Equal("8-bit console", si.Description);
        Assert.Equal("Nintendo", si.Manufacturer);
        Assert.Equal("Console", si.Category);
    }

    [Fact]
    public void SystemInfo_OptionalFieldsAreNull()
    {
        var si = new SystemInfo(Guid.NewGuid(), "SNES");
        Assert.Null(si.Description);
        Assert.Null(si.Manufacturer);
        Assert.Null(si.Category);
    }

    [Fact]
    public void MediaTypeInfo_ConstructsCorrectly()
    {
        var mt = new MediaTypeInfo("Logos - Game", "Game logos");
        Assert.Equal("Logos - Game", mt.Name);
        Assert.Equal("Game logos", mt.Description);
    }

    [Fact]
    public void MediaTypeInfo_DescriptionOptional()
    {
        var mt = new MediaTypeInfo("Snapshots - Game");
        Assert.Equal("Snapshots - Game", mt.Name);
        Assert.Null(mt.Description);
    }

    [Fact]
    public void DownloadStats_DefaultValues()
    {
        var stats = new DownloadStats();
        Assert.Equal(0, stats.Downloaded);
        Assert.Equal(0, stats.Skipped);
        Assert.Equal(0, stats.NotAvailable);
        Assert.Equal(0, stats.Failed);
        Assert.Equal(0, stats.TotalGames);
        Assert.Equal("", stats.CurrentFile);
        Assert.Equal("", stats.CurrentType);
    }

    [Fact]
    public void DownloadStats_CanSetAllProperties()
    {
        var stats = new DownloadStats
        {
            Downloaded = 5,
            Skipped = 3,
            NotAvailable = 2,
            Failed = 1,
            TotalGames = 11,
            CurrentFile = "game.zip",
            CurrentType = "Logos - Game"
        };

        Assert.Equal(5, stats.Downloaded);
        Assert.Equal(3, stats.Skipped);
        Assert.Equal(2, stats.NotAvailable);
        Assert.Equal(1, stats.Failed);
        Assert.Equal(11, stats.TotalGames);
        Assert.Equal("game.zip", stats.CurrentFile);
        Assert.Equal("Logos - Game", stats.CurrentType);
    }

    [Fact]
    public void MediaFile_ConstructsCorrectly()
    {
        var mf = new MediaFile("game.zip", "https://example.com/game.zip", 1024);
        Assert.Equal("game.zip", mf.Filename);
        Assert.Equal("https://example.com/game.zip", mf.Url);
        Assert.Equal(1024, mf.Size);
    }
}
