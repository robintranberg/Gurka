---
name: recurring-issues
description: Feltyper och friktion som dykt upp i Gurka-testningen – API som ändras under testskrivning, tillstånd som inte nollställs vid avbrott
metadata:
  type: project
---

- Developer ändrade publika signaturer (Lobby.Attach, krav på NewRoom, rumskod 4->6 tecken) medan integrationstester skrevs. Skriv regeltester först mot Game (stabilt), integrationstester sist.
- När bygget är trasigt: fråga developer kort om läget i stället för att gissa (developer bad uttryckligen om det). Deras meddelanden kan också komma fram sent – läs om API-beskrivningen när de dyker upp.
- Avbrutet spel (Leave mitt i) nollställde inte Hand/Deck/Zl/Discards – åtgärdat 2026-10-16, täcks av PrivacyTests. Kolla alltid nollställning när faser avbryts.
- Game.Clean tog bort \t/\n helt – åtgärdat 2026-10-16 (blir mellanslag).
- Informationsläckor i View (t.ex. vilka som är Zlatan-kandidater) är en återkommande risk – testa vad varje plats ser, inte bara spelets tillstånd.
- Regellogiken i Game.cs hade inga regelbuggar 2026-10-16; 9 mutationer fångades alla.

- Egna testdatafel: dubbla kort mellan hand och `next` (Cards.Deck kastar – bra) och fel antagande om vem som drar vad. Mutationskoll avslöjade 2026-10-17 ett test som inte testade det det påstod (Zlatan-avgörandekort). Kör alltid mutationer på ny regel.
- Developer kan lägga till extra tömning utöver kontraktet (Ties töms även vid respond, inte bara StartPlay) – ger ekvivalenta mutanter; fråga hellre än att anta fel.
- Developer kan avvika från sitt eget kontrakt under implementationen (2026-10-17, 1 upp: det uppvända kortet blev kvar som Deck[0] i stället för att dras). Skriv invarianter så att de är lätta att byta (en rad i AssertConserved) och läs om koden innan du rapporterar ett fel mot kontraktet.
- Mutanter som överlever beror oftast på att testdatan bara använder index 0 (markering/insättning). Variera index i minst ett test per regel.
- 2026-10-17 igen: Build.Init ändrades från (webRoot, contentRoot) till (contentRoot) och wwwroot togs bort ur hashen medan jag skrev BuildTests. Läs alltid om den aktuella koden precis innan första bygget i stället för att lita på kontraktsmeddelandet.
- Tester för likhet (Boot == Now) säger inget om vilket värde som returneras. Skapa ett läge där värdena skiljer sig (ändra en fil efter Init) innan du asserterar.
- Statiskt globalt tillstånd (Build) + WebApplicationFactory: lägg alla berörda klasser i [Collection("Server")] så att de inte körs parallellt.
- Vid regeländring: samma speldata återanvänds i flera filer (EndTests-sticket fanns även i SeatTests). Kör hela sviten innan du rapporterar, inte bara de filer du själv justerat. 2026-10-18 missade jag SeatTests i första filtrerade körningen.

- Ett nytt testfall som läggs till för att döda en mutant måste först vara grönt mot den riktiga koden. 2026-10-18 dödades mutanten "värd lägsta plats" av ett testfall som själv var trasigt (Start från en tom plats). Kör alltid sviten mot originalet efter att du lagt till fallet, och sedan mutanten.
- Developer kan lägga till beteende utöver kontraktet (FixHost: värdskapet flyttas vid frånkoppling). Fråga och flagga det i rapporten när det rör en regel som användaren har bestämt.

- Python-redigering av JS via heredoc: 
 i strängar tolkas av Python och matchar inte. Använd r"""...""" (råsträngar) för JS-källkod. Om assert faller skrivs filen inte, och då kör originalet. 2026-10-18 körde host.js därför mot frontends server på 5077. Kontrollera med grep att redigeringen gick in innan du kör.

**Why:** Ger fokus nästa gång: var fel brukar gömma sig och vad som orsakar omarbete.
**How to apply:** Testa avbrott/återställning, View-sekretess och kantfall i lobby/anslutning extra noga; se [[test-patterns]].
