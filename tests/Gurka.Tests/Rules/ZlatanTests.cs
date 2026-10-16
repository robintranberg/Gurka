using static Gurka.Tests.Cards;

namespace Gurka.Tests.Rules;

// Zlatan: en spelare utan kort över tio får byta hela handen mot sju nya innan startspelarens byte genomförs.
public class ZlatanTests
{
    // plats 0 börjar, plats 1 är Zlatan-kandidat, plats 2 har en dam
    static readonly string[] Three = ["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh"];

    [Fact]
    public void Byte_med_kandidat_ger_Zlatan_fas_och_startspelarens_byte_väntar()
    {
        var t = Table.Seated(3).Deal(Three);
        var before = t.Hand(0).ToList();
        Assert.Null(t.G.Exchange(0, [0, 1]));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Equal(before, t.Hand(0));
        Assert.Equal(31, t.G.Deck.Count);
        Assert.Equal(2, t.G.ExN);
        Assert.Equal(-1, t.G.CurTurn);
    }

    [Fact]
    public void Inget_byte_hoppar_över_Zlatan()
    {
        var t = Table.Seated(3).Deal(Three);
        Assert.Null(t.G.Exchange(0, null));
        Assert.Equal("respond", t.G.Phase);
        Assert.Equal("Du kan inte köra Zlatan nu.", t.G.Zlatan(1, true));
    }

    [Fact]
    public void Zlatan_drar_sju_kort_före_startspelarens_byte()
    {
        var t = Table.Seated(3).Deal(Three, next: "8h 9h Th Jh Kh Ah 8s 9s Ts");
        Assert.Null(t.G.Exchange(0, [0, 1]));
        var old = t.Hand(1).ToList();
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Equal(H("8h 9h Th Jh Kh Ah 8s"), t.Hand(1));
        Assert.Equal(H("9s Ts 4k 5k 6k 7k As"), t.Hand(0)); // startspelaren får korten efter Zlatan
        Assert.Equal(old.Concat(H("2k 3k")), t.G.Discards);
        Assert.Equal("respond", t.G.Phase);
        Assert.True(t.G.RespFree);
        Assert.Equal(new[] { 1, 2 }, t.G.Resp.ToArray());
        Assert.Contains(t.G.Notes, n => n.Contains("körde Zlatan"));
        t.AssertConserved();
    }

    [Fact]
    public void Avstå_Zlatan_behåller_handen()
    {
        var t = Table.Seated(3).Deal(Three, next: "8h 9h");
        Assert.Null(t.G.Exchange(0, [0, 1]));
        var old = t.Hand(1).ToList();
        Assert.Null(t.G.Zlatan(1, false));
        Assert.Equal(old, t.Hand(1));
        Assert.Equal(H("8h 9h 4k 5k 6k 7k As"), t.Hand(0));
        Assert.Equal("respond", t.G.Phase);
        Assert.DoesNotContain(t.G.Notes, n => n.Contains("Zlatan"));
    }

    [Fact]
    public void Bara_kandidater_får_välja_och_bara_en_gång()
    {
        var t = Table.Seated(4).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh", "2s 3s 4s 5s 6s 7s 8s"]);
        Assert.Null(t.G.Exchange(0, [0]));
        Assert.Equal("Du kan inte köra Zlatan nu.", t.G.Zlatan(0, true)); // startspelaren
        Assert.Equal("Du kan inte köra Zlatan nu.", t.G.Zlatan(2, true)); // har dam
        Assert.Equal("Du kan inte köra Zlatan nu.", t.G.Zlatan(-1, true));
        Assert.Null(t.G.Zlatan(3, false)); // valfri ordning
        Assert.Equal("Du kan inte köra Zlatan nu.", t.G.Zlatan(3, true));
        Assert.Equal("zlatan", t.G.Phase); // väntar på plats 1
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Equal("respond", t.G.Phase);
    }

    [Fact]
    public void Zlatan_nekas_när_leken_inte_räcker_för_både_Zlatan_och_startbytet()
    {
        // 4 spelare: 24 kort kvar. Startspelaren byter 7. Två Zlatan ryms (24-7=17, 17-7=10), den tredje inte (10-7=3 < 7).
        var t = Table.Seated(4).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h 8h", "2s 3s 4s 5s 6s 7s 8s"]);
        Assert.Null(t.G.Exchange(0, [0, 1, 2, 3, 4, 5, 6]));
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Null(t.G.Zlatan(2, true));
        Assert.Equal(10, t.G.Deck.Count);
        Assert.Equal("Leken räcker inte för 7 nya kort.", t.G.Zlatan(3, true));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Null(t.G.Zlatan(3, false));
        Assert.Equal("respond", t.G.Phase);
        Assert.Equal(3, t.G.Deck.Count);
        t.AssertConserved();
    }

    [Fact]
    public void Zlatan_spelaren_svarar_sedan_som_vanligt()
    {
        var t = Table.Seated(3).Deal(Three, next: "8h 9h Th Jh Kh Ah 8s 9s Ts");
        Assert.Null(t.G.Exchange(0, [0, 1]));
        Assert.Null(t.G.Zlatan(1, true));
        Assert.Null(t.Swap(1, "8h 9h"));
        Assert.Null(t.Swap(2, ""));
        Assert.Equal("play", t.G.Phase);
        Assert.Equal(0, t.G.Turn);
    }
}
