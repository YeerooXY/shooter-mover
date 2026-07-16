# Shooter Mover Reward/Progression Wave 2 Handoff

## Current boundary

Wave 0 and Wave 1 are complete. Verified `main` is:

`6d04451883127dcf597c4f6fec199aeaec2a7f9e`

Merged Wave 1 foundations:

- `REW-001` through `06012ea116c1b8bd1f087a5f9275079d5fd882bd`
- `OBJ-001` through `e967daee4a23ca3372de468e2a4a8d122f99eea0`
- `RNG-001` through `46cccb17c057b07a6d408b9aabe286228a921915`
- `EQP-001` through `0bac603dc5921ab1da1b89895f725a0b97261fae`
- `LED-001` through `95a19fbf60fe81c443ad1a366422bf67d17d953e`
- `PRG-001` through `6d04451883127dcf597c4f6fec199aeaec2a7f9e`

The authoritative roadmap remains
`docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md`.

## Verified combined state

- Unity `6000.3.19f1` cold import/compile passed.
- Full EditMode passed 568/568.
- OBJ-001 focused PlayMode passed 11/11.
- Layout, assembly graph, and duplicate-GUID checks passed.
- Full PlayMode has three unrelated pre-existing failures documented in
  `assembly/dispatch/wave2/VALIDATION.md`.

## Wave 2 execution shape

Eight tasks may start in parallel from exact base `6d04451...`:

1. `MON-001` - money authority.
2. `SCR-001` - scrap authority.
3. `INV-001` - holdings authority.
4. `GEN-001` - shared deterministic generator.
5. `SRC-001` - reward definitions/source authoring.
6. `DOOR-001` - reusable doors.
7. `VOID-001` - reusable void hazards.
8. `NORM-001` - Blaster Turret registration normalization.

Use the exact prompts under `assembly/dispatch/wave2/`.

## Continuation gates

- `RAP-001` waits for merged `MON-001`, `SCR-001`, and `INV-001`.
- First `SIM-001` generator mode waits for merged `GEN-001`.
- `PROP-001` waits for merged `SRC-001`.
- `BOX-001`, `CRA-001`, `AUG-001`, and `SHOP-001` remain gated by `RAP-001`
  and their other named dependencies.
- `INT-001` remains the sole final Stage 1 serialized owner.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Dispatch all eight Wave 2 prompts to isolated GitHub web/coding agents. Keep
every PR draft until owned-path review and required proof are complete.
