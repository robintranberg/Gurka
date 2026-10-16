using static Gurka.Tests.Cards;

namespace Gurka.Tests.Rules;

// Startspelarens byte och svaret från övriga när startspelaren INTE bytte (kasta alla sju eller behålla).
public class ExchangeTests
{
    // seat 1 börjar (As), ingen har bara låga kort, så ingen Zlatan
    static readonly string[] Three = ["2k 3k 4k 5k 6k 7k Kk", "2r 3r 4r 5r 6r 7r As", "2h 3h 4h 5h 6h 7h Qh"];

    [Fact]
    public void Bara_startspelaren_får_byta()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Equal("Inte din tur.", t.G.Exchange(0, [0]));
        Assert.Equal("Inte din tur.", t.G.Exchange(2, null));
        Assert.Equal("Inte din tur.", t.G.Exchange(-1, null));
        Assert.Equal("exchange", t.G.Phase);
    }

    [Theory]
    [InlineData(new[] { 0, 0 })]
    [InlineData(new[] { -1 })]
    [InlineData(new[] { 7 })]
    [InlineData(new[] { 0, 1, 2, 3, 4, 5, 6, 6 })]
    [InlineData(new[] { int.MaxValue })]
    [InlineData(new[] { int.MinValue })]
    public void Ogiltiga_index_avvisas(int[] idx)
    {
        var t = Table.Seated(3).Deal(Three);
        var before = t.Hand(1).ToList();
        Assert.Equal("Ogiltigt val.", t.G.Exchange(1, idx));
        Assert.Equal("exchange", t.G.Phase);
        Assert.Equal(before, t.Hand(1));
    }

    [Fact]
    public void Inget_byte_ger_svarsfas_utan_fritt_byte_medsols_från_startspelaren()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Null(t.G.Exchange(1, null));
        Assert.Equal("respond", t.G.Phase);
        Assert.False(t.G.RespFree);
        Assert.Equal(0, t.G.ExN);
        Assert.Equal(new[] { 2, 0 }, t.G.Resp.ToArray());
        Assert.Equal(2, t.G.CurTurn);
        Assert.Empty(t.G.Reveal);
    }

    [Fact]
    public void Tom_array_är_samma_som_inget_byte()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Null(t.G.Exchange(1, []));
        Assert.Equal("respond", t.G.Phase);
        Assert.False(t.G.RespFree);
    }

    [Fact]
    public void Byte_ersätter_valda_kort_med_lekens_översta_i_ordning()
    {
        var t = Table.Seated(3).Deal(Three, next: "Ar Kr Qr");
        Assert.Null(t.Exchange(1, "3r 5r 2r"));
        Assert.Equal(H("Qr Ar 4r Kr 6r 7r As"), t.Hand(1)); // index 1,3,0 får Ar, Kr, Qr
        Assert.Equal(H("3r 5r 2r"), t.G.Discards);
        Assert.Equal(52 - 21 - 3, t.G.Deck.Count);
        Assert.Equal("respond", t.G.Phase);
        Assert.True(t.G.RespFree);
        Assert.Equal(3, t.G.ExN);
    }

    [Fact]
    public void Startspelaren_kan_byta_alla_sju()
    {
        var t = Table.Seated(2).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr"], next: "8h 9h Th Jh Qh Kh Ah");
        Assert.Null(t.G.Exchange(0, [0, 1, 2, 3, 4, 5, 6]));
        Assert.Equal(H("8h 9h Th Jh Qh Kh Ah"), t.Hand(0));
        Assert.Equal(7, t.G.ExN);
    }

    [Fact]
    public void Kasta_alla_ger_sju_nya_kort_och_gamla_hamnar_i_slasken()
    {
        var t = Table.Seated(3).Deal(Three, next: "8r 9r Tr Jr Qr Kr Ar");
        Assert.Null(t.G.Exchange(1, null));
        var old = t.Hand(2).ToList();
        Assert.Null(t.G.Respond(2, true));
        Assert.Equal(H("8r 9r Tr Jr Qr Kr Ar"), t.Hand(2));
        Assert.Equal(old, t.G.Discards);
        t.AssertConserved();
    }

    [Fact]
    public void Behålla_ändrar_ingenting()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Null(t.G.Exchange(1, null));
        var old = t.Hand(2).ToList(); int deck = t.G.Deck.Count;
        Assert.Null(t.G.Respond(2, false));
        Assert.Equal(old, t.Hand(2));
        Assert.Equal(deck, t.G.Deck.Count);
        Assert.Empty(t.G.Discards);
    }

    [Fact]
    public void Svar_i_fel_ordning_avvisas()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Null(t.G.Exchange(1, null));
        Assert.Equal("Inte din tur.", t.G.Respond(0, true));
        Assert.Equal("Inte din tur.", t.G.Respond(1, true)); // startspelaren svarar inte
        Assert.Null(t.G.Respond(2, false));
        Assert.Equal("Inte din tur.", t.G.Respond(2, false)); // redan svarat
        Assert.Null(t.G.Respond(0, false));
    }

    [Fact]
    public void Fritt_byte_är_inte_tillåtet_när_startspelaren_inte_bytte()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Null(t.G.Exchange(1, null));
        Assert.Equal("Inte din tur.", t.G.Swap(2, [0], false));
        Assert.Equal("Inte din tur.", t.G.Swap(2, [0, 1, 2, 3, 4, 5, 6], true));
        Assert.Equal(2, t.G.CurTurn);
    }

    [Fact]
    public void När_alla_svarat_börjar_spelet_hos_startspelaren()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Null(t.G.Exchange(1, null));
        Assert.Null(t.G.Respond(2, true));
        Assert.Null(t.G.Respond(0, false));
        Assert.Equal("play", t.G.Phase);
        Assert.Equal(1, t.G.Turn);
        Assert.Equal(0, t.G.Count);
        Assert.Empty(t.G.Trick);
    }

    [Fact]
    public void Kasta_alla_avvisas_om_leken_har_färre_än_sju_kort()
    {
        // kort lek: bara 2 kort kvar efter given
        var t = new Table();
        t.G.Join("a", "A"); t.G.Join("b", "B");
        var d = Deck(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr"]).Take(16).ToList();
        t.G.NewDeck = () => d.ToList();
        Assert.Null(t.G.Start(0));
        Assert.Null(t.G.Exchange(0, null));
        Assert.Equal("Leken räcker inte för 7 nya kort.", t.G.Respond(1, true));
        Assert.Equal("respond", t.G.Phase);
        Assert.Null(t.G.Respond(1, false));
    }

    [Fact]
    public void Startspelaren_kan_inte_byta_fler_kort_än_leken_har()
    {
        var t = new Table();
        t.G.Join("a", "A"); t.G.Join("b", "B");
        var d = Deck(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr"]).Take(16).ToList();
        t.G.NewDeck = () => d.ToList();
        Assert.Null(t.G.Start(0));
        Assert.Equal("Leken har bara 2 kort kvar.", t.G.Exchange(0, [0, 1, 2]));
        Assert.Null(t.G.Exchange(0, [0, 1]));
    }
}
