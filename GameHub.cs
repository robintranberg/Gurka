using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

public record Card(int R, int S); // R: 2..14 (14 = ess), S: 0 klöver, 1 ruter, 2 hjärter, 3 spader
public class Pl { public string Id = "", Name = ""; public List<Card> Hand = new(); public int Score; }
public class Pay { public int Seat; public List<Card> Cards = new(); public bool Sak; }

public class GameHub : Hub
{
    static readonly object L = new();
    static readonly ConcurrentDictionary<string, byte> Conns = new();
    static readonly Pl?[] Seats = new Pl?[4];
    static readonly Dictionary<int, Card> Reveal = new();
    static readonly List<Pay> Trick = new();
    static readonly List<List<Pay>> Past = new();
    static readonly List<Card> Discards = new(); // slasken: kastade kort är ute ur leken
    static List<Card> Deck = new();
    static Queue<int> Resp = new();
    static string Phase = "lobby"; // lobby, exchange, zlatan, respond, play, over
    static bool Done, RespFree;
    static long Ver; // versionsnummer så klienten kan ignorera tillstånd som kommer i fel ordning
    static int Starter, Turn, Count, Top = -1, Gurka = -1, Winner = -1, Points, ExN;
    static List<int> TopRs = new(); // korten som ligger högst i sticket, sorterade stigande – slås kort för kort
    static HashSet<int> Zl = new(); // spelare utan kort över 10 som ännu inte valt Zlatan (7 nya kort före startbytet)
    static int[] ExIdx = Array.Empty<int>(); // korten startspelaren valt att byta, väntar på Zlatan-beslut

    static IEnumerable<int> Active => Enumerable.Range(0, 4).Where(i => Seats[i] != null);
    static int Next(int s) { do s = (s + 1) % 4; while (Seats[s] == null); return s; }
    static List<Card> Draw(int n) { var r = Deck.Take(n).ToList(); Deck.RemoveRange(0, n); return r; }
    int Me() => Array.FindIndex(Seats, p => p?.Id == Context.ConnectionId);

    static void Deal()
    {
        var d = Enumerable.Range(0, 52).Select(i => new Card(i % 13 + 2, i / 13)).ToArray();
        Random.Shared.Shuffle(d); Deck = d.ToList();
        Reveal.Clear(); Trick.Clear(); Past.Clear(); Discards.Clear(); Points = 0; Done = false; Top = Gurka = Winner = -1; Count = 0;
        foreach (var i in Active) { Seats[i]!.Hand = Draw(7); Reveal[i] = Seats[i]!.Hand[6]; }
        Starter = Active.OrderByDescending(i => Reveal[i].R * 4 + Reveal[i].S).First();
        Zl = Active.Where(i => i != Starter && Seats[i]!.Hand.All(c => c.R <= 10)).ToHashSet();
        Phase = "exchange";
    }
    static void StartPlay() { Reveal.Clear(); Phase = "play"; Turn = Starter; Count = 0; }
    static void Finish()
    {
        Phase = "over";
        var m = Trick.Select((p, i) => (i, seat: p.Seat, mx: p.Cards.Max(c => c.R))).ToList();
        Gurka = m.OrderByDescending(x => x.mx).ThenByDescending(x => x.i).First().seat; // högst kort (lika => sist spelade)
        Winner = m.OrderBy(x => x.mx).ThenBy(x => x.i).First().seat;                    // lägst kort (lika => först spelade)
        Points = m.Max(x => x.mx) * 5;                                                   // högsta kortet * 5 till vinnaren
        Seats[Winner]!.Score += Points;
        if (Gurka != Winner) Seats[Gurka]!.Score -= Points; // förloraren får minuspoäng
    }

    static object View(int you, long v) => new
    {
        v, phase = Phase, you, starter = Starter, count = Count, top = Top, done = Done, free = RespFree, exn = ExN, deck = Deck.Count, slask = Discards.Count, gurka = Gurka, winner = Winner, zl = Zl.ToList(),
        turn = Phase == "play" ? Turn : Phase == "exchange" ? Starter : Phase == "respond" && Resp.Count > 0 ? Resp.Peek() : -1,
        seats = Enumerable.Range(0, 4).Select(i => Seats[i] == null ? (object?)null : new { seat = i, name = Seats[i]!.Name, n = Seats[i]!.Hand.Count, score = Seats[i]!.Score }).ToList(),
        hand = you >= 0 ? (object?)Seats[you]!.Hand.Select(c => new { r = c.R, s = c.S }).ToList() : null,
        reveal = Reveal.Select(k => new { seat = k.Key, r = k.Value.R, s = k.Value.S }).ToList(),
        trick = Trick.Select(p => new { seat = p.Seat, sak = p.Sak, cards = p.Cards.Select(c => new { r = c.R, s = c.S }).ToList() }).ToList(),
        points = Points,
        past = Past.Select(t => t.Select(p => new { seat = p.Seat, sak = p.Sak, cards = p.Cards.Select(c => new { r = c.R, s = c.S }).ToList() }).ToList()).ToList()
    };

