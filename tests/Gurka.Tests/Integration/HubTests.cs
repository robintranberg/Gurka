using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Gurka.Tests.Integration;

// Hubben via riktiga SignalR-klienter mot en testserver. Varje test får en egen server och därmed en egen Lobby.
public class HubTests : IAsyncLifetime
{
    readonly WebApplicationFactory<Program> f = new();
    readonly List<HubClient> clients = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { foreach (var c in clients) await c.DisposeAsync(); await f.DisposeAsync(); }

    Lobby Lobby => f.Services.GetRequiredService<Lobby>();

    async Task<HubClient> Connect()
    {
        var c = await HubClient.ConnectAsync(f);
        clients.Add(c);
        return c;
    }

    // bord skapas med NewRoom; testerna använder namn som "R1" som översätts till den riktiga koden
    readonly Dictionary<string, string> codes = new();
    HubClient? host;
    async Task<string> Code(string key)
    {
        if (key == "") return "";
        if (!codes.TryGetValue(key, out var c))
        {
            host ??= await Connect();
            c = codes[key] = await host.Conn.InvokeAsync<string?>("NewRoom") ?? throw new Exception("NewRoom gav null");
        }
        return c;
    }

    async Task<JsonElement> Hello(HubClient c, string? token, string room) => await c.Conn.InvokeAsync<JsonElement>("Hello", token, await Code(room));

    // ansluter, hälsar med rumskod och sätter sig
    async Task<(HubClient c, string token)> Player(string room, string name, string? token = null)
    {
        var c = await Connect();
        var h = await Hello(c, token, room);
        Assert.Equal(JsonValueKind.Object, h.ValueKind);
        await c.Conn.InvokeAsync("Join", name);
        await c.WaitFor(s => s.GetProperty("you").GetInt32() >= 0);
        return (c, h.GetProperty("token").GetString()!);
    }

    static int Seat(JsonElement s) => s.GetProperty("you").GetInt32();
    static string Phase(JsonElement s) => s.GetProperty("phase").GetString()!;

    [Fact]
    public async Task Ny_anslutning_får_tillstånd_i_lobbyn_som_åskådare()
    {
        var c = await Connect();
        var s = c.Last!.Value;
        Assert.Equal("lobby", Phase(s));
        Assert.Equal(-1, Seat(s));
        Assert.Equal(JsonValueKind.Null, s.GetProperty("hand").ValueKind);
    }

    [Fact]
    public async Task Hello_ger_token_och_normaliserad_rumskod()
    {
        var code = await Code("R1");
        var c = await Connect();
        var h = await c.Conn.InvokeAsync<JsonElement>("Hello", null, " " + code[..3].ToLowerInvariant() + "-" + code[3..] + "! ");
        Assert.Matches("^[0-9a-f]{32}$", h.GetProperty("token").GetString());
        Assert.Equal(code, h.GetProperty("room").GetString());
        Assert.Equal(-1, h.GetProperty("seat").GetInt32());
    }

    [Fact]
    public async Task Hello_till_okänt_bord_ger_fel()
    {
        var c = await Connect();
        var h = await c.Conn.InvokeAsync<JsonElement>("Hello", null, "ZZZZZZ");
        Assert.Equal(JsonValueKind.Null, h.ValueKind);
        Assert.Equal(Lobby.NoRoom, await c.WaitForError());
    }

    [Fact]
    public async Task Standardbordet_finns_alltid()
    {
        var c = await Connect();
        var h = await c.Conn.InvokeAsync<JsonElement>("Hello", null, "");
        Assert.Equal("", h.GetProperty("room").GetString());
        await c.Conn.InvokeAsync("Join", "Anna");
        await c.WaitFor(s => Seat(s) == 0);
    }

    [Fact]
    public void Rumsgissningar_bromsas_per_IP()
    {
        var lb = Lobby; string real;
        lock (lb.L) real = lb.NewRoom("1.2.3.4")!;
        var t = new HashSet<string>();
        lock (lb.L)
        {
            for (int i = 0; i < 10; i++) Assert.Equal(Lobby.NoRoom, lb.Attach("g" + i, null, "NOPE" + i, t, out _, "6.6.6.6"));
            // efter tio missar avvisas även rätt kod från samma IP, med samma svar
            Assert.Equal(Lobby.NoRoom, lb.Attach("g10", null, real, t, out _, "6.6.6.6"));
            // en annan IP påverkas inte
            Assert.Null(lb.Attach("h0", null, real, t, out _, "7.7.7.7"));
        }
    }

