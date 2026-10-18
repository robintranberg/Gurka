---
name: review-patterns
description: Vulnerability patterns specific to Gurka's architecture (single static SignalR hub state, broadcast View) to check on every review
metadata:
  type: feedback
---

Check every review for:
1. Every field in View() goes to ALL connections, including spectators. Derived information about hidden cards (e.g. the zl list) is as sensitive as the cards themselves.
2. Every mutation of static/room state happens under the same lock, including OnDisconnected and timer callbacks (reconnect grace period).
3. Tokens: RandomNumberGenerator, never Random.Shared. Never in View, never in a URL. FixedTimeEquals. Rotate on reconnect.
4. CSP requires no inline script/onclick in index.html.
5. Service workers do not register on http://LAN-IP (not a secure context), so the PWA only works on localhost/HTTPS.

**Why:** Gurka's architecture broadcasts state per connection from a shared static state. These are the places where leaks and races occur.
**How to apply:** use this as a checklist on every new review. Log findings in [[vuln-status]].

6. Backend and frontend are built in parallel: check that client calls (hub.invoke names and arguments) actually match the hub signatures. Mismatches hide security features (reconnect/token) that look implemented on both sides.
7. Order of checks in Attach-like functions: caps/limits must not run before the "is this an existing seated player" check, or else an attacker can lock out legitimate players.

8. Always run `node --check wwwroot/app.js` on frontend diffs. app.js is one flat script full of short const names (tie, card, mini); a new helper easily collides -> SyntaxError kills the whole client.
9. Files change while teammates work in parallel: re-run `git diff`/`node --check` right before every SendMessage report, and state which state the report refers to (see [[feedback-versioning]]). Feedback from frontend 2026-10-17.

10. `dotnet test` fails to build if the user's server is running (Gurka.exe locked). Don't kill it: run `dotnet test tests/Gurka.Tests/Gurka.Tests.csproj -o <scratchpad>/testout` instead (developer tip, works).
11. For new phases: check CurTurn, View (incl. you=-1), every Clear path (Deal/StartPlay/Leave) and that each loop has a guaranteed stop seat.
12. Give file:line on EVERY point, INFO included (developer feedback 2026-10-17: makes them faster to act on).
13. Every report should also say what I did NOT check (e.g. layout/a11y, runtime against a live server, proxy setup) so Team Lead sees where the boundary is (frontend feedback 2026-10-17).
14. Proxy trust: trusting a whole CDN range (Cloudflare) is NOT the same as trusting the CDN hop. Anyone can originate from CDN IPs (Workers, WARP) and then spoof XFF to the left. Allow at most the expected number of CDN hops.
