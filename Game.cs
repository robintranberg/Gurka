using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public record Card(int R, int S); // R: 2..14 (14 = ess), S: 0 klöver, 1 ruter, 2 hjärter, 3 spader
public class Pl { public string Id = "", Prev = "", Name = ""; public List<Card> Hand = new(); public int Score; public string? Conn; public DateTime? Gone; } // Id = hemlig spelartoken, Prev = föregående token (gäller bara medan anslutningen är borta), Gone = när anslutningen tappades
public class Pay { public int Seat; public List<Card> Cards = new(); public bool Sak; }

// Ett bord med hela spellogiken. Inte trådsäker – anroparen låser. Metoderna tar sittplats (0..3, -1 = ingen) och returnerar felmeddelande eller null.
public class Game
{
    public readonly Pl?[] Seats = new Pl?[4];
    public readonly Dictionary<int, Card> Reveal = new();
    public readonly List<(int Seat, Card Card, int Round)> Ties = new(); // kort som dragits för att avgöra lika visade kort (gick till slasken)
    public readonly List<Pay> Trick = new();
    public readonly List<List<Pay>> Past = new();
    public readonly List<Card> Discards = new(); // slasken: kastade kort är ute ur leken
    public readonly List<string> Notes = new();  // meddelanden från bordet som hubben skickar i chatten efter anropet
    public List<Card> Deck = new();
    public Queue<int> Resp = new();
    public string Phase = "lobby"; // lobby, exchange, zlatan, respond, play, over
    public bool Done, RespFree;
    public long Ver; // versionsnummer så klienten kan ignorera tillstånd som kommer i fel ordning
    public int Starter, Turn, Count, Top = -1, Gurka = -1, Winner = -1, Points, ExN;
    public List<int> TopRs = new(); // korten som ligger högst i sticket, sorterade stigande – slås kort för kort
    public HashSet<int> Zl = new(); // spelare utan kort över 10 som ännu inte valt Zlatan (7 nya kort före startbytet)
    public int[] ExIdx = Array.Empty<int>(); // korten startspelaren valt att byta, väntar på Zlatan-beslut
    public string? Last; // senaste händelsen, för animationer i klienten

    public readonly Random Rng;
    public Func<List<Card>> NewDeck; // tester kan ge en bestämd lek; korten delas 7 åt gången i platsordning från toppen

