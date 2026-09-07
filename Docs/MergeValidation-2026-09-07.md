# City/casino integration validation

Local source: `732d5060` (saved Unity scene, street updates, casino and metadata).
Remote source: `b1fb2951` from `origin/main`.
Merge commit: `e893e3aa`.

The shared scene `Assets/zzz.unity` was merged by Unity document/fileID rather than accepting either whole scene. All 4,366 locally added serialized documents and 98 remotely added documents were retained, including 34 placed Neubeckum building prefab instances. Four locally repositioned road objects conflicted with remote deletion; their local positions and complete child/component dependencies were preserved. Transform child lists and scene root lists were combined. The remaining scalar conflict was a ProBuilder cache version, with unchanged geometry.

Validation in Unity 6000.5.9f1:

- Scene opened successfully and the colleague's buildings were visibly present alongside local roads, terrain and casino.
- Editor compilation completed; Console displayed zero errors and 47 warnings at the final save. No Play Mode, multiplayer or standalone build test was run.
- 15,815 unique scene documents before and after Unity's save; no objects were dropped by the Editor round trip.
- The save changed document ordering and 17 ProBuilder cache-version fields only.
- No new dangling scene references or invalid explicit parent-child links were found by structural validation.
- All 34 generated-building prefab references resolve. Two script GUIDs not found under Assets/embedded Packages were already referenced in the pre-merge local scene; these were preserved.

Both histories remain reachable through the merge parents. The existing `codex/casino-local-backup` remote branch retains the earlier casino snapshot. No force push or reset was used.
