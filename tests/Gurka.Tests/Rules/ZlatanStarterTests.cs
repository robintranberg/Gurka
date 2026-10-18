using System.Text.Json;
using static Gurka.Tests.Cards;

namespace Gurka.Tests.Rules;

// Startspelarens Zlatan (2026-10-18): saknar startspelaren kort över tio får den i bytesfasen först ta sju nya kort
// (eller avstå) och byter sedan som vanligt. Övrigas Zlatan-fas kommer nu även när startspelaren behåller alla kort.
public class ZlatanStarterTests
{
    static JsonElement View(Game g, int you) => JsonSerializer.SerializeToElement(g.View(you));
    static int[] Ints(JsonElement s, string p) => s.GetProperty(p).EnumerateArray().Select(x => x.GetInt32()).ToArray();
    const string No = "Du kan inte köra Zlatan nu.";

    // plats 0 börjar med tian och har inget kort över tio; plats 1 och 2 har klätt kort, så ingen annan är Zlatan
    static readonly string[] Low = ["2k 3k 4k 5k 6k 7k Tk", "Qr 3r 4r 5r 6r 7r 8r", "Kh 3h 4h 5h 6h 7h 9h"];
    // samma visade kort, men startspelaren har en knekt
    static readonly string[] High = ["Jk 3k 4k 5k 6k 7k Tk", "Qr 3r 4r 5r 6r 7r 8r", "Kh 3h 4h 5h 6h 7h 9h"];
    // startspelaren saknar kort över tio och plats 1 är dessutom Zlatan-kandidat
    static readonly string[] Both = ["2k 3k 4k 5k 6k 7k Tk", "2r 3r 4r 5r 6r 7r 8r", "Kh 3h 4h 5h 6h 7h 9h"];
    const string Seven = "8h Th Jh Qh Ah 9s Ts";

    [Fact]
    public void Startspelare_utan_kort_över_tio_får_ZlSt_och_fasen_är_exchange()
    {
        var t = Table.Seated(3).Deal(Low);
        Assert.Equal(0, t.G.Starter);
        Assert.True(t.G.ZlSt);
        Assert.Empty(t.G.Zl);
        Assert.Equal("exchange", t.G.Phase);
        Assert.Equal(0, t.G.CurTurn);
    }

    [Theory]
    [InlineData("Jk 3k 4k 5k 6k 7k Tk")] // knekt i handen
    [InlineData("2k 3k 4k 5k 6k 7k Ak")] // visat ess
    public void Startspelare_med_kort_över_tio_får_inte_ZlSt(string starter)
    {
        var t = Table.Seated(3).Deal([starter, Low[1], Low[2]]);
        Assert.Equal(0, t.G.Starter);
        Assert.False(t.G.ZlSt);
        Assert.Equal(No, t.G.Zlatan(0, true));
        Assert.Equal(No, t.G.Zlatan(0, false));
        Assert.Equal("exchange", t.G.Phase);
    }

    [Fact]
    public void ZlSt_gäller_bara_startspelaren_och_övriga_Zl_beräknas_utan_den()
    {
        var t = Table.Seated(3).Deal(Both);
        Assert.True(t.G.ZlSt);
        Assert.Equal(new[] { 1 }, t.G.Zl);
    }

    [Fact]
    public void Zlatan_ger_sju_nya_kort_och_startspelaren_byter_sedan()
    {
        var t = Table.Seated(3).Deal(Low, next: Seven);
        var old = t.Hand(0).ToList();
        Assert.Null(t.G.Zlatan(0, true));
        Assert.Equal(H(Seven), t.Hand(0));
        Assert.Equal(old, t.G.Discards);
        Assert.Equal(24, t.G.Deck.Count);
        Assert.Contains("P0 körde Zlatan och fick 7 nya kort!", t.G.Notes);
        Assert.False(t.G.ZlSt);
        Assert.Equal("exchange", t.G.Phase);
        Assert.Equal(0, t.G.CurTurn);
        t.AssertConserved();
    }

    [Fact]
    public void Zlatan_sedan_inget_byte_ger_respond_utan_valfritt_byte()
    {
        var t = Table.Seated(3).Deal(Low, next: Seven);
        Assert.Null(t.G.Zlatan(0, true));
        Assert.Null(t.G.Exchange(0, null));
        Assert.Equal("respond", t.G.Phase);
        Assert.False(t.G.RespFree);
        Assert.Equal(H(Seven), t.Hand(0));
        Assert.Equal(new[] { 1, 2 }, t.G.Resp.ToArray());
        Assert.Null(t.G.Respond(1, false));
        Assert.Null(t.G.Respond(2, true));
        Assert.Equal("play", t.G.Phase);
        Assert.Equal(0, t.G.Turn);
        t.AssertConserved();
    }

