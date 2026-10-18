using System.Text.Json;
using static Gurka.Tests.Cards;

namespace Gurka.Tests.Rules;

// 1 upp: startspelaren bytte exakt ett kort. I tur och ordning lägger den som har tur sitt markerade kort i slasken och vänder upp översta kortet.
// Tar spelaren det går turen vidare. Tackar spelaren nej får BARA de som inte haft sin tur frågan åt vänster (aldrig startspelaren eller den
// som förbrukat sin tur). Tar någon kortet slänger den ett eget kort och förbrukar sin tur. Finns ingen att fråga går kortet direkt till slasken.
// Sedan får den som tackade nej nästa kort ur leken (dolt).
public class OneUpTests
{
    // plats 0 börjar (ess), inga Zlatan-kandidater
    static readonly string[] Hands = ["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr", "2h 3h 4h 5h 6h 7h Qh", "2s 3s 4s 5s 6s 7s Js"];

    static JsonElement View(Game g, int you) => JsonSerializer.SerializeToElement(g.View(you));
    static JsonElement Up(Game g, int you) => View(g, you).GetProperty("up");
    static string J(Card c) => $"{{\"r\":{c.R},\"s\":{c.S}}}";

    static void Ok(Table t, string? e) { Assert.Null(e); t.AssertConserved(); }
    static void No(Table t, string? e) { Assert.NotNull(e); t.AssertConserved(); }

    // n spelare, startspelaren (plats 0) markerar sitt första kort (2k)
    static Table Start(int n, string next)
    {
        var t = Table.Seated(n).Deal(Hands[..n], next);
        Ok(t, t.G.Exchange(0, [0]));
        AssertDecide(t, 0);
        return t;
    }

    static void AssertDecide(Table t, int owner)
    {
        Assert.Equal("oneup", t.G.Phase);
        Assert.Equal("decide", t.G.UpStage);
        Assert.Equal(owner, t.G.UpOwner);
        Assert.Equal(owner, t.G.CurTurn);
        Assert.NotNull(t.G.UpCard);
        Assert.Equal(6, t.Hand(owner).Count);
    }

    static void AssertPick(Table t, int owner)
    {
        Assert.Equal("oneup", t.G.Phase);
        Assert.Equal("pick", t.G.UpStage);
        Assert.Equal(owner, t.G.UpOwner);
        Assert.Equal(owner, t.G.CurTurn);
        Assert.Null(t.G.UpCard);
        Assert.All(t.G.Active, s => Assert.Equal(7, t.Hand(s).Count));
    }

    static void AssertPlay(Table t, int starter)
    {
        Assert.Equal("play", t.G.Phase);
        Assert.Equal(starter, t.G.Turn);
        Assert.Null(t.G.UpCard);
        Assert.Equal(JsonValueKind.Null, Up(t.G, starter).ValueKind);
        Assert.All(t.G.Active, s => Assert.Equal(7, t.Hand(s).Count));
    }

    [Fact]
    public void Startspelarens_markerade_kort_går_till_slasken_och_översta_kortet_vänds_upp()
    {
        var t = Start(3, "Ah 8s");
        Assert.Equal(H("3k 4k 5k 6k 7k As"), t.Hand(0));
        Assert.Equal(H("2k"), t.G.Discards);
        Assert.Equal(C("Ah"), t.G.UpCard);
        Assert.Equal(31, t.G.Deck.Count); // det uppvända kortet ligger kvar överst i leken tills det avgjorts
        Assert.Equal(-1, t.G.UpAsk);
        Assert.Contains("P0 kör 1 upp – E♥ vänds upp.", t.G.Notes);
    }

    [Fact]
    public void Ta_kortet_ersätter_det_markerade_och_turen_går_vidare()
    {
        var t = Start(3, "Ah 8s");
        Ok(t, t.G.UpTake(0, true, 0));
        Assert.Equal(H("Ah 3k 4k 5k 6k 7k As"), t.Hand(0)); // på det markerade kortets plats
        Assert.Equal(H("2k"), t.G.Discards);
        Assert.Equal(30, t.G.Deck.Count);
        AssertPick(t, 1);
    }

