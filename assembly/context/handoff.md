# Shooter Mover Reward/Progression Wave 1 Handoff

## Current boundary

Wave 0 is complete:

- PR #130 merged `AUD-001`.
- PR #132 merged `ADR-001`.
- PR #131 merged `DEMO-001`.

Current verified `main` is:

`0e678a9333956aa29ba2e3598265c8e1a4122e72`

The authoritative roadmap remains:

`docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md`

The repository is now in Wave 1 foundation dispatch.

## Verified current state

- `ADR-001` locks reward, equipment, ledger, progression, placed-object,
  lifecycle, simulator, and single-scene-owner boundaries.
- `AUD-001` confirms the existing enemy foundation and physical projectile path
  should be retained, with narrow future turret/prop normalization.
- `DEMO-001` publishes the robot and complete playable Stage 1 baseline.
- The local demo passed all eight focused Stage 1 integration tests from the
  final merged commit.
- No open PR currently claims a Wave 1 task branch or owned path.

## Wave 1 execution shape

Six tasks may begin in parallel from exact base
`0e678a9333956aa29ba2e3598265c8e1a4122e72`:

1. `OBJ-001` — placed identity, explicit/parent scope, variants, capabilities,
   and instance overrides.
2. `REW-001` — immutable reward/economy vocabulary.
3. `EQP-001` — equipment and augment definitions/instances.
4. `RNG-001` — deterministic PRNG and soft progression curves.
5. `LED-001` — typed exact-once ledger primitive.
6. `PRG-001` — explicit progression context and providers.

All six are suitable for isolated GitHub web/coding agents. Agents without a
Unity executable must leave their PR draft and report the exact unexecuted proof.

## Ownership

- No Wave 1 task may edit the Stage 1 scene/controller/tests.
- `OBJ-001`, `REW-001`, `EQP-001`, `LED-001`, and `PRG-001` own distinct
  subtrees named in their packets.
- `RNG-001` owns only random and `Progression/Curves/**`; `PRG-001` exclusively
  owns `Progression/Context/**`.
- Existing gameplay packages are read-only during Wave 1.
- `INT-001` remains the later sole final Stage 1 serialized owner.

## Dispatch artifacts

Use these prompts without silently broadening their scopes:

- `assembly/dispatch/wave1/OBJ-001_WEB_AGENT.md`
- `assembly/dispatch/wave1/REW-001_WEB_AGENT.md`
- `assembly/dispatch/wave1/EQP-001_WEB_AGENT.md`
- `assembly/dispatch/wave1/RNG-001_WEB_AGENT.md`
- `assembly/dispatch/wave1/LED-001_WEB_AGENT.md`
- `assembly/dispatch/wave1/PRG-001_WEB_AGENT.md`

Every task uses a fresh branch and must record its exact base commit in the PR.

## Merge and continuation rule

- All six Wave 1 tasks are independently mergeable and may run concurrently.
- Each PR must remain within its exact owned paths.
- Wave 2 tasks use named dependency merges, not bookkeeping state, as their
  dispatch gate.
- `MON-001` and `SCR-001` wait for `REW-001` plus `LED-001`.
- `INV-001` waits for `REW-001`, `EQP-001`, and `LED-001`.
- `GEN-001` waits for `REW-001`, `EQP-001`, `RNG-001`, and `PRG-001`.
- `SRC-001` waits for `REW-001` plus `OBJ-001`.
- `DOOR-001` and `VOID-001` wait for `OBJ-001`.
- `NORM-001` waits for `OBJ-001`; its audit dependency is already merged.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Dispatch the six Wave 1 prompts to six isolated GitHub web/coding agents from
the exact shared base. Review each PR for owned-path purity and proof. Merge
proof-complete foundations independently, then dispatch only the Wave 2 tasks
whose complete dependency sets have merged.