    [Fact]
    public void Zlatan_sedan_byte_av_två_kort()
    {
        var t = Table.Seated(3).Deal(Low, next: Seven + " 2s 3s");
        Assert.Null(t.G.Zlatan(0, true));
        Assert.Null(t.Exchange(0, "Th Ah"));
        Assert.Equal(H("8h 2s Jh Qh 3s 9s Ts"), t.Hand(0));
        Assert.Equal(H("2k 3k 4k 5k 6k 7k Tk Th Ah"), t.G.Discards);
        Assert.Equal("respond", t.G.Phase);
        Assert.True(t.G.RespFree);
        Assert.Equal(2, t.G.ExN);
        t.AssertConserved();
    }

    [Fact]
    public void Zlatan_sedan_1_upp()
    {
        var t = Table.Seated(3).Deal(Low, next: Seven + " 2s");
        Assert.Null(t.G.Zlatan(0, true));
        Assert.Null(t.Exchange(0, "Qh"));
        Assert.Equal("oneup", t.G.Phase);
        Assert.Equal(C("2s"), t.G.UpCard);
        Assert.Equal(0, t.G.UpOwner);
        t.AssertConserved();
    }

    [Fact]
    public void Avstå_behåller_handen_och_startspelaren_byter_sedan()
    {
        var t = Table.Seated(3).Deal(Low, next: "2s 3s");
        var old = t.Hand(0).ToList();
        Assert.Null(t.G.Zlatan(0, false));
        Assert.Equal(old, t.Hand(0));
        Assert.False(t.G.ZlSt);
        Assert.Equal("exchange", t.G.Phase);
        Assert.Equal(31, t.G.Deck.Count);
        Assert.Empty(t.G.Discards);
        Assert.DoesNotContain(t.G.Notes, n => n.Contains("Zlatan"));
        Assert.Equal(No, t.G.Zlatan(0, true)); // valet är gjort
        Assert.Null(t.Exchange(0, "2k 3k"));
        Assert.Equal(H("2s 3s 4k 5k 6k 7k Tk"), t.Hand(0));
        Assert.Equal("respond", t.G.Phase);
        t.AssertConserved();
    }

    [Fact]
    public void Bara_en_Zlatan_per_giv()
    {
        var t = Table.Seated(3).Deal(Low, next: Seven);
        Assert.Null(t.G.Zlatan(0, true));
        var h = t.Hand(0).ToList();
        Assert.Equal(No, t.G.Zlatan(0, true));
        Assert.Equal(No, t.G.Zlatan(0, false));
        Assert.Equal(h, t.Hand(0));
        Assert.Equal(24, t.G.Deck.Count);
        Assert.Single(t.G.Notes, n => n.Contains("körde Zlatan"));
    }

    [Fact]
    public void Byte_utan_Zlatan_avstår_och_ZlSt_töms()
    {
        var t = Table.Seated(3).Deal(Low);
        Assert.Null(t.G.Exchange(0, null));
        Assert.False(t.G.ZlSt);
        Assert.Equal(No, t.G.Zlatan(0, true));
        Assert.Equal("respond", t.G.Phase);
    }

    [Fact]
    public void Ogiltigt_byte_tömmer_inte_ZlSt()
    {
        var t = Table.Seated(3).Deal(Low);
        Assert.Equal("Ogiltigt val.", t.G.Exchange(0, [7]));
        Assert.True(t.G.ZlSt);
        Assert.Null(t.G.Zlatan(0, true));
    }

    [Fact]
    public void Fel_spelare_kan_inte_köra_Zlatan_i_bytesfasen()
    {
        var t = Table.Seated(3).Deal(Both);
        Assert.Equal(No, t.G.Zlatan(1, true)); // Zlatan-kandidat, men väntar på startspelarens byte
        Assert.Equal(No, t.G.Zlatan(1, false));
        Assert.Equal(No, t.G.Zlatan(2, true));
        Assert.Equal(No, t.G.Zlatan(-1, true));
        Assert.Equal(No, t.G.Zlatan(3, true)); // tom plats
        Assert.True(t.G.ZlSt);
        Assert.Equal(new[] { 1 }, t.G.Zl);
        Assert.Equal("exchange", t.G.Phase);
    }

    [Fact]
    public void Startspelaren_kan_inte_köra_Zlatan_i_övrigas_Zlatan_fas()
    {
        var t = Table.Seated(3).Deal(Both);
        Assert.Null(t.G.Exchange(0, [0, 1]));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Equal(No, t.G.Zlatan(0, true));
        Assert.Null(t.G.Zlatan(1, false));
        Assert.Equal("respond", t.G.Phase);
    }