    public Game(Random? rng = null, Func<List<Card>>? deck = null)
    {
        Rng = rng ?? Random.Shared;
        NewDeck = deck ?? (() => { var d = Enumerable.Range(0, 52).Select(i => new Card(i % 13 + 2, i / 13)).ToArray(); Rng.Shuffle(d); return d.ToList(); });
    }

    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(75); // frist innan en frånkopplad spelare förlorar platsen
    public IEnumerable<int> Active => Enumerable.Range(0, 4).Where(i => Seats[i] != null);
    public bool Idle => Phase == "lobby" || Phase == "over";
    public int SeatOf(string id) => string.IsNullOrEmpty(id) ? -1 : Array.FindIndex(Seats, p => p != null && Eq(p.Id, id));
    public static bool Eq(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b)); // tokens jämförs i konstant tid
    public int CurTurn => Phase == "play" ? Turn : Phase == "exchange" ? Starter : Phase == "respond" && Resp.Count > 0 ? Resp.Peek() : -1;
    int Next(int s) { do s = (s + 1) % 4; while (Seats[s] == null); return s; }
    List<Card> Draw(int n) { var r = Deck.Take(n).ToList(); Deck.RemoveRange(0, n); return r; }
    bool Has(int m) => m >= 0 && m < 4 && Seats[m] != null;

    // tar bort styrtecken, osynliga tecken och extra mellanslag; kapar utan att dela ett emoji
    public static string Clean(string? s, int max)
    {
        var t = Regex.Replace(s ?? "", @"\s+", " "); // tab och radbrytning blir mellanslag innan styrtecken tas bort
        t = new string(t.Where(c => !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray());
        t = Regex.Replace(t, @" {2,}", " ").Trim();
        if (t.Length > max) t = t[..(char.IsHighSurrogate(t[max - 1]) ? max - 1 : max)].TrimEnd();
        return t;
    }

    void Deal()
    {
        Deck = NewDeck();
        Reveal.Clear(); Ties.Clear(); Trick.Clear(); Past.Clear(); Discards.Clear(); Points = 0; Done = false; Top = Gurka = Winner = -1; Count = 0;
        foreach (var i in Active) { Seats[i]!.Hand = Draw(7); Reveal[i] = Seats[i]!.Hand[6]; }
        // högst visad valör börjar; vid lika drar just de ett nytt kort var (till slasken) tills en är ensam högst
        var t = Active.ToList(); var v = t.ToDictionary(i => i, i => Reveal[i]);
        for (int r = 1; ; r++)
        {
            int hi = t.Max(i => v[i].R); t = t.Where(i => v[i].R == hi).ToList();
            if (t.Count == 1 || Deck.Count < t.Count) break;
            Notes.Add("Lika visade kort – " + Names(t) + " drar ett nytt kort var.");
            foreach (var i in t) { v[i] = Draw(1)[0]; Discards.Add(v[i]); Ties.Add((i, v[i], r)); }
        }
        Starter = t.OrderByDescending(i => v[i].S).First(); // leken slut (i praktiken omöjligt): färgen avgör
        if (Ties.Count > 0) Notes.Add(Seats[Starter]!.Name + " fick högst och börjar.");
        Zl = Active.Where(i => i != Starter && Seats[i]!.Hand.All(c => c.R <= 10)).ToHashSet();
        Phase = "exchange"; Last = "deal";
    }
    string Names(List<int> t) { var n = t.Select(i => Seats[i]!.Name).ToList(); return n.Count == 1 ? n[0] : string.Join(", ", n.Take(n.Count - 1)) + " och " + n[^1]; }
    void StartPlay() { Reveal.Clear(); Ties.Clear(); Phase = "play"; Turn = Starter; Count = 0; }
    void Finish()
    {
        Phase = "over";
        var m = Trick.Select((p, i) => (i, seat: p.Seat, mx: p.Cards.Max(c => c.R))).ToList();
        int lo = m.Min(x => x.mx);
        if (m.Count(x => x.mx == lo) > 1) { Gurka = Winner = -1; Points = 0; Last = "over"; return; } // flera går ut på samma lägsta valör: oavgjort, inga poäng
        Gurka =m.OrderByDescending(x => x.mx).ThenByDescending(x => x.i).First().seat; // högst kort (lika => sist spelade)
        Winner = m.OrderBy(x => x.mx).First().seat;                                      // lägst kort (alltid entydigt här)
        Points = m.Max(x => x.mx) * 5;                                                   // högsta kortet * 5 till vinnaren
        Seats[Winner]!.Score += Points;
        if (Gurka != Winner) Seats[Gurka]!.Score -= Points; // förloraren får minuspoäng
        Last = "over";
    }

    public string? Join(string id, string? name)
    {
        if (string.IsNullOrEmpty(id)) return "Ogiltig session.";
        if (SeatOf(id) >= 0) return null;
        if (!Idle) return "Spelet pågår – vänta tills det är slut.";
        int i = Array.FindIndex(Seats, p => p == null);
        if (i < 0) return "Bordet är fullt (4/4).";
        string b = Clean(name, 14); if (b.Length == 0) b = "Spelare " + (i + 1);
        // unika namn; "Bordet" och "Åskådare" används av servern
        bool Taken(string n) => n.Equals("Bordet", StringComparison.OrdinalIgnoreCase) || n.Equals("Åskådare", StringComparison.OrdinalIgnoreCase)
            || Seats.Any(p => p != null && p.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
        string nm = b; for (int k = 2; Taken(nm); k++) nm = Clean(b, 11) + " " + k;
        Seats[i] = new Pl { Id = id, Name = nm }; Last = "join";
        return null;
    }

    // lämnar bordet direkt (eller när frist för tappad anslutning gått ut)
    public void Leave(int i)
    {
        if (!Has(i)) return;
        string n = Seats[i]!.Name + " lämnade bordet."; Seats[i] = null; Last = "leave";
        if (!Idle)
        {
            Phase = "lobby"; Trick.Clear(); Past.Clear(); Reveal.Clear(); Ties.Clear(); Done = false; n = n.TrimEnd('.') + " – spelet avbröts.";
            foreach (var p in Seats) p?.Hand.Clear(); // inga gamla kort i lobbyn
            Deck.Clear(); Discards.Clear(); Zl.Clear(); Resp.Clear(); ExN = Count = 0; Top = -1;
        }
        else if (Phase == "over" && (i == Gurka || i == Winner)) Phase = "lobby"; // resultatet pekar annars på en tom plats
        Notes.Add(n);
    }

    public string? Start(int m)
    {
        if (!Has(m) || !Idle) return "Kan inte starta nu.";
        if (Active.Count() < 2) return "Minst 2 spelare krävs.";
        Deal(); return null;
    }

    public string? Exchange(int m, int[]? idx)
    {
        if (Phase != "exchange" || m != Starter) return "Inte din tur.";
        idx ??= Array.Empty<int>();
        if (idx.Distinct().Count() != idx.Length || idx.Any(i => i < 0 || i > 6)) return "Ogiltigt val.";
        if (idx.Length > Deck.Count) return $"Leken har bara {Deck.Count} kort kvar.";
        ExIdx = idx; ExN = idx.Length; Last = "exchange";
        if (ExN > 0 && Zl.Count > 0) { Phase = "zlatan"; return null; } // Zlatan-spelarna väljer innan bytet sker
        DoExchange(); return null;
    }

    void DoExchange()
    {
        var h = Seats[Starter]!.Hand;
        if (ExN > 0)
        {
            var old = ExIdx.Select(i => h[i]).ToList(); var nw = Draw(ExN);
            for (int k = 0; k < ExN; k++) h[ExIdx[k]] = nw[k];
            Discards.AddRange(old);
        }
        Zl.Clear(); // ingen ska efteråt kunna se vem som saknade kort över 10
        RespFree = ExN > 0; // bytte startspelaren kort får övriga byta valfria kort; annars kasta alla / behålla
        Reveal.Clear(); Ties.Clear(); Phase = "respond"; Resp = new Queue<int>();
        for (int s = Next(Starter); s != Starter; s = Next(s)) Resp.Enqueue(s);
    }

    public string? Zlatan(int m, bool take)
    {
        if (Phase != "zlatan" || !Zl.Contains(m)) return "Du kan inte köra Zlatan nu.";
        if (take)
        {
            if (Deck.Count - ExN < 7) return "Leken räcker inte för 7 nya kort."; // startspelarens byte måste rymmas
            var old = Seats[m]!.Hand; Seats[m]!.Hand = Draw(7); Discards.AddRange(old);
            Notes.Add(Seats[m]!.Name + " körde Zlatan och fick 7 nya kort!"); Last = "zlatan";
        }
        Zl.Remove(m);
        if (Zl.Count == 0) DoExchange();
        return null;
    }

    public string? Respond(int m, bool all)
    {
        if (Phase != "respond" || RespFree || Resp.Count == 0 || Resp.Peek() != m) return "Inte din tur.";
        if (all && Deck.Count < 7) return "Leken räcker inte för 7 nya kort.";
        Resp.Dequeue(); Last = "swap";
        if (all) { var old = Seats[m]!.Hand; Seats[m]!.Hand = Draw(7); Discards.AddRange(old); }
        if (Resp.Count == 0) StartPlay();
        return null;
    }

    public string? Swap(int m, int[]? idx, bool dig)
    {
        if (Phase != "respond" || !RespFree || Resp.Count == 0 || Resp.Peek() != m) return "Inte din tur.";
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
        Resp.Dequeue(); var h = Seats[m]!.Hand; Last = "swap";
        if (k > 0)
        {
            var old = idx.Select(i => h[i]).ToList();
            List<Card> nw;
            if (dig) { var a = Discards.ToArray(); Rng.Shuffle(a); nw = a.Take(k).ToList(); foreach (var c in nw) Discards.Remove(c); }
            else nw = Draw(k);
            for (int j = 0; j < k; j++) h[idx[j]] = nw[j];
            Discards.AddRange(old);
        }
        if (Resp.Count == 0) StartPlay();
        return null;
    }

    public string? Play(int m, int[]? idx)
    {
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
        Last = lead ? "lead" : sak ? "sak" : "beat";
        if (Trick.Count == Active.Count()) { Done = true; if (h.Count == 0) Finish(); else Turn = Top; }
        else Turn = Next(m);
        return null;
    }

    public object View(int you, string room = "") => new
    {
        v = Ver, phase = Phase, you, starter = Starter, count = Count, top = Top, done = Done, free = RespFree, exn = ExN, deck = Deck.Count, slask = Discards.Count, gurka = Gurka, winner = Winner,
        // vem som kan köra Zlatan avslöjar svaga händer: varje klient får bara veta om sig själv, plus hur många som väntar
        zl = Phase == "zlatan" && Zl.Contains(you) ? new List<int> { you } : new List<int>(), zlMe = Phase == "zlatan" && Zl.Contains(you), zlN = Phase == "zlatan" ? Zl.Count : 0,
        turn = CurTurn, room, last = Last,
        seats = Enumerable.Range(0, 4).Select(i => Seats[i] == null ? (object?)null : new { seat = i, name = Seats[i]!.Name, n = Seats[i]!.Hand.Count, score = Seats[i]!.Score, on = Seats[i]!.Gone == null,
            grace = Seats[i]!.Gone is { } g ? Math.Max(0, (int)Math.Ceiling((Grace - (DateTime.UtcNow - g)).TotalSeconds)) : 0 }).ToList(),
        hand = you >= 0 ? (object?)Seats[you]!.Hand.Select(c => new { r = c.R, s = c.S }).ToList() : null,
        reveal = Reveal.Select(k => new { seat = k.Key, r = k.Value.R, s = k.Value.S }).ToList(),
        tie = Ties.Select(t => new { seat = t.Seat, r = t.Card.R, s = t.Card.S, round = t.Round }).ToList(),
        trick = Trick.Select(p => new { seat = p.Seat, sak = p.Sak, cards = p.Cards.Select(c => new { r = c.R, s = c.S }).ToList() }).ToList(),
        points = Points,
        past = Past.Select(t => t.Select(p => new { seat = p.Seat, sak = p.Sak, cards = p.Cards.Select(c => new { r = c.R, s = c.S }).ToList() }).ToList()).ToList()
    };
}
