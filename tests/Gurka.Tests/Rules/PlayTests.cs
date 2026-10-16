namespace Gurka.Tests.Rules;

// Spelet: utspel med 1–4 lika kort, slå kort för kort, saka de lägsta, vem som tar sticket.
public class PlayTests
{
    static readonly string[] Fives = ["5k 5r 5h 5s 9k 2k As", "6k 6r 6h 6s Tk 3r Kr"];

    [Theory]
    [InlineData("5k")]
    [InlineData("5k 5r")]
    [InlineData("5k 5r 5h")]
    [InlineData("5k 5r 5h 5s")]
    public void Utspel_med_ett_till_fyra_lika_kort(string cards)
    {
        var t = Table.Playing(Fives);
        int n = cards.Split(' ').Length;
        t.Ok(0, cards);
        Assert.Equal(n, t.G.Count);
        Assert.Equal(0, t.G.Top);
        Assert.Equal(Enumerable.Repeat(5, n), t.G.TopRs);
        Assert.Equal(1, t.G.Turn);
        Assert.Equal(7 - n, t.Hand(0).Count);
        var p = Assert.Single(t.G.Trick);
        Assert.Equal(0, p.Seat); Assert.False(p.Sak);
        Assert.Equal(Cards.H(cards), p.Cards);
    }

    [Theory]
    [InlineData("5k 9k")]
    [InlineData("5k 5r 2k")]
    [InlineData("As 5k")]
    public void Utspel_med_olika_valörer_avvisas(string cards)
    {
        var t = Table.Playing(Fives);
        Assert.Equal("Spela ut ett kort eller flera lika.", t.Play(0, cards));
        Assert.Empty(t.G.Trick);
        Assert.Equal(7, t.Hand(0).Count);
    }

    [Theory]
    [InlineData("6k")]
    [InlineData("6k 6r 6h")]
    public void Man_måste_lägga_lika_många_kort_som_utspelet(string cards)
    {
        var t = Table.Playing(Fives);
        t.Ok(0, "5k 5r");
        Assert.Equal("Du måste lägga 2 kort.", t.Play(1, cards));
        Assert.Equal(1, t.G.Turn);
    }

    [Fact]
    public void Högre_kort_slår_och_tar_sticket()
    {
        var t = Table.Playing(Fives);
        t.Ok(0, "5k 5r");
        t.Ok(1, "6k 6r");
        Assert.Equal(1, t.G.Top);
        Assert.True(t.G.Done);
        Assert.Equal(1, t.G.Turn);
        Assert.False(t.G.Trick[1].Sak);
    }

    [Fact]
    public void Lika_valör_slår()
    {
        var t = Table.Playing("9k 2k 3k 4k 5k 6k As", "9r 2r 3r 4r 5r 6r Kr");
        t.Ok(0, "9k");
        t.Ok(1, "9r");
        Assert.Equal(1, t.G.Top);
        Assert.False(t.G.Trick[1].Sak);
        Assert.Equal(1, t.G.Turn);
    }

    static readonly string[] ThreeCbc = ["5k 5r 2k 3k 4k 6k As", "6r Tk 3r 4r 2r 7k Kr", "7h Jh 7s 2h 3h 4h Ks"];

    [Fact]
    public void Slå_kort_för_kort_lägst_mot_lägst()
    {
        var t = Table.Playing(ThreeCbc);
        t.Ok(0, "5k 5r");
        t.Ok(1, "Tk 6r"); // ordningen spelaren väljer i spelar ingen roll
        Assert.Equal(1, t.G.Top);
        Assert.Equal(new[] { 6, 10 }, t.G.TopRs);
        // 7 slår 6 men 7 slår inte 10
        Assert.Equal("Slå kort för kort med lika/högre kort, eller saka ditt lägsta.", t.Play(2, "7h 7s"));
        t.Ok(2, "7h Jh");
        Assert.Equal(2, t.G.Top);
        Assert.Equal(new[] { 7, 11 }, t.G.TopRs);
        Assert.True(t.G.Done);
        Assert.Equal(2, t.G.Turn);
    }

    [Fact]
    public void Slå_ett_och_saka_det_lägsta()
    {
        var t = Table.Playing(ThreeCbc);
        t.Ok(0, "5k 5r");
        t.Ok(1, "6r Tk");
        t.Ok(2, "2h Ks"); // 2h är lägsta kortet, Ks slår mot det lägsta kortet i sticket
        Assert.True(t.G.Trick[2].Sak);
        Assert.Equal(1, t.G.Top);
        Assert.Equal(1, t.G.Turn);
    }

