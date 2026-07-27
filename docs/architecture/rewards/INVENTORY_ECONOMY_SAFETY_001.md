# INVENTORY-ECONOMY-SAFETY-001

## Exact base and scope

- Starting `main` SHA: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`
- Branch: `agent/inventory-economy-safety-001`
- Primary planning evidence: `docs/audits/INVENTORY_ECONOMY_AUDIT_AND_ROADMAP.md` from draft PR #344
- Scope: safety gates and Inventory diagnostics only
- Out of scope: canonical augment transactions, armour Inventory, Shop/Crafting composition, core acquisition, overclock installation, currencies, strongbox changes and firing changes

## Requested visible behavior

When a production canonical weapon is selected in Inventory:

- the old generic augment-upgrade route is visibly blocked with a specific reason;
- overclock installation is visibly unavailable;
- a weapon carrying a non-empty unsupported overclock assignment is shown as blocked from live execution;
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
3. identifies only receipts whose runtime reference resolves to the production canonical weapon catalogue;
4. rejects them with `canonical-weapon-upgrade-route-unsupported` before issuing a quote.

Synthetic or historical generic equipment that does not resolve to the production canonical catalogue keeps the existing generic route.

### Generic upgrade confirmation

`AugmentUpgradeServiceV1.TryPrepare`

The same decision is recomputed from current catalogue and holding state before replacement construction and before `Execute` can spend money or remove holdings. A previously created or fabricated quote cannot bypass the gate.

### Production weapon rewards

`CanonicalFirstPlayerHoldingsAuthorityV2.TryResolveCanonicalMutation`

Canonical definition and overclock policy are validated before the canonical-first ownership commit. Rejection occurs before either canonical ownership or the immutable receipt ledger changes.

### Destructive canonical removal

Canonical removal resolves the exact current canonical definition before mutating ownership or its receipt. An unresolved canonical definition fails closed.

### Live execution

`CanonicalWeaponEquipmentProjectionLookupV2.TryResolve`

The exact owned canonical instance is resolved first. `LastAvailability` records the structured decision. Non-empty overclock assignments reject before any compatibility equipment projection is produced.

## Transaction behavior

### Generic canonical-weapon upgrade attempt

- Pre-commit changes: none
- Commit point: none; operation is rejected during quote or preparation
- Rollback: not required because no wallet, holdings, reward, assignment or mount authority was mutated
- Retry: deterministic rejection while the same unsupported state remains
- Compatibility receipt: remains immutable and cannot become canonical truth

### Canonical weapon reward

- Pre-commit validation: exact definition resolution and overclock safety policy
- Commit point: canonical `TryAdd`, followed by receipt write
- Receipt failure: existing snapshot compensation restores both authorities
- Retry: existing deterministic reward identities and authority replay semantics remain unchanged

### Live execution

- Commit point: none; lookup is read-only
- Unsupported state: no projection is returned and no fallback weapon is created
- Retry: succeeds only after authoritative state becomes supported

## Failure matrix

| Condition | Behavior |
|---|---|
| Production canonical receipt enters generic quote | Reject with `canonical-weapon-upgrade-route-unsupported` |
| Old/fabricated quote enters confirmation | Re-evaluate and reject before spend/remove |
| Canonical reward definition is unresolved | Reject before canonical or receipt mutation |
| Canonical reward carries overclock assignment | Reject before canonical or receipt mutation |
| Canonical destructive removal definition is unresolved | Reject before removal |
| Live instance is missing | Reject; no fallback |
| Live definition is unresolved | Reject with structured decision |
| Live instance carries overclock assignment | Reject with `canonical-weapon-overclock-policy-unsupported` |
| Immutable augment receipt does not match exact assignment IDs | Reject; no projection |
| Duplicate supported reward/removal | Existing exact replay behavior remains authoritative |
| Restart after rejected upgrade | No partial state exists to restore |

## Runtime, editor, persistence and assembly boundaries

- Domain policy contains no Unity, editor, persistence or application dependencies.
- Application services consume the policy at existing transaction boundaries.
- The Unity live adapter exposes read-only diagnostics and remains a projection.
- The Inventory overlay is attached by the existing production Hub/Inventory composition and performs no mutation.
- No schema version, serialized field, generated asset or migration path changes.
- One Unity `.meta` file is added only for the focused EditMode regression file.

## Production caller paths

```text
Inventory route
→ ProductionHubLoadoutCompositionV1.BindInventoryScene
→ InventoryLoadoutScreenControllerV1 canonical snapshot
→ InventoryEconomySafetyOverlayV1
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
Live firing resolution
→ CanonicalWeaponEquipmentProjectionLookupV2.TryResolve
→ CanonicalWeaponSafetyPolicyV1.EvaluateLiveExecution
→ compatibility projection only when supported
```

## Manual Unity acceptance route

1. Import/compile the project in Unity.
2. Enter the production Hub with an account-backed character that owns a canonical weapon.
3. Open Inventory.
4. Select a canonical weapon.
5. Confirm the disabled `AUGMENT UPGRADE — BLOCKED` control and the specific canonical-upgrade rejection.
6. Confirm overclock installation is marked unavailable.
7. Attempt the historical generic upgrade route through a controlled harness and confirm wallet balance/sequence, generic holdings, canonical holdings, assignments and mounts do not change.
8. Load a controlled canonical snapshot containing one non-empty overclock assignment.
9. Confirm Inventory reports live execution blocked and the live projection returns `canonical-weapon-overclock-policy-unsupported` without a fallback.
10. Verify normal selection, equip, replace and unequip still work for supported weapons.
11. Enter the authored playable level and verify an ordinary supported equipped weapon still fires.
12. Restart and verify the rejected operation left no persistent partial state.

## Validation evidence policy

Static source inspection, compilation, automated tests, Unity framework execution and manual gameplay are reported separately in the pull request. Authored tests are not described as passing unless they were actually executed.
