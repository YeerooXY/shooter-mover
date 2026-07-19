# WPN-LIVE-001 — Inventory-backed live weapon execution

## Launch boundary

- Base: `f00482a2a86232275517e8b992a9f290be07a152`
- Branch: `agent/wpn-live-001-inventory-execution`
- Target: `main`

## Runtime flow

The adapter composes existing authorities rather than introducing another weapon runtime:

`active concrete equipment-instance ID`
→ `IPlayerHoldingsAuthorityV1` exact immutable equipment payload
→ existing equipment definition and runtime weapon reference
→ JSON-derived `WeaponCatalog` definition
→ `WeaponExecutionCore.TryExecute`
→ atomic inventory/live effect sink
→ immutable execution result and effect batch.

`InventoryBackedWeaponExecutionAdapter` is a reusable Unity-facing adapter and implements
the existing `IEquippedWeaponInstanceResolver` port. It accepts only the concrete active
instance ID and finds the exact matching equipment payload in the holdings authority
snapshot. Missing, non-equipment, mismatched, or unknown instances fail closed.

## WPN-CORE ownership

WPN-CORE-002 remains the sole firing authority for:

- actor and lifecycle ownership;
- exact equipment-instance identity;
- catalog profile resolution;
- behavior selection;
- deterministic spread;
- projectile count;
- cooldown state;
- accepted operation replay;
- conflicting duplicate detection;
- atomic sink acceptance;
- shot sequence.

The adapter does not contain a weapon-name switch, runtime-ID switch, default weapon, or
fallback behavior. Blaster, Shotgun, Rocket Launcher, Flamethrower, and future definitions
follow the same catalog-driven path.

## Damage-over-time handoff

WPN-CORE-002 currently models direct, explosive, and chain effect descriptions. The live
adapter therefore creates an immutable core-compatible catalog projection for WPN-CORE and
keeps the original JSON-derived definition attached to the atomic live effect batch. This
preserves damage-over-time and pool fields without changing WPN-CORE cooldown, replay,
spread, or acceptance authority.

The projection is derived once from the supplied immutable catalog. It owns no mutable
state, registration, balance, lookup identity, or fallback and is not a second catalog or
weapon authority. The downstream live sink receives:

- the accepted immutable WPN-CORE effect batch;
- exact definition identity;
- damage and area damage;
- fire rate and core-equivalent cooldown ticks;
- spread and projectile count;
- projectile speed and range;
- pierce;
- damage-over-time DPS and duration;
- pool radius and duration;
- chain, knockback, and damage-type metadata.

The downstream sink is called inside WPN-CORE's atomic sink boundary. Rejection does not
commit cooldown, replay, or shot-sequence state.

## Replay behavior

Accepted live batches are retained only as an immutable presentation projection keyed by
actor, equipment instance, operation, and lifecycle generation. WPN-CORE still decides
whether a request is an exact accepted replay or a conflicting duplicate.

- Exact replay returns the original immutable live batch and does not call the downstream
  sink again.
- A changed command under an accepted operation ID is rejected as
  `ConflictingDuplicate`.
- Different concrete equipment instances retain independent cooldown state.

## Stage 1 controller boundary

`Assets/ShooterMover/Production/Stage1/Stage1VisibleSliceController.cs` is intentionally not
modified. Final scene/controller composition can inject the active-equipment source and
Unity effect sink without moving weapon selection or firing logic back into that controller.

## Focused verification command

```bash
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.Weapons.Live.InventoryBackedWeaponExecutionAdapterTests \
  -testResults artifacts/test-results/WPN-LIVE-001-EditMode.xml \
  -logFile artifacts/test-results/WPN-LIVE-001-EditMode.log
```

Full compilation command:

```bash
Unity -batchmode -projectPath <project> -quit \
  -logFile artifacts/test-results/WPN-LIVE-001-Compile.log
```
