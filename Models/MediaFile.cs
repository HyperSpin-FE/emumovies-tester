namespace EmuMoviesTester.Models;

public record MediaFile(
    string Filename,
    string? Url = null,
    long? Size = null
);
