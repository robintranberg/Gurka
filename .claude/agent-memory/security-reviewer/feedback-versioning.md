---
name: feedback-versioning
description: Always state which file version/time a review covers; developer works in parallel and fixes can land before the report
metadata:
  type: feedback
---

Every review report states which version it covers (time + `git diff --stat`/hash, or "files read at HH:MM").

**Why:** developer pointed out (2026-10-16) that B1/B9 were already fixed when my report arrived, because I read an older version and it cost an extra check. The team works in parallel on uncommitted files.
Frontend gave the same feedback (2026-10-16): the first review covered an old index.html even though a new one was already on disk.

**How to apply:** run `ls -l --time-style=+%H:%M` on the files under review first, re-read the files right before sending findings, and mark in the report which version it covers. See [[review-patterns]].