    static readonly string[] HighLead = ["Qk 2k 3k 4k 5k 6k As", "2r 3r 4r 5r 6r 7r Jr"];

    [Fact]
    public void Kan_man_inte_slå_måste_man_saka_sitt_lägsta()
    {
        var t = Table.Playing(HighLead);
        t.Ok(0, "Qk");
        Assert.Equal("Du kan inte slå – du måste saka ditt lägsta kort.", t.Play(1, "3r"));
        Assert.Equal("Du kan inte slå – du måste saka ditt lägsta kort.", t.Play(1, "Jr"));
        t.Ok(1, "2r");
        Assert.True(t.G.Trick[1].Sak);
        Assert.Equal(0, t.G.Top);
        Assert.Equal(new[] { 12 }, t.G.TopRs);
        Assert.True(t.G.Done);
        Assert.Equal(0, t.G.Turn); // den som låg högst spelar ut igen
    }

    [Fact]
    public void Man_får_saka_sitt_lägsta_även_om_man_kan_slå()
    {
        var t = Table.Playing("Qk 2k 3k 4k 5k 6k As", "2r 3r 4r 5r 6r 7r Kr");
        t.Ok(0, "Qk");
        t.Ok(1, "2r");
        Assert.True(t.G.Trick[1].Sak);
        Assert.Equal(0, t.G.Top);
    }

    [Fact]
    public void Kan_man_slå_får_man_inte_lägga_ett_lägre_kort_som_inte_är_ens_lägsta()
    {
        var t = Table.Playing("Qk 2k 3k 4k 5k 6k As", "2r 3r 4r 5r 6r 7r Kr");
        t.Ok(0, "Qk");
        Assert.Equal("Slå kort för kort med lika/högre kort, eller saka ditt lägsta.", t.Play(1, "3r"));
        t.Ok(1, "Kr");
        Assert.Equal(1, t.G.Top);
    }

    [Fact]
    public void Två_lika_lägsta_vilket_som_helst_får_sakas()
    {
        var t = Table.Playing("Qk 2k 3k 4k 5k 6k As", "2r 2h 4r 5r 6r 7r Kr");
        t.Ok(0, "Qk");
        t.Ok(1, "2h");
        Assert.True(t.G.Trick[1].Sak);
    }

    static readonly string[] Nines = ["9k 9r 2k 3k 4k 5k As", "2r 3r Tr Th 6r 7r Kr"];

    [Fact]
    public void Saka_två_lägsta()
    {
        var t = Table.Playing(Nines);
        t.Ok(0, "9k 9r");
        t.Ok(1, "3r 2r");
        Assert.True(t.G.Trick[1].Sak);
        Assert.Equal(0, t.G.Top);
    }

    [Fact]
    public void Saka_lägsta_och_slå_med_resten()
    {
        var t = Table.Playing(Nines);
        t.Ok(0, "9k 9r");
        t.Ok(1, "2r Tr");
        Assert.True(t.G.Trick[1].Sak); // delvis sakat räknas som sakat: sticket tas inte
        Assert.Equal(0, t.G.Top);
        Assert.Equal(new[] { 9, 9 }, t.G.TopRs);
    }

    [Fact]
    public void Saka_lägsta_men_resten_slår_inte_avvisas()
    {
        var t = Table.Playing(Nines);
        t.Ok(0, "9k 9r");
        Assert.Equal("Slå kort för kort med lika/högre kort, eller saka ditt lägsta.", t.Play(1, "2r 6r"));
    }

    [Fact]
    public void Slå_två_tar_sticket()
    {
        var t = Table.Playing(Nines);
        t.Ok(0, "9k 9r");
        t.Ok(1, "Tr Th");
        Assert.False(t.G.Trick[1].Sak);
        Assert.Equal(1, t.G.Top);
    }

    static readonly string[] Treble = ["5k 5r 5h 2k 3k 4k As", "2r 3r Kr 6h 7h 8h Ks"];