    [Fact]
    public void Tacka_nej_och_nästa_spelare_tar_kortet_då_förbrukas_dess_tur()
    {
        var t = Start(3, "Ah 8s");
        Ok(t, t.G.UpTake(0, false, 0));
        Assert.Equal("offer", t.G.UpStage);
        Assert.Equal(1, t.G.UpAsk);
        Assert.Equal(1, t.G.CurTurn);
        Assert.Equal(0, t.G.UpOwner);
        Assert.Contains("P0 tackar nej.", t.G.Notes);

        Ok(t, t.G.UpTake(1, true, 0)); // slänger 2r
        Assert.Equal(H("Ah 3r 4r 5r 6r 7r Kr"), t.Hand(1));
        Assert.Equal(H("8s 3k 4k 5k 6k 7k As"), t.Hand(0)); // nästa kort i leken
        Assert.Equal(H("2k 2r"), t.G.Discards);
        Assert.Contains(1, t.G.UpUsed);
        Assert.Contains("P1 tar kortet.", t.G.Notes);
        Assert.Contains("P0 får nästa kort ur leken.", t.G.Notes);
        AssertPick(t, 2); // plats 1 hoppas över

        Ok(t, t.G.Respond(2, false));
        AssertPlay(t, 0);
    }

    [Fact]
    public void Den_som_redan_haft_sin_tur_tillfrågas_inte()
    {
        var t = Start(3, "Ah 8s 9s");
        Ok(t, t.G.UpTake(0, true, 0));
        Ok(t, t.G.UpPick(1, 0, false)); // 2r markeras
        AssertDecide(t, 1);
        Assert.Equal(C("8s"), t.G.UpCard);
        Assert.Equal(H("2k 2r"), t.G.Discards);
        Ok(t, t.G.UpTake(1, false, 0));
        Assert.Equal(2, t.G.UpAsk);
        Ok(t, t.G.UpTake(2, false, 0)); // startspelaren (plats 0) har haft sin tur och tillfrågas inte
        Assert.Contains("Ingen tog kortet.", t.G.Notes);
        Assert.Equal(H("Ah 3k 4k 5k 6k 7k As"), t.Hand(0));
        Assert.Equal(H("9s 3r 4r 5r 6r 7r Kr"), t.Hand(1));
        Assert.Equal(H("2k 2r 8s"), t.G.Discards);
        AssertPick(t, 2); // plats 2 tackade nej men har kvar sin egen tur
    }

    [Fact]
    public void Fyra_spelare_bara_de_utan_tur_tillfrågas()
    {
        // A (0) start, B, C, D: A nej -> B, C, D. B nej -> C, D. C nej -> D. D nej -> ingen.
        var t = Start(4, "Ah Kh 8k 9k Tk Jk Qk Kk"); // uppvänt/nästa: A Ah/Kh, B 8k/9k, C Tk/Jk, D Qk/Kk
        Ok(t, t.G.UpTake(0, false, 0));
        foreach (var s in new[] { 1, 2, 3 }) { Assert.Equal(s, t.G.UpAsk); Ok(t, t.G.UpTake(s, false, 0)); }
        AssertPick(t, 1);
        Ok(t, t.G.UpPick(1, 0, false));
        Ok(t, t.G.UpTake(1, false, 0));
        foreach (var s in new[] { 2, 3 }) { Assert.Equal(s, t.G.UpAsk); Ok(t, t.G.UpTake(s, false, 0)); }
        AssertPick(t, 2);
        Ok(t, t.G.UpPick(2, 0, false));
        Ok(t, t.G.UpTake(2, false, 0));
        Assert.Equal(3, t.G.UpAsk);
        Ok(t, t.G.UpTake(3, false, 0));
        AssertPick(t, 3);
        Ok(t, t.G.UpPick(3, 0, false));
        var up = t.G.UpCard!;
        Assert.Equal(C("Qk"), up);
        Ok(t, t.G.UpTake(3, false, 0)); // ingen att fråga: inget offer-steg
        Assert.Contains("Ingen kunde ta kortet.", t.G.Notes);
        Assert.Contains(up, t.G.Discards);
        Assert.Equal(C("Kk"), t.Hand(3)[0]); // nästa kort, dolt
        AssertPlay(t, 0);
    }

