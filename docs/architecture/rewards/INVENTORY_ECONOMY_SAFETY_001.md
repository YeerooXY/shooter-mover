# INVENTORY-ECONOMY-SAFETY-001

## Exact base and scope

- Starting `main` SHA: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`
- Branch: `agent/inventory-economy-safety-001`
- Primary planning evidence: `docs/audits/INVENTORY_ECONOMY_AUDIT_AND_ROADMAP.md` from draft PR #344
- Scope: safety gates and integrated Inventory diagnostics only
- Out of scope: canonical augment transactions, armour Inventory, Shop/Crafting composition, core acquisition, overclock installation, currencies, strongbox changes and weapon mechanics

## Requested visible behavior

When a production canonical weapon is selected in Inventory:

- the old generic augment-upgrade route is visibly blocked with a specific reason;
- overclock installation is visibly unavailable;
- a weapon carrying a non-empty unsupported overclock assignment is shown as blocked from live execution;
- the diagnostic is rendered inside the existing selected-weapon panel rather than in a detached window;
- ordinary weapon browsing, selection, equip, unequip and supported firing remain unchanged.

## Authority ledger

| Concept | Authoritative owner | Compatibility/projection role |
|---|---|---|
| Exact owned weapon | `ProductionWeaponHoldingsAuthorityV2` | Generic `EquipmentInstance` remains an immutable reward/strongbox receipt only |
| Equipped exact weapon | `ProductionWeaponMountLoadoutAuthorityV2` | Legacy loadout projection remains compatibility/navigation state |
| Weapon definition | Canonical `WeaponDefinitionId` resolved by `ProductionWeaponCatalogProvider` | `EquipmentDefinition.RuntimeWeaponReferenceId` links the generic receipt projection |
| Generic augment upgrade | `AugmentUpgradeServiceV1` for non-canonical generic equipment only | It must never replace a canonical weapon receipt |
| Unsupported-operation decision | `CanonicalWeaponSafetyPolicyV1` | Inventory and live adapters consume its structured result; neither becomes an authority |
| Live compatibility projection | `CanonicalWeaponEquipmentProjectionLookupV2` | May read immutable augment receipt payloads but cannot write ownership or assignments |

## Explicit design decisions

### Generic upgrade mutation model

The existing generic upgrade service keeps its established immutable-replacement model for supported generic equipment. It creates a deterministic replacement, retires the old generic holding and applies the replacement through the existing reward route.

That model is **not** adopted for canonical weapons in this PR. A production-catalog weapon receipt is rejected at quote time and rechecked at confirmation preparation before money, holdings or reward mutation.

### Augment assignment identity

Current canonical weapon `AugmentAssignments` are treated as exact owned augment-instance identities, not definition IDs. This follows the existing migrated receipt projection, which matches canonical assignment IDs to `AugmentInstance.InstanceId` values in the immutable generic receipt.

This PR does not create augment ownership or installation authority.

### Overclock shape

Whether overclocks are singular, slotted or capacity-based remains intentionally undecided. No slot count, capacity, replacement or stacking rule is inferred.

Therefore:

- installation availability is rejected;
- runtime canonical additions reject any instance carrying non-empty overclock assignments;
- production reward admission rejects any future canonical reward carrying non-empty overclock assignments;
- live execution rejects any canonical instance with non-empty overclock assignments;
- Inventory displays the structured rejection.

### Future core ownership

Character-bound versus account-wide core ownership remains intentionally undecided. No core holding, wallet, inventory or persistence authority is introduced.

## Trusted guard boundaries

### Generic upgrade quote

`AugmentUpgradeServiceV1.Quote`

1. resolves the generic holding;
2. resolves its equipment definition;
3. classifies production-catalog weapon receipts;
4. rejects them with `canonical-weapon-upgrade-route-unsupported` before issuing a quote.

Synthetic or historical generic equipment outside the authoritative production catalogue keeps the existing generic route.

### Generic upgrade confirmation

`AugmentUpgradeServiceV1.TryPrepare`

The same decision is recomputed from current catalogue and holding state before replacement construction and before `Execute` can spend money or remove holdings. A previously created or fabricated quote cannot bypass the gate.

### Canonical holdings writer

`ProductionWeaponHoldingsAuthorityV2`

- `TryAdd` resolves the canonical definition and evaluates reward/admission safety before a new exact instance can enter runtime authority state.
- `TryRemove` resolves the exact current canonical definition before destructive mutation.
- rejected runtime writes return a structured rejection code and leave the sequence and snapshot unchanged.
- `ImportSnapshot` remains the separate restore boundary. It preserves a valid persisted snapshot so unsupported historical state can be diagnosed, blocked from execution and recovered deliberately rather than silently discarded.

### Production weapon rewards and receipts

`CanonicalFirstPlayerHoldingsAuthorityV2.TryResolveCanonicalMutation`

Canonical definition and overclock policy are validated before the canonical-first ownership commit. Rejection occurs before either canonical ownership or the immutable receipt ledger changes.

When canonical ownership is missing but an exact retained equipment receipt exists, the receipt must be classified before removal. An unresolved definition fails closed instead of falling through to generic deletion; a recognized weapon receipt reports canonical/receipt drift.

### Live execution

`CanonicalWeaponEquipmentProjectionLookupV2.TryResolve`

The exact owned canonical instance is resolved first. `LastAvailability` records the structured decision. Non-empty overclock assignments reject before any compatibility equipment projection is produced.

`CanonicalPlayerWeaponSourceV2.TryResolveLiveEquipment` propagates that exact rejection code to the gameplay caller and stores the fuller diagnostic message. It creates no fallback weapon.

## Transaction behavior

### Generic canonical-weapon upgrade attempt

- Pre-commit changes: none
- Commit point: none; operation is rejected during quote or preparation
- Rollback: not required because no wallet, holdings, reward, assignment or mount authority was mutated
- Retry: deterministic rejection while the same unsupported state remains
- Compatibility receipt: remains immutable and cannot become canonical truth

### Canonical weapon reward or direct runtime add

- Pre-commit validation: exact definition resolution and overclock safety policy
- Commit point: canonical `TryAdd`, followed by receipt write for the reward route
- Receipt failure: existing snapshot compensation restores both authorities
- Direct rejection: canonical sequence and snapshot remain unchanged
- Retry: supported existing reward identities retain their existing replay behavior; unsupported state remains rejected

### Canonical weapon removal

- Existing canonical instance: definition resolution occurs before removal
- Missing canonical instance with retained receipt: receipt classification occurs before generic removal
- Unresolved or ambiguous state: reject without changing either authority
- Exact duplicate after both records are absent: remains an idempotent no-change path

### Live execution

- Commit point: none; lookup is read-only
- Unsupported state: no projection is returned and no fallback weapon is created
- Diagnostic: exact structured rejection code reaches the gameplay source caller
- Retry: succeeds only after authoritative state becomes supported

## Failure matrix

| Condition | Behavior |
|---|---|
| Production canonical receipt enters generic quote | Reject with `canonical-weapon-upgrade-route-unsupported` |
| Old/fabricated quote enters confirmation | Re-evaluate and reject before spend/remove |
| Direct canonical add definition is unresolved | Reject before canonical mutation |
| Direct canonical add carries overclock assignment | Reject with `canonical-weapon-overclock-policy-unsupported` before mutation |
| Canonical reward definition is unresolved | Reject before canonical or receipt mutation |
| Canonical reward carries overclock assignment | Reject before canonical or receipt mutation |
| Existing canonical destructive removal definition is unresolved | Reject before removal |
| Canonical ownership missing, retained receipt definition unresolved | Reject before generic receipt removal |
| Canonical ownership missing, retained receipt is a known weapon | Reject as authority drift |
| Live instance is missing | Reject; no fallback |
| Live definition is unresolved | Reject with structured decision |
| Live instance carries overclock assignment | Reject with `canonical-weapon-overclock-policy-unsupported` |
| Immutable augment receipt does not match exact assignment IDs | Reject; no projection |
| Duplicate supported reward/removal | Existing exact replay behavior remains authoritative |
| Restart after rejected upgrade | No partial state exists to restore |

## Runtime, editor, persistence and assembly boundaries

- Domain policy contains no Unity, editor, persistence or application dependencies.
- Application services consume the policy at transaction boundaries.
- The canonical holdings authority enforces runtime add and destructive-removal admission itself; compatibility wrappers are not the only guard.
- The Unity live adapter exposes read-only diagnostics and remains a projection.
- Inventory renders the warning inside the existing selected-weapon scroll panel and performs no mutation.
- No detached overlay component or overlapping screen-space window is installed.
- No schema version, serialized field, generated asset or migration path changes.
- One Unity `.meta` file is added only for the focused EditMode regression file.

## Production caller paths

```text
Inventory route
→ ProductionHubLoadoutCompositionV1.BindInventoryScene
→ InventoryLoadoutScreenControllerV1.DrawSelectedWeapon
→ DrawCanonicalSafety
→ CanonicalWeaponSafetyPolicyV1
```

```text
Generic upgrade caller
→ AugmentUpgradeServiceV1.Quote / Confirm
→ EvaluateGenericUpgradeAvailability
→ CanonicalWeaponSafetyPolicyV1
→ reject before AugmentUpgradeExecutionV1
```

```text
Production reward or direct runtime write
→ CanonicalFirstPlayerHoldingsAuthorityV2 / direct canonical caller
→ ProductionWeaponHoldingsAuthorityV2.TryAdd / TryRemove
→ CanonicalWeaponSafetyPolicyV1 or definition-resolution guard
→ commit only when supported
```

```text
Live firing resolution
→ CanonicalPlayerWeaponSourceV2.TryResolveLiveEquipment
→ CanonicalWeaponEquipmentProjectionLookupV2.TryResolve
→ CanonicalWeaponSafetyPolicyV1.EvaluateLiveExecution
→ exact rejection code or supported compatibility projection
```

## Focused regression coverage authored

- canonical production receipt quote rejection with unchanged wallet and holdings;
- fabricated quote confirmation rejection with unchanged wallet and holdings;
- unsupported overclock fixture rejected at the live projection boundary;
- unmodified canonical weapon remains policy-eligible;
- direct canonical add with an overclock assignment is rejected without mutation;
- direct canonical removal with an unresolved definition is rejected without mutation;
- retained ambiguous receipt cannot fall through to generic removal and both authority sequences remain unchanged.

These tests are authored evidence only until executed by Unity/NUnit.

## Manual Unity acceptance route

1. Import/compile the project in Unity.
2. Enter the production Hub with an account-backed character that owns a canonical weapon.
3. Open Inventory.
4. Select a canonical weapon.
5. Confirm the disabled `AUGMENT UPGRADE — BLOCKED` control and the specific canonical-upgrade rejection appear inside the selected-weapon panel.
6. Confirm the warning scrolls with selected-weapon details and does not cover Equip, Unequip, Refresh, Confirm or Back controls.
7. Confirm overclock installation is marked unavailable.
8. Attempt the historical generic upgrade route through a controlled harness and confirm wallet balance/sequence, generic holdings, canonical holdings, assignments and mounts do not change.
9. Load a controlled canonical snapshot containing one non-empty overclock assignment.
10. Confirm Inventory reports live execution blocked and the gameplay source returns `canonical-weapon-overclock-policy-unsupported` without a fallback.
11. Verify normal selection, equip, replace and unequip still work for supported weapons.
12. Enter the authored playable level and verify an ordinary supported equipped weapon still fires.
13. Restart and verify the rejected operation left no persistent partial state.

## Validation evidence policy

Static source inspection, compilation, automated tests, Unity framework execution and manual gameplay are reported separately in the pull request. Authored tests are not described as passing unless they were actually executed.