    [Fact]
    public async Task NewRoom_ger_kort_kod_utan_förväxlingsbara_tecken()
    {
        var c = await Connect();
        var seen = new HashSet<string>();
        for (int i = 0; i < 3; i++) // högst tre bord per IP
        {
            var code = await c.Conn.InvokeAsync<string?>("NewRoom");
            Assert.Matches("^[A-HJ-NP-Z2-9]{4,8}$", code);
            Assert.True(seen.Add(code!));
        }
        Assert.Null(await c.Conn.InvokeAsync<string?>("NewRoom"));
        Assert.Equal("Du kan inte skapa fler bord just nu.", await c.WaitForError());
    }

    [Fact]
    public async Task Join_sätter_spelaren_vid_bordet_och_alla_ser_det()
    {
        var (a, _) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        var s = await a.WaitFor(s => s.GetProperty("seats")[1].ValueKind != JsonValueKind.Null);
        Assert.Equal("Anna", s.GetProperty("seats")[0].GetProperty("name").GetString());
        Assert.Equal("Bert", s.GetProperty("seats")[1].GetProperty("name").GetString());
        Assert.Equal(0, Seat(s));
        Assert.Equal(1, Seat(b.Last!.Value));
    }

    [Fact]
    public async Task Start_med_en_spelare_ger_fel_bara_till_den_som_anropade()
    {
        var (a, _) = await Player("R1", "Anna");
        var watcher = await Connect();
        await Hello(watcher, null, "R1");
        await a.Conn.InvokeAsync("Start");
        Assert.Equal("Minst 2 spelare krävs.", await a.WaitForError());
        await Task.Delay(200);
        Assert.Empty(watcher.Errors);
    }

    [Fact]
    public async Task Varje_spelare_ser_bara_sin_egen_hand()
    {
        var (a, _) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        var spec = await Connect();
        await Hello(spec, null, "R1");
        await a.Conn.InvokeAsync("Start");
        var sa = await a.WaitFor(s => Phase(s) == "exchange");
        var sb = await b.WaitFor(s => Phase(s) == "exchange");
        var ss = await spec.WaitFor(s => Phase(s) == "exchange");
        Assert.Equal(7, sa.GetProperty("hand").GetArrayLength());
        Assert.Equal(7, sb.GetProperty("hand").GetArrayLength());
        Assert.NotEqual(sa.GetProperty("hand").GetRawText(), sb.GetProperty("hand").GetRawText());
        Assert.Equal(JsonValueKind.Null, ss.GetProperty("hand").ValueKind);
        // Berts sex dolda kort får inte finnas någonstans i Annas tillstånd (det sjunde visas öppet för alla)
        var raw = sa.GetRawText();
        foreach (var c in sb.GetProperty("hand").EnumerateArray().Take(6))
            Assert.DoesNotContain($"{{\"r\":{c.GetProperty("r").GetInt32()},\"s\":{c.GetProperty("s").GetInt32()}}}", raw);
    }

    [Fact]
    public async Task Rum_är_isolerade_från_varandra()
    {
        var (a, _) = await Player("RUMA", "Anna");
        var (b, _) = await Player("RUMB", "Bert");
        await a.Conn.InvokeAsync("Chat", "hej rum A");
        await a.WaitForChat(m => m.text == "hej rum A");
        await Task.Delay(300);
        Assert.DoesNotContain(b.Chats, m => m.text == "hej rum A");
        Assert.Equal(JsonValueKind.Null, b.Last!.Value.GetProperty("seats")[1].ValueKind); // Anna syns inte i rum B
        Assert.Equal(0, Seat(b.Last!.Value)); // Bert sitter på plats 0 i sitt eget rum
    }

    [Fact]
    public async Task Chatt_städas_kapas_och_åskådare_heter_Åskådare()
    {
        var (a, _) = await Player("R1", "Anna");
        var spec = await Connect();
        await Hello(spec, null, "R1");
        await a.Conn.InvokeAsync("Chat", "  " + new string('x', 300) + "  ");
        var m = await spec.WaitForChat(m => m.from == "Anna");
        Assert.Equal(200, m.text.Length);
        await spec.Conn.InvokeAsync("Chat", "hej‮");
        var m2 = await a.WaitForChat(m => m.from == "Åskådare");
        Assert.Equal("hej", m2.text);
    }