    [Fact]
    public void Den_som_förbrukat_sin_tur_genom_att_ta_tillfrågas_inte()
    {
        var t = Start(4, "Ah Kh 8k 9k Tk Jk");
        Ok(t, t.G.UpTake(0, false, 0));
        Ok(t, t.G.UpTake(1, true, 0)); // B tar och förbrukar sin tur
        AssertPick(t, 2);
        Ok(t, t.G.UpPick(2, 0, false));
        Ok(t, t.G.UpTake(2, false, 0));
        Assert.Equal(3, t.G.UpAsk); // inte B
        Ok(t, t.G.UpTake(3, false, 0));
        Assert.Contains("Ingen tog kortet.", t.G.Notes);
        AssertPick(t, 3);
        Ok(t, t.G.UpPick(3, 0, false));
        Ok(t, t.G.UpTake(3, false, 0)); // B har tagit, A och C har haft sin tur
        Assert.Contains("Ingen kunde ta kortet.", t.G.Notes);
        AssertPlay(t, 0);
    }

    [Fact]
    public void Två_spelare_andra_spelaren_som_ägare_tackar_nej_utan_fråga()
    {
        var t = Start(2, "Ah 8s 9s");
        Ok(t, t.G.UpTake(0, true, 0));
        Ok(t, t.G.UpPick(1, 0, false));
        Ok(t, t.G.UpTake(1, false, 0)); // startspelaren tillfrågas aldrig
        Assert.Contains("P1 tackar nej.", t.G.Notes);
        Assert.Contains("Ingen kunde ta kortet.", t.G.Notes);
        Assert.Contains(C("8s"), t.G.Discards);
        Assert.Equal(C("9s"), t.Hand(1)[0]);
        AssertPlay(t, 0);
    }

    [Fact]
    public void Tre_spelare_sista_ägaren_tackar_nej_utan_fråga()
    {
        var t = Start(3, "Ah Kh 8k 9k");
        Ok(t, t.G.UpTake(0, true, 0));
        Ok(t, t.G.Respond(1, false));
        Ok(t, t.G.UpPick(2, 0, false));
        Ok(t, t.G.UpTake(2, false, 0)); // plats 1 behöll men har haft sin tur
        Assert.Contains("Ingen kunde ta kortet.", t.G.Notes);
        Assert.Equal(C("8k"), t.Hand(2)[0]); // Kh vändes upp och gick till slasken
        AssertPlay(t, 0);
    }

    [Fact]
    public void Tackar_alla_nej_går_kortet_till_slasken()
    {
        var t = Start(3, "Ah 8s");
        Ok(t, t.G.UpTake(0, false, 0));
        Ok(t, t.G.UpTake(1, false, 0));
        Assert.Equal(2, t.G.UpAsk);
        Ok(t, t.G.UpTake(2, false, 0));
        Assert.Equal(H("2k Ah"), t.G.Discards);
        Assert.Equal(H("8s 3k 4k 5k 6k 7k As"), t.Hand(0));
        Assert.Equal(29, t.G.Deck.Count);
        Assert.Contains("Ingen tog kortet.", t.G.Notes);
        AssertPick(t, 1);
    }

    [Fact]
    public void Nästa_kort_hamnar_på_det_markerade_kortets_plats()
    {
        var t = Table.Seated(3).Deal(Hands[..3], "Ah 8s");
        Ok(t, t.G.Exchange(0, [3])); // 5k
        AssertDecide(t, 0);
        Assert.Equal(3, Up(t.G, 0).GetProperty("mark").GetInt32());
        Ok(t, t.G.UpTake(0, false, 0));
        Ok(t, t.G.UpTake(1, true, 4)); // slänger 6r
        Assert.Equal(H("2r 3r 4r 5r Ah 7r Kr"), t.Hand(1));
        Assert.Equal(H("2k 3k 4k 8s 6k 7k As"), t.Hand(0));
        Assert.Equal(H("5k 6r"), t.G.Discards);
    }

