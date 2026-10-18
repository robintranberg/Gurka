---
name: feedback-version
description: Testresultat till teamet ska ange exakt vilken kodversion körningen gäller (commit + ändrade filers tidsstämpel), inte bara antal gröna tester
metadata:
  type: feedback
---

Ange i varje resultat till team-lead och developer vilken version av koden körningen gäller: `git rev-parse --short HEAD` och, om arbetskopian är ändrad, tidsstämpel för de berörda filerna (t.ex. Net.cs och Program.cs). Skriv det i första meningen tillsammans med det exakta resultatet, t.ex. "391/391 mot HEAD ff02019 + Net.cs 10:42".

**Why:** 2026-10-18 gällde mitt "339/339" en äldre Net.cs än developers senaste. Developer såg det bara genom att köra själv, och det kostade en extra runda. Security-reviewer anger redan version på det här sättet.

**How to apply:** Alla testresultat, särskilt när flera kontraktsversioner korsas. Se även [[recurring-issues]].
