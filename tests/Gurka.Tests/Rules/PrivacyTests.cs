using System.Text.Json;

namespace Gurka.Tests.Rules;

// Vad varje spelare får se: Zlatan-kandidater ska inte läcka, och ett avbrutet spel ska inte lämna kort kvar.
public class PrivacyTests
{
    static JsonElement View(Game g, int you) => JsonSerializer.SerializeToElement(g.View(you));
    static int[] Ints(JsonElement s, string p) => s.GetProperty(p).EnumerateArray().Select(x => x.GetInt32()).ToArray();

    // plats 0 börjar, plats 1 och 3 är Zlatan-kandidater, plats 2 har en dam
    static Table Four() => Table.Seated(4).Deal(["2k 3k 4k 5k 6k 7k As", "2r 3r 4r 5r 6r 7r 8r", "2h 3h 4h 5h 6h 7h Qh", "2s 3s 4s 5s 6s 7s 8s"]);

    [Fact]
    public void Zlatan_kandidater_syns_inte_före_Zlatan_fasen()
    {
        var t = Four();
        for (int i = -1; i < 4; i++)
        {
            var s = View(t.G, i);
            Assert.Empty(Ints(s, "zl"));
            Assert.False(s.GetProperty("zlMe").GetBoolean());
            Assert.Equal(0, s.GetProperty("zlN").GetInt32());
        }
    }

    [Fact]
    public void I_Zlatan_fasen_ser_bara_kandidaten_sig_själv_och_övriga_bara_antalet()
    {
        var t = Four();
        Assert.Null(t.G.Exchange(0, [0]));
        Assert.Equal("zlatan", t.G.Phase);
        Assert.Equal(new[] { 1 }, Ints(View(t.G, 1), "zl"));
        Assert.True(View(t.G, 1).GetProperty("zlMe").GetBoolean());
        Assert.Equal(new[] { 3 }, Ints(View(t.G, 3), "zl"));
        foreach (var i in new[] { -1, 0, 2 })
        {
            var s = View(t.G, i);
            Assert.Empty(Ints(s, "zl"));
            Assert.False(s.GetProperty("zlMe").GetBoolean());
            Assert.Equal(2, s.GetProperty("zlN").GetInt32());
        }
        Assert.Null(t.G.Zlatan(3, false));
        Assert.Empty(Ints(View(t.G, 3), "zl")); // har valt
        Assert.Equal(1, View(t.G, 0).GetProperty("zlN").GetInt32());
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 0 })]
    public void Kandidaterna_töms_efter_bytesfasen(int[] idx)
    {
        var t = Four();
        Assert.Null(t.G.Exchange(0, idx));
        if (t.G.Phase == "zlatan") { Assert.Null(t.G.Zlatan(1, false)); Assert.Null(t.G.Zlatan(3, false)); }
        Assert.Equal(idx.Length == 1 ? "oneup" : "respond", t.G.Phase); // ett bytt kort ger 1 upp
        Assert.Empty(t.G.Zl);
        for (int i = -1; i < 4; i++) Assert.Empty(Ints(View(t.G, i), "zl"));
    }

    [Fact]
    public void Andra_spelares_kort_finns_inte_i_tillståndet()
    {
        var t = Four();
        Assert.Null(t.G.Exchange(0, null));
        var raw = View(t.G, 0).GetRawText();
        foreach (var c in t.Hand(1).Concat(t.Hand(2)).Concat(t.G.Deck))
            Assert.DoesNotContain($"{{\"r\":{c.R},\"s\":{c.S}}}", raw);
        Assert.Equal(JsonValueKind.Null, View(t.G, -1).GetProperty("hand").ValueKind);
    }

    [Fact]
    public void Avbrutet_spel_lämnar_inga_kort_kvar()
    {
        var t = Table.Playing("Qk 2k 3k 4k 5k 6k As", "2r 3r 4r 5r 6r 7r Kr", "2h 3h 4h 5h 6h 7h Kh");
        t.Ok(0, "Qk");
        t.G.Leave(2);
        var s = View(t.G, 0);
        Assert.Equal(0, s.GetProperty("hand").GetArrayLength());
        Assert.Equal(0, s.GetProperty("deck").GetInt32());
        Assert.Equal(0, s.GetProperty("slask").GetInt32());
        Assert.Empty(Ints(s, "zl"));
        Assert.All(t.G.Seats.Where(p => p != null), p => Assert.Empty(p!.Hand));
    }
}
