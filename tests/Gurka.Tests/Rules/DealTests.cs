using static Gurka.Tests.Cards;

namespace Gurka.Tests.Rules;

public class DealTests
{
    [Fact]
    public void Start_kräver_minst_två_spelare()
    {
        var t = Table.Seated(1);
        Assert.Equal("Minst 2 spelare krävs.", t.G.Start(0));
        Assert.Equal("lobby", t.G.Phase);
    }

    [Fact]
    public void Start_kräver_att_man_sitter_vid_bordet()
    {
        var t = Table.Seated(2);
        Assert.Equal("Kan inte starta nu.", t.G.Start(-1));
        Assert.Equal("Kan inte starta nu.", t.G.Start(3));
        Assert.Equal("Kan inte starta nu.", t.G.Start(99));
    }

    [Fact]
    public void Start_går_inte_mitt_i_ett_spel()
    {
        var t = Table.Playing("2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr");
        Assert.Equal("Kan inte starta nu.", t.G.Start(0));
        Assert.Equal("play", t.G.Phase);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Var_och_en_får_sju_kort_och_resten_blir_lek(int n)
    {
        var t = Table.Seated(n);
        Assert.Null(t.G.Start(0));
        Assert.Equal("exchange", t.G.Phase);
        for (int i = 0; i < n; i++) Assert.Equal(7, t.Hand(i).Count);
        Assert.Equal(52 - 7 * n, t.G.Deck.Count);
        Assert.Empty(t.G.Discards);
        t.AssertConserved();
    }

    [Fact]
    public void Slumpad_lek_innehåller_52_olika_kort()
    {
        var g = new Game(new Random(42));
        g.Join("a", "A"); g.Join("b", "B");
        Assert.Null(g.Start(0));
        var all = g.Seats[0]!.Hand.Concat(g.Seats[1]!.Hand).Concat(g.Deck).ToList();
        Assert.Equal(52, all.Distinct().Count());
        Assert.All(all, c => { Assert.InRange(c.R, 2, 14); Assert.InRange(c.S, 0, 3); });
    }

    [Fact]
    public void Det_sjunde_kortet_visas_och_högst_visat_kort_börjar()
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k Kk", "2r 3r 4r 5r 6r 7r Ah", "2h 3h 4h 5h 6h 7h 9s"]);
        Assert.Equal(1, t.G.Starter);
        Assert.Equal(C("Kk"), t.G.Reveal[0]);
        Assert.Equal(C("Ah"), t.G.Reveal[1]);
        Assert.Equal(C("9s"), t.G.Reveal[2]);
        Assert.Equal(1, t.G.CurTurn);
    }

    [Theory]
    [InlineData("Ah", "As", 1)] // spader slår hjärter
    [InlineData("Ar", "Ak", 0)] // ruter slår klöver
    [InlineData("Th", "Tr", 0)] // hjärter slår ruter
    public void Lika_visad_valör_avgörs_av_färgen(string first, string second, int starter)
    {
        var t = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k " + first, "2r 3r 4r 5r 6r 7r " + second]);
        Assert.Equal(starter, t.G.Starter);
    }

    [Fact]
    public void Zlatan_kandidater_är_icke_startspelare_utan_kort_över_tio()
    {
        var t = Table.Seated(4).Deal([
            "2k 3k 4k 5k 6k 7k As", // startspelare
            "2r 3r 4r 5r 6r 7r Tr", // högst tia -> Zlatan
            "2h 3h 4h 5h 6h 7h Jh", // knekt -> inte Zlatan
            "2s 3s 4s 5s 6s 8s 9s"  // högst nia -> Zlatan
        ]);
        Assert.Equal(0, t.G.Starter);
        Assert.Equal(new[] { 1, 3 }, t.G.Zl.OrderBy(x => x));
    }

    [Fact]
    public void Startspelaren_kan_aldrig_vara_Zlatan_kandidat()
    {
        var t = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k Ts", "2r 3r 4r 5r 6r 7r 9r"]);
        Assert.Equal(0, t.G.Starter);
        Assert.Equal(new[] { 1 }, t.G.Zl);
    }

    [Fact]
    public void Ny_giv_nollställer_bordet_men_behåller_poängen()
    {
        var t = Table.Playing("3k 3r 3h 3s Qk Qr As", "4k 4r 4h 4s 2k 2r Kr");
        EndTests.PlayToEnd(t);
        Assert.Equal("over", t.G.Phase);
        int s0 = t.G.Seats[0]!.Score, s1 = t.G.Seats[1]!.Score;
        Assert.Null(t.G.Start(1));
        Assert.Equal("exchange", t.G.Phase);
        Assert.Empty(t.G.Trick); Assert.Empty(t.G.Past); Assert.Empty(t.G.Discards);
        Assert.Equal(-1, t.G.Gurka); Assert.Equal(-1, t.G.Winner); Assert.Equal(0, t.G.Points);
        Assert.Equal(s0, t.G.Seats[0]!.Score); Assert.Equal(s1, t.G.Seats[1]!.Score);
        t.AssertConserved();
    }
}
