# Gurka

Kortspelet Gurka för 2–4 spelare. Servern är byggd i ASP.NET Core 8 med SignalR: spellogiken finns i `Game.cs`, bord, tokens, återanslutning och flodskydd i `Lobby.cs`, ett tunt hubblager i `GameHub.cs` och uppstart, CSP och säkerhetsheaders i `Program.cs`. Klienten finns i `wwwroot/`: `index.html` (bara markup), `app.js`, `app.css`, `lib/signalr.min.js` (lokal kopia) samt PWA-filer. Testerna (xUnit) ligger i `tests/Gurka.Tests/`. Kör dem med `dotnet test Gurka.sln`.

Starta med `dotnet run`. Servern lyssnar som standard på `http://0.0.0.0:5000` och kan nås från mobilen i samma nätverk. Porten kan ändras med `--urls` eller `ASPNETCORE_URLS`. Lokalt körs spelet utan HTTPS, så service worker, Wake Lock och urklipp är inte aktiva via `http://<IP>`. Publikt körs det på Render (Docker, HTTPS, auto-deploy vid push till `main`), se `DEPLOY.md`. E2e-skript finns i `tests/e2e/`.

Kommunicera med användaren på svenska.

## Agentteamet

| Roll | Agent | Äger | Används |
|---|---|---|---|
| Team Lead | huvudsessionen | samordning, commit, push och små ändringar | alltid |
| Developer | `developer` | all kod utom `tests/` (server, `wwwroot/`, driftfiler) | vid kodändringar |
| Tester | `tester` | `tests/` (xUnit och e2e) | vid ändringar i regler eller beteende |
| Security Reviewer | `security-reviewer` | ingenting (granskar bara) | bara vid behov, se nedan |

### Gemensamma regler
- **En ägare per fil.** Ändra aldrig en fil som någon annan äger. Skicka ett meddelande till ägaren i stället.
- **Var ärlig om resultaten.** Säg inte att något fungerar om det inte är verifierat, och redovisa misslyckade tester.
- **Spelreglerna har användaren bestämt.** Ändra inte en regel på eget initiativ. Fråga Team Lead om något är oklart.
- **Spara tokens.** En rapport per agent och uppgift, när den är klar. Inga mellanrapporter, upprepningar, kvitteringar eller tackmeddelanden. Ingen återkopplingsrunda efter varje uppgift. Lärdomar sparas i agentens minne (`.claude/agent-memory/<agent>/`) bara när något faktiskt gick fel eller användaren bekräftade något nytt.

### Team Lead (huvudsessionen)
- **Små ändringar gör du själv** (en text, en enstaka rad, dokumentation) utan att starta agenter. Kör testerna innan du committar.
- **Större ändringar:** developer implementerar → tester testar → (vid behov) security-reviewer granskar → fel går tillbaka till developer.
- **Security-reviewer kallas bara in** vid ändringar som rör nätverk, anslutningar, tokens, tak och flodskydd, publik drift, ny data som renderas i klienten eller ny information i `View()`. Inte vid rena regeländringar eller texter.
- Fatta själv beslut om detaljer med en kort motivering. Påverkar frågan spelreglerna eller designen frågar du användaren.
- Committa och pusha till `origin/main` när en uppgift är klar och verifierad. Bara Team Lead committar.
- Sammanfatta kort för användaren när ni är klara: vad som gjordes, vad som testades, säkerhetsfynd, beslut och vad som återstår.
