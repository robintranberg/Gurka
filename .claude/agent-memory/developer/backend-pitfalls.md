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
- Spelet körs även publikt via Codespaces och Cloudflare-tunnel (2026-10-16). Klientens IP räknas fram i Net.cs, och UseForwardedHeaders litar bara på loopback och privata nät. Det betyder att en enhet i LAN:et kan förfalska X-Forwarded-For. Det är accepterat, eftersom LAN:et räknas som betrott.
  - CF-Connecting-IP används bara när Gurka:Cloudflare=true. Genom Codespaces kommer trafiken också från loopback, och där kan headern förfalskas.
  - Taken per IP använder Net.Key (IPv6 grupperas per /64).
  - Röktest: Nodes WebSocket tar `{headers}` som andra argument.
  - Verifierat i ett riktigt codespace 2026-10-16 (Team Lead): Codespaces-proxyn ERSÄTTER X-Forwarded-For med klientens riktiga IP (en förfalskad header tas bort). Den skickar också X-Real-IP och X-Forwarded-Proto/Host. Taken per IP gäller alltså per riktig klient, och Net.Proxies behöver ingen extra betrodd hop för Codespaces.
- Regel som användaren bestämt 2026-10-17 om vem som börjar: om flera har samma högsta visade valör drar just de ett kort var från Deck (i platsordning). Korten går till Discards och loggas i `Ties`. Detta upprepas tills en är ensam högst. Färgen avgör bara om leken tar slut. Lika på lägre valör spelar ingen roll. Zl räknas efter avgörandet.
- C# tillåter inte att en lambda-parameter skuggar en lokal variabel i samma metod (CS0136). Ett exempel är lokalen `c` mot `c => c.R` i Deal. Välj lokala namn som inte krockar med befintliga lambdor.
- Regeln "1 upp" (användaren 2026-10-17, gäller när startspelaren byter exakt 1 kort): fas "oneup" med stage pick/decide/offer, fälten Up* och view-fältet `up`. Det uppvända kortet ligger kvar som Deck[0] tills det är avgjort, och owners markerade kort ligger i slasken under tiden (owner har 6 kort). Annars blir händer + Deck + Discards != 52. Team Lead-beslut: markera före uppvändning. Med tom lek ges ett slumpat slaskkort, helst inte owners eget eller det uppvända. Zlatan reserverar ExN (= 1) kort.
- `dotnet test Gurka.sln` misslyckas när servern kör, eftersom bin/Gurka.exe är låst. Kör `dotnet test tests/Gurka.Tests/Gurka.Tests.csproj -o <scratch>/tb`. För snabba regelkontroller fungerar ett konsolprojekt i scratch med `<Compile Include="C:/projects_dotnetcloud/Gurka/Game.cs" />` och en slumpsimulering som kontrollerar invarianter efter varje steg. Den hittade invariantfelet med det uppvända kortet direkt.
- Versionshandslag (2026-10-17): `Build` i Program.cs hashar bara serverns källor (*.cs och *.csproj, översta nivån i content root) vid start (Boot) och nu (Now). wwwroot togs bort ur hashen eftersom den läses från disken utan omstart och gav falsklarm (frontends invändning). Hello returnerar build=Boot och GET /build.js (no-store) ger Now. Bakgrunden var att användarens `dotnet run` (Gurka.exe på :5000) körde gammal logik mot ny wwwroot. Frontends testserver brukar ligga på :5077, så använd en annan port för röktester.
- Skriv aldrig `\0` eller andra escape-sekvenser via Python i bash-heredoc till C#-källor, eftersom det blev ett riktigt NUL-tecken i Program.cs (Edit/Write vägrar då). Kontrollera med `file X.cs` efter sådana skript. Undvik breda `taskkill /IM dotnet.exe`. Stoppa bara din egen process via CommandLine-filter (Get-CimInstance Win32_Process).
- Spelreglerna i Game.cs är facit. Användaren har sagt att reglerna inte får ändras på eget initiativ (2026-10-16).

**Why:** De här kostade tid eller gav tysta fel under refaktoreringen 2026-10-16.
**How to apply:** Läs detta innan du ändrar Lobby.Attach/Detach, Join eller Program.cs. Se även [[team-collab]].