    [Fact]
    public void Startspelarens_Zlatan_sedan_övrigas_Zlatan()
    {
        var t = Table.Seated(3).Deal(Both, next: Seven + " 8k 9k Jk Qk Kk Ak 9r 2s 3s");
        Assert.Null(t.G.Zlatan(0, true));
        Assert.Null(t.Exchange(0, "8h Th"));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Equal(H("8k 9k Jk Qk Kk Ak 9r"), t.Hand(1));
        Assert.Equal(H("2s 3s Jh Qh Ah 9s Ts"), t.Hand(0));
        Assert.Equal("respond", t.G.Phase);
        Assert.Equal(2, t.G.Notes.Count(n => n.Contains("körde Zlatan")));
        t.AssertConserved();
    }

    // 3 spelare visar tior. Två avgörandekort-rundor med tre, sedan åtta med plats 0 och 1 (slutar med 9h mot 8s):
    // 24 kort till slasken, exakt 7 kvar i leken.
    const string TieDraws = "8k 8r 8h  9k 9r 2s  Jk Jr Qk Qr Kk Kr Ak Ar Jh Js Qh Qs Kh Ks Ah As  9h 8s";
    static readonly string[] Tens = ["2k 3k 4k 5k 6k 7k Tk", "2r 3r 4r 5r 6r 7r Tr", "2h 3h 4h 5h 6h 7h Th"];

    [Fact]
    public void Zlatan_med_exakt_sju_kort_kvar_tömmer_leken()
    {
        var t = Table.Seated(3).Deal(Tens, next: TieDraws);
        Assert.Equal(0, t.G.Starter);
        Assert.Equal(7, t.G.Deck.Count);
        Assert.True(t.G.ZlSt);
        Assert.Null(t.G.Zlatan(0, true));
        Assert.Equal(H("3s 4s 5s 6s 7s 9s Ts"), t.Hand(0));
        Assert.Empty(t.G.Deck);
        Assert.Equal("exchange", t.G.Phase);
        t.AssertConserved();
    }

    [Fact]
    public void Zlatan_nekas_när_leken_har_färre_än_sju_kort()
    {
        // 4 spelare visar tior; fyra rundor lika, femte avgör (spaderesset): 24 - 20 = 4 kort kvar
        var t = Table.Seated(4).Deal(["2k 3k 4k 5k 6k 7k Tk", "2r 3r 4r 5r 6r 7r Tr", "2h 3h 4h 5h 6h 7h Th", "2s 3s 4s 5s 6s 7s Ts"],
            next: "8k 8r 8h 8s 9k 9r 9h 9s Jk Jr Jh Js Qk Qr Qh Qs Kk Kr Kh As");
        Assert.Equal(3, t.G.Starter);
        Assert.Equal(4, t.G.Deck.Count);
        Assert.True(t.G.ZlSt);
        var h = t.Hand(3).ToList();
        Assert.Equal("Leken räcker inte för 7 nya kort.", t.G.Zlatan(3, true));
        Assert.Equal(h, t.Hand(3));
        Assert.True(t.G.ZlSt); // nekat val ändrar inget
        Assert.Equal("exchange", t.G.Phase);
        Assert.Null(t.G.Zlatan(3, false));
        Assert.False(t.G.ZlSt);
        t.AssertConserved();
    }

    // --- Vad varje plats ser ---

    [Fact]
    public void Bara_startspelaren_ser_sitt_ZlSt()
    {
        var t = Table.Seated(3).Deal(Both);
        var s0 = View(t.G, 0);
        Assert.True(s0.GetProperty("zlMe").GetBoolean());
        Assert.Equal(0, s0.GetProperty("zlN").GetInt32());
        foreach (var i in new[] { -1, 1, 2 })
        {
            var s = View(t.G, i);
            Assert.False(s.GetProperty("zlMe").GetBoolean());
            Assert.Equal(0, s.GetProperty("zlN").GetInt32());
            Assert.Empty(Ints(s, "zl"));
        }
    }

    // två givar som bara skiljer sig i startspelarens dolda kort (2k mot Jk): övriga platser ska få exakt samma tillstånd
    [Fact]
    public void Övriga_platser_får_samma_tillstånd_med_och_utan_ZlSt()
    {
        var a = Table.Seated(3).Deal(Low);
        var b = Table.Seated(3).Deal(High);
        Assert.True(a.G.ZlSt);
        Assert.False(b.G.ZlSt);
        foreach (var i in new[] { -1, 1, 2 })
            Assert.Equal(View(b.G, i).GetRawText(), View(a.G, i).GetRawText());
        Assert.NotEqual(View(b.G, 0).GetProperty("zlMe").GetBoolean(), View(a.G, 0).GetProperty("zlMe").GetBoolean());
    }