    [Fact]
    public void Fyra_spelare_frågas_åt_vänster_hela_varvet()
    {
        var t = Start(4, "Ah 8h");
        Ok(t, t.G.UpTake(0, false, 0));
        foreach (var s in new[] { 1, 2, 3 })
        {
            Assert.Equal(s, t.G.UpAsk);
            Ok(t, t.G.UpTake(s, false, 0));
        }
        AssertPick(t, 1);
    }

    [Fact]
    public void Det_uppvända_kortet_syns_för_alla_men_markeringen_bara_för_ägaren()
    {
        var t = Start(3, "Ah 8s");
        for (int i = -1; i < 3; i++)
        {
            var up = Up(t.G, i);
            Assert.Equal(0, up.GetProperty("owner").GetInt32());
            Assert.Equal("decide", up.GetProperty("stage").GetString());
            Assert.Equal(J(C("Ah")), up.GetProperty("card").GetRawText());
            Assert.Equal(i == 0 ? 0 : -1, up.GetProperty("mark").GetInt32());
        }
    }

    [Fact]
    public void Kort_nummer_två_är_nästa_kort_och_dolt_för_andra()
    {
        var t = Start(3, "Ah 8s");
        Ok(t, t.G.UpTake(0, false, 0));
        Ok(t, t.G.UpTake(1, false, 0));
        Ok(t, t.G.UpTake(2, false, 0));
        var hidden = C("8s");
        Assert.Equal(hidden, t.Hand(0)[0]);
        Assert.Contains(J(hidden), View(t.G, 0).GetRawText());
        foreach (var i in new[] { -1, 1, 2 })
        {
            Assert.DoesNotContain(J(hidden), View(t.G, i).GetRawText());
            Assert.Equal(JsonValueKind.Null, Up(t.G, i).GetProperty("card").ValueKind);
        }
        Assert.DoesNotContain(t.G.Notes, n => n.Contains("8♠"));
    }

    [Fact]
    public void Kort_nummer_två_är_dolt_även_när_någon_tog_det_uppvända()
    {
        var t = Start(3, "Ah 8s");
        Ok(t, t.G.UpTake(0, false, 0));
        Ok(t, t.G.UpTake(1, true, 0));
        foreach (var i in new[] { -1, 1, 2 }) Assert.DoesNotContain(J(C("8s")), View(t.G, i).GetRawText());
    }

    [Fact]
    public void Övriga_kan_byta_alla_sju_eller_behålla()
    {
        var t = Start(3, "Ah 8h 9h Th Jh Kh 8s 9s");
        Ok(t, t.G.UpTake(0, true, 0));
        var old1 = t.Hand(1).ToList(); var old2 = t.Hand(2).ToList();
        Ok(t, t.G.Respond(1, true));
        Assert.Equal(H("8h 9h Th Jh Kh 8s 9s"), t.Hand(1));
        Assert.All(old1, c => Assert.Contains(c, t.G.Discards));
        AssertPick(t, 2);
        Ok(t, t.G.Respond(2, false));
        Assert.Equal(old2, t.Hand(2));
        AssertPlay(t, 0);
    }

    [Fact]
    public void Övriga_kör_1_upp_på_sin_tur()
    {
        var t = Start(3, "Ah 8s 9s");
        Ok(t, t.G.UpTake(0, true, 0));
        Ok(t, t.G.UpPick(1, 6, false)); // Kr markeras
        AssertDecide(t, 1);
        Assert.Equal(H("2r 3r 4r 5r 6r 7r"), t.Hand(1));
        Assert.Equal(C("8s"), t.G.UpCard);
        Assert.Equal(6, View(t.G, 1).GetProperty("up").GetProperty("mark").GetInt32());
        Ok(t, t.G.UpTake(1, true, 0));
        Assert.Equal(H("2r 3r 4r 5r 6r 7r 8s"), t.Hand(1));
        AssertPick(t, 2);
    }

