using static Gurka.Tests.Cards;

namespace Gurka.Tests.Rules;

// Fritt byte: startspelaren bytte N kort. Övriga får byta N kort, alla 7 eller inga.
// Räcker inte leken: byt det som finns kvar eller gräv N kort ur slasken. Är leken slut: gräv eller behåll.
public class SwapTests
{
    static readonly string[] Three = ["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr", "2h 3h 4h 5h 6h 7h Qh"];
    // fyra spelare utan Zlatan-kandidater
    static readonly string[] Four = ["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr", "2h 3h 4h 5h 6h 7h Qh", "2s 3s 4s 5s 6s 7s Js"];

    static Table AfterStarterSwaps(int n, string next = "")
    {
        var t = Table.Seated(3).Deal(Three, next);
        Assert.Null(t.G.Exchange(0, Enumerable.Range(0, n).ToArray()));
        Assert.Equal("respond", t.G.Phase);
        Assert.True(t.G.RespFree);
        return t;
    }

    [Fact]
    public void Byta_lika_många_som_startspelaren()
    {
        var t = AfterStarterSwaps(2, next: "8k 9k Ah Kh");
        Assert.Null(t.Swap(1, "6r 2r"));
        Assert.Equal(H("Kh 3r 4r 5r Ah 7r Kr"), t.Hand(1)); // index 4 (6r) -> Ah, index 0 (2r) -> Kh
        Assert.Equal(H("2k 3k 6r 2r"), t.G.Discards);
    }

    [Fact]
    public void Byta_alla_sju()
    {
        var t = AfterStarterSwaps(2, next: "8k 9k 8h 9h Th Jh Kh Ah 8s");
        Assert.Null(t.G.Swap(1, [0, 1, 2, 3, 4, 5, 6], false));
        Assert.Equal(H("8h 9h Th Jh Kh Ah 8s"), t.Hand(1));
    }

