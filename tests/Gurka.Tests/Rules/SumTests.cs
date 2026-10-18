namespace Gurka.Tests.Rules;

// Summaregeln i sista sticket: varje spelares summa av korten den la (även sakade, Kn 11, D 12, K 13, E 14).
// Högst summa = gurka (lika => sist lagd), lägst summa = vinnare (lika => oavgjort, 0 poäng). Poäng = gurkans summa * 5.
public class SumTests
{
    static void AssertResult(Table t, int gurka, int winner, int points)
    {
        Assert.Equal("over", t.G.Phase);
        Assert.Equal(gurka, t.G.Gurka);
        Assert.Equal(winner, t.G.Winner);
        Assert.Equal(points, t.G.Points);
        foreach (var s in t.G.Active)
            Assert.Equal(s == winner ? points : s == gurka ? -points : 0, t.G.Seats[s]!.Score);
        t.AssertConserved();
    }

    [Fact]
    public void Användarens_exempel_tre_treor_mot_tre_fem_sju()
    {
        // plats 0 visar spaderkung och börjar; plats 1 kan inte slå kungarna och sakar tvåorna
        var t = Table.Playing("3k 3r 3h Kk Kr Kh Ks", "2k 2r 2h 2s 3s 5k 7k");
        t.Ok(0, "Kk Kr Kh Ks");
        t.Ok(1, "2k 2r 2h 2s");
        t.Ok(0, "3k 3r 3h");
        t.Ok(1, "3s 5k 7k");
        AssertResult(t, gurka: 1, winner: 0, points: 75); // 3+5+7 = 15 mot 3+3+3 = 9
    }

    [Fact]
    public void Summan_avgör_inte_högsta_kortet()
    {
        // plats 0 visar kung och börjar med fyra treor; plats 1 slår med fyrorna och spelar ut nior
        var t = Table.Playing("3k 3r 3h 3s 2k 2r Kh", "4k 4r 4h 4s 9k 9r 9h");
        t.Ok(0, "3k 3r 3h 3s");
        t.Ok(1, "4k 4r 4h 4s");
        t.Ok(1, "9k 9r 9h");
        t.Ok(0, "2k 2r Kh"); // sakar tvåorna, kungen slår
        Assert.True(t.G.Trick[1].Sak);
        AssertResult(t, gurka: 1, winner: 0, points: 135); // 9+9+9 = 27 mot 2+2+K = 17
    }

    [Fact]
    public void Sakade_kort_räknas_med_i_summan()
    {
        // plats 1 visar kung och börjar
        var t = Table.Playing("4k 4r 4h 4s 6k 9k Qk", "3k 3r 3h 3s 7k 7r Kr");
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(0, "4k 4r 4h 4s");
        t.Ok(0, "Qk");
        t.Ok(1, "Kr");
        t.Ok(1, "7k 7r");
        t.Ok(0, "6k 9k"); // sakar sexan, nian slår
        Assert.True(t.G.Trick[1].Sak);
        AssertResult(t, gurka: 0, winner: 1, points: 75); // 6+9 = 15 mot 7+7 = 14; utan sexan hade plats 1 fått gurkan
    }

    [Fact]
    public void Fyra_spelare_högst_summa_är_gurka_och_lägst_vinner()
    {
        // plats 2 visar ess och börjar; plats 3 tar sticket med femmorna och spelar ut sexor
        var t = Table.Playing("2k 2r 2h 2s 7k 8k 9k", "3k 3r 3h 3s 6s Tk Jk", "4k 4r 4h 4s Qk Kk Ak", "5k 5r 5h 5s 6k 6r 6h");
        t.Ok(2, "4k 4r 4h 4s");
        t.Ok(3, "5k 5r 5h 5s");
        t.Ok(0, "2k 2r 2h 2s");
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(3, "6k 6r 6h");
        t.Ok(0, "7k 8k 9k");
        t.Ok(1, "6s Tk Jk");
        t.Ok(2, "Qk Kk Ak");
        AssertResult(t, gurka: 2, winner: 3, points: 195); // 18, 24, 27, 39
    }

    [Fact]
    public void Lika_lägsta_summa_är_oavgjort_utan_poäng()
    {
        var t = Table.Playing("5k 5r 5h 5s 2k 6k Qk", "3k 3r 3h 3s 4k 4r Kr");
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(0, "5k 5r 5h 5s");
        t.Ok(0, "Qk");
        t.Ok(1, "Kr");
        t.Ok(1, "4k 4r");
        t.Ok(0, "2k 6k"); // sakar tvåan, sexan slår: 2+6 = 8 mot 4+4 = 8
        AssertResult(t, gurka: -1, winner: -1, points: 0);
    }

    [Fact]
    public void Lika_högsta_summa_ger_gurkan_till_den_som_spelade_sist()
    {
        // plats 0 visar ess och börjar; plats 2 tar sticket och spelar ut femmor. Plats 0 och 1 får båda 39, plats 1 spelar sist.
        var t = Table.Playing("2k 2r 2h 2s Qk Kk Ak", "3k 3r 3h 3s Kr Kh Ks", "4k 4r 4h 4s 5k 5r 5h");
        t.Ok(0, "2k 2r 2h 2s");
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(2, "4k 4r 4h 4s");
        t.Ok(2, "5k 5r 5h");
        t.Ok(0, "Qk Kk Ak"); // 39 med esset som högsta kort
        t.Ok(1, "Kr Kh Ks"); // 39 utan ess
        AssertResult(t, gurka: 1, winner: 2, points: 195);
    }
}
