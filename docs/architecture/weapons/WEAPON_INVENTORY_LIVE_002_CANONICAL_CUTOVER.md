# WEAPON-INVENTORY-LIVE-002 — canonical weapon holdings cutover

## Branch and base

- Branch: `agent/weapon-inventory-live-002`
- Target: `main`
- Exact starting `main` SHA: `7defe07dfea16a4435567f0dc053b195d6b5705e`
- The branch was created directly from that SHA.
- Final pre-PR comparison: branch ahead of `main`, zero commits behind, with the same merge base.

## Canonical ownership chain

Production weapon ownership now resolves through this chain:

```text
InstanceId
→ ProductionWeaponHoldingsAuthorityV2
→ WeaponEquipmentInstance
→ WeaponDefinitionId
→ ProductionWeaponCatalogProvider / WeaponCatalog
→ exact effective weapon projection
```

`WeaponEquipmentInstance` remains the only weapon-instance state stored by schema V2:

```text
InstanceId
WeaponDefinitionId
AugmentAssignments
OverclockAssignments
```

Generic equipment metadata such as item level, quality, acquisition source, loot source, timestamps, sequence numbers, or copied display names is not added to canonical weapon holdings.

The exact loadout stores only the existing mount/slot bridge and the exact instance identity:

```text
MountId → loadout slot bridge → InstanceId
```

## Persistence and migration

A new save component is registered with:

```text
ComponentId: save-component.weapon-holdings
SchemaVersion: 2
ContentVersion: weapon-holdings-explicit-v2
```

The V2 codec writes deterministic, sorted canonical instances and fingerprints the full exact-instance payload, including augment and overclock assignment IDs.

Restore behavior is dual-read:

1. If the schema-V2 component exists, it is decoded and imported directly.
2. If it does not exist, the retained schema-V1 generic holdings snapshot is read deterministically.
3. Weapon entries are converted without changing their opaque instance IDs.
4. Existing generic holdings and immutable reward receipts are not rewritten.
5. The next normal character save emits the V2 component.

Migration is idempotent because conversion is a pure projection of the same immutable V1 payload, while `ProductionWeaponHoldingsAuthorityV2.TryAdd` treats an exact duplicate as no change.

Accepted future generic weapon rewards are projected into canonical holdings by `CanonicalizingPlayerHoldingsAuthorityV2` using the same instance ID. The generic authority remains the immutable receipt ledger; it is no longer the source of truth for owned weapon selection.

## Class physical mount projection

Only physical mounts authored by `ProductionWeaponMountPolicyV1` are rendered.

| Class | Physical projection | Active | Locked |
|---|---|---:|---:|
| Aggressive | Outer Left, Center, Outer Right | 2 | Center |
| Healer | Outer Left, Center, Outer Right | 3 | none |
| Defensive | Outer Left, Inner Left, Inner Right, Outer Right | 4 | none |

The aggressive center mount is visible, has no instance ID, receives no starter, rejects Equip, and exposes the skill-required reason. Nonexistent mounts are absent rather than rendered as placeholders.

Empty active mounts are valid. `ProductionWeaponMountSetV1.ConfiguredBindings` represents all active physical mounts, while `EnabledBindings` contains only active mounts with an exact instance bound.

## Starter onboarding

Starter creation occurs only in `ProductionWeaponOnboardingV2.CreateStarter` during fresh character construction.

```text
active baseline mount count = X
→ allocate X opaque IDs
→ create X distinct unmodified starter WeaponEquipmentInstance values
→ add all X to that character's canonical holdings
→ bind each exact ID to its corresponding active mount
```

Current counts:

- Aggressive: 2
- Healer: 3
- Defensive: 4

The Inventory screen never calls starter onboarding or any repair/grant path.

## Inventory screen

The real `InventoryLoadoutScreenControllerV1` production composition now connects to:

- generic immutable receipts/non-weapon holdings;
- canonical V2 weapon holdings;
- exact loadout authority;
- class physical mount layout;
- weapon catalogue.

It renders:

### EQUIPPED WEAPONS

Only class-defined physical mounts, including the visible aggressive locked center.

### OWNED WEAPONS

One card per exact `WeaponEquipmentInstance`. Multiple unmodified Rattler MK1 instances remain separately selectable because cards are keyed by `InstanceId`, not by definition.

### SELECTED WEAPON

Resolved display/family/definition information, augment IDs, overclock IDs, a debug-labelled exact instance ID, and explicit Equip/Unequip actions.

