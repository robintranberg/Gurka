namespace Gurka.Tests.Rules;

// Att sätta sig och lämna bordet.
public class SeatTests
{
    [Fact]
    public void Spelare_får_första_lediga_plats()
    {
        var t = Table.Seated(3);
        t.G.Leave(1);
        Assert.Null(t.G.Join("x", "X"));
        Assert.Equal(1, t.G.SeatOf("x"));
    }

    [Fact]
    public void Femte_spelaren_får_inte_plats()
    {
        var t = Table.Seated(4);
        Assert.Equal("Bordet är fullt (4/4).", t.G.Join("p4", "Fem"));
        Assert.Equal(-1, t.G.SeatOf("p4"));
    }

    [Fact]
    public void Samma_token_två_gånger_ger_samma_plats()
    {
        var t = Table.Seated(2);
        Assert.Null(t.G.Join("p0", "Annat namn"));
        Assert.Equal(2, t.G.Active.Count());
        Assert.Equal("P0", t.G.Seats[0]!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ogiltig_token_avvisas(string? id)
    {
        var g = new Game();
        Assert.Equal("Ogiltig session.", g.Join(id!, "A"));
        Assert.Equal(-1, g.SeatOf(id!));
    }

    [Fact]
    public void Man_kan_inte_sätta_sig_mitt_i_ett_spel()
    {
        var t = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr"]);
        Assert.Equal("Spelet pågår – vänta tills det är slut.", t.G.Join("p2", "Sen"));
    }

    [Theory]
    [InlineData("  Anna  ", "Anna")]
    [InlineData("Ett väldigt långt namn", "Ett väldigt lå")]
    [InlineData("a\tb\nc", "a b c")]    // tab och radbrytning blir mellanslag
    [InlineData("a   b", "a b")]
    [InlineData("A​nna", "Anna")]  // osynliga tecken tas bort
    [InlineData("A‮nna", "Anna")]  // riktningsbyte tas bort
    public void Namn_städas_och_kapas_till_14_tecken(string name, string expected)
    {
        var g = new Game();
        Assert.Null(g.Join("a", name));
        Assert.Equal(expected, g.Seats[0]!.Name);
        Assert.True(g.Seats[0]!.Name.Length <= 14);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("​​")]
    public void Tomt_namn_blir_Spelare_N(string? name)
    {
        var g = new Game();
        g.Join("a", "A");
        Assert.Null(g.Join("b", name));
        Assert.Equal("Spelare 2", g.Seats[1]!.Name);
    }

    [Fact]
    public void Emoji_delas_inte_när_namnet_kapas()
    {
        var g = new Game();
        Assert.Null(g.Join("a", "1234567890123😀"));
        var n = g.Seats[0]!.Name;
        Assert.False(char.IsHighSurrogate(n[^1]), "Namnet slutar med ett halvt emoji");
    }

    [Theory]
    [InlineData("bordet")]
    [InlineData("Åskådare")]
    public void Reserverade_namn_får_ett_nummer(string name)
    {
        var g = new Game();
        Assert.Null(g.Join("a", name));
        Assert.NotEqual(name, g.Seats[0]!.Name, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dubbla_namn_blir_unika()
    {
        var g = new Game();
        g.Join("a", "Anna"); g.Join("b", "anna"); g.Join("c", "Anna");
        var names = g.Seats.Where(p => p != null).Select(p => p!.Name.ToLowerInvariant()).ToList();
        Assert.Equal(3, names.Distinct().Count());
        Assert.All(names, n => Assert.True(n.Length <= 14));
    }

    [Fact]
    public void Lämna_i_lobbyn_frigör_platsen()
    {
        var t = Table.Seated(2);
        t.G.Leave(1);
        Assert.Null(t.G.Seats[1]);
        Assert.Equal("lobby", t.G.Phase);
        Assert.Contains("P1 lämnade bordet.", t.G.Notes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Lämna_ogiltig_plats_gör_ingenting(int seat)
    {
        var t = Table.Seated(2);
        t.G.Leave(seat);
        Assert.Equal(2, t.G.Active.Count());
        Assert.Empty(t.G.Notes);
    }

    [Fact]
    public void Lämna_mitt_i_spelet_avbryter_det()
    {
        var t = Table.Playing("Qk 2k 3k 4k 5k 6k As", "2r 3r 4r 5r 6r 7r Kr", "2h 3h 4h 5h 6h 7h Kh");
        t.Ok(0, "Qk");
        t.G.Leave(2);
        Assert.Equal("lobby", t.G.Phase);
        Assert.Empty(t.G.Trick);
        Assert.Empty(t.G.Past);
        Assert.False(t.G.Done);
        Assert.Contains(t.G.Notes, n => n.Contains("spelet avbröts"));
        Assert.Null(t.G.Start(0)); // de två kvar kan starta igen
    }

    [Theory]
    [InlineData("exchange")]
    [InlineData("zlatan")]
    [InlineData("respond")]
    [InlineData("oneup")]
    public void Lämna_i_varje_fas_avbryter_spelet(string phase)
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh"]);
        if (phase != "exchange") Assert.Null(t.G.Exchange(0, phase == "oneup" ? [0] : [0, 1])); // ett bytt kort ger 1 upp
        if (phase is "respond" or "oneup") Assert.Null(t.G.Zlatan(1, false));
        Assert.Equal(phase, t.G.Phase);
        t.G.Leave(2);
        Assert.Equal("lobby", t.G.Phase);
    }

    [Fact]
    public void Lämnar_gurkan_efter_slutet_går_bordet_till_lobbyn()
    {
        var t = Table.Playing(EndTests.Simple);
        EndTests.PlayToEnd(t);
        t.G.Leave(t.G.Gurka);
        Assert.Equal("lobby", t.G.Phase);
    }

    [Fact]
    public void Lämnar_en_tredje_spelare_efter_slutet_står_resultatet_kvar()
    {
        var t = Table.Playing("2k 2r 2h 2s 9k 9r As", "3k 3r 3h 3s 6r Kr 5r", "4k 4r 4h 4s Th Ts 5k");
        foreach (var (s, c) in new[] { (0, "2k 2r 2h 2s"), (1, "3k 3r 3h 3s"), (2, "4k 4r 4h 4s"), (2, "5k"), (0, "As"), (1, "5r"), (0, "9k 9r"), (1, "6r Kr"), (2, "Th Ts") })
            t.Ok(s, c);
        Assert.Equal("over", t.G.Phase);
        t.G.Leave(2); // varken gurka (1) eller vinnare (0)
        Assert.Equal("over", t.G.Phase);
    }
}
