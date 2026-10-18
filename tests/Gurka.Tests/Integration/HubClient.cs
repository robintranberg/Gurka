using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Gurka.Tests.Integration;

// En SignalR-klient mot testservern som samlar "state", "err" och "chat" så att testerna kan vänta på dem.
public sealed class HubClient : IAsyncDisposable
{
    public HubConnection Conn { get; }
    readonly ConcurrentQueue<JsonElement> states = new();
    readonly ConcurrentQueue<string> errors = new();
    readonly ConcurrentQueue<(string from, string text)> chats = new();
    readonly SemaphoreSlim signal = new(0);

    public JsonElement? Last { get; private set; }
    public IReadOnlyCollection<string> Errors => errors;
    public IReadOnlyCollection<(string from, string text)> Chats => chats;

    HubClient(HubConnection c)
    {
        Conn = c;
        c.On<JsonElement>("state", s => { lock (this) { if (Last == null || s.GetProperty("v").GetInt64() > Last.Value.GetProperty("v").GetInt64() || Room(s) != Room(Last.Value)) Last = s; } states.Enqueue(s); signal.Release(); });
        c.On<string>("err", e => { errors.Enqueue(e); signal.Release(); });
        c.On<string, string>("chat", (f, t) => { chats.Enqueue((f, t)); signal.Release(); });
    }

    // versionsnumret räknas per bord, så ett nytt bord börjar om (samma regel som klienten i app.js)
    static string? Room(JsonElement s) => s.TryGetProperty("room", out var r) ? r.GetString() : null;

    public static async Task<HubClient> ConnectAsync<T>(WebApplicationFactory<T> f, Action<IHubConnectionBuilder>? cfg = null) where T : class
    {
        var server = f.Server;
        var b = new HubConnectionBuilder().WithUrl(new Uri(server.BaseAddress, "hub"), o =>
        {
            o.Transports = HttpTransportType.LongPolling;
            o.HttpMessageHandlerFactory = _ => server.CreateHandler();
        });
        cfg?.Invoke(b);
        var c = new HubClient(b.Build());
        await c.Conn.StartAsync(); // inget standardbord: tillstånd kommer först efter Hello med en rumskod
        return c;
    }

    // Väntar tills senaste tillståndet uppfyller villkoret.
    public async Task<JsonElement> WaitFor(Func<JsonElement, bool> pred, int ms = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (true)
        {
            var l = Last;
            if (l != null && pred(l.Value)) return l.Value;
            var left = until - DateTime.UtcNow;
            if (left <= TimeSpan.Zero) throw new TimeoutException("Fick aldrig väntat tillstånd. Senaste: " + l?.GetRawText());
            await signal.WaitAsync(left < TimeSpan.FromMilliseconds(200) ? left : TimeSpan.FromMilliseconds(200));
        }
    }

    public async Task<string> WaitForError(int ms = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        string? e;
        while (!errors.TryDequeue(out e))
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Fick inget felmeddelande.");
            await signal.WaitAsync(100);
        }
        return e;
    }

    public async Task<(string from, string text)> WaitForChat(Func<(string from, string text), bool> pred, int ms = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (true)
        {
            var hit = chats.FirstOrDefault(pred);
            if (hit != default) return hit;
            if (DateTime.UtcNow > until) throw new TimeoutException("Fick inget matchande chattmeddelande.");
            await signal.WaitAsync(100);
        }
    }

    public async ValueTask DisposeAsync() => await Conn.DisposeAsync();
}
