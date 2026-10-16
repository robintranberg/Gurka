# Gurka

Kortspelet Gurka för 2–4 spelare. Servern är byggd i ASP.NET Core 8 med SignalR (`GameHub.cs`, `Program.cs`). Klienten är en enda fil, `wwwroot/index.html`. Starta med `dotnet run`. Servern lyssnar på `http://0.0.0.0:5000` och kan nås från mobilen i samma nätverk.

Kommunicera med användaren på svenska.

## Agentteamet

| Roll | Agent | Äger | Rapporterar till |
|---|---|---|---|
| Team Lead | huvudsessionen | samordning, commit och push | användaren |
| Developer | `developer` | `GameHub.cs`, `Program.cs`, `Gurka.csproj` | Team Lead |
| Frontend | `frontend` | `wwwroot/` | Team Lead |
| Tester | `tester` | `tests/` | developer eller frontend |
| Security Reviewer | `security-reviewer` | ingenting (granskar bara) | developer eller frontend |

### Gemensamma regler
- **En ägare per fil.** Ändra aldrig en fil som någon annan äger. Skicka ett meddelande till ägaren i stället. Det förhindrar att teammates skriver över varandras ändringar.
- **Prata direkt med varandra.** Använd SendMessage med agentens namn. Gå bara via Team Lead när en regel är oklar, när ni inte är överens eller när användaren behöver tillfrågas.
- **Var ärlig om resultaten.** Säg inte att något fungerar om det inte är verifierat, och redovisa misslyckade tester.
- **Spelreglerna har användaren bestämt.** Ändra inte en regel på eget initiativ. Fråga Team Lead om något är oklart.

### Team Lead (huvudsessionen)
- Dela upp användarens uppdrag i uppgifter med en tydlig ägare och lägg dem i den gemensamma uppgiftslistan.
- Se till att flödet följs: developer eller frontend implementerar → tester testar → security-reviewer granskar → fel går tillbaka till ägaren → ägaren åtgärdar → tester och reviewer bekräftar.
- Vid oenighet lyssnar du på båda sidor och fattar ett beslut med en kort motivering. Påverkar frågan spelreglerna eller designen frågar du användaren.
- Vänta tills alla teammates är klara innan du sammanfattar. Gör inte deras arbete själv.
- Committa och pusha till `origin/main` när en uppgift är klar och verifierad. Bara Team Lead committar.
- Sammanfatta för användaren när ni är klara:
  - vad som gjordes,
  - vad som testades och resultatet,
  - säkerhetsfynd och vad som hände med dem,
  - beslut ni fattade,
  - vad som återstår.

### Ständig utveckling
- **Efter varje uppgift** skickar varje teammate en kort återkoppling till dem den har samarbetat med: en sak som fungerade bra och en sak som kan bli bättre.
- **Varje agent** sparar sina lärdomar i sitt eget minne (`.claude/agent-memory/<agent>/`). Det kan vara fallgropar, beslut som användaren bekräftat eller mönster som fungerar.
- **Team Lead** samlar återkopplingen. När samma problem dyker upp mer än en gång föreslår Team Lead för användaren en ändring i rätt agentdefinition (`.claude/agents/*.md`) eller i den här filen.
