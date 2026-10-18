---
name: vuln-status
description: Gurka vulnerabilities already reported (IDs B*/F*) with status, plus verified non-issues; check before reporting to avoid duplicates
metadata:
  type: project
---

Baseline 2026-10-16 (commit 5e79b10). B* = backend (developer), F* = frontend.

- B1 MEDEL: zl leak. VERIFIED fixed (View sends zlMe/zlN only in zlatan, Zl.Clear in DoExchange).
- B2 MEDEL: a disconnect aborts the game. VERIFIED fixed (75 s grace period, Lobby.cs).
- B3 MEDEL: rate limit. VERIFIED fixed (buckets per conn+IP, chat/Hello stricter, 4 KB, per-room Push).
- B4 LÅG: duplicate names, "Bordet" impersonation, bidi/control characters. VERIFIED fixed (Game.Clean, Taken).
- B5 LÅG/INFO: HTTP 0.0.0.0:5000. Headers now in place (Program.cs). HTTP in LAN = accepted risk per CLAUDE.md.
- B6 INFO: Random.Shared is not for tokens. VERIFIED: the token uses RandomNumberGenerator.
- B7 MEDEL: VERIFIED fixed (Owner before cap, 8/IP, 400 total, Limbo+Abort 15 s).
- B8 LÅG: VERIFIED fixed (NewRoom only, TryAdd under lock, 6 chars, 3/IP, NoRoom + guess bucket per IP with the same reply).
- B9 LÅG: accepted risk, but in practice fixed (rotation, FixedTimeEquals, Prev only when Conn==null).
- B10 LÅG: VERIFIED fixed (script-src 'self'; also style-src 'self', no inline style in index.html/app.js).
- F1 MEDEL: inline script/onclick. VERIFIED fixed (app.js, data-a delegation).
- F2 LÅG-MEDEL: CDN without SRI. VERIFIED fixed: self-hosted lib/signalr.min.js, SHA-256 matches signalr@8.0.7 (e28a720a...).
- F3 LÅG: esc() '. VERIFIED fixed.
- F4 MEDEL: client token/API mismatch. VERIFIED fixed (Hello(t,code), server token stored, Join(n)).
- F6 KRITISK (tillgänglighet) 2026-10-17: duplicate `const tie` in app.js (tie cards vs. draw text) -> SyntaxError, the whole client dead. VERIFIED fixed (renamed to tbk, node --check OK).
- F5 LÅG: tokens in localStorage. CLOSED as deliberate design: 'toks' map TTL 10 min, cleared on Leave, server only accepts the current token (+Prev while disconnected).

Non-issues (do not report): View sends only your own hand; reveal/past/trick are public by design; the server validates phase/turn/index/deck; no XSS in the baseline; name[..14] surrogate split gives U+FFFD (tested, no exception); Next() cannot loop forever. Tie-break (Game.Deal, 2026-10-17): Ties public by design, the loop breaks before Draw when the deck is short, Ties cleared in Deal/StartPlay/Leave; Ties is now also cleared at respond (line 155, verified). 1 upp (oneup, 2026-10-17): hidden refill card, mark (owner only), upOwn never in View; UpCard stays in Deck[0] until decided (count 52 OK); UpNext/offer loops terminate; Leave -> UpClear. UpPick idx now checked against Hand.Count (VERIFIED 21:58, Game.cs:266); slask refill excluding the taker's just-discarded card: fixed per Team Lead decision, VERIFIED 21:59 (Game.cs:251/257/291, `gone`); the fallback to the whole slask when nothing else remains is accepted. Frontend 1 upp final version (app.js 22:03) VERIFIED clean: esc(cn()), +s.deck, say=textContent, data-a, calls match the hub.

**Why:** avoid double-reporting. **How to apply:** update the status when a fix is verified. See [[review-patterns]].

As of 2026-10-16 17:27: all B*/F* fixed or accepted. Accepted: HTTP in LAN (B5), B9 (but in practice fixed), X-Frame-Options SAMEORIGIN.

Version handshake (2026-10-17 22:52) VERIFIED clean: /build.js (Program.cs:48) returns only a 12-hex SHA-256 over *.cs/*.csproj, no-store, CSP applies; Build.L is separate from lb.L; Hello.build=Boot is public by design; client chkBuild (app.js:68) uses fixed text via textContent, reload is blocked via sessionStorage. INFO (do not re-report): /build.js is not covered by Flood (HTTP), cheap (stat + hash only on change).
Sum scoring in Finish (2026-10-18, Game.cs:88-101) VERIFIED clean: Finish only with a full trick (>=2, non-empty plays), Leave aborts first, max 4x14x5=280/deal. Client sumsH/sumTxt/tie: esc(who), numbers only.
