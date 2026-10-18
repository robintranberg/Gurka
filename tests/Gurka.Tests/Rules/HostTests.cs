using System.Text.Json;

namespace Gurka.Tests.Rules;

// Värd: den som skapar bordet (eller först sätter sig) är värd. Bara värden startar spelet och varje ny giv.
// Lämnar värden går värdskapet till nästa sittande spelare i platsordning (cykliskt).
public class HostTests
{
    static int HostInView(Game g, int you) => JsonSerializer.SerializeToElement(g.View(you)).GetProperty("host").GetInt32();

    [Fact]
    public void Första_spelaren_som_sätter_sig_blir_värd()
    {
        var g = new Game();
        Assert.Equal(-1, g.Host);
        Assert.Null(g.Join("a", "A"));
        Assert.Null(g.Join("b", "B"));
        Assert.Equal(0, g.Host);
        for (int i = -1; i < 2; i++) Assert.Equal(0, HostInView(g, i));
    }

    [Fact]
    public void Skaparen_blir_värd_även_om_någon_satte_sig_före()
    {
        var g = new Game();
        Assert.Null(g.Join("a", "A"));
        Assert.Null(g.Join("b", "B", host: true));
        Assert.Equal(1, g.Host);
    }

    [Fact]
    public void Join_som_redan_sitter_ändrar_inte_värdskapet()
    {
        var g = new Game();
        Assert.Null(g.Join("a", "A"));
        Assert.Null(g.Join("b", "B"));
        Assert.Null(g.Join("b", "B", host: true)); // sitter redan: ingen ny plats tas
        Assert.Equal(0, g.Host);
    }

    [Fact]
    public void Bara_värden_kan_starta()
    {
        var t = Table.Seated(3);
        Assert.Equal("Bara värden kan starta spelet.", t.G.Start(1));
        Assert.Equal("Bara värden kan starta spelet.", t.G.Start(2));
        Assert.Equal("lobby", t.G.Phase);
        Assert.Null(t.G.Start(0));
        Assert.Equal("exchange", t.G.Phase);
    }

    [Fact]
    public void Felmeddelandenas_ordning()
    {
        var g = new Game();
        Assert.Equal("Kan inte starta nu.", g.Start(-1)); // ingen plats
        Assert.Null(g.Join("a", "A"));
        Assert.Equal("Minst 2 spelare krävs.", g.Start(0));
        Assert.Null(g.Join("b", "B"));
        Assert.Equal("Bara värden kan starta spelet.", g.Start(1));
        Assert.Equal("Kan inte starta nu.", g.Start(3)); // tom plats
        Assert.Null(g.Start(0));
        Assert.Equal("Kan inte starta nu.", g.Start(0)); // fel fas
        Assert.Equal("Kan inte starta nu.", g.Start(1));
    }

    [Fact]
    public void Bara_värden_kan_starta_en_ny_giv()
    {
        var t = Table.Playing(EndTests.Simple);
        EndTests.PlayToEnd(t);
        Assert.Equal("over", t.G.Phase);
        Assert.Equal("Bara värden kan starta spelet.", t.G.Start(1));
        Assert.Equal("over", t.G.Phase);
        Assert.Null(t.G.Start(0));
        Assert.Equal("exchange", t.G.Phase);
    }

    [Theory]
    [InlineData(4, 0, 1)]
    [InlineData(4, 3, 0)] // cykliskt
    [InlineData(4, 1, 2)] // nästa efter värden, inte lägsta platsen (0)
    [InlineData(3, 2, 0)]
    public void Värden_lämnar_och_nästa_i_platsordning_tar_över(int n, int host, int next)
    {
        var g = new Game();
        for (int i = 0; i < n; i++) Assert.Null(g.Join("p" + i, "P" + i, host: i == host));
        Assert.Equal(host, g.Host);
        g.Leave(host);
        Assert.Equal(next, g.Host);
        Assert.Equal(next, HostInView(g, -1));
        Assert.Equal("Bara värden kan starta spelet.", g.Start(g.Active.First(s => s != next)));
        Assert.Null(g.Start(next));
    }

    [Fact]
    public void Tom_plats_hoppas_över_när_värdskapet_går_vidare()
    {
        var g = new Game();
        for (int i = 0; i < 4; i++) Assert.Null(g.Join("p" + i, "P" + i));
        g.Leave(1);
        Assert.Equal(0, g.Host);
        g.Leave(0);
        Assert.Equal(2, g.Host); // plats 1 är tom
        Assert.Equal("Bara värden kan starta spelet.", g.Start(3));
        Assert.Null(g.Start(2));
    }

    [Fact]
    public void Annan_spelare_lämnar_och_värden_står_kvar()
    {
        var g = new Game();
        for (int i = 0; i < 3; i++) Assert.Null(g.Join("p" + i, "P" + i));
        g.Leave(2);
        Assert.Equal(0, g.Host);
    }

    [Fact]
    public void Sista_spelaren_lämnar_och_ingen_är_värd()
    {
        var g = new Game();
        Assert.Null(g.Join("a", "A"));
        g.Leave(0);
        Assert.Equal(-1, g.Host);
        Assert.Null(g.Join("b", "B")); // nästa som sätter sig blir värd
        Assert.Equal(0, g.Host);
    }

    // FixHost (utöver kontraktet, se rapporten): en frånkopplad värd inom fristen lämnar värdskapet till nästa anslutna spelare
    [Fact]
    public void Frånkopplad_värd_lämnar_värdskapet_till_nästa_anslutna()
    {
        var g = new Game();
        for (int i = 0; i < 3; i++) Assert.Null(g.Join("p" + i, "P" + i));
        g.Seats[1]!.Gone = DateTime.UtcNow; // plats 1 är också frånkopplad och hoppas över
        g.Seats[0]!.Gone = DateTime.UtcNow;
        g.FixHost();
        Assert.Equal(2, g.Host);
        g.Seats[0]!.Gone = null; // tillbaka: får inte värdskapet igen
        g.FixHost();
        Assert.Equal(2, g.Host);
    }

    [Fact]
    public void Frånkopplad_värd_utan_anslutna_spelare_behåller_värdskapet()
    {
        var g = new Game();
        for (int i = 0; i < 2; i++) Assert.Null(g.Join("p" + i, "P" + i));
        g.Seats[1]!.Gone = DateTime.UtcNow;
        g.Seats[0]!.Gone = DateTime.UtcNow;
        g.FixHost();
        Assert.Equal(0, g.Host);
    }

    [Fact]
    public void Värden_lämnar_mitt_i_spelet_och_den_nya_värden_kan_starta_igen()
    {
        var t = Table.Seated(3).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r Kr", "2h 3h 4h 5h 6h 7h Qh"]);
        t.G.Leave(0);
        Assert.Equal("lobby", t.G.Phase);
        Assert.Equal(1, t.G.Host);
        Assert.Equal("Bara värden kan starta spelet.", t.G.Start(2));
        Assert.Null(t.G.Start(1));
    }
}