    [Theory]
    [InlineData("2r 3r Kr", true)] // två lägsta sakas, kungen slår
    [InlineData("2r 6h 7h", true)] // lägsta sakas, två slår
    [InlineData("6h 7h 8h", false)] // alla tre slår
    public void Tre_kort_slå_eller_saka(string cards, bool sak)
    {
        var t = Table.Playing(Treble);
        t.Ok(0, "5k 5r 5h");
        t.Ok(1, cards);
        Assert.Equal(sak, t.G.Trick[1].Sak);
        Assert.Equal(sak ? 0 : 1, t.G.Top);
    }

    [Fact]
    public void Tre_kort_saka_ett_som_inte_är_lägst_avvisas()
    {
        var t = Table.Playing(Treble);
        t.Ok(0, "5k 5r 5h");
        Assert.Equal("Slå kort för kort med lika/högre kort, eller saka ditt lägsta.", t.Play(1, "3r 6h 7h"));
    }

    [Fact]
    public void Sticket_flyttas_till_historiken_vid_nästa_utspel_och_antalet_får_ändras()
    {
        var t = Table.Playing(Fives);
        t.Ok(0, "5k 5r");
        t.Ok(1, "6k 6r");
        Assert.Equal("Inte din tur.", t.Play(0, "9k"));
        t.Ok(1, "Tk");
        Assert.False(t.G.Done);
        Assert.Equal(1, t.G.Count);
        var past = Assert.Single(t.G.Past);
        Assert.Equal(2, past.Count);
        var lead = Assert.Single(t.G.Trick);
        Assert.Equal(1, lead.Seat);
        Assert.Equal(0, t.G.Turn);
    }

    [Fact]
    public void Tre_spelare_turordning_medsols()
    {
        var t = Table.Playing(ThreeCbc);
        Assert.Equal(0, t.G.Turn);
        t.Ok(0, "2k");
        Assert.Equal(1, t.G.Turn);
        Assert.False(t.G.Done);
        t.Ok(1, "2r");
        Assert.Equal(2, t.G.Turn);
        t.Ok(2, "2h");
        Assert.True(t.G.Done);
        Assert.Equal(2, t.G.Top); // lika slår, sist lagda tvåan ligger högst
        Assert.Equal(2, t.G.Turn);
    }

    [Fact]
    public void Tomma_platser_hoppas_över()
    {
        var t = Table.Seated(3);
        t.G.Leave(1);
        Assert.Equal("lobby", t.G.Phase);
        t.Deal(["9k 2k 3k 4k 5k 6k As", "Th 2h 3h 4h 5h 6h Kh"]); // plats 0 och 2
        Assert.Equal(7, t.Hand(2).Count);
        Assert.Null(t.G.Exchange(0, null));
        Assert.Equal(new[] { 2 }, t.G.Resp.ToArray());
        Assert.Null(t.G.Respond(2, false));
        t.Ok(0, "9k");
        Assert.Equal(2, t.G.Turn);
        t.Ok(2, "Th");
        Assert.True(t.G.Done);
        Assert.Equal(2, t.G.Turn);
    }

    [Fact]
    public void Fel_spelare_får_inte_spela()
    {
        var t = Table.Playing(Fives);
        Assert.Equal("Inte din tur.", t.Play(1, "6k"));
        Assert.Equal("Inte din tur.", t.G.Play(-1, [0]));
        Assert.Equal("Inte din tur.", t.G.Play(3, [0]));
    }

    [Fact]
    public void Spela_går_inte_utanför_spelfasen()
    {
        var t = Table.Seated(2).Deal(Fives);
        Assert.Equal("Inte din tur.", t.G.Play(0, [0]));
        Assert.Equal("Inte din tur.", t.G.Play(t.G.Starter, [0]));
    }

    public static TheoryData<int[]?> BadIdx => new() { null, Array.Empty<int>(), new[] { 0, 0 }, new[] { -1 }, new[] { 7 }, new[] { int.MaxValue }, new[] { 0, 1, 2, 3, 4, 5, 6, 0 } };

    [Theory]
    [MemberData(nameof(BadIdx))]
    public void Ogiltiga_index_avvisas(int[]? idx)
    {
        var t = Table.Playing(Fives);
        Assert.Equal("Ogiltigt val.", t.G.Play(0, idx));
        Assert.Empty(t.G.Trick);
        Assert.Equal(7, t.Hand(0).Count);
    }

    [Fact]
    public void Index_utanför_den_krympta_handen_avvisas()
    {
        var t = Table.Playing(Fives);
        t.Ok(0, "5k");
        t.Ok(1, "6k");
        Assert.Equal("Ogiltigt val.", t.G.Play(1, [6]));
    }
}
