using System.Net;
using System.Text;
using System.Text.Json;
using EmuMoviesTester.Services;
using Xunit;

namespace EmuMoviesTester.Tests;

/// <summary>
/// A delegating handler that uses a provided Func to handle requests.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);
}

public class EmuMoviesApiClientTests : IDisposable
{
    private readonly string _tempDir;

    public EmuMoviesApiClientTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        Directory.SetCurrentDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static HttpClient MakeApiClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://emapi.emumovies.com/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        return client;
    }

    [Fact]
    public void Constructor_SetsXApiKeyHeader()
    {
        var apiHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }));
        var authHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var authClient = new HttpClient(authHandler);
        var apiClient = MakeApiClient(apiHandler);

        using var sut = new EmuMoviesApiClient(authClient, apiClient);

        Assert.True(apiClient.DefaultRequestHeaders.Contains("X-API-Key"));
        var value = apiClient.DefaultRequestHeaders.GetValues("X-API-Key").First();
        Assert.Equal("45a36b69f17cfd831c085dd7fcc39caf4c38b58b007e0acacb50c34e00e400eb", value);
    }

    [Fact]
    public async Task AuthenticateAsync_SendsCorrectFormData()
    {
        IEnumerable<KeyValuePair<string, string>>? capturedForm = null;

        var authHandler = new FakeHttpMessageHandler(async (req, ct) =>
        {
            var form = await req.Content!.ReadAsStringAsync(ct);
            // parse the url-encoded form
            capturedForm = form.Split('&')
                .Select(p => p.Split('='))
                .Where(p => p.Length == 2)
                .Select(p => new KeyValuePair<string, string>(
                    Uri.UnescapeDataString(p[0]),
                    Uri.UnescapeDataString(p[1])));

            var json = JsonSerializer.Serialize(new { access_token = "tok123", expires_in = 3600 });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var apiHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var sut = new EmuMoviesApiClient(new HttpClient(authHandler), MakeApiClient(apiHandler));
        var result = await sut.AuthenticateAsync("testuser", "testpass");

        Assert.True(result);
        Assert.NotNull(capturedForm);
        var dict = capturedForm!.ToDictionary(k => k.Key, v => v.Value);
        Assert.Equal("password", dict["grant_type"]);
        Assert.Equal("profile", dict["scope"]);
        Assert.Equal("testuser", dict["username"]);
        Assert.Equal("testpass", dict["password"]);
        Assert.True(dict.ContainsKey("client_id"));
        Assert.True(dict.ContainsKey("client_secret"));
    }

    [Fact]
    public async Task AuthenticateAsync_SavesTokenToConfigJson()
    {
        var authHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            var json = JsonSerializer.Serialize(new { access_token = "mytoken", expires_in = 7200 });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var sut = new EmuMoviesApiClient(new HttpClient(authHandler), MakeApiClient(new FakeHttpMessageHandler((r, c) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        await sut.AuthenticateAsync("u", "p");

        Assert.True(File.Exists("config.json"));
        var content = await File.ReadAllTextAsync("config.json");
        Assert.Contains("mytoken", content);
    }

    [Fact]
    public async Task AuthenticateAsync_SetsEmuMoviesTokenHeader()
    {
        HttpRequestMessage? capturedApiReq = null;

        var authHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            var json = JsonSerializer.Serialize(new { access_token = "bearer-tok", expires_in = 3600 });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var apiHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            capturedApiReq = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        });

        var apiClient = MakeApiClient(apiHandler);
        using var sut = new EmuMoviesApiClient(new HttpClient(authHandler), apiClient);
        await sut.AuthenticateAsync("u", "p");

        Assert.True(apiClient.DefaultRequestHeaders.Contains("X-EmuMovies-Token"));
        Assert.Equal("bearer-tok", apiClient.DefaultRequestHeaders.GetValues("X-EmuMovies-Token").First());
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsFalse_OnNonSuccess()
    {
        var authHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("invalid")
            }));

        using var sut = new EmuMoviesApiClient(new HttpClient(authHandler), MakeApiClient(new FakeHttpMessageHandler((r, c) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        var result = await sut.AuthenticateAsync("u", "p");
        Assert.False(result);
    }

    [Fact]
    public void TryLoadCachedToken_ReturnsFalse_WhenNoFile()
    {
        using var sut = new EmuMoviesApiClient(new HttpClient(), MakeApiClient(new FakeHttpMessageHandler((r, c) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        Assert.False(sut.TryLoadCachedToken());
    }

    [Fact]
    public async Task TryLoadCachedToken_ReturnsFalse_WhenExpired()
    {
        var cache = new { AccessToken = "expiredtok", ExpiresAt = DateTime.UtcNow.AddMinutes(-10) };
        await File.WriteAllTextAsync("config.json", JsonSerializer.Serialize(cache));

        using var sut = new EmuMoviesApiClient(new HttpClient(), MakeApiClient(new FakeHttpMessageHandler((r, c) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        Assert.False(sut.TryLoadCachedToken());
    }

    [Fact]
    public async Task TryLoadCachedToken_ReturnsTrue_WhenValid()
    {
        var cache = new { AccessToken = "validtok", ExpiresAt = DateTime.UtcNow.AddHours(2) };
        await File.WriteAllTextAsync("config.json", JsonSerializer.Serialize(cache));

        using var sut = new EmuMoviesApiClient(new HttpClient(), MakeApiClient(new FakeHttpMessageHandler((r, c) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        Assert.True(sut.TryLoadCachedToken());
    }

    [Fact]
    public async Task GetSystemsAsync_RetryOnServerError_ThenSuccess()
    {
        int callCount = 0;
        var apiHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            callCount++;
            if (callCount == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var json = "[{\"Id\":\"b09b27d4-c443-45d6-6f37-08da70cd4f80\",\"Name\":\"NES\"}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var sut = new EmuMoviesApiClient(new HttpClient(), MakeApiClient(apiHandler));
        var systems = await sut.GetSystemsAsync();

        Assert.Equal(2, callCount);
        Assert.Single(systems);
        Assert.Equal("NES", systems[0].Name);
    }

    [Fact]
    public async Task GetSystemsAsync_ReturnsEmpty_AfterAllRetriesFail()
    {
        var apiHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        using var sut = new EmuMoviesApiClient(new HttpClient(), MakeApiClient(apiHandler));
        var systems = await sut.GetSystemsAsync();

        Assert.Empty(systems);
    }

    [Fact]
    public async Task GetMediaTypesAsync_ParsesNameAndDescription()
    {
        var json = "[{\"name\":\"Logos - Game\",\"description\":\"Logos - Game\"}]";
        var apiHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));

        using var sut = new EmuMoviesApiClient(new HttpClient(), MakeApiClient(apiHandler));
        var types = await sut.GetMediaTypesAsync();

        Assert.Single(types);
        Assert.Equal("Logos - Game", types[0].Name);
        Assert.Equal("Logos - Game", types[0].Description);
    }

    [Fact]
    public async Task DownloadFileAsync_Handles429RateLimit()
    {
        int callCount = 0;
        var apiHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
                return Task.FromResult(r);
            }
            var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

        var authHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            var json = JsonSerializer.Serialize(new { access_token = "tok", expires_in = 3600 });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var apiClient = MakeApiClient(apiHandler);
        using var sut = new EmuMoviesApiClient(new HttpClient(authHandler), apiClient);
        await sut.AuthenticateAsync("u", "p");

        var outPath = Path.Combine(_tempDir, "test_output", "file.zip");
        var systemId = Guid.NewGuid();
        var (success, code) = await sut.DownloadFileAsync(systemId, "Logos", "file.zip", outPath);

        Assert.True(success);
        Assert.Equal(200, code);
        Assert.Equal(2, callCount);
    }
}