    async Task Push()
    {
        List<(string id, object view)> l;
        lock (L) { long v = ++Ver; l = Conns.Keys.Select(id => (id, View(Array.FindIndex(Seats, p => p?.Id == id), v))).ToList(); }
        await Task.WhenAll(l.Select(x => Clients.Client(x.id).SendAsync("state", x.view)));
    }

    async Task Run(Func<string?> f)
    {
        string? e; lock (L) e = f();
        if (e != null) await Clients.Caller.SendAsync("err", e); else await Push();
    }

    public override async Task OnConnectedAsync() { Conns[Context.ConnectionId] = 1; await Push(); await base.OnConnectedAsync(); }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        Conns.TryRemove(Context.ConnectionId, out _); string? n = null;
        lock (L)
        {
            int i = Me();
            if (i >= 0)
            {
                n = Seats[i]!.Name + " lämnade bordet."; Seats[i] = null;
                if (Phase != "lobby" && Phase != "over") { Phase = "lobby"; Trick.Clear(); Past.Clear(); Reveal.Clear(); Done = false; n = n.TrimEnd('.') + " – spelet avbröts."; }
                else if (Phase == "over" && (i == Gurka || i == Winner)) Phase = "lobby"; // resultatet pekar annars på en tom plats
            }
        }
        if (n != null) await Clients.All.SendAsync("chat", "Bordet", n);
        await Push(); await base.OnDisconnectedAsync(ex);
    }

    public Task Join(string name) => Run(() =>
    {
        if (Me() >= 0) return null;
        if (Phase != "lobby" && Phase != "over") return "Spelet pågår – vänta tills det är slut.";
        int i = Array.FindIndex(Seats, p => p == null);
        if (i < 0) return "Bordet är fullt (4/4).";
        name = (name ?? "").Trim(); if (name.Length == 0) name = "Spelare " + (i + 1);
        Seats[i] = new Pl { Id = Context.ConnectionId, Name = name[..Math.Min(14, name.Length)] };
        return null;
    });

    public Task Start() => Run(() =>
    {
        if (Me() < 0 || (Phase != "lobby" && Phase != "over")) return "Kan inte starta nu.";
        if (Active.Count() < 2) return "Minst 2 spelare krävs.";
        Deal(); return null;
    });

    public Task Exchange(int[] idx) => Run(() =>
    {
        if (Phase != "exchange" || Me() != Starter) return "Inte din tur.";
        idx ??= Array.Empty<int>();
        if (idx.Distinct().Count() != idx.Length || idx.Any(i => i < 0 || i > 6)) return "Ogiltigt val.";
        if (idx.Length > Deck.Count) return $"Leken har bara {Deck.Count} kort kvar.";
        ExIdx = idx; ExN = idx.Length;
        if (ExN > 0 && Zl.Count > 0) { Phase = "zlatan"; return null; } // Zlatan-spelarna väljer innan bytet sker
        DoExchange(); return null;
    });

    static void DoExchange()
    {
        var h = Seats[Starter]!.Hand;
        if (ExN > 0)
        {
            var old = ExIdx.Select(i => h[i]).ToList(); var nw = Draw(ExN);
            for (int k = 0; k < ExN; k++) h[ExIdx[k]] = nw[k];
            Discards.AddRange(old);
        }
        RespFree = ExN > 0; // bytte startspelaren kort får övriga byta valfria kort; annars kasta alla / behålla
        Reveal.Clear(); Phase = "respond"; Resp = new Queue<int>();
        for (int s = Next(Starter); s != Starter; s = Next(s)) Resp.Enqueue(s);
    }

    public async Task Zlatan(bool take)
    {
        string? n = null;
        await Run(() =>
        {
            int m = Me();
            if (Phase != "zlatan" || !Zl.Contains(m)) return "Du kan inte köra Zlatan nu.";
            if (take)
            {
                if (Deck.Count - ExN < 7) return "Leken räcker inte för 7 nya kort."; // startspelarens byte måste rymmas
                var old = Seats[m]!.Hand; Seats[m]!.Hand = Draw(7); Discards.AddRange(old);
                n = Seats[m]!.Name;
            }
            Zl.Remove(m);
            if (Zl.Count == 0) DoExchange();
            return null;
        });
        if (n != null) await Clients.All.SendAsync("chat", "Bordet", n + " körde Zlatan och fick 7 nya kort!");
    }

    public Task Respond(bool all) => Run(() =>
    {
        if (Phase != "respond" || RespFree || Resp.Count == 0 || Resp.Peek() != Me()) return "Inte din tur.";
        if (all && Deck.Count < 7) return "Leken räcker inte för 7 nya kort.";
        int m = Resp.Dequeue();
        if (all) { var old = Seats[m]!.Hand; Seats[m]!.Hand = Draw(7); Discards.AddRange(old); }
        if (Resp.Count == 0) StartPlay();
        return null;
    });

    public Task Swap(int[] idx, bool dig) => Run(() =>
    {
        if (Phase != "respond" || !RespFree || Resp.Count == 0 || Resp.Peek() != Me()) return "Inte din tur.";
        idx ??= Array.Empty<int>();
        if (idx.Distinct().Count() != idx.Length || idx.Any(i => i < 0 || i > 6)) return "Ogiltigt val.";
        int k = idx.Length, d = Deck.Count;
        if (k > 0)
        {
            if (dig) { if (d >= ExN || k != ExN || Discards.Count < ExN) return "Du kan inte gräva i slasken nu."; }
            else if (!((k == ExN && d >= ExN) || (k == 7 && d >= 7) || (d > 0 && d < ExN && k == d)))
                return d >= ExN ? $"Du kan byta {ExN} kort, alla 7 eller inga."
                     : d > 0 ? $"Leken räcker inte: byt {d} kort eller gräv i slasken ({ExN} kort)."
                     : $"Leken är slut: gräv i slasken ({ExN} kort) eller behåll.";
        }
        int m = Resp.Dequeue(); var h = Seats[m]!.Hand;
        if (k > 0)
        {
            var old = idx.Select(i => h[i]).ToList();
            List<Card> nw;
            if (dig) { var a = Discards.ToArray(); Random.Shared.Shuffle(a); nw = a.Take(k).ToList(); foreach (var c in nw) Discards.Remove(c); }
            else nw = Draw(k);
            for (int j = 0; j < k; j++) h[idx[j]] = nw[j];
            Discards.AddRange(old);
        }
        if (Resp.Count == 0) StartPlay();
        return null;
    });

    public Task PlayCards(int[] idx) => Run(() =>
    {
        int m = Me();
        if (Phase != "play" || m != Turn) return "Inte din tur.";
        var h = Seats[m]!.Hand;
        if (idx == null || idx.Length == 0 || idx.Distinct().Count() != idx.Length || idx.Any(i => i < 0 || i >= h.Count)) return "Ogiltigt val.";
        var ch = idx.Select(i => h[i]).ToList();
        bool lead = Trick.Count == 0 || Done, sak = false;
        var pr = ch.Select(c => c.R).OrderBy(r => r).ToList();
        if (lead)
        {
            if (!ch.All(c => c.R == ch[0].R)) return "Spela ut ett kort eller flera lika.";
        }
        else
        {
            if (ch.Count != Count) return $"Du måste lägga {Count} kort.";
            // varje kort (lägst mot lägst) måste vara lika/högre än motsvarande kort i sticket
            bool Beats(IEnumerable<int> cs) => cs.Select((r, i) => r >= TopRs[i]).All(b => b);
            if (!Beats(pr))
            {
                // saka: de lägsta valda korten är dina lägsta, resten slår kort för kort (t.ex. slå ett + saka ett)
                var low = h.Select(c => c.R).OrderBy(r => r).ToList();
                if (!Enumerable.Range(1, Count).Any(j => pr.Take(j).SequenceEqual(low.Take(j)) && Beats(pr.Skip(j))))
                    return h.Any(c => c.R >= TopRs[0]) ? "Slå kort för kort med lika/högre kort, eller saka ditt lägsta." : "Du kan inte slå – du måste saka ditt lägsta kort.";
                sak = true;
            }
        }
        if (lead) { if (Trick.Count > 0) Past.Add(Trick.ToList()); Trick.Clear(); Done = false; Count = ch.Count; Top = m; TopRs = pr; }
        else if (!sak) { Top = m; TopRs = pr; }
        foreach (var i in idx.OrderByDescending(i => i)) h.RemoveAt(i);
        Trick.Add(new Pay { Seat = m, Cards = ch, Sak = sak });
        if (Trick.Count == Active.Count()) { Done = true; if (h.Count == 0) Finish(); else Turn = Top; }
        else Turn = Next(m);
        return null;
    });

    public async Task Chat(string text)
    {
        text = (text ?? "").Trim(); if (text.Length == 0) return; if (text.Length > 200) text = text[..200];
        string n; lock (L) { int i = Me(); n = i >= 0 ? Seats[i]!.Name : "Åskådare"; }
        await Clients.All.SendAsync("chat", n, text);
    }
}
