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

**Why:** Ger fokus nästa gång: var fel brukar gömma sig och vad som orsakar omarbete.
**How to apply:** Testa avbrott/återställning, View-sekretess och kantfall i lobby/anslutning extra noga; se [[test-patterns]].
