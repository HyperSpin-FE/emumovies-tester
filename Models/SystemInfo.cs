namespace EmuMoviesTester.Models;

public record SystemInfo(
    int Id,
    string Name,
    string? Description = null,
    string? Manufacturer = null,
    string? Category = null
);
