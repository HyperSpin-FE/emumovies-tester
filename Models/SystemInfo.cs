namespace EmuMoviesTester.Models;

public record SystemInfo(
    Guid Id,
    string Name,
    string? Description = null,
    string? Manufacturer = null,
    string? Category = null
);
