---
name: team-collab
description: Hur developer samarbetar med frontend/tester/security-reviewer i Gurka – kontraktsmeddelanden och ordning
metadata:
  type: feedback
---

Skicka det exakta kontraktet (metoder, fältnamn/typer, nya err/chat-texter) till frontend i ett enda meddelande. Håll alla ändringar bakåtkompatibla tills frontend bekräftat. Tester får API:t för Game/Lobby och reglerna som ska testas, inklusive hur tidsberoende saker testas utan att vänta (t.ex. sätt `Gone` bakåt i tiden och anropa `Sweep`).

Inled varje kontraktsmeddelande med en kort lista över hubbens signaturer, t.ex. `Hello(string? token, string? room) -> {token, room, seat}`. Det var security-reviewers återkoppling 2026-10-16. Granskare och testare läser ofta en äldre version av filerna. Svara då med radnummer i nuvarande kod i stället för att göra om arbetet, och kontrollera klientens anrop med grep i app.js.

Skicka helst ETT samlat meddelande märkt "slutligt kontrakt vN" när kontraktet sitter, i stället för flera deländringar medan frontend bygger. Det var frontends återkoppling 2026-10-16, när koden för rumskoder ändrades i flera steg. Fältet `last` i state används inte av klienten, som jämför tidigare och nytt state själv.

Ange alltid ordningen för listor i kontraktet (t.ex. "sorterad på round, sedan sittplats") och när fältet töms. Det var frontends återkoppling 2026-10-17 om `tie`. Att ge ett nytt fält samma livslängd som ett befintligt (`tie` som `reveal`) gjorde det enkelt för frontend att bygga parallellt.

Ändrar du något efter att kontraktet skickats, till exempel var ett fält töms, ska du märka meddelandet till tester tydligt som "ändring mot kontraktet" och ange radnummer. Annars korsar det testerna, och testaren upptäcker ändringen först via en mutation som överlever (testers återkoppling 2026-10-17).

**Why:** Team Lead kräver det (uppdraget 2026-10-16). Frontend och tester jobbar parallellt och kan inte läsa koden medan den ändras.
**How to apply:** Gäller varje gång View(), hubbmetoder eller events ändras. Se även [[backend-pitfalls]].