Opening or refreshing the screen performs only reads and draft reconstruction. It does not mutate holdings.

## Equip and unequip semantics

Equip validates that:

- the selected exact instance exists in this character's canonical holdings;
- the target is a physical mount owned by the class;
- the target mount is active;
- the selected instance is not bound elsewhere;
- the slot is a weapon-accepting production mount.

Replacing an occupied mount changes only the draft/loadout binding. The displaced instance and replacement instance both remain in canonical holdings.

Unequip clears only the mount binding. It does not remove, recreate, replace, or re-equip any weapon instance.

Confirm fingerprints canonical holdings before and after authority application and rejects any unexpected holdings mutation.

## Gameplay exact-instance resolution

`ProductionPlayerLoadoutRuntimeV1.TryResolveFirstActiveEquippedWeapon` scans class-authored active physical mounts in order, skips valid empty mounts, and resolves the first bound `InstanceId` through canonical holdings.

`PlayerInventoryWeaponRuntimeCompositionRoot.CreateCanonical` then:

1. resolves that exact canonical instance;
2. verifies the route projection contains the same exact ID;
3. resolves its `WeaponDefinitionId` through the production weapon catalogue;
4. creates the retained scheduler compatibility projection with the same exact instance ID;
5. never substitutes another held weapon and never fabricates a scene-local Rattler.

`ProductionCanonicalWeaponGameplayBindingV2` also binds the spawned playable character to the exact canonical first-mount instance and definition in the playable scene.

For migrated augment assignments, immutable V1 receipt payloads may supply the existing `AugmentInstance` details by the same exact instance ID. Canonical holdings still decide ownership, definition, assignments, and selection. Overclock assignment IDs are persisted and displayed; because the retained scheduler currently has no overclock execution policy, non-empty overclock assignments fail closed rather than being silently ignored.

## Automated coverage added or updated

EditMode coverage includes:

- physical/active/locked mount counts for every class;
- aggressive locked-center visibility and Equip rejection;
- exact starter counts and distinct opaque IDs;
- empty active mounts;
- no grants on Inventory open or refresh;
- exact unequip/re-equip/replacement behavior;
- displaced instance retention;
- duplicate-definition cards remaining distinct;
- deterministic V1 migration preserving opaque IDs and receipt fingerprints;
- V2 codec round-trip including assignment IDs;
- character ownership isolation;
- schema-V2 restart preserving holdings, loadout, and exact first-mount selection.

PlayMode coverage includes:

- production Inventory controller connected to canonical authorities;
- replacement of slot one with another exact held instance;
- exact live projection of that newly equipped instance;
- explicit proof that an unknown instance ID does not produce a fallback weapon.

## Execution evidence

This change was authored through the connected GitHub repository interface. That environment does not provide the Unity project checkout or a Unity Editor executable, so the following results are intentionally not claimed:

| Evidence | Result |
|---|---|
| Unity script compilation | Not run in this environment |
| EditMode test execution | Not run in this environment |
| PlayMode test execution | Not run in this environment |
| Manual gameplay run | Not run in this environment |

Static evidence completed before opening the draft PR:

- branch comparison against `main`;
- merge base verified as `7defe07dfea16a4435567f0dc053b195d6b5705e`;
- branch verified zero commits behind at pre-PR check;
- all new Unity C# sources supplied with stable `.meta` files;
- exact-instance migration, Inventory, persistence, and gameplay paths traced through their production composition points;
- focused EditMode and PlayMode coverage committed for Unity/CI execution.

## Manual acceptance runbook

For a fresh Aggressive, Healer, and Defensive character:

1. Verify only the class's physical mounts are visible.
2. Verify active and skill-locked states.
3. Verify starter count equals active baseline mount count.
4. Verify every starter has a distinct opaque ID.
5. Unequip one weapon and confirm it remains under Owned Weapons.
6. Save/confirm and reopen Inventory; confirm no new weapon appears.
7. Re-equip the same exact instance ID.
8. Free a second instance, replace an occupied mount with it, and confirm the displaced instance remains owned.
9. Enter combat and inspect `ProductionCanonicalWeaponGameplayBindingV2.ExactWeaponInstanceId`; it must equal the newly equipped first-mount instance.
10. Switch characters and verify the prior character's exact IDs are absent.
11. Restart the application and verify holdings, bindings, assignments, and IDs are unchanged.
12. Reopen Inventory repeatedly and verify holdings count and fingerprint remain stable.