    [Fact]
    public async Task Tomt_chattmeddelande_skickas_inte()
    {
        var (a, _) = await Player("R1", "Anna");
        await a.Conn.InvokeAsync("Chat", "   ");
        await a.Conn.InvokeAsync("Chat", null);
        await Task.Delay(300);
        Assert.DoesNotContain(a.Chats, m => m.from == "Anna");
    }

    [Fact]
    public async Task Återanslutning_med_token_ger_tillbaka_platsen_och_handen()
    {
        var (a, tokA) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        await a.Conn.InvokeAsync("Start");
        var before = await a.WaitFor(s => Phase(s) == "exchange");

        await a.Conn.StopAsync();
        var gone = await b.WaitFor(s => !s.GetProperty("seats")[0].GetProperty("on").GetBoolean());
        Assert.Equal("exchange", Phase(gone)); // spelet avbryts inte direkt
        await b.WaitForChat(m => m.text.Contains("tappade anslutningen"));

        var a2 = await Connect();
        var h = await Hello(a2, tokA, "R1");
        Assert.Equal(0, h.GetProperty("seat").GetInt32());
        var s = await a2.WaitFor(s => Seat(s) == 0);
        Assert.Equal("exchange", Phase(s));
        Assert.Equal(before.GetProperty("hand").GetRawText(), s.GetProperty("hand").GetRawText());
        await b.WaitFor(s => s.GetProperty("seats")[0].GetProperty("on").GetBoolean());
        await b.WaitForChat(m => m.text.Contains("är tillbaka"));
    }

    [Fact]
    public async Task Gammal_token_slutar_gälla_när_den_bytts()
    {
        var (a, tok1) = await Player("R1", "Anna");
        var h2 = await Hello(a, tok1, "R1");
        var tok2 = h2.GetProperty("token").GetString();
        Assert.NotEqual(tok1, tok2);
        Assert.Equal(0, h2.GetProperty("seat").GetInt32());

        var thief = await Connect();
        var h3 = await Hello(thief, tok1, "R1");
        Assert.Equal(-1, h3.GetProperty("seat").GetInt32());
        Assert.NotEqual(tok1, h3.GetProperty("token").GetString());
    }

    [Fact]
    public async Task Samma_spelare_i_ny_flik_tar_över_och_gamla_fliken_blir_åskådare()
    {
        var (a, tok) = await Player("R1", "Anna");
        var a2 = await Connect();
        var h = await Hello(a2, tok, "R1");
        Assert.Equal(0, h.GetProperty("seat").GetInt32());
        Assert.Equal("Du spelar nu i en annan flik eller enhet.", await a.WaitForError());
        await a.WaitFor(s => Seat(s) == -1);
        await a.Conn.InvokeAsync("Start");
        Assert.Equal("Kan inte starta nu.", await a.WaitForError()); // gamla fliken styr inte längre platsen
    }

    [Fact]
    public async Task Föregående_token_gäller_igen_när_spelaren_är_frånkopplad()
    {
        // svaret på Hello kan gå förlorat; då har klienten bara den gamla token
        var (a, tok1) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        await Hello(a, tok1, "R1"); // token roteras, tok1 blir föregående
        await a.Conn.StopAsync();
        await b.WaitFor(s => !s.GetProperty("seats")[0].GetProperty("on").GetBoolean());
        var a2 = await Connect();
        var h = await Hello(a2, tok1, "R1");
        Assert.Equal(0, h.GetProperty("seat").GetInt32());
    }

    [Fact]
    public async Task Hello_till_okänt_bord_lämnar_anslutningen_kvar_vid_sitt_bord()
    {
        var (a, tok) = await Player("R1", "Anna");
        var h = await a.Conn.InvokeAsync<JsonElement>("Hello", tok, "QQQQQQ");
        Assert.Equal(JsonValueKind.Null, h.ValueKind);
        Assert.Equal(Lobby.NoRoom, await a.WaitForError());
        await a.Conn.InvokeAsync("Chat", "fortfarande här");
        var m = await a.WaitForChat(m => m.text == "fortfarande här");
        Assert.Equal("Anna", m.from);
        lock (Lobby.L) Assert.False(Lobby.Rooms.ContainsKey("QQQQQQ"));
    }