    [Fact]
    public void Zlatan_avgörs_före_1_upp()
    {
        // plats 1 saknar kort över tio
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh"], next: "8h 9h Th Jh Kh Ah 8s 9s");
        Ok(t, t.G.Exchange(0, [0]));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Null(t.G.UpCard);
        Assert.Equal(7, t.Hand(0).Count);
        No(t, t.G.UpTake(0, true, 0));
        Ok(t, t.G.Zlatan(1, true));
        Assert.Equal(H("8h 9h Th Jh Kh Ah 8s"), t.Hand(1));
        AssertDecide(t, 0);
        Assert.Equal(C("9s"), t.G.UpCard); // vänds upp efter Zlatan-korten
    }

    [Fact]
    public void Avstår_Zlatan_vänds_översta_kortet_upp()
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh"], next: "8h 9h");
        Ok(t, t.G.Exchange(0, [0]));
        Ok(t, t.G.Zlatan(1, false));
        AssertDecide(t, 0);
        Assert.Equal(C("8h"), t.G.UpCard);
        Assert.Empty(t.G.Zl);
    }

    [Fact]
    public void Fel_spelare_och_fel_steg_avvisas()
    {
        var t = Table.Seated(3).Deal(Hands[..3], "Ah 8s");
        No(t, t.G.UpTake(0, true, 0)); // exchange
        No(t, t.G.UpPick(0, 0, false));
        Ok(t, t.G.Exchange(0, [0]));

        // decide: bara ägaren och bara UpTake
        foreach (var e in new[] { t.G.UpTake(1, true, 0), t.G.UpTake(2, false, 0), t.G.UpTake(-1, true, 0), t.G.UpPick(0, 0, false), t.G.UpPick(1, 0, false),
                                  t.G.Respond(0, false), t.G.Respond(1, false), t.G.Swap(1, [0], false), t.G.Exchange(0, [0]), t.G.Play(0, [0]), t.G.Zlatan(1, true) })
            No(t, e);
        AssertDecide(t, 0);
        Assert.Equal(C("Ah"), t.G.UpCard);

        // offer: bara den tillfrågade
        Ok(t, t.G.UpTake(0, false, 0));
        foreach (var e in new[] { t.G.UpTake(0, true, 0), t.G.UpTake(0, false, 0), t.G.UpTake(2, true, 0), t.G.UpTake(-1, true, 0), t.G.UpPick(1, 0, false), t.G.Respond(1, false) })
            No(t, e);
        Assert.Equal("offer", t.G.UpStage);
        Assert.Equal(1, t.G.UpAsk);
        Ok(t, t.G.UpTake(1, false, 0));
        Ok(t, t.G.UpTake(2, false, 0));

        // pick: bara ägaren, och inte UpTake
        foreach (var e in new[] { t.G.UpTake(1, true, 0), t.G.UpTake(1, false, 0), t.G.UpPick(2, 0, false), t.G.UpPick(0, 0, false), t.G.Respond(2, false), t.G.Respond(0, true) })
            No(t, e);
        AssertPick(t, 1);
    }

    [Fact]
    public void Utanför_1_upp_avvisas_UpPick_och_UpTake()
    {
        var t = Table.Seated(3).Deal(Hands[..3]);
        Ok(t, t.G.Exchange(0, [0, 1]));
        Assert.Equal("respond", t.G.Phase);
        No(t, t.G.UpPick(1, 0, false));
        No(t, t.G.UpTake(1, true, 0));
        var p = Table.Playing(Hands[..3]);
        No(p, p.G.UpPick(0, 0, false));
        No(p, p.G.UpTake(0, true, 0));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Ogiltiga_index_avvisas(int i)
    {
        var t = Start(3, "Ah 8s 9s");
        Ok(t, t.G.UpTake(0, true, 0));
        Assert.Equal("Ogiltigt val.", t.G.UpPick(1, i, false));
        AssertPick(t, 1);
        Ok(t, t.G.UpPick(1, 0, false));
        Ok(t, t.G.UpTake(1, false, 0));
        Assert.Equal("Ogiltigt val.", t.G.UpTake(2, true, i));
        t.AssertConserved();
        Assert.Equal("offer", t.G.UpStage);
        Assert.Equal(2, t.G.UpAsk);
        Assert.Equal(C("8s"), t.G.UpCard);
    }

    // 4 spelare, plats 1–3 kör Zlatan: 24 -> 3 kort. Startspelaren vänder upp ett och tar det (2 kvar).
    static Table LowDeck(int seed)
    {
        var t = Table.Seated(4, seed).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h 9h", "2s 3s 4s 5s 6s 7s Ts"]);
        Ok(t, t.G.Exchange(0, [0]));
        foreach (var s in new[] { 1, 2, 3 }) Ok(t, t.G.Zlatan(s, true));
        AssertDecide(t, 0);
        Assert.Equal(3, t.G.Deck.Count);
        Ok(t, t.G.UpTake(0, true, 0));
        return t;
    }

    [Fact]
    public void Gräva_i_slasken_nekas_när_leken_har_kort()
    {
        var t = LowDeck(1);
        No(t, t.G.UpPick(1, 0, true));
        AssertPick(t, 1);
    }

    [Fact]
    public void Alla_sju_nekas_när_leken_inte_räcker()
    {
        var t = LowDeck(1);
        No(t, t.G.Respond(1, true));
        AssertPick(t, 1);
    }

    [Fact]
    public void Ett_kort_kvar_och_alla_tackar_nej_ger_ett_slumpat_kort_ur_slasken() { foreach (var seed in Many) AllaNej(seed); }

    static void AllaNej(int seed)
    {
        var t = LowDeck(seed);
        Ok(t, t.G.UpPick(1, 0, false));
        Ok(t, t.G.UpTake(1, true, 0));
        Assert.Single(t.G.Deck);
        var mark = t.Hand(2)[0];
        Ok(t, t.G.UpPick(2, 0, false)); // sista kortet vänds upp
        Assert.Equal(t.G.UpCard, Assert.Single(t.G.Deck));
        var up = t.G.UpCard!;
        Ok(t, t.G.UpTake(2, false, 0));
        Assert.Equal(3, t.G.UpAsk); // plats 0 och 1 har haft sin tur
        Ok(t, t.G.UpTake(3, false, 0));
        var got = t.Hand(2)[0];
        Assert.NotEqual(mark, got);
        Assert.NotEqual(up, got);
        Assert.DoesNotContain(got, t.G.Discards);
        Assert.Contains("P2 får ett kort ur slasken.", t.G.Notes);
        AssertPick(t, 3);
    }

    [Fact]
    public void Ett_kort_kvar_och_ingen_att_fråga_ger_ett_slumpat_kort_ur_slasken() { foreach (var seed in Many) IngenAttFråga(seed); }

    static void IngenAttFråga(int seed)
    {
        var t = LowDeck(seed);
        Ok(t, t.G.UpPick(1, 0, false));
        Ok(t, t.G.UpTake(1, true, 0));
        Ok(t, t.G.Respond(2, false));
        var mark = t.Hand(3)[0];
        Ok(t, t.G.UpPick(3, 0, false)); // sista kortet vänds upp
        var up = t.G.UpCard!;
        Ok(t, t.G.UpTake(3, false, 0)); // alla har haft sin tur
        Assert.Contains("Ingen kunde ta kortet.", t.G.Notes);
        Assert.Empty(t.G.Deck);
        var got = t.Hand(3)[0];
        Assert.NotEqual(mark, got);
        Assert.NotEqual(up, got);
        Assert.Contains(up, t.G.Discards);
        Assert.Contains("P3 får ett kort ur slasken.", t.G.Notes);
        AssertPlay(t, 0);
    }

    // 4 spelare, plats 1–3 kör Zlatan (3 kvar). Plats 0 tar (2), plats 1 tar (1), plats 2 tar (0). Plats 3 har tom lek.
    static Table EmptyDeck(int seed)
    {
        var t = LowDeck(seed);
        Ok(t, t.G.UpPick(1, 0, false));
        Ok(t, t.G.UpTake(1, true, 0));
        Ok(t, t.G.UpPick(2, 0, false));
        Ok(t, t.G.UpTake(2, true, 0));
        Assert.Empty(t.G.Deck);
        AssertPick(t, 3);
        return t;
    }

    [Fact]
    public void Ett_kort_kvar_och_någon_tar_ägaren_får_inte_tagarens_slängda_kort() { foreach (var seed in Many) NågonTar(seed); }

    static void NågonTar(int seed)
    {
        var t = LowDeck(seed);
        Ok(t, t.G.UpPick(1, 0, false));
        Ok(t, t.G.UpTake(1, true, 0));
        var mark = t.Hand(2)[3];
        Ok(t, t.G.UpPick(2, 3, false)); // sista kortet vänds upp
        var up = t.G.UpCard!;
        Ok(t, t.G.UpTake(2, false, 0));
        Assert.Equal(3, t.G.UpAsk);
        var gone = t.Hand(3)[5];
        Ok(t, t.G.UpTake(3, true, 5)); // plats 3 tar och slänger gone
        Assert.Equal(up, t.Hand(3)[5]);
        Assert.Empty(t.G.Deck);
        var got = t.Hand(2)[3];
        Assert.NotEqual(gone, got); // tagaren vet annars vad ägaren fick
        Assert.NotEqual(mark, got);
        Assert.Contains(gone, t.G.Discards);
        Assert.Contains(mark, t.G.Discards);
        Assert.Contains("P2 får ett kort ur slasken.", t.G.Notes);
        AssertPlay(t, 0); // plats 3 förbrukade sin tur
    }

    [Fact]
    public void Finns_bara_undantagna_kort_i_slasken_får_ägaren_ett_av_dem()
    {
        // konstruerat läge som inte uppstår i ett riktigt parti: leken har bara det uppvända kortet och slasken bara ägarens markerade kort
        var t = Start(2, "Ah 8s");
        t.G.Deck.RemoveRange(1, t.G.Deck.Count - 1);
        Assert.Equal(H("2k"), t.G.Discards);
        Assert.Null(t.G.UpTake(0, false, 0));
        Assert.Null(t.G.UpTake(1, true, 0)); // slänger 2r
        Assert.Equal(H("Ah 3r 4r 5r 6r 7r Kr"), t.Hand(1));
        Assert.Contains(t.Hand(0)[0], H("2k 2r"));
        Assert.Single(t.G.Discards);
        Assert.Equal(7, t.Hand(0).Count);
        Assert.Empty(t.G.Deck);
        Assert.Equal("play", t.G.Phase);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Tom_lek_gräva_ger_ett_annat_slaskkort_än_det_egna(int seed)
    {
        var t = EmptyDeck(seed);
        No(t, t.G.UpPick(3, 0, false));
        No(t, t.G.Respond(3, true));
        var mark = t.Hand(3)[0]; var slask = t.G.Discards.ToList();
        Ok(t, t.G.UpPick(3, 0, true));
        var got = t.Hand(3)[0];
        Assert.NotEqual(mark, got);
        Assert.Contains(got, slask);
        Assert.DoesNotContain(got, t.G.Discards);
        Assert.Contains(mark, t.G.Discards);
        Assert.Contains("P3 gräver i slasken.", t.G.Notes);
        AssertPlay(t, 0);
    }

    [Fact]
    public void Tom_lek_behålla()
    {
        var t = EmptyDeck(1);
        var old = t.Hand(3).ToList();
        Ok(t, t.G.Respond(3, false));
        Assert.Equal(old, t.Hand(3));
        AssertPlay(t, 0);
    }

    static readonly IEnumerable<int> Many = Enumerable.Range(1, 200); // slumpundantagen träffas sällan per frö – många frön krävs för att fånga fel
    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 30).Select(s => new object[] { s });

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Alla_kör_1_upp_och_tar_kortet(int n)
    {
        var next = H("Ah Kh Qs 8h");
        var t = Start(n, Str(next));
        Ok(t, t.G.UpTake(0, true, 0));
        for (int s = 1; s < n; s++)
        {
            AssertPick(t, s);
            Ok(t, t.G.UpPick(s, 0, false));
            AssertDecide(t, s);
            Assert.Equal(next[s], t.G.UpCard);
            Ok(t, t.G.UpTake(s, true, 0));
            Assert.Equal(next[s], t.Hand(s)[0]);
        }
        Assert.Equal(next[0], t.Hand(0)[0]);
        AssertPlay(t, 0);
        Assert.Equal(52 - 7 * n - n, t.G.Deck.Count);
    }

    [Fact]
    public void Två_spelare_motspelaren_tar_kortet_och_spelet_börjar()
    {
        var t = Start(2, "Ah 8s");
        Ok(t, t.G.UpTake(0, false, 0));
        Assert.Equal(1, t.G.UpAsk);
        Ok(t, t.G.UpTake(1, true, 6)); // slänger Kr
        Assert.Equal(H("2r 3r 4r 5r 6r 7r Ah"), t.Hand(1));
        Assert.Equal(H("8s 3k 4k 5k 6k 7k As"), t.Hand(0));
        AssertPlay(t, 0);
    }

    [Fact]
    public void Två_spelare_alla_tackar_nej()
    {
        var t = Start(2, "Ah 8s");
        Ok(t, t.G.UpTake(0, false, 0));
        Ok(t, t.G.UpTake(1, false, 0));
        Assert.Equal(H("2k Ah"), t.G.Discards);
        AssertPick(t, 1);
        Ok(t, t.G.Respond(1, false));
        AssertPlay(t, 0);
    }

    [Fact]
    public void Startspelare_på_annan_plats_turordningen_går_runt_och_spelet_börjar_hos_startspelaren()
    {
        // plats 1 har esset och börjar
        var t = Table.Seated(3).Deal(["2r 3r 4r 5r 6r 7r Kr", "2k 3k 4k 5k 6k 7k As", "2h 3h 4h 5h 6h 7h Qh"], next: "Ah 8s 9s");
        Assert.Equal(1, t.G.Starter);
        Ok(t, t.G.Exchange(1, [0]));
        AssertDecide(t, 1);
        Ok(t, t.G.UpTake(1, true, 0));
        AssertPick(t, 2);
        Ok(t, t.G.UpPick(2, 0, false));
        Ok(t, t.G.UpTake(2, false, 0));
        Assert.Equal(0, t.G.UpAsk);
        Ok(t, t.G.UpTake(0, false, 0)); // startspelaren (plats 1) tillfrågas aldrig
        Assert.Contains("Ingen tog kortet.", t.G.Notes);
        AssertPick(t, 0);
        Ok(t, t.G.Respond(0, false));
        AssertPlay(t, 1);
    }

    [Fact]
    public void Byte_av_två_kort_är_oförändrat()
    {
        var t = Table.Seated(3).Deal(Hands[..3], next: "8k 9k");
        Ok(t, t.G.Exchange(0, [0, 1]));
        Assert.Equal("respond", t.G.Phase);
        Assert.True(t.G.RespFree);
        Assert.Equal(H("8k 9k 4k 5k 6k 7k As"), t.Hand(0));
        Assert.Null(t.G.UpCard);
        for (int i = -1; i < 3; i++) Assert.Equal(JsonValueKind.Null, Up(t.G, i).ValueKind);
        Ok(t, t.G.Swap(1, [0, 1], false));
        Ok(t, t.G.Swap(2, null, false));
        AssertPlay(t, 0);
    }

    [Fact]
    public void Inget_byte_är_oförändrat()
    {
        var t = Table.Seated(3).Deal(Hands[..3]);
        Ok(t, t.G.Exchange(0, null));
        Assert.Equal("respond", t.G.Phase);
        Assert.False(t.G.RespFree);
        Assert.Null(t.G.UpCard);
    }

    [Fact]
    public void Lämna_mitt_i_1_upp_rensar_det_uppvända_kortet()
    {
        var t = Start(3, "Ah 8s");
        Ok(t, t.G.UpTake(0, false, 0));
        t.G.Leave(2);
        Assert.Equal("lobby", t.G.Phase);
        Assert.Null(t.G.UpCard);
        Assert.Empty(t.G.UpUsed);
        Assert.Equal(JsonValueKind.Null, Up(t.G, 0).ValueKind);
    }
}
