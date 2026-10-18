using Microsoft.AspNetCore.SignalR;

// Tunt lager: slår upp anslutningens bord och plats, kör spellogiken i Game under lås och skickar ut nytt tillstånd.
public class GameHub(Lobby lb) : Hub
{
    string Conn => Context.ConnectionId;

    async Task Run(Func<Room, int, string?> f)
    {
        string? e; string? code = null;
        lock (lb.L)
        {
            var x = lb.Find(Conn);
            lb.Touch(Conn);
            if (x == null) e = "Inget bord – skapa ett bord eller öppna en inbjudningslänk.";
            else { var (r, m) = x.Value; e = f(r, m); if (e == null || r.G.Notes.Count > 0) code = r.Code; }
        }
        if (e != null) await Clients.Caller.SendAsync("err", e);
        if (code != null) await lb.Flush(new[] { code });
    }

    string Ip => Context.Items["ip"] as string ?? Flood.Ip(Context);

    // en ny anslutning har inget bord: den måste anropa Hello inom Lobby.LimboTtl, annars kopplas den ner
    public override async Task OnConnectedAsync()
    {
        bool ok;
        lock (lb.L)
        {
            ok = lb.Admit(Flood.Ip(Context));
            if (ok) { Context.Items["ip"] = Flood.Ip(Context); lb.Limbo[Conn] = (DateTime.UtcNow, Context); lb.Live[Conn] = (Context, DateTime.UtcNow); }
        }
        if (!ok) { Context.Abort(); return; } // för många anslutningar totalt, från samma IP eller samma IPv6 /48
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var t = new HashSet<string>();
        lock (lb.L) { lb.Detach(Conn, t); lb.Live.Remove(Conn); if (Context.Items["ip"] is string ip) lb.Release(ip); }
        await lb.Flush(t); await base.OnDisconnectedAsync(ex);
    }

    // klienten skickar sparad token och rumskod. Svarar med { token, room, seat, build }; token byts vid varje anrop och skickas bara
    // till den som anropar. Okänd token ger en ny token utan plats. Rumskod "" = inget bord (seat -1). Okänd rumskod ger err och null.
    public async Task<object?> Hello(string? token, string? room)
    {
        var t = new HashSet<string>(); string? e; int seat = -1; string code = Lobby.RoomCode(room), tok;
        lock (lb.L)
        {
            lb.Touch(Conn);
            e = lb.Attach(Conn, token, code, t, out tok, Ip);
            if (e == null && code != "") seat = lb.Rooms[code].G.SeatOf(tok);
        }
        await lb.Flush(t);
        if (e != null) { await Clients.Caller.SendAsync("err", e); return null; }
        return new { token = tok, room = code, seat, build = Build.Boot }; // build: identitet när processen startade (se Build i Program.cs)
    }

    // skapar ett bord och returnerar koden (8 tecken); anslutningen blir värd när den sätter sig (Join); null + err om taket är nått. Anropa sedan Hello(token, kod).
    public async Task<string?> NewRoom()
    {
        string? c; lock (lb.L) { lb.Touch(Conn); c = lb.NewRoom(Ip, Conn); }
        if (c == null) await Clients.Caller.SendAsync("err", "Du kan inte skapa fler bord just nu.");
        return c;
    }

    public Task Join(string name) => Run((r, m) =>
    {
        var id = lb.Conns[Conn].id;
        bool host = r.Creator != "" && r.Creator == Conn;
        var e = r.G.Join(id, name, host);
        if (e == null) { r.G.Seats[r.G.SeatOf(id)]!.Conn = Conn; if (host) r.Creator = ""; } // värdskapet från att skapa bordet tas bara en gång
        return e;
    });

    public Task Leave() => Run((r, m) => { r.G.Leave(m); return null; });
    public Task Start() => Run((r, m) => r.G.Start(m));
    public Task Exchange(int[] idx) => Run((r, m) => r.G.Exchange(m, idx));
    public Task Zlatan(bool take) => Run((r, m) => r.G.Zlatan(m, take));
    public Task Respond(bool all) => Run((r, m) => r.G.Respond(m, all));
    public Task UpPick(int idx, bool dig) => Run((r, m) => r.G.UpPick(m, idx, dig));
    public Task UpTake(bool take, int idx) => Run((r, m) => r.G.UpTake(m, take, idx));
    public Task Swap(int[] idx, bool dig) => Run((r, m) => r.G.Swap(m, idx, dig));
    public Task PlayCards(int[] idx) => Run((r, m) => r.G.Play(m, idx));

    public async Task Chat(string text)
    {
        text = Game.Clean(text, 200); if (text.Length == 0) return;
        string n; List<string> ids;
        lock (lb.L)
        {
            lb.Touch(Conn); var x = lb.Find(Conn); if (x == null) return;
            var (r, i) = x.Value; n = i >= 0 ? r.G.Seats[i]!.Name : "Åskådare"; ids = r.Conns.ToList();
        }
        await Clients.Clients(ids).SendAsync("chat", n, text);
    }
}
