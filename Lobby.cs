using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

public class Room { public string Code = "", Ip = ""; public DateTime At = DateTime.UtcNow; public Game G = new(); public HashSet<string> Conns = new(); } // Ip = den som skapade bordet

// Alla bord och anslutningar. Allt tillstånd, även frånkopplingar och fristen i Sweeper, låses med L.
public class Lobby(IHubContext<GameHub> hub)
{
    public const int MaxRooms = 200, MaxRoomsPerIp = 3, MaxConnsPerRoom = 40, MaxConns = 400, MaxConnsPerIp = 8;
    public static readonly TimeSpan NewRoomTtl = TimeSpan.FromSeconds(60), LimboTtl = TimeSpan.FromSeconds(15);
    public const string NoRoom = "Bordet finns inte.";
    public readonly object L = new();
    public readonly Dictionary<string, Room> Rooms = new() { [""] = new Room() }; // "" = standardbordet, finns alltid
    public readonly Dictionary<string, (string room, string id)> Conns = new(); // anslutning -> rum och token (ny token för åskådare)
    public readonly Dictionary<string, (DateTime at, HubCallerContext ctx)> Limbo = new(); // anslutningar utan bord; kopplas ner efter LimboTtl
    public readonly Dictionary<string, int> Ips = new();
    readonly Dictionary<string, (double n, DateTime at)> Misses = new(); // misslyckade rumsgissningar per IP
    public readonly List<(string conn, string msg)> Errs = new(); // fel till enskilda anslutningar, skickas i Flush

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(); // 128 bitar
    public static bool IsToken(string? t) => t != null && Regex.IsMatch(t, "^[0-9a-f]{32}$");
    public static string RoomCode(string? r) { var c = Regex.Replace(r ?? "", "[^A-Za-z0-9]", "").ToUpperInvariant(); return c.Length > 8 ? c[..8] : c; } // "" = standardbordet

    // skapar ett bord med en ny kod (reserverad direkt); null om taket är nått
    public string? NewRoom(string ip)
    {
        const string a = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // utan 0/O och 1/I; 32^6 ≈ 1 miljard koder
        if (Rooms.Count - 1 >= MaxRooms || Rooms.Values.Count(r => r.Ip == ip) >= MaxRoomsPerIp) return null;
        while (true)
        {
            var c = string.Concat(Enumerable.Range(0, 6).Select(_ => a[RandomNumberGenerator.GetInt32(a.Length)]));
            if (Rooms.TryAdd(c, new Room { Code = c, Ip = ip })) return c;
        }
    }

    // tak för antal anslutningar totalt och per IP
    public bool Admit(string ip)
    {
        if (Ips.Values.Sum() >= MaxConns || Ips.GetValueOrDefault(ip) >= MaxConnsPerIp) return false;
        Ips[ip] = Ips.GetValueOrDefault(ip) + 1; return true;
    }
    public void Release(string ip) { if (Ips.TryGetValue(ip, out var n)) { if (n <= 1) Ips.Remove(ip); else Ips[ip] = n - 1; } }

    // tokenhink för rumsgissningar per IP: 10 i en skur, sedan en per 10 s
    bool CanGuess(string ip) { var now = DateTime.UtcNow; var (n, at) = Misses.GetValueOrDefault(ip, (10, now)); n = Math.Min(10, n + (now - at).TotalSeconds * 0.1); Misses[ip] = (n, now); return n >= 1; }
    void Miss(string ip) { var (n, at) = Misses[ip]; Misses[ip] = (n - 1, at); }

    public (Room r, int seat)? Find(string conn) => Conns.TryGetValue(conn, out var x) && Rooms.TryGetValue(x.room, out var r) ? (r, r.G.SeatOf(x.id)) : null;