    [Fact]
    public void Behålla_alla()
    {
        var t = AfterStarterSwaps(2);
        var old = t.Hand(1).ToList(); int deck = t.G.Deck.Count;
        Assert.Null(t.G.Swap(1, null, false));
        Assert.Equal(old, t.Hand(1));
        Assert.Equal(deck, t.G.Deck.Count);
        Assert.Equal(2, t.G.CurTurn);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Annat_antal_avvisas(int k)
    {
        var t = AfterStarterSwaps(2);
        Assert.Equal("Du kan byta 2 kort, alla 7 eller inga.", t.G.Swap(1, Enumerable.Range(0, k).ToArray(), false));
        Assert.Equal(1, t.G.CurTurn);
    }

    [Theory]
    [InlineData(new[] { 0, 0 })]
    [InlineData(new[] { 0, 7 })]
    [InlineData(new[] { -1, 2 })]
    public void Ogiltiga_index_avvisas(int[] idx)
    {
        var t = AfterStarterSwaps(2);
        Assert.Equal("Ogiltigt val.", t.G.Swap(1, idx, false));
        Assert.Equal(1, t.G.CurTurn);
    }

    [Fact]
    public void Svar_i_fel_ordning_och_vanligt_svar_avvisas_vid_fritt_byte()
    {
        var t = AfterStarterSwaps(2);
        Assert.Equal("Inte din tur.", t.G.Swap(2, [0, 1], false));
        Assert.Equal("Inte din tur.", t.G.Swap(0, [0, 1], false));
        Assert.Equal("Inte din tur.", t.G.Respond(1, true));
        Assert.Equal("Inte din tur.", t.G.Respond(1, false));
    }

    [Fact]
    public void Gräva_i_slasken_går_inte_när_leken_räcker()
    {
        var t = AfterStarterSwaps(2);
        Assert.Equal("Du kan inte gräva i slasken nu.", t.G.Swap(1, [0, 1], true));
    }

    [Fact]
    public void Gräva_utan_kort_är_samma_som_behålla()
    {
        var t = AfterStarterSwaps(2);
        var old = t.Hand(1).ToList();
        Assert.Null(t.G.Swap(1, [], true));
        Assert.Equal(old, t.Hand(1));
    }

    [Fact]
    public void Efter_sista_svaret_börjar_spelet_hos_startspelaren()
    {
        var t = AfterStarterSwaps(2); // ett bytt kort ger 1 upp, se OneUpTests
        Assert.Null(t.G.Swap(1, [0, 1], false));
        Assert.Null(t.G.Swap(2, null, false));
        Assert.Equal("play", t.G.Phase);
        Assert.Equal(0, t.G.Turn);
        t.AssertConserved();
    }

    // 4 spelare, startspelaren byter 7: 24 -> 17. Plats 1 och 2 byter 7: 17 -> 10 -> 3. Plats 3 har 3 kort kvar i leken.
    static Table LowDeck()
    {
        var t = Table.Seated(4).Deal(Four);
        Assert.Null(t.G.Exchange(0, [0, 1, 2, 3, 4, 5, 6]));
        Assert.Null(t.G.Swap(1, [0, 1, 2, 3, 4, 5, 6], false));
        Assert.Null(t.G.Swap(2, [0, 1, 2, 3, 4, 5, 6], false));
        Assert.Equal(3, t.G.Deck.Count);
        Assert.Equal(3, t.G.CurTurn);
        return t;
    }

    [Fact]
    public void Leken_räcker_inte_byt_det_som_finns_kvar()
    {
        var t = LowDeck();
        var rest = t.G.Deck.ToList();
        Assert.Null(t.G.Swap(3, [4, 5, 6], false));
        Assert.Equal(H("2s 3s 4s 5s").Concat(rest), t.Hand(3));
        Assert.Empty(t.G.Deck);
        Assert.Equal("play", t.G.Phase);
        t.AssertConserved();
    }

    [Theory]
    [InlineData(7)]
    [InlineData(2)]
    [InlineData(1)]
    public void Leken_räcker_inte_annat_antal_avvisas(int k)
    {
        var t = LowDeck();
        Assert.Equal("Leken räcker inte: byt 3 kort eller gräv i slasken (7 kort).", t.G.Swap(3, Enumerable.Range(0, k).ToArray(), false));
    }

    [Fact]
    public void Leken_räcker_inte_gräv_lika_många_som_startspelaren_bytte()
    {
        var t = LowDeck();
        var slask = t.G.Discards.ToList(); var old = t.Hand(3).ToList();
        Assert.Null(t.G.Swap(3, [0, 1, 2, 3, 4, 5, 6], true));
        Assert.All(t.Hand(3), c => Assert.Contains(c, slask));        // nya korten kommer ur slasken
        Assert.All(t.Hand(3), c => Assert.DoesNotContain(c, t.G.Discards)); // och är borttagna därifrån
        Assert.All(old, c => Assert.Contains(c, t.G.Discards));        // gamla korten hamnar i slasken
        Assert.Equal(3, t.G.Deck.Count);                               // leken rörs inte
        t.AssertConserved();
    }

    [Fact]
    public void Leken_räcker_inte_gräva_fel_antal_avvisas()
    {
        var t = LowDeck();
        Assert.Equal("Du kan inte gräva i slasken nu.", t.G.Swap(3, [0, 1, 2], true));
    }

    // 4 spelare, plats 1 kör Zlatan: 24 -> 17. Startspelaren byter 7: 17 -> 10. Plats 1 byter 7: 10 -> 3. Plats 2 byter 3: 3 -> 0.
    static Table EmptyDeck()
    {
        var t = Table.Seated(4).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh", "2s 3s 4s 5s 6s 7s Js"]);
        Assert.Null(t.G.Exchange(0, [0, 1, 2, 3, 4, 5, 6]));
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Null(t.G.Swap(1, [0, 1, 2, 3, 4, 5, 6], false));
        Assert.Null(t.G.Swap(2, [0, 1, 2], false));
        Assert.Empty(t.G.Deck);
        Assert.Equal(3, t.G.CurTurn);
        return t;
    }

    [Theory]
    [InlineData(7)]
    [InlineData(3)]
    [InlineData(1)]
    public void Leken_slut_byte_ur_leken_avvisas(int k)
    {
        var t = EmptyDeck();
        Assert.Equal("Leken är slut: gräv i slasken (7 kort) eller behåll.", t.G.Swap(3, Enumerable.Range(0, k).ToArray(), false));
    }

    [Fact]
    public void Leken_slut_gräv_i_slasken()
    {
        var t = EmptyDeck();
        Assert.Null(t.G.Swap(3, [0, 1, 2, 3, 4, 5, 6], true));
        Assert.Equal(7, t.Hand(3).Count);
        Assert.Equal("play", t.G.Phase);
        t.AssertConserved();
    }

    [Fact]
    public void Leken_slut_behåll()
    {
        var t = EmptyDeck();
        var old = t.Hand(3).ToList();
        Assert.Null(t.G.Swap(3, null, false));
        Assert.Equal(old, t.Hand(3));
        Assert.Equal("play", t.G.Phase);
    }

    [Fact]
    public void Grävning_med_samma_frö_ger_samma_kort()
    {
        List<Card> Dig(int seed)
        {
            var t = Table.Seated(4, seed).Deal(Four);
            Assert.Null(t.G.Exchange(0, [0, 1, 2, 3, 4, 5, 6]));
            Assert.Null(t.G.Swap(1, [0, 1, 2, 3, 4, 5, 6], false));
            Assert.Null(t.G.Swap(2, [0, 1, 2, 3, 4, 5, 6], false));
            Assert.Null(t.G.Swap(3, [0, 1, 2, 3, 4, 5, 6], true));
            return t.Hand(3).ToList();
        }
        Assert.Equal(Dig(7), Dig(7));
    }
}
