# Drift på Render

Gurka kan köras gratis på [Render](https://render.com) som en Docker-tjänst. Varje push till `main` byggs och deployas automatiskt.

## Första gången

1. Logga in på Render med ditt GitHub-konto och ge Render åtkomst till repot.
2. Välj **New → Blueprint** och välj repot. Render läser `render.yaml` och föreslår tjänsten `gurka` (gratisnivån, Docker). Klicka på **Apply**.
   - Alternativ: **New → Web Service**, välj repot, *Language: Docker*, *Instance type: Free*, *Health Check Path: `/healthz`*, och lägg till miljövariabeln `GURKA__RENDER=true`.
3. Det första bygget tar några minuter. Följ det under **Events** eller **Logs**.
4. Länken visas överst på tjänstens sida, till exempel `https://gurka-xxxx.onrender.com`. Dela den med de du vill spela med. Bord med egen kod delas via **Bjud in** som vanligt.

## Bra att veta

- **Tjänsten somnar.** På gratisnivån stängs tjänsten av efter cirka 15 minuter utan besökare. Nästa besök väcker den, och det tar ungefär en minut innan sidan laddas.
- **Varje push deployar.** När något pushas till `main` byggs en ny version och den gamla stängs. Pågående spel och bord försvinner då, eftersom allt speltillstånd bara finns i minnet. Pusha alltså inte mitt i en omgång.
- **Insomning raderar också spelen.** Av samma skäl försvinner borden när tjänsten somnar.
- **HTTPS ingår.** Render ger https, så installation som app (PWA), skärmlås (Wake Lock) och kopiering av länken fungerar, vilket de inte gör via `http://<IP>` hemma.
- **Versionsbannern** ("starta om servern") visas inte på Render. Den gäller bara `dotnet run` lokalt.

## Loggar

Öppna tjänsten i Render och välj **Logs**. Där syns start, varningar och fel. Vanliga anrop loggas inte.

## Kontrollera spelarnas IP (valfritt, en gång)

Flodskyddet och taken per IP behöver se varje spelares riktiga IP. Trafiken går via Cloudflare och Renders proxy. Servern läser kedjan från höger och hoppar bara över Renders interna adresser och högst ett Cloudflare-hopp (se `Net.cs`). En vanlig spelare kan därför inte förfalska sin IP. Den som själv utgår från Cloudflares nät (t.ex. Cloudflare Workers eller WARP) hamnar på en Cloudflare-adress, som delas med andra sådana klienter. Det kan bli strikt, men aldrig förfalskat, förutsatt att kedjan ser ut som i kontrollen nedan.

Så här kontrollerar du det efter första deployen:

1. Under **Environment** sätter du `GURKA__LOGIP` till `true` och sparar. Tjänsten startas om.
2. Öppna spelet från två olika nät, till exempel mobilen via 4G och datorn via wifi.
3. I **Logs** syns rader som `Klient-IP 81.x.x.x (anslutning från 10.x.x.x, X-Forwarded-For: ...)`. Kontrollera två saker:
   - Klient-IP ska vara din riktiga adress (jämför med en sida som visar din IP) och skilja sig mellan de två enheterna.
   - Listan efter `X-Forwarded-For:` ska sluta med en Cloudflare-adress (t.ex. `162.158.x.x`, `172.64`–`172.71.x.x`, `104.16`–`104.23.x.x` eller `2400:cb00:…`). Den får bara följas av eventuella `10.x`-adresser, och din egen IP ska stå direkt före den. Skyddet mot förfalskning bygger på det.
4. Sätt tillbaka `GURKA__LOGIP` till `false`.

Kopiera en loggrad och skicka den till utvecklaren om något av följande gäller:
- Klient-IP är en Cloudflare-adress.
- Klient-IP är samma för alla enheter.
- X-Forwarded-For slutar med din egen IP i stället för en Cloudflare-adress.

Ta gärna med vad `CF-Connecting-IP` visar. Då kan servern justeras efter hur Render faktiskt skickar kedjan.

## Köra imagen lokalt

```
docker build -t gurka .
docker run --rm -e PORT=10000 -p 10000:10000 gurka
```

Öppna sedan `http://localhost:10000`. Hälsokollen finns på `/healthz`.
