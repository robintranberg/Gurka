namespace Gurka.Tests.Rules;

// Slutet: när sista sticket är lagt räknas varje spelares summa av korten den la i sista sticket (även sakade).
// Högst summa får gurkan (lika => sist lagd), lägst summa vinner. Vinnaren får gurkans summa * 5, gurkan samma som minus.
// Med ett kort är summan kortets valör. Delar flera spelare den lägsta summan är det oavgjort: ingen vinnare, ingen gurka, inga poäng.
// Fler fall med summor finns i SumTests.
public class EndTests
{
    public static readonly string[] Simple = ["3k 3r 3h 3s Qk Qr As", "4k 4r 4h 4s 2k 2r Kr"];

    // spelar Simple till slut: sista sticket blir As (plats 0) mot sakat Kr (plats 1)
    internal static void PlayToEnd(Table t)
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
    public void Två_spelare_går_ut_på_samma_valör_oavgjort_utan_poäng()
    {
        var t = Table.Playing("4k 4r 4h 4s 2k Ah 2r", "3k 3r 3h 3s Qk As Qr"); // plats 1 börjar (visar Qr mot 2r; essen ligger inte sjunde så att avgörande vid lika undviks)
        Assert.Equal(1, t.G.Turn);
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(0, "4k 4r 4h 4s");
        t.Ok(0, "2k 2r");
        t.Ok(1, "Qk Qr");
        t.Ok(1, "As");
        t.Ok(0, "Ah");
        Assert.Equal("over", t.G.Phase);
        Assert.Equal(-1, t.G.Gurka);
        Assert.Equal(-1, t.G.Winner);
        Assert.Equal(0, t.G.Points);
        Assert.Equal(0, t.G.Seats[0]!.Score);
        Assert.Equal(0, t.G.Seats[1]!.Score);
    }

    // tre spelare där plats 0 och 2 delar lägsta summan (9+9 = 18) i sista sticket; plats 1 har 6+K = 19
    static Table ThreeWayDraw()
    {
        var t = Table.Playing("2k 2r 2h 2s 9k 9r As", "3k 3r 3h 3s 6r Kr 5r", "4k 4r 4h 4s 9h 9s 5k");
        t.Ok(0, "2k 2r 2h 2s");
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(2, "4k 4r 4h 4s");
        t.Ok(2, "5k");
        t.Ok(0, "As");
        t.Ok(1, "5r");
        t.Ok(0, "9k 9r");
        t.Ok(1, "6r Kr"); // sakar sexan, kungen slår nian
        t.Ok(2, "9h 9s"); // lika slår
        return t;
    }

    [Fact]
    public void Tre_spelare_två_delar_lägsta_summan_oavgjort_även_om_tredje_har_högre()
    {
        var t = ThreeWayDraw();
        Assert.Equal("over", t.G.Phase);
        Assert.Equal(-1, t.G.Gurka);
        Assert.Equal(-1, t.G.Winner);
        Assert.Equal(0, t.G.Points);
        Assert.All(t.G.Seats.Where(p => p != null), p => Assert.Equal(0, p!.Score));
        Assert.Equal(-1, t.G.CurTurn);
        t.AssertConserved();
    }

    [Fact]
    public void Oavgjort_ändrar_inte_tidigare_poäng()
    {
        var t = Table.Playing(Simple);
        PlayToEnd(t); // plats 0: -70, plats 1: +70
        t.Deal(["4k 4r 4h 4s 2k Ah 2r", "3k 3r 3h 3s Qk As Qr"]); // samma händer som i oavgjort-testet ovan
        Assert.Null(t.G.Exchange(t.G.Starter, null));
        while (t.G.Phase == "respond") Assert.Null(t.G.Respond(t.G.Resp.Peek(), false));
        t.Ok(1, "3k 3r 3h 3s");
        t.Ok(0, "4k 4r 4h 4s");
        t.Ok(0, "2k 2r");
        t.Ok(1, "Qk Qr");
        t.Ok(1, "As");
        t.Ok(0, "Ah");
        Assert.Equal(-1, t.G.Winner);
        Assert.Equal(-70, t.G.Seats[0]!.Score);
        Assert.Equal(70, t.G.Seats[1]!.Score);
    }

    [Fact]
    public void Avhopp_efter_oavgjort_lämnar_bordet_i_slutläget()
    {
        var t = ThreeWayDraw();
        t.G.Leave(1);
        Assert.Null(t.G.Seats[1]);
        Assert.Equal("over", t.G.Phase); // ingen vinnare eller gurka pekar på den tomma platsen
        Assert.Null(t.G.Start(0));       // går att spela igen
        Assert.Equal("exchange", t.G.Phase);
    }

    [Fact]
    public void Gurkan_avgörs_av_summan_och_spelaren_däremellan_får_inga_poäng()
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
        // summor: plats 0 9+9 = 18, plats 1 6+K = 19 (kungen är högsta kortet), plats 2 T+T = 20
        Assert.Equal(2, t.G.Gurka);
        Assert.Equal(0, t.G.Winner);
        Assert.Equal(20 * 5, t.G.Points);
        Assert.Equal(100, t.G.Seats[0]!.Score);
        Assert.Equal(0, t.G.Seats[1]!.Score);
        Assert.Equal(-100, t.G.Seats[2]!.Score);
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
        Assert.Null(t.G.Start(0)); // bara värden (plats 0) startar en ny giv
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