    [Fact]
    public async Task Sittande_token_släpps_in_även_när_gissningshinken_är_tom()
    {
        var (a, tok) = await Player("R1", "Anna");
        var code = await Code("R1");
        var lb = Lobby; var t = new HashSet<string>();
        lock (lb.L)
        {
            for (int i = 0; i < 12; i++) lb.Attach("g" + i, null, "NOPE" + i, t, out _, "?");
            Assert.Equal(Lobby.NoRoom, lb.Attach("x", null, code, t, out _, "?"));
            Assert.Null(lb.Attach("y", tok, code, t, out var nt, "?"));
            Assert.Equal(0, lb.Rooms[code].G.SeatOf(nt));
        }
    }

    [Fact]
    public async Task Flodskydd_för_Hello()
    {
        var c = await Connect();
        for (int i = 0; i < 7; i++) await c.Conn.InvokeAsync<JsonElement>("Hello", null, "");
        Assert.Equal("För många anrop – vänta lite.", await c.WaitForError());
    }

    [Theory]
    [InlineData("inte-en-token")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("")]
    public async Task Okänd_token_ger_ny_token_utan_plats(string token)
    {
        await Player("R1", "Anna");
        var c = await Connect();
        var h = await Hello(c, token, "R1");
        Assert.Equal(-1, h.GetProperty("seat").GetInt32());
        Assert.Matches("^[0-9a-f]{32}$", h.GetProperty("token").GetString());
        Assert.NotEqual(token, h.GetProperty("token").GetString());
    }

    [Fact]
    public async Task Frånkopplad_spelare_förlorar_platsen_när_fristen_gått_ut()
    {
        var (a, _) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        await a.Conn.InvokeAsync("Start");
        await b.WaitFor(s => Phase(s) == "exchange");
        await a.Conn.StopAsync();
        await b.WaitFor(s => !s.GetProperty("seats")[0].GetProperty("on").GetBoolean());

        var lb = Lobby; var touched = new HashSet<string>(); var code = await Code("R1");
        lock (lb.L)
        {
            var p = lb.Rooms[code].G.Seats[0]!;
            p.Gone = DateTime.UtcNow - Game.Grace - TimeSpan.FromSeconds(1);
            lb.Sweep(touched);
        }
        await lb.Flush(touched);
        var s = await b.WaitFor(s => s.GetProperty("seats")[0].ValueKind == JsonValueKind.Null);
        Assert.Equal("lobby", Phase(s));
        await b.WaitForChat(m => m.text.Contains("spelet avbröts"));
    }

    [Fact]
    public async Task Leave_lämnar_platsen_direkt()
    {
        var (a, _) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        await a.Conn.InvokeAsync("Leave");
        var s = await b.WaitFor(s => s.GetProperty("seats")[0].ValueKind == JsonValueKind.Null);
        Assert.Equal(-1, Seat(a.Last!.Value));
    }

