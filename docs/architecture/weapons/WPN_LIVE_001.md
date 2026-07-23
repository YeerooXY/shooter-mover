# WPN-LIVE-001 — Inventory-backed live weapon execution

## Launch boundary

- Base: `f00482a2a86232275517e8b992a9f290be07a152`
- Branch: `agent/wpn-live-001-inventory-execution`
- Target: `main`

## Corrected runtime flow

`PlayerRouteProfilePayloadV1` exact four-slot loadout
→ selected concrete equipment-instance identity
→ immutable `InventoryWeaponFireRequest` locking that identity
→ sequence-cached exact equipment lookup over `IPlayerHoldingsAuthorityV1`
→ equipment definition/runtime weapon reference
→ JSON-derived `WeaponCatalog` definition
→ `WeaponExecutionCore.TryExecute`
→ canonical atomic `WeaponEffectBatch`
→ transactional Unity effect emission.

No fire retry asks “which weapon is active now?” The concrete equipment identity is captured when the
fire intent is created and remains part of the command fingerprint.

## Operation and cooldown ownership

WPN-CORE-002 now keeps two deliberately different scopes:

- accepted fire-operation admission is keyed by actor, lifecycle generation, and operation identity;
- cooldown and shot sequence remain keyed by actor, concrete equipment instance, and lifecycle generation.

Consequences:

- an exact retry of the original locked request returns `ReplayAccepted` and emits nothing twice;
- reusing the operation identity with another equipment instance returns `ConflictingDuplicate`;
- two legitimate equipment instances still have independent cooldowns;
- a new lifecycle generation receives a fresh operation and cooldown scope.

## Canonical DoT and pool effects

Damage-over-time support no longer uses a zeroed catalog projection or side metadata. WPN-CORE owns a
first-class `DamageOverTimeProjectileEffect` containing:

- origin and locked direction;
- speed and range;
- direct damage and pierce;
- DoT DPS and duration;
- persistent pool radius and duration;
- knockback and damage type;
- full actor, participant, equipment, definition, operation, lifecycle, shot, and ordinal identity.

Those fields are included in `ToCanonicalString()`, core validation, and the immutable batch fingerprint.
Definitions with incomplete DoT/pool pairs fail closed. The built-in DoT behavior is selected from catalog
facts without weapon-name or weapon-ID branching.

## Holdings hot-path lookup

`PlayerHoldingsEquipmentInstanceLookup` checks the authority sequence on each lookup and exports/canonicalizes
a holdings snapshot only when that sequence changes. Repeated high-rate fire against unchanged holdings is a
dictionary lookup rather than a full holdings, stack, transaction, ledger, and fingerprint rebuild.

## Production composition

The production-compatible composition is independent of any deleted demo controller:

- `RouteProfileActiveWeaponSource` consumes the real immutable route/loadout payload and owns only active-slot selection;
- `PlayerRuntimeWeaponStateAdapter` projects actor, participant, and lifecycle facts from the real `PlayerRuntimeComposition`;
- `PlayerInventoryWeaponRuntimeCompositionRoot` creates the lookup, adapter, intent factory, active-slot source, and runtime;
- `InventoryWeaponEffectEmitter2D` stages the complete core batch under an inactive Unity root, configures every effect, and activates the root only after all effects succeed;
- canonical DoT projectiles create physical `InventoryWeaponPersistentDamageArea2D` pool objects from the core effect description.

The emitter is idempotent by actor/lifecycle/operation and rejects a conflicting batch fingerprint. It does
not create health, inventory, loadout, cooldown, replay, or weapon-definition authority.

## Focused verification commands

Cold Unity script compilation:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath <project> \
  -quit -logFile artifacts/test-results/WPN-LIVE-001-Compile.log
```

Focused EditMode:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath <project> \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.Weapons \
  -testResults artifacts/test-results/WPN-LIVE-001-EditMode.xml \
  -logFile artifacts/test-results/WPN-LIVE-001-EditMode.log
```

Focused PlayMode:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath <project> \
  -runTests -testPlatform PlayMode \
  -testFilter ShooterMover.Tests.PlayMode.Weapons.Live.InventoryWeaponRuntimePlayModeTests \
  -testResults artifacts/test-results/WPN-LIVE-001-PlayMode.xml \
  -logFile artifacts/test-results/WPN-LIVE-001-PlayMode.log
```

Unity 6.3 test commands intentionally omit `-quit` because `-runTests` exits automatically and `-quit` may
terminate before the Test Framework begins.

## Proof status

The connector authoring environment has no Unity editor or repository checkout, so it cannot generate valid
Unity compilation logs or XML. The PR must remain draft and non-merge-ready until a Unity-capable runner
executes the exact reviewed head and attaches zero-failure XML/log evidence.
