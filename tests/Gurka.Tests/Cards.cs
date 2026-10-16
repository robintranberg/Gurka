namespace Gurka.Tests;

// Korthjälp: "5h" = hjärter femma, "Ts" = spader tia, "As" = spader ess.
// Färger: k klöver (0), r ruter (1), h hjärter (2), s spader (3).
public static class Cards
{
    const string Ranks = "23456789TJQKA", Suits = "krhs";

    public static Card C(string s)
    {
        int r = s.Length == 2 ? Ranks.IndexOf(char.ToUpperInvariant(s[0])) : -1, f = s.Length == 2 ? Suits.IndexOf(char.ToLowerInvariant(s[1])) : -1;
        if (r < 0 || f < 0) throw new ArgumentException("Okänt kort i testet: " + s);
        return new(r + 2, f);
    }

    public static List<Card> H(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(C).ToList();

    public static string Str(Card c) => $"{Ranks[c.R - 2]}{Suits[c.S]}";
    public static string Str(IEnumerable<Card> cs) => string.Join(" ", cs.Select(Str));

    public static IEnumerable<Card> All() => Enumerable.Range(0, 52).Select(i => new Card(i % 13 + 2, i / 13));

    // Bygger en full lek på 52 kort: först sju kort per spelare i platsordning (det sjunde visas och avgör vem som börjar),
    // sedan "next" i den ordning de ska dras, och sist resten av leken i fast ordning.
    public static List<Card> Deck(IEnumerable<string> hands, string next = "")
    {
        var top = hands.SelectMany(H).Concat(H(next)).ToList();
        if (top.Distinct().Count() != top.Count) throw new ArgumentException("Samma kort förekommer två gånger i testleken: " + Str(top.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key)));
        return top.Concat(All().Except(top)).ToList();
    }
}