    [Fact]
    public async Task Byta_rum_lämnar_platsen_i_det_gamla()
    {
        var (a, tok) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        await Hello(a, tok, "R2");
        await b.WaitFor(s => s.GetProperty("seats")[0].ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Drag_utan_plats_avvisas()
    {
        var (a, _) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        var spec = await Connect();
        await Hello(spec, null, "R1");
        await a.Conn.InvokeAsync("Start");
        await a.WaitFor(s => Phase(s) == "exchange");
        await spec.Conn.InvokeAsync("Exchange", new[] { 0 });
        Assert.Equal("Inte din tur.", await spec.WaitForError());
        await spec.Conn.InvokeAsync("PlayCards", new[] { 0 });
        Assert.Equal("Inte din tur.", await spec.WaitForError());
        await spec.Conn.InvokeAsync("Start");
        Assert.Equal("Kan inte starta nu.", await spec.WaitForError());
    }

    [Fact]
    public async Task Null_och_skräp_som_argument_kraschar_inte_hubben()
    {
        var (a, _) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        await a.Conn.InvokeAsync("Start");
        var s = await a.WaitFor(s => Phase(s) == "exchange");
        int starter = s.GetProperty("starter").GetInt32();
        var st = starter == 0 ? a : b;
        await st.Conn.InvokeAsync("Exchange", (int[]?)null);
        await st.WaitFor(s => Phase(s) is "respond" or "zlatan");
        await a.Conn.InvokeAsync("PlayCards", (int[]?)null);
        await a.Conn.InvokeAsync("Swap", (int[]?)null, true);
        await a.Conn.InvokeAsync("Join", (string?)null);
        // anslutningen lever fortfarande
        Assert.Equal(HubConnectionState.Connected, a.Conn.State);
        Assert.Matches("^[A-Z0-9]{4,8}$", await a.Conn.InvokeAsync<string>("NewRoom"));
    }

    [Fact]
    public async Task Helt_spel_via_hubben_med_bestämd_lek()
    {
        var (a, _) = await Player("R1", "Anna");
        var (b, _) = await Player("R1", "Bert");
        var code = await Code("R1");
        lock (Lobby.L) Lobby.Rooms[code].G.NewDeck = () => Cards.Deck(Rules.EndTests.Simple);
        await a.Conn.InvokeAsync("Start");
        await a.WaitFor(s => Phase(s) == "exchange");
        await a.Conn.InvokeAsync("Exchange", Array.Empty<int>());
        await b.WaitFor(s => Phase(s) == "respond" && s.GetProperty("turn").GetInt32() == 1);
        await b.Conn.InvokeAsync("Respond", false);
        await a.WaitFor(s => Phase(s) == "play");

        // samma drag som EndTests.PlayToEnd men via index i den hand klienten ser
        async Task Play(HubClient c, string cards)
        {
            var hand = c.Last!.Value.GetProperty("hand").EnumerateArray().Select(x => new Card(x.GetProperty("r").GetInt32(), x.GetProperty("s").GetInt32())).ToList();
            var idx = Cards.H(cards).Select(x => hand.IndexOf(x)).ToArray();
            Assert.DoesNotContain(-1, idx);
            long v = c.Last!.Value.GetProperty("v").GetInt64();
            await c.Conn.InvokeAsync("PlayCards", idx);
            await a.WaitFor(s => s.GetProperty("v").GetInt64() > v);
            await b.WaitFor(s => s.GetProperty("v").GetInt64() > v);
        }
        await Play(a, "3k 3r 3h 3s");
        await Play(b, "4k 4r 4h 4s");
        await Play(b, "2k 2r");
        await Play(a, "Qk Qr");
        await Play(a, "As");
        await Play(b, "Kr");
        var end = await a.WaitFor(s => Phase(s) == "over");
        Assert.Equal(0, end.GetProperty("gurka").GetInt32());
        Assert.Equal(1, end.GetProperty("winner").GetInt32());
        Assert.Equal(70, end.GetProperty("points").GetInt32());
        Assert.Equal(-70, end.GetProperty("seats")[0].GetProperty("score").GetInt32());
        Assert.Equal(70, end.GetProperty("seats")[1].GetProperty("score").GetInt32());
        Assert.Empty(a.Errors); Assert.Empty(b.Errors);
    }

    [Fact]
    public async Task Flodskydd_stoppar_för_många_anrop()
    {
        var (a, _) = await Player("R1", "Anna");
        for (int i = 0; i < 40; i++) await a.Conn.InvokeAsync("Exchange", new[] { 0 });
        var errs = new List<string>();
        try { while (true) errs.Add(await a.WaitForError(500)); } catch (TimeoutException) { }
        Assert.Contains("För många anrop – vänta lite.", errs);
        Assert.Equal(HubConnectionState.Connected, a.Conn.State);
    }

    [Fact]
    public async Task Flodskydd_för_chatten_är_strängare()
    {
        var (a, _) = await Player("R1", "Anna");
        for (int i = 0; i < 8; i++) await a.Conn.InvokeAsync("Chat", "spam " + i);
        Assert.Equal("Du skriver för snabbt – vänta lite.", await a.WaitForError());
        await Task.Delay(300);
        Assert.InRange(a.Chats.Count(m => m.from == "Anna"), 1, 5);
    }

    [Fact]
    public async Task För_stora_meddelanden_avvisas()
    {
        var (a, _) = await Player("R1", "Anna");
        await Assert.ThrowsAnyAsync<Exception>(() => a.Conn.InvokeAsync("Chat", new string('x', 10_000)));
    }

    [Fact]
    public async Task Startsidan_och_hälsokontrollen_svarar_med_säkerhetsheaders()
    {
        var http = f.CreateClient();
        var r = await http.GetAsync("/");
        r.EnsureSuccessStatusCode();
        Assert.Contains("text/html", r.Content.Headers.ContentType!.ToString());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True((await http.GetAsync("/health")).IsSuccessStatusCode);
    }
}