    [Fact]
    public void Avstå_lämnar_inga_spår_hos_övriga()
    {
        var a = Table.Seated(3).Deal(Low);
        var b = Table.Seated(3).Deal(High);
        Assert.Null(a.G.Zlatan(0, false));
        foreach (var i in new[] { -1, 1, 2 })
            Assert.Equal(View(b.G, i).GetRawText(), View(a.G, i).GetRawText());
        Assert.False(View(a.G, 0).GetProperty("zlMe").GetBoolean());
    }

    [Fact]
    public void Inget_ZlSt_syns_efter_bytet()
    {
        var t = Table.Seated(3).Deal(Both);
        Assert.Null(t.G.Exchange(0, [0, 1]));
        Assert.False(View(t.G, 0).GetProperty("zlMe").GetBoolean());
        Assert.True(View(t.G, 1).GetProperty("zlMe").GetBoolean());
        Assert.Equal(1, View(t.G, 2).GetProperty("zlN").GetInt32());
    }

    // --- Övrigas Zlatan när startspelaren behåller alla kort ---

    static readonly string[] Three = ["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh"];

    [Fact]
    public void Inget_byte_med_kandidat_ger_Zlatan_fas()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.False(t.G.ZlSt);
        Assert.Null(t.G.Exchange(0, null));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Equal(0, t.G.ExN);
        Assert.Equal(-1, t.G.CurTurn);
        Assert.True(View(t.G, 1).GetProperty("zlMe").GetBoolean());
        Assert.Equal(1, View(t.G, 0).GetProperty("zlN").GetInt32());
    }

    [Fact]
    public void Zlatan_vid_inget_byte_sedan_bara_kasta_alla_eller_behålla()
    {
        var t = Table.Seated(3).Deal(Three, next: "8h 9h Th Jh Kh Ah 8s 8k 9k Tk Jk Qk Kk Ak");
        var start = t.Hand(0).ToList();
        Assert.Null(t.G.Exchange(0, null));
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Equal(H("8h 9h Th Jh Kh Ah 8s"), t.Hand(1));
        Assert.Equal(start, t.Hand(0));
        Assert.Equal("respond", t.G.Phase);
        Assert.False(t.G.RespFree);
        Assert.Empty(t.G.Zl);
        Assert.Equal(new[] { 1, 2 }, t.G.Resp.ToArray());
        Assert.Equal("Inte din tur.", t.Swap(1, "8h"));
        Assert.Null(t.G.Respond(1, true));
        Assert.Equal(H("8k 9k Tk Jk Qk Kk Ak"), t.Hand(1));
        Assert.Null(t.G.Respond(2, false));
        Assert.Equal("play", t.G.Phase);
        t.AssertConserved();
    }

    [Fact]
    public void Avstå_vid_inget_byte_går_till_respond()
    {
        var t = Table.Seated(3).Deal(Three);
        var h = t.Hand(1).ToList();
        Assert.Null(t.G.Exchange(0, null));
        Assert.Null(t.G.Zlatan(1, false));
        Assert.Equal(h, t.Hand(1));
        Assert.Equal("respond", t.G.Phase);
        Assert.False(t.G.RespFree);
        Assert.Empty(t.G.Discards);
    }

    [Fact]
    public void Zlatan_vid_inget_byte_nekas_först_när_leken_inte_räcker()
    {
        // 4 spelare, 24 kort kvar och inget startbyte: två Zlatan ryms (17, 10), även den tredje (3) – men inte en fjärde
        var t = Table.Seated(4).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h 8h", "2s 3s 4s 5s 6s 7s 8s"]);
        Assert.Null(t.G.Exchange(0, null));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Null(t.G.Zlatan(2, true));
        Assert.Null(t.G.Zlatan(3, true)); // 10 - 0 >= 7: inget startbyte att reservera för
        Assert.Equal(3, t.G.Deck.Count);
        Assert.Equal("respond", t.G.Phase);
        Assert.False(t.G.RespFree);
        t.AssertConserved();
    }

    [Fact]
    public void Byte_utan_kandidater_hoppar_fortfarande_över_Zlatan()
    {
        var t = Table.Seated(3).Deal(High);
        Assert.Null(t.G.Exchange(0, null));
        Assert.Equal("respond", t.G.Phase);
    }
}
