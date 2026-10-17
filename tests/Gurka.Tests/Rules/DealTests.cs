using System.Text.Json;
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
        var all = g.Seats[0]!.Hand.Concat(g.Seats[1]!.Hand).Concat(g.Deck).Concat(g.Discards).ToList(); // slasken kan ha avgörandekort vid lika
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

    static (int, string, int)[] Ties(Table t) => t.G.Ties.Select(x => (x.Seat, Str(x.Card), x.Round)).ToArray();
    static JsonElement Tie(Game g, int you) => JsonSerializer.SerializeToElement(g.View(you)).GetProperty("tie");

    // Lika högsta visade valör: just de drar ett nytt kort var från leken (till slasken) tills en är ensam högst. Färgen avgör inte.
    [Theory]
    [InlineData("As", "Ah", "3h 4h", 1)] // spader avgör inte längre: plats 1 drar högre
    [InlineData("Ah", "As", "9r 5s", 0)]
    [InlineData("Tk", "Ts", "2h Kk", 1)]
    public void Två_lika_högsta_drar_ett_nytt_kort_var_och_högst_börjar(string first, string second, string draws, int starter)
    {
        var t = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k " + first, "2r 3r 4r 5r 6r 7r " + second], next: draws);
        var d = H(draws);
        Assert.Equal(starter, t.G.Starter);
        Assert.Equal(starter, t.G.CurTurn);
        Assert.Equal(new[] { (0, Str(d[0]), 1), (1, Str(d[1]), 1) }, Ties(t));
        Assert.Equal(d, t.G.Discards);                  // avgörandekorten går till slasken
        Assert.Equal(52 - 14 - 2, t.G.Deck.Count);
        Assert.Equal(C(first), t.G.Reveal[0]);          // det visade kortet ändras inte
        Assert.Equal(C(second), t.G.Reveal[1]);
        Assert.Equal(H("2k 3k 4k 5k 6k 7k " + first), t.Hand(0)); // händerna har fortfarande sina sju kort
        Assert.Equal(H("2r 3r 4r 5r 6r 7r " + second), t.Hand(1));
    }

    [Fact]
    public void Tre_lika_drar_i_platsordning()
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k Kk", "2r 3r 4r 5r 6r 7r Kr", "2h 3h 4h 5h 6h 7h Ks"], next: "8h Jh 9h");
        Assert.Equal(1, t.G.Starter);
        Assert.Equal(new[] { (0, "8h", 1), (1, "Jh", 1), (2, "9h", 1) }, Ties(t));
        Assert.Equal(H("8h Jh 9h"), t.G.Discards);
        Assert.Equal(52 - 21 - 3, t.G.Deck.Count);
    }

    // tre damer; runda 1 ger 9, 4, 9 och runda 2 avgörs mellan plats 0 och 2 (3 mot 8)
    static Table QueensTwice(Table? t = null) => (t ?? Table.Seated(3)).Deal(["2k 3k 4k 5k 6k 7k Qk", "2r 3r 4r 5r 6r 7r Qr", "2h 3h 4h 5h 6h 7h Qs"], next: "9k 4s 9r 3s 8s", by: t == null ? -1 : 1);

    [Fact]
    public void Lika_igen_då_drar_bara_de_som_fortfarande_är_lika()
    {
        var t = QueensTwice();
        Assert.Equal(2, t.G.Starter);
        Assert.Equal(new[] { (0, "9k", 1), (1, "4s", 1), (2, "9r", 1), (0, "3s", 2), (2, "8s", 2) }, Ties(t));
        Assert.Equal(H("9k 4s 9r 3s 8s"), t.G.Discards);
        Assert.Equal(52 - 21 - 5, t.G.Deck.Count);
    }

    [Fact]
    public void Bara_de_med_högsta_valören_drar_även_om_avgörandekorten_blir_lägre()
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k Ah", "2r 3r 4r 5r 6r 7r Ak", "2h 3h 4h 5h 6h 7h Ks"], next: "2s 3s");
        Assert.Equal(1, t.G.Starter); // kungen på plats 2 kommer inte tillbaka
        Assert.Equal(new[] { (0, "2s", 1), (1, "3s", 1) }, Ties(t));
    }

    [Fact]
    public void Lika_på_lägre_valör_avgörs_inte()
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k 9k", "2r 3r 4r 5r 6r 7r As", "2h 3h 4h 5h 6h 7h 9r"]);
        Assert.Equal(1, t.G.Starter);
        Assert.Empty(t.G.Ties);
        Assert.Empty(t.G.Discards);
        Assert.Equal(52 - 21, t.G.Deck.Count);
        Assert.DoesNotContain(t.G.Notes, n => n.Contains("Lika"));
    }

    [Fact]
    public void Byte_efter_avgörande_drar_korten_som_ligger_efter_avgörandekorten()
    {
        var t = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k Ah", "2r 3r 4r 5r 6r 7r As"], next: "5h 9h Kh Qh");
        Assert.Equal(1, t.G.Starter);
        Assert.Null(t.Exchange(1, "2r 3r"));
        Assert.Equal(H("Kh Qh 4r 5r 6r 7r As"), t.Hand(1));
        Assert.Equal(H("5h 9h 2r 3r"), t.G.Discards);
    }

    [Fact]
    public void Avgörandet_skrivs_i_chatten()
    {
        var t = QueensTwice();
        Assert.Contains(t.G.Notes, n => n.Contains("P0, P1 och P2"));
        Assert.Contains(t.G.Notes, n => n.Contains("P0 och P2") && !n.Contains("P1"));
        Assert.Contains(t.G.Notes, n => n.Contains("P2") && n.Contains("börjar"));
    }

    [Fact]
    public void Avgörandekorten_syns_för_alla_i_tillståndet()
    {
        var t = QueensTwice();
        for (int you = -1; you < 3; you++)
        {
            var tie = Tie(t.G, you).EnumerateArray().Select(x => (x.GetProperty("seat").GetInt32(), x.GetProperty("r").GetInt32(), x.GetProperty("s").GetInt32(), x.GetProperty("round").GetInt32())).ToArray();
            Assert.Equal(new[] { (0, 9, 0, 1), (1, 4, 3, 1), (2, 9, 1, 1), (0, 3, 3, 2), (2, 8, 3, 2) }, tie);
        }
    }

    [Fact]
    public void Avgörandet_töms_när_spelet_börjar()
    {
        var t = Table.Playing("2k 3k 4k 5k 6k 7k Ah", "2r 3r 4r 5r 6r 7r As");
        Assert.Empty(t.G.Ties);
        Assert.Empty(Tie(t.G, 0).EnumerateArray());
        Assert.Equal(2, t.G.Discards.Count); // korten ligger kvar i slasken
        t.AssertConserved();
    }

    [Fact]
    public void Avgörandet_töms_när_spelet_avbryts_och_kommer_inte_tillbaka_vid_ny_giv()
    {
        var t = QueensTwice();
        Assert.Equal(5, t.G.Ties.Count);
        t.G.Leave(2);
        Assert.Equal("lobby", t.G.Phase);
        Assert.Empty(t.G.Ties);
        Assert.Empty(Tie(t.G, 0).EnumerateArray());
        Assert.Null(t.G.Join("p2", "P2"));
        t.Deal(["2k 3k 4k 5k 6k 7k 9k", "2r 3r 4r 5r 6r 7r As", "2h 3h 4h 5h 6h 7h 9r"]); // ny giv utan lika
        Assert.Empty(t.G.Ties);
        Assert.Empty(t.G.Discards);
    }

    [Fact]
    public void Alla_saknar_kort_över_tio_avgörandet_ger_startspelare_och_övriga_är_Zlatan()
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k Tk", "2r 3r 4r 5r 6r 7r Tr", "2h 3h 4h 5h 6h 7h 9h"], next: "4s 8s");
        Assert.Equal(1, t.G.Starter);
        Assert.Equal(new[] { 0, 2 }, t.G.Zl.OrderBy(x => x));
    }

    [Fact]
    public void Avgörandekortet_räknas_inte_till_handen_för_Zlatan()
    {
        // avgörandekorten (ess, kung) räknas inte: plats 0 har inget kort över tio i handen och är Zlatan-kandidat
        var t = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k Tk", "2r 3r 4r 5r 6r 7r Tr"], next: "Ah 2h");
        Assert.Equal(0, t.G.Starter);
        Assert.Equal(new[] { 1 }, t.G.Zl);
        var u = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k Tk", "2r 3r 4r 5r 6r 7r Tr"], next: "Kh Ah");
        Assert.Equal(1, u.G.Starter);
        Assert.Equal(new[] { 0 }, u.G.Zl);
        Assert.Null(u.G.Exchange(1, [0]));
        Assert.Equal("zlatan", u.G.Phase);
        Assert.Null(u.G.Zlatan(0, true));
        u.AssertConserved();
    }

    [Fact]
    public void Tar_leken_slut_mitt_i_avgörandet_avgör_färgen()
    {
        // fyra ess visas och sex rundor med fyrtal tömmer leken; sista rundans spader (plats 1) avgör
        var t = Table.Seated(4).Deal(
            ["2k 3k 4k 5k 6k 7k Ak", "2r 3r 4r 5r 6r 7r Ar", "2h 3h 4h 5h 6h 7h Ah", "2s 3s 4s 5s 6s 7s As"],
            next: "8k 8r 8h 8s 9k 9r 9h 9s Tk Tr Th Ts Jk Jr Jh Js Qk Qr Qh Qs Kk Ks Kr Kh");
        Assert.Empty(t.G.Deck);
        Assert.Equal(24, t.G.Ties.Count);
        Assert.Equal(6, t.G.Ties.Max(x => x.Round));
        Assert.Equal(1, t.G.Starter);
        Assert.Empty(t.G.Zl);
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
