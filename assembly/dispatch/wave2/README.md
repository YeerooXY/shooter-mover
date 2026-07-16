# Wave 2 dispatch index

Wave 1 is complete on `main` through exact commit:

`6d04451883127dcf597c4f6fec199aeaec2a7f9e`

All eight tasks below have their named dependencies merged and may start in
parallel from that exact base.

| Task | Agent type | Main output |
|---|---|---|
| `MON-001` | GitHub web/coding agent | Money wallet authority |
| `SCR-001` | GitHub web/coding agent | Scrap wallet authority |
| `INV-001` | GitHub web/coding agent | Player holdings and equipment inventory |
| `GEN-001` | GitHub web/coding agent | Shared deterministic reward/equipment generator |
| `SRC-001` | GitHub web/coding agent | Reward definitions and placed-source authoring |
| `DOOR-001` | GitHub web/coding agent | Reusable door package |
| `VOID-001` | GitHub web/coding agent | Reusable void/fall-hazard package |
| `NORM-001` | GitHub web/coding agent | Blaster Turret identity and registration normalization |

## Dispatch rules

- Give one prompt to each isolated agent.
- Every branch starts from exact base `6d04451883127dcf597c4f6fec199aeaec2a7f9e`.
- Every PR targets `main` and remains draft until its required proof is complete.
- Agents must verify that all dependency merge commits named in their prompt are
  ancestors of their branch before editing.
- Agents without Unity add all required tests, run available static validation,
  list exact unexecuted proof, and leave the PR draft.
- No Wave 2 task edits `Assets/ShooterMover/Scenes/**`, the Stage 1 controller,
  Stage 1 integration tests, shared asmdefs, project settings, packages,
  generated output, or handoff/dispatch files.
- Package-local prefabs are allowed only for `DOOR-001` and `VOID-001`.
- Existing gameplay packages remain read-only except for the exact
  `NORM-001` Blaster Turret files and focused tests named in its packet.

## Combined-main proof at dispatch

- Unity `6000.3.19f1` cold import/compile: passed.
- EditMode: 568/568 passed.
- OBJ-001 focused PlayMode fixture: 11/11 passed.
- Repository layout and assembly graph: passed.
- Duplicate Unity GUID audit: zero duplicates.
- Full PlayMode baseline: 263/266 passed. The three failures are pre-existing,
  unrelated assertions listed in `VALIDATION.md`; Wave 2 agents must not edit
  those paths unless separately assigned.

Prompt files:

- [MON-001](MON-001_WEB_AGENT.md)
- [SCR-001](SCR-001_WEB_AGENT.md)
- [INV-001](INV-001_WEB_AGENT.md)
- [GEN-001](GEN-001_WEB_AGENT.md)
- [SRC-001](SRC-001_WEB_AGENT.md)
- [DOOR-001](DOOR-001_WEB_AGENT.md)
- [VOID-001](VOID-001_WEB_AGENT.md)
- [NORM-001](NORM-001_WEB_AGENT.md)
