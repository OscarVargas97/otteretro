using System.Diagnostics;
using System.Net;
using Otteretro.App;
using Otteretro.Data;

namespace Otteretro.Tests;

public class ApiClientTests
{
    /// <summary>Responde lo que se le pida y anota cuándo llegó cada llamada.</summary>
    sealed class FakeServer(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public readonly List<TimeSpan> Calls = [];
        readonly Stopwatch clock = Stopwatch.StartNew();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Calls) Calls.Add(clock.Elapsed);
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent($"{request.RequestUri} {body}") };
        }
    }

    static Database NewDb(TempDir tmp)
    {
        var db = new Database(tmp["otteretro.db"]);
        Migrations.Apply(db);
        return db;
    }

    /// <summary>Listo cuando de F1.3: repetir una consulta sale de la caché, sin las claves en la clave.</summary>
    [Fact]
    public async Task RepeatedQueryComesFromTheCache()
    {
        using var tmp = new TempDir();
        var server = new FakeServer();
        var api = new ApiClient(NewDb(tmp), server);

        var first = await api.Send("test-cache", HttpMethod.Post, "https://x/games", "fields name;", TimeSpan.FromDays(1), r => r.Headers.Add("Authorization", "Bearer a"));
        var again = await api.Send("test-cache", HttpMethod.Post, "https://x/games", "fields name;", TimeSpan.FromDays(1), r => r.Headers.Add("Authorization", "Bearer b"));
        await api.Send("test-cache", HttpMethod.Post, "https://x/games", "fields year;", TimeSpan.FromDays(1));
        await api.Send("test-cache", HttpMethod.Get, "https://x/old", null, TimeSpan.Zero);
        await api.Send("test-cache", HttpMethod.Get, "https://x/old", null, TimeSpan.Zero);

        Assert.Equal(first, again);
        Assert.Equal(4, server.Calls.Count); // la segunda consulta igual no salió; la vencida sí
    }

    [Fact]
    public async Task ErrorsAreNotCached()
    {
        using var tmp = new TempDir();
        var server = new FakeServer(HttpStatusCode.TooManyRequests);
        var api = new ApiClient(NewDb(tmp), server);
        for (var i = 0; i < 2; i++)
        {
            var e = await Assert.ThrowsAsync<HttpRequestException>(() => api.Send("test-error", HttpMethod.Get, "https://x/a", null, TimeSpan.FromDays(1)));
            Assert.Equal(HttpStatusCode.TooManyRequests, e.StatusCode);
        }
        Assert.Equal(2, server.Calls.Count);
    }

    /// <summary>Listo cuando de F1.3: con muchas llamadas a la vez, IGDB nunca recibe más de 4 en un segundo.</summary>
    [Fact]
    public async Task IgdbNeverGetsMoreThanFourPerSecond()
    {
        using var tmp = new TempDir();
        var server = new FakeServer();
        var api = new ApiClient(NewDb(tmp), server);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            api.Send("igdb", HttpMethod.Post, "https://api.igdb.com/v4/games", $"where id = {i};", TimeSpan.FromDays(1))));

        var calls = server.Calls.Order().ToList();
        Assert.Equal(10, calls.Count);
        for (var i = 0; i + 4 < calls.Count; i++)
            Assert.True(calls[i + 4] - calls[i] >= TimeSpan.FromSeconds(1), $"5 llamadas en {calls[i + 4] - calls[i]}");
    }
}