    // platsen som token pekar på; föregående token duger bara medan spelaren är frånkopplad (svaret på Hello kan ha gått förlorat)
    (Room r, int seat)? Owner(string? token)
    {
        if (!IsToken(token)) return null;
        foreach (var r in Rooms.Values)
            for (int i = 0; i < 4; i++)
                if (r.G.Seats[i] is { } p && (Game.Eq(p.Id, token!) || (p.Conn == null && p.Prev != "" && Game.Eq(p.Prev, token!)))) return (r, i);
        return null;
    }

    // kopplar en anslutning till ett befintligt bord. Har token en plats där tas den tillbaka och token byts (tok = nya token).
    // Har token en plats vid ett annat bord lämnas den. Returnerar fel eller null; vid fel ändras ingenting. Rum som berörts läggs i touched.
    public string? Attach(string conn, string? token, string code, HashSet<string> touched, out string tok, string ip = "")
    {
        tok = "";
        var own = Owner(token);
        bool seated = own is { } q && q.r.Code == code; // en spelare med plats vid bordet släpps alltid in
        if (code != "" && !seated && !CanGuess(ip)) return NoRoom; // samma svar oavsett orsak, så koder inte kan räknas upp
        if (!Rooms.TryGetValue(code, out var r)) { Miss(ip); return NoRoom; }
        if (!seated && !r.Conns.Contains(conn) && r.Conns.Count >= MaxConnsPerRoom) return "Bordet har för många åskådare.";
        Limbo.Remove(conn);
        if (Conns.TryGetValue(conn, out var old) && !(own is { } w && w.r.Code == old.room && w.seat == w.r.G.SeatOf(old.id))) Detach(conn, touched, false);
        else if (old.room != null && Rooms.TryGetValue(old.room, out var or)) { or.Conns.Remove(conn); Conns.Remove(conn); }
        if (own is { } o && o.r != r) { o.r.G.Leave(o.seat); touched.Add(o.r.Code); own = null; }
        tok = NewToken();
        if (own is { } m)
        {
            var p = r.G.Seats[m.seat]!;
            // bara en anslutning styr platsen: andra flikar med samma spelare blir åskådare
            foreach (var c in r.Conns.Where(c => c != conn && Conns[c].id == p.Id).ToList()) { Conns[c] = (code, NewToken()); Errs.Add((c, "Du spelar nu i en annan flik eller enhet.")); }
            if (p.Gone != null) r.G.Notes.Add(p.Name + " är tillbaka.");
            p.Prev = p.Id; p.Id = tok; p.Conn = conn; p.Gone = null;
        }
        Rooms.TryAdd(code, r); // Detach ovan kan ha städat bort ett tomt bord
        Conns[conn] = (code, tok); r.Conns.Add(conn); touched.Add(code);
        return null;
    }

    // anslutningen försvann (drop) eller bytte bord/token (lämnar platsen direkt); en spelare som tappat anslutningen får frist
    public void Detach(string conn, HashSet<string> touched, bool drop = true)
    {
        Limbo.Remove(conn);
        if (!Conns.Remove(conn, out var x) || !Rooms.TryGetValue(x.room, out var r)) return;
        r.Conns.Remove(conn); touched.Add(r.Code);
        int i = r.G.SeatOf(x.id);
        if (i >= 0 && r.G.Seats[i]!.Conn == conn)
        {
            var p = r.G.Seats[i]!; p.Conn = null;
            if (drop) { p.Gone = DateTime.UtcNow; r.G.Notes.Add($"{p.Name} tappade anslutningen – platsen hålls i {(int)Game.Grace.TotalSeconds} s."); }
            else r.G.Leave(i);
        }
        Drop(r);
    }

    // tomma bord tas bort; standardbordet finns alltid och ett nytt bord får en stund på sig att få besökare
    void Drop(Room r) { if (r.Code != "" && r.Conns.Count == 0 && r.G.Seats.All(p => p == null) && DateTime.UtcNow - r.At > NewRoomTtl) Rooms.Remove(r.Code); }

