namespace Gurka.Tests.Rules;

// Slutet: när sista sticket är lagt får den med högst kort i sista sticket gurkan (lika => sist lagd),
// lägst kort vinner (lika => först lagd). Vinnaren får högsta kortet * 5, gurkan samma som minus.
public class EndTests
{
    public static readonly string[] Simple = ["3k 3r 3h 3s Qk Qr As", "4k 4r 4h 4s 2k 2r Kr"];

    // spelar Simple till slut: sista sticket blir As (plats 0) mot sakat Kr (plats 1)
    public static void PlayToEnd(Table t)
    {
        t.Ok(0, "3k 3r 3h 3s");
        t.Ok(1, "4k 4r 4h 4s");
        t.Ok(1, "2k 2r");
        t.Ok(0, "Qk Qr");
        t.Ok(0, "As");
        Assert.Equal("play", t.G.Phase);
        t.Ok(1, "Kr");
    }

    [Fact]
    public void Högst_kort_i_sista_sticket_får_gurkan_och_lägst_vinner()
    {
        var t = Table.Playing(Simple);
        PlayToEnd(t);
        Assert.Equal("over", t.G.Phase);
        Assert.True(t.G.Trick[1].Sak);
        Assert.Equal(0, t.G.Gurka);
        Assert.Equal(1, t.G.Winner);
        Assert.Equal(14 * 5, t.G.Points);
        Assert.Equal(-70, t.G.Seats[0]!.Score);
        Assert.Equal(70, t.G.Seats[1]!.Score);
        Assert.All(t.G.Seats.Where(p => p != null), p => Assert.Empty(p!.Hand));
        Assert.Equal(-1, t.G.CurTurn);
        Assert.Equal(2, t.G.Past.Count);
        t.AssertConserved();
    }

    [Fact]
    public void Lika_högsta_kort_gurkan_till_den_som_lade_sist_och_vinsten_till_den_som_lade_först()
    {
        var t = Table.Playing("4k 4r 4h 4s 2k 2r Ah", "3k 3r 3h 3s Qk Qr As"); // plats 1 börjar
        Assert.Equal(1, t.G.Turn);
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(0, "4k 4r 4h 4s");
        t.Ok(0, "2k 2r");
        t.Ok(1, "Qk Qr");
        t.Ok(1, "As");
        t.Ok(0, "Ah");
        Assert.Equal("over", t.G.Phase);
        Assert.Equal(0, t.G.Gurka);  // lade sist
        Assert.Equal(1, t.G.Winner); // lade först
        Assert.Equal(70, t.G.Points);
        Assert.Equal(-70, t.G.Seats[0]!.Score);
        Assert.Equal(70, t.G.Seats[1]!.Score);
    }

    [Fact]
    public void Gurkan_avgörs_av_högsta_kortet_även_om_det_sakades_och_tredje_spelaren_får_inga_poäng()
    {
        var t = Table.Playing("2k 2r 2h 2s 9k 9r As", "3k 3r 3h 3s 6r Kr 5r", "4k 4r 4h 4s Th Ts 5k");
        t.Ok(0, "2k 2r 2h 2s");
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(2, "4k 4r 4h 4s");
        Assert.Equal(2, t.G.Turn);
        t.Ok(2, "5k");
        t.Ok(0, "As");
        t.Ok(1, "5r"); // kan inte slå esset: sakar lägsta
        Assert.Equal(0, t.G.Turn);
        t.Ok(0, "9k 9r");
        t.Ok(1, "6r Kr"); // sakar sexan, kungen slår nian
        Assert.True(t.G.Trick[1].Sak);
        t.Ok(2, "Th Ts");
        Assert.Equal("over", t.G.Phase);
        Assert.Equal(2, t.G.Top);    // tian tog sticket ...
        Assert.Equal(1, t.G.Gurka);  // ... men kungen är högst
        Assert.Equal(0, t.G.Winner); // nian är lägst
        Assert.Equal(13 * 5, t.G.Points);
        Assert.Equal(65, t.G.Seats[0]!.Score);
        Assert.Equal(-65, t.G.Seats[1]!.Score);
        Assert.Equal(0, t.G.Seats[2]!.Score);
    }

    [Fact]
    public void Inga_drag_efter_slutet()
    {
        var t = Table.Playing(Simple);
        PlayToEnd(t);
        Assert.Equal("Inte din tur.", t.G.Play(0, [0]));
        Assert.Equal("Inte din tur.", t.G.Exchange(0, null));
        Assert.Equal("Inte din tur.", t.G.Respond(1, false));
        Assert.Equal("Inte din tur.", t.G.Swap(1, null, false));
        Assert.Equal("Du kan inte köra Zlatan nu.", t.G.Zlatan(1, true));
    }

    [Fact]
    public void Poängen_summeras_över_flera_rundor()
    {
        var t = Table.Playing(Simple);
        PlayToEnd(t);
        Assert.Null(t.G.Start(1));
        Assert.Null(t.G.Exchange(0, null));
        Assert.Null(t.G.Respond(1, false));
        PlayToEnd(t);
        Assert.Equal(-140, t.G.Seats[0]!.Score);
        Assert.Equal(140, t.G.Seats[1]!.Score);
    }

    [Fact]
    public void Ny_spelare_får_sätta_sig_efter_slutet()
    {
        var t = Table.Playing(Simple);
        PlayToEnd(t);
        Assert.Null(t.G.Join("p2", "Ny"));
        Assert.Equal(2, t.G.SeatOf("p2"));
        Assert.Equal("over", t.G.Phase);
    }
}
