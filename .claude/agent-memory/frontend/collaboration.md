---
name: collaboration
description: How to coordinate contracts with developer and reviews with security-reviewer in the Gurka team
metadata:
  type: feedback
---
Ask developer whether they have already started on a feature before proposing a hub contract, then build only against the contract they confirm as final.

**Why:** in the 2026-10-16 redesign I proposed Join(name, token)/Rejoin while developer was already building Hello(token, room). Security-reviewer then reviewed my draft that called methods that did not exist, which cost an extra round. The team lead decided that tokens are server-issued and rotated on each Hello (security requirement).

**How to apply:** 
- When you change client API calls, send developer and security-reviewer a short list of the calls you make.
- Read the server code (Game.View, the hub) before you assume field names.
- Rules for Zlatan privacy: never show who is considering Zlatan, only zlN.

Browser unload order matters for any "tab alive" logic: beforeunload → pagehide → visibilitychange(hidden). A later 'hidden' write will overwrite a 'released' marker unless you guard it. See [[design-decisions]].
