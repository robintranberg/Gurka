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

In "done" reports, list exactly which scenarios/phases were exercised (e.g. "3-way tie round 1, 2-way round 2, decided round 3, exchange phase, non-starter view") – developer asked for this 2026-10-17. Run `node --check` before reporting so reviewers never see a broken snapshot.

Before proposing a contract, check the inbox: developer's contract often arrives while I am reading code (crossed messages on 1 upp, 2026-10-17). Security-reviewer wants an explicit "UI is final" message so they review the final version.
Crossed messages happened twice on 2026-10-17 (1 upp, version handshake) even though I asked "have you started?" in the same message as my proposal. Better: send only a one-line question/constraint first (e.g. "what we must catch: X; have you started?") and wait for the reply before writing a full proposal.
