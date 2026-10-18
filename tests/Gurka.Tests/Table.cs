namespace Gurka.Tests;

// Testbord: ett Game med bestämd lek och hjälpmetoder som spelar kort efter namn i stället för index.
public sealed class Table
{
    public Game G { get; }
    List<Card> deck = Cards.All().ToList();

    public Table(int seed = 1) { G = new Game(new Random(seed), () => deck.ToList()); }

    // n spelare sätter sig på plats 0..n-1 med token "p0".."p3"
    public static Table Seated(int n, int seed = 1)
    {
        var t = new Table(seed);
        for (int i = 0; i < n; i++) Assert.Null(t.G.Join("p" + i, "P" + i));
        return t;
    }

    // delar ut: hands är sju kort per aktiv plats i platsordning, next är korten som dras därefter
    public Table Deal(string[] hands, string next = "", int by = -1)
    {
        deck = Cards.Deck(hands, next);
        Assert.Null(G.Start(by >= 0 ? by : G.Active.First()));
        Assert.Equal("exchange", G.Phase);
        AssertConserved();
        return this;
    }

    // delar ut och går direkt till spelet: startspelaren byter inget och alla behåller sina kort
    public static Table Playing(params string[] hands)
    {
        var t = Seated(hands.Length).Deal(hands);
        Assert.Null(t.G.Exchange(t.G.Starter, null));
        foreach (var z in t.G.Zl.ToList()) Assert.Null(t.G.Zlatan(z, false)); // Zlatan-fasen kommer även utan startbyte
        while (t.G.Phase == "respond") Assert.Null(t.G.Respond(t.G.Resp.Peek(), false));
        Assert.Equal("play", t.G.Phase);
        return t;
    }

    public List<Card> Hand(int seat) => G.Seats[seat]!.Hand;

    // index i handen för de namngivna korten, i den ordning de anges
    public int[] Idx(int seat, string cards)
    {
        var h = Hand(seat);
        return Cards.H(cards).Select(c =>
        {
            int i = h.IndexOf(c);
            Assert.True(i >= 0, $"{Cards.Str(c)} finns inte i handen på plats {seat}: {Cards.Str(h)}");
            return i;
        }).ToArray();
    }

    public string? Play(int seat, string cards) { var e = G.Play(seat, Idx(seat, cards)); AssertConserved(); return e; }
    public void Ok(int seat, string cards) => Assert.Null(Play(seat, cards));
    public string? Exchange(int seat, string cards) { var e = G.Exchange(seat, Idx(seat, cards)); AssertConserved(); return e; }
    public string? Swap(int seat, string cards, bool dig = false) { var e = G.Swap(seat, Idx(seat, cards), dig); AssertConserved(); return e; }

    // Varje kort i leken ska finnas exakt en gång: i en hand, i leken, i slasken eller på bordet.
    // 1 upp: det uppvända kortet ligger kvar överst i leken tills det avgjorts.
    public void AssertConserved()
    {
        var all = G.Seats.Where(p => p != null).SelectMany(p => p!.Hand)
            .Concat(G.Deck).Concat(G.Discards)
            .Concat(G.Trick.SelectMany(p => p.Cards))
            .Concat(G.Past.SelectMany(t => t).SelectMany(p => p.Cards)).ToList();
        if (G.Phase == "lobby") return; // vid avbrott rensas bordet och korten räknas inte längre
        var dup = all.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dup.Count == 0, "Kort finns två gånger: " + Cards.Str(dup));
        Assert.Equal(52, all.Count);
        if (G.UpCard is { } up) Assert.Equal(up, G.Deck[0]);
    }
}
