# Klicktester (e2e)

Skript som styr klienten i en riktig webbläsare med puppeteer. De körs manuellt mot en startad server och ingår inte i `dotnet test`.

## Krav

- Node 18 eller senare
- Microsoft Edge (eller Chrome via `BROWSER`)
- `npm install` i den här mappen (installerar `puppeteer-core`, som inte laddar ner någon egen webbläsare)

## Starta servern

Från repots rot, på en egen port så att den inte krockar med ett vanligt `dotnet run`:

```
dotnet run --urls http://127.0.0.1:5077
```

Om servern redan kör är `bin/` låst. Stoppa den eller bygg till en annan mapp.

## Kommandon

| Kommando | Vad det gör |
|---|---|
| `node up.js <2\|4> <take\|other\|allno\|other2\|keep2>` | Spelar 1 upp genom gränssnittet och kontrollerar knappar, att det uppvända kortet syns och att inga handkort läcker. |
| `node play.js [spelare] [omgångar]` | Spelar hela omgångar med slumpade giltiga drag och loggar fel från sidan och servern. |
| `node ovl.js [utmapp] [tagg]` | Ritar påhittade tillstånd i alla faser och storlekar och mäter om meddelanden, stick och skyltar överlappar. |
| `node ver.js [gammal-url]` | Versionshandslaget: svarar på `/build.js` med olika värden och kontrollerar omladdning och `#verBanner`. Med en url till en server före handslaget körs även fall F. |
| `node fix.js` | Ritar View-fixturerna i `fixtures/` i tre storlekar och skriver ut text och knappar. |

Varje skript skriver `Inga fel.` (eller `problem: 0` för `ovl.js`) när allt är bra. Skärmdumpar hamnar i `shots/`.

## Inställningar

| Miljövariabel | Standard | |
|---|---|---|
| `GURKA_URL` | `http://127.0.0.1:5077` | Serverns adress |
| `BROWSER` | Edge på standardplatsen | Sökväg till webbläsaren |
| `SHOTS` | `shots/` | Mapp för skärmdumpar |
| `ROOM` | nytt bord | `play.js`: bordskod att gå in i |
| `FIXTURES` | `fixtures/` | `fix.js`: mapp med View-JSON |
| `SIZES`, `PH` | alla | `ovl.js`: storlekar (`844x390,...`) och faser |

## Att tänka på

- Vänta cirka 45 sekunder mellan körningarna. Alla klienter kommer från samma IP och annars slår flodskyddet till (högst 3 bord och 8 anslutningar per IP).
- Mot en Codespaces-länk (`GURKA_URL=https://...app.github.dev`) klickar skripten automatiskt bort varningssidan "Codespaces Access Port".
- Fixturerna är genererade från `Game.View` och ska genereras om om View ändras.
