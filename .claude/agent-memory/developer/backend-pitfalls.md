---
name: backend-pitfalls
description: Fallgropar i Gurkas backend (Game/Lobby/GameHub-uppdelning, sessioner, SignalR-filter, bygg och röktest)
metadata:
  type: project
---

- `Pl.Conn` måste sättas när en spelare sätter sig (hubbens Join). Annars blir Gone aldrig satt vid en disconnect, och platsen fastnar för alltid.
- Token byts vid varje Hello. Den gamla tokenen (`Prev`) godtas bara medan `p.Conn == null`, eftersom svaret på Hello kan gå förlorat när mobilen tappar anslutningen. När någon tar över platsen blir andra flikar åskådare med en ny token, så två anslutningar styr aldrig samma plats. Krav från security-reviewer 2026-10-16.
- `Detach` kan städa bort ett tomt bord mitt i `Attach`. Lägg därför tillbaka bordet med `Rooms.TryAdd` innan anslutningen läggs till.
- Bord skapas bara via NewRoom(), som reserverar koden under lås. Hello med en okänd kod ger alltid "Bordet finns inte." (Team Lead-beslut B8, 2026-10-16). Standardbordet "" finns alltid och räknas inte mot taket. En sittande token måste alltid släppas in vid sitt bord, oavsett tak. Annars kan en angripare fylla bordet och sparka ut spelare (B7).
- Taken per IP slår till i röktester med Node. Alla lokala klienter har samma IP: 8 anslutningar, 3 bord och 5 Hello/NewRoom i en skur per anslutning. Starta om servern mellan körningar, för bord lever 60 s.
- Maskinen har .NET SDK 10.0.400, där `dotnet new sln` skapar .slnx som standard. Använd `--format sln` om ett klassiskt .sln behövs. Gurka.sln i roten innehåller servern och tests/Gurka.Tests, och `dotnet test` körs från roten.
- Bash-heredocs med långa Python-skript och svenska tecken eller citattecken kan ge "unexpected EOF". Skriv i så fall skriptet eller texten till en fil i scratch med Write.
- Allt i View() går till alla vid bordet. Härledd information om dolda kort är lika känslig som korten själva. Exempel: listan `Zl` avslöjade svaga händer. Team Lead beslutade 2026-10-16 att varje klient bara får `zlMe` och `zlN`, och att `Zl` töms i DoExchange.
- `o.AddFilter(...)` på HubOptions kräver `using Microsoft.AspNetCore.SignalR;` i Program.cs. Det namnrummet ingår inte i implicit usings.
- Bygg till scratch (`dotnet build -o <scratch>/build`) och starta med `dotnet Gurka.dll --contentRoot C:/projects_dotnetcloud/Gurka`. Röktesta med Node 22 (inbyggd WebSocket) och SignalR JSON-protokollet direkt, utan npm: POST /hub/negotiate?negotiateVersion=1, sedan ws://...?id=connectionToken, handshake `{"protocol":"json","version":1}\x1e`.
- Regel som användaren bestämt 2026-10-16: om flera spelare i sista sticket delar den lägsta valören (min av varje spelares högsta kort) blir det oavgjort. Då gäller Winner = Gurka = -1, Points = 0 och inga poängändringar. Kod som läser Winner eller Gurka måste därför klara -1.
- Spelreglerna i Game.cs är facit. Användaren har sagt att reglerna inte får ändras på eget initiativ (2026-10-16).

**Why:** De här kostade tid eller gav tysta fel under refaktoreringen 2026-10-16.
**How to apply:** Läs detta innan du ändrar Lobby.Attach/Detach, Join eller Program.cs. Se även [[team-collab]].
