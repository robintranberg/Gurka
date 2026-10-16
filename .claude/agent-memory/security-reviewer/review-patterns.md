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