    // släpper platser vars frist gått ut, tar bort tomma bord och kopplar ner anslutningar utan bord
    public void Sweep(HashSet<string> touched)
    {
        var now = DateTime.UtcNow;
        foreach (var r in Rooms.Values.ToList())
        {
            for (int i = 0; i < 4; i++) if (r.G.Seats[i] is { Gone: { } g } && now - g > Game.Grace) { r.G.Leave(i); touched.Add(r.Code); }
            Drop(r);
        }
        foreach (var (c, x) in Limbo.ToList()) if (now - x.at > LimboTtl) { Limbo.Remove(c); x.ctx.Abort(); }
        foreach (var k in Misses.Where(x => now - x.Value.at > TimeSpan.FromMinutes(5)).Select(x => x.Key).ToList()) Misses.Remove(k);
    }

    // skickar fel, bordsmeddelanden och tillstånd till alla vid de berörda borden
    public async Task Flush(IEnumerable<string> codes)
    {
        var sends = new List<Task>();
        lock (L)
        {
            foreach (var (c, m) in Errs) sends.Add(hub.Clients.Client(c).SendAsync("err", m));
            Errs.Clear();
            foreach (var code in codes.Distinct())
            {
                if (!Rooms.TryGetValue(code, out var r)) continue;
                var ids = r.Conns.ToList(); ++r.G.Ver;
                foreach (var n in r.G.Notes) sends.Add(hub.Clients.Clients(ids).SendAsync("chat", "Bordet", n));
                r.G.Notes.Clear();
                foreach (var c in ids) sends.Add(hub.Clients.Client(c).SendAsync("state", r.G.View(r.G.SeatOf(Conns[c].id), code)));
            }
        }
        await Task.WhenAll(sends);
    }
}

// släpper platser vars frist gått ut och uppdaterar nedräkningen hos klienterna
public class Sweeper(Lobby lb) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var t = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await t.WaitForNextTickAsync(ct))
        {
            var touched = new HashSet<string>();
            lock (lb.L) lb.Sweep(touched);
            if (touched.Count > 0) await lb.Flush(touched);
        }
    }
}

// flodskydd: tokenhinkar per anslutning och per IP, strängare för chatt och Hello (rumsgissning)
public class Flood : IHubFilter
{
    readonly Dictionary<string, (double n, DateTime at)> B = new();
    bool Take(string key, double rate, double cap)
    {
        lock (B)
        {
            var now = DateTime.UtcNow;
            if (B.Count > 20000) foreach (var k in B.Where(x => now - x.Value.at > TimeSpan.FromMinutes(1)).Select(x => x.Key).ToList()) B.Remove(k);
            var (n, at) = B.TryGetValue(key, out var x) ? x : (cap, now);
            n = Math.Min(cap, n + (now - at).TotalSeconds * rate);
            if (n < 1) { B[key] = (n, now); return false; }
            B[key] = (n - 1, now); return true;
        }
    }
    public static string Ip(HubCallerContext c) => c.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "?";
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext c, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        string m = c.HubMethodName, id = c.Context.ConnectionId, ip = "ip:" + Ip(c.Context);
        bool chat = m == nameof(GameHub.Chat), hello = m == nameof(GameHub.Hello) || m == nameof(GameHub.NewRoom);
        bool ok = Take(id, 5, 20) && Take(ip, 15, 60)
            && (!chat || (Take(id + ":c", 0.5, 5) && Take(ip + ":c", 1, 10)))
            && (!hello || (Take(id + ":h", 0.5, 5) && Take(ip + ":h", 1, 10)));
        if (!ok)
        {
            await c.Hub.Clients.Caller.SendAsync("err", chat ? "Du skriver för snabbt – vänta lite." : "För många anrop – vänta lite.");
            return null;
        }
        return await next(c);
    }
    public Task OnDisconnectedAsync(HubLifetimeContext c, Exception? e, Func<HubLifetimeContext, Exception?, Task> next)
    {
        var id = c.Context.ConnectionId;
        lock (B) { B.Remove(id); B.Remove(id + ":c"); B.Remove(id + ":h"); }
        return next(c, e);
    }
}
