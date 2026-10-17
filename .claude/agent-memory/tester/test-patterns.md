---
name: test-patterns
description: Testmönster som fungerar i Gurka – Table-hjälparen med bestämd lek, kortsträngar, integrationstester via SignalR, mutationskoll
metadata:
  type: project
---

Testmönster som visat sig fungera (2026-10-16):
- Regler testas mot `Game` direkt (ingen hubb): `new Game(new Random(seed), () => deck)`. Leken delas 7 kort per aktiv plats i platsordning; det 7:e kortet visas; högst valör börjar, vid lika högsta valör drar de lika ett kort var ur leken (till slasken, Game.Ties) – färgen avgör bara om leken tar slut (sedan 2026-10-17). Avgörandekorten styrs med `next:` i Table.Deal. Hjälparen `Cards.Deck(hands, next)` bygger en full 52-korts lek.
- Kort som strängar "5h", "Ts", "As" (färger k r h s – INTE "d"; parsern kastar på okänt kort, gjorde den inte först och det gav tysta fel).
- `Table.AssertConserved()` efter varje drag: alla 52 kort exakt en gång i händer/lek/slask/bord. Fångar byte- och slaskfel billigt.
- Undvik oavsiktligt lika visade valörer i testhänder (t.ex. Ah/As som 7:e kort) – då dras extra kort och Deck/Discards ändras. Lägg hellre esset tidigare i handsträngen. Kolla gamla tester med ett skript som jämför 7:e kortens valör per Deal/Playing-anrop.
- Mutationskoll (scratch-csproj med Compile Include av tests/Rules/*.cs, Table.cs, Cards.cs + kopia av Game.cs; Python-skript med (namn, gammal, ny)-par, assert att gammal förekommer exakt en gång): kopiera Game.cs till scratchpad, ändra en regel med sed, kör sviten och se att något test faller. Görs i ett separat scratch-csproj som länkar Game.cs + testfilerna, så det går även när GameHub inte kompilerar.
- Integration: `WebApplicationFactory<Program>` + HubConnection med LongPolling och `server.CreateHandler()`. Ny factory per test = egen Lobby och eget flodskydd. Bord skapas med `NewRoom` innan `Hello`. Versionsnumret `v` räknas per bord – acceptera state när `room` ändras.
- TestServer ger IP "?" för alla klienter, så IP-tak (8 anslutningar, 3 bord, 10 Hello-skur) delas av alla klienter i ett test.
- Bygg med `dotnet test -o <scratchpad>/out` så låst bin/ inte stör.
- Klientens regelspegel (legal/legalSets i wwwroot/app.js) testas differentiellt: ett C#-program låter Game.Play avgöra varje delmängd av handen i slumpade positioner (spelar om dragen från bestämd lek, eftersom Game saknar klon) och skriver JSONL. Node klipper ut regelblocket ur app.js och jämför. Mutera för att se att jämförelsen fångar fel.
- Program.cs har hårdkodad UseUrls(0.0.0.0:5000), så --urls och ASPNETCORE_URLS ignoreras. En testserver hamnar på 5000; kontrollera att porten är ledig och stoppa bara din egen process.
- Stoppa aldrig processer med mönster på kommandoraden. Spara PID när servern startas och stoppa just den PID:en. Ett filter på '*scratchpad*srv*' träffade 2026-10-16 troligen även frontends testserver. Säg till frontend innan klienttester så att port och bord samordnas (taket är 3 bord och 8 anslutningar per IP).
- Slumpregler (kort ur slasken) testas med en [Fact] som loopar över 200 frön och kontrollerar vad som INTE får väljas. Med 30 frön som Theory fångade bara 1 av 30 fall mutanten (undantaget träffas med p≈1/24 per frö), vilket är för tunt. Räkna på träffsannolikheten innan du väljer antal frön. Metoden som loopar får inte ha samma namn som testet (xUnit1024).
- 1 upp-fasen: `UpCard` ligger kvar som `Deck[0]` tills det avgjorts. AssertConserved kontrollerar det. Ägaren har 6 kort i decide/offer.
- View-fixturer till frontend: generera JSON direkt från `Game.View(seat)` i ett scratch-test (samma objekt som hubben skickar, bara v=0) och namnge filerna efter fas/stage, deck och perspektiv. Det avslöjade 2026-10-17 felaktiga knapptexter vid tom lek i klienten. Erbjud det vid nya faser och kantfall.

**Why:** Sparar tid nästa gång sviten ska utökas.
**How to apply:** Lägg nya regeltester i tests/Gurka.Tests/Rules med Table-hjälparen; se även [[recurring-issues]].
