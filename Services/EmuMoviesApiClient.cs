using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmuMoviesTester.Models;
using Spectre.Console;

namespace EmuMoviesTester.Services;

public class EmuMoviesApiClient : IDisposable
{
    private readonly HttpClient _authClient;
    private readonly HttpClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _accessToken;
    private const int MaxRetries = 3;

    private const string AuthUrl = "https://emumovies.com/oauth/token/";
    private const string ApiBase = "https://emapi.emumovies.com/";
    private const string ClientId = "ff650dea5028d095b35d5ed6596b90b2";
    private const string ClientSecret = "bab6c5d31ae2dad7cafb62c645fb024bb3cb951de9bf11f9";

    public EmuMoviesApiClient()
    {
        _authClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _apiClient = new HttpClient
        {
            BaseAddress = new Uri(ApiBase),
            Timeout = TimeSpan.FromSeconds(60)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"] = "password",
            ["scope"] = "profile",
            ["username"] = username,
            ["password"] = password
        });

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await _authClient.PostAsync(AuthUrl, formData);

                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    AnsiConsole.MarkupLine($"[red]Auth failed ({(int)response.StatusCode}): {Markup.Escape(errBody)}[/]");
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, _jsonOptions);

                if (tokenResponse?.AccessToken == null)
                {
                    AnsiConsole.MarkupLine("[red]Auth failed: No access token in response[/]");
                    return false;
                }

                _accessToken = tokenResponse.AccessToken;
                _apiClient.DefaultRequestHeaders.Remove("X-EmuMovies-Token");
                _apiClient.DefaultRequestHeaders.Add("X-EmuMovies-Token", _accessToken);

                // Cache token
                var config = new TokenCache
                {
                    AccessToken = _accessToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600)
                };
                await File.WriteAllTextAsync("config.json", JsonSerializer.Serialize(config, _jsonOptions));

                return true;
            }
            catch (Exception ex)
            {
                if (attempt < MaxRetries)
                {
                    AnsiConsole.MarkupLine($"[yellow]Auth retry ({attempt}/{MaxRetries}): {Markup.Escape(ex.Message)}[/]");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Auth error: {Markup.Escape(ex.Message)}[/]");
                    return false;
                }
            }
        }

        return false;
    }

    public bool TryLoadCachedToken()
    {
        try
        {
            if (!File.Exists("config.json")) return false;
            var json = File.ReadAllText("config.json");
            var cache = JsonSerializer.Deserialize<TokenCache>(json, _jsonOptions);
            if (cache?.AccessToken == null || cache.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
                return false;

            _accessToken = cache.AccessToken;
            _apiClient.DefaultRequestHeaders.Remove("X-EmuMovies-Token");
            _apiClient.DefaultRequestHeaders.Add("X-EmuMovies-Token", _accessToken);
            return true;
        }
        catch { return false; }
    }

    public async Task<List<SystemInfo>> GetSystemsAsync()
    {
        var result = await ExecuteWithRetryAsync<List<SystemInfoInternal>>("api/Systems");
        if (result == null) return new List<SystemInfo>();

        return result
            .Select(s => new SystemInfo(s.Id, s.Name ?? "Unknown", s.Description, s.Manufacturer, s.Category))
            .OrderBy(s => s.Name)
            .ToList();
    }

    public async Task<List<MediaTypeInfo>> GetMediaTypesAsync()
    {
        var result = await ExecuteWithRetryAsync<List<MediaTypeInternal>>("api/Media/types");
        if (result == null) return new List<MediaTypeInfo>();

        return result
            .Select(t => new MediaTypeInfo(t.Id, t.Name ?? "Unknown", t.Description))
            .OrderBy(t => t.Name)
            .ToList();
    }

    public async Task<List<MediaFile>> GetMediaFilesAsync(int systemId, string mediaTypeName)
    {
        var result = await ExecuteWithRetryAsync<List<MediaFileInternal>>(
            $"api/Media/systems/{systemId}/files?type={Uri.EscapeDataString(mediaTypeName)}");
        if (result == null) return new List<MediaFile>();

        return result
            .Select(f => new MediaFile(f.Filename ?? f.Name ?? "unknown", f.Url, f.Size))
            .ToList();
    }

    public async Task<(bool success, int statusCode)> DownloadFileAsync(
        int systemId, string mediaTypeName, string filename, string outputPath)
    {
        if (_accessToken == null)
            return (false, 401);

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var url = $"api/Media/systems/{systemId}/download?type={Uri.EscapeDataString(mediaTypeName)}&filename={Uri.EscapeDataString(filename)}";
                var response = await _apiClient.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return (false, 404);

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return (false, 403);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
                    AnsiConsole.MarkupLine($"[yellow]Rate limited. Waiting {retryAfter.TotalSeconds}s...[/]");
                    await Task.Delay(retryAfter);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    return (false, (int)response.StatusCode);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                using var stream = await response.Content.ReadAsStreamAsync();
                using var file = File.Create(outputPath);
                await stream.CopyToAsync(file);
                return (true, 200);
            }
            catch (Exception ex)
            {
                if (attempt < MaxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                else
                    AnsiConsole.MarkupLine($"[red]Download error: {Markup.Escape(ex.Message)}[/]");
            }
        }

        return (false, 0);
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(string endpoint) where T : class
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await _apiClient.GetAsync(endpoint);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
                    await Task.Delay(wait);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < MaxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        AnsiConsole.MarkupLine($"[red]API error: {lastException?.Message ?? "Unknown"}[/]");
        return null;
    }

    public void Dispose()
    {
        _authClient.Dispose();
        _apiClient.Dispose();
    }

    // Internal models
    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType
    );

    private class TokenCache
    {
        public string? AccessToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    private record SystemInfoInternal(
        int Id,
        string? Name,
        string? Description,
        string? Manufacturer,
        string? Category
    );

    private record MediaTypeInternal(int Id, string? Name, string? Description);

    private record MediaFileInternal(string? Filename, string? Name, string? Url, long? Size);
}
