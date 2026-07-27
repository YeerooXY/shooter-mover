# WEAPON-INVENTORY-LIVE-002 — canonical weapon holdings and mount cutover

## Branch and base

- Branch: `agent/weapon-inventory-live-002`
- Target: `main`
- Exact starting `main` SHA: `7defe07dfea16a4435567f0dc053b195d6b5705e`
- The branch was created directly from that SHA.
- The draft PR remains unmerged and does not enable auto-merge.

## Canonical weapon state

Production weapon ownership now resolves through:

```text
InstanceId
→ ProductionWeaponHoldingsAuthorityV2
→ WeaponEquipmentInstance
→ WeaponDefinitionId
→ ProductionWeaponCatalogProvider / WeaponCatalog
```

`WeaponEquipmentInstance` stores only:

```text
InstanceId
WeaponDefinitionId
AugmentAssignments
OverclockAssignments
```

It does not copy item level, quality, acquisition source, loot source, timestamps,
creation order, presentation names, class, mount, or semantic identities into the
canonical weapon instance.

## Canonical physical mount state

The equipped weapon authority is now independent from the retained generic slot
array:

```text
WeaponMountLoadoutSnapshotV2
└── MountId → InstanceId
```

It contains exactly one entry per physical mount authored for the character class.
It does not persist nonexistent weapon-slot placeholders.

The retained `InventoryLoadoutAuthoritySnapshotV1` remains only as:

- an armor loadout authority;
- a route compatibility projection for consumers that still read four route slots;
- deterministic V1 migration input.

V2 saves write all legacy weapon-slot entries as null. Weapon equipped truth is
read exclusively from `WeaponMountLoadoutSnapshotV2`.

## Persistence components

### Canonical holdings

```text
ComponentId: save-component.weapon-holdings
SchemaVersion: 2
ContentVersion: weapon-holdings-explicit-v2
```

### Canonical physical mount loadout

```text
ComponentId: save-component.weapon-mount-loadout
SchemaVersion: 2
ContentVersion: weapon-mount-loadout-explicit-v2
```

Both codecs produce deterministic sorted payloads and fingerprint their complete
canonical state.

Restore behavior is dual-read:

1. Decode canonical holdings V2 when present.
2. Otherwise convert weapon entries from immutable V1 generic holdings while
   preserving every opaque exact instance ID.
3. Decode the physical mount V2 component when present.
4. Otherwise migrate each class-authored physical mount from its legacy route-slot
   bridge.
5. Reject corrupt V2 data rather than silently repairing it.
6. Write canonical holdings and physical mount components on the next normal save.
7. Write only armor state into the retained generic loadout component.

The serialized restart test round-trips the holdings, mount and armor payloads
through their actual codecs before rebuilding the runtime. It verifies that the
restored first physical mount contains the same exact instance and that reopening
Inventory does not grant or repair anything.

## Canonical-first rewards

Reward and strongbox systems may still deliver a generic equipment command at the
compatibility edge. `CanonicalFirstPlayerHoldingsAuthorityV2` handles weapon
commands in this order:

```text
validate/convert exact weapon payload
→ commit canonical WeaponEquipmentInstance ownership
→ write immutable generic reward receipt
→ rollback canonical ownership if the receipt write rejects or throws
```

The generic holdings record is therefore a receipt, not a second weapon ownership
authority. A rejected receipt can no longer leave a ghost weapon in only one side
of the system.

Non-weapon holdings continue through the existing generic authority unchanged.

## Class physical mount projection

| Class | Physical projection | Active | Locked |
|---|---|---:|---:|
| Aggressive | Outer Left, Center, Outer Right | 2 | Center |
| Healer | Outer Left, Center, Outer Right | 3 | none |
| Defensive | Outer Left, Inner Left, Inner Right, Outer Right | 4 | none |

The Aggressive center mount is visible, has no instance ID, receives no starter,
rejects Equip, and reports the skill requirement.

`ProductionWeaponMountPolicyV1.ResolveLayout` now fails closed for an unsupported
or malformed profile ID. It no longer silently gives an unknown class the
Defensive four-mount layout and four starter weapons.

The authored production IDs are covered by the exact IDs and by these established
character-profile suffixes:

```text
loadout-profile.<character>-aggressive
loadout-profile.<character>-healer
loadout-profile.<character>-defensive
```

## Starter onboarding

Starter creation runs only in `ProductionWeaponOnboardingV2.CreateStarter` during
fresh character construction:

```text
active physical mount count = X
→ allocate X opaque exact IDs
→ create X distinct unmodified WeaponEquipmentInstance values
→ add all X to the character's canonical holdings
→ bind each exact ID directly to its physical MountId
```

Current counts:

- Aggressive: 2
- Healer: 3
- Defensive: 4

Inventory never calls onboarding or a repair/grant path.

## Inventory

The production Inventory connects to:

- generic immutable receipts and non-weapon holdings;
- canonical weapon holdings;
- the one character-local physical mount authority;
- the retained armor/route projection;
- the class physical mount layout;
- the weapon catalogue.

It renders only class-authored physical mounts and one card per exact owned weapon.
Two weapons with the same definition remain separate cards because identity is the
opaque `InstanceId`.

Equip and unequip modify the canonical physical mount draft. Confirm commits the
physical mount authority first, updates the retained compatibility projection
second, and rolls both authorities back if the compatibility update rejects or
mismatches. Canonical holdings are fingerprinted before and after confirm and must
remain unchanged.

Replacing a mount never destroys the displaced exact instance. Unequip clears only
one `MountId → InstanceId` binding.

The selected panel resolves display name, family and definition and shows exact
augment and overclock assignment IDs plus a debug-labelled exact instance ID. A
future presentation-only pass may expand this panel with additional catalogue
combat statistics; that is not an ownership, persistence or gameplay authority.

## Gameplay handoff

`ProductionPlayerLoadoutRuntimeV1.TryResolveFirstActiveEquippedWeapon` scans the
class-authored active physical mounts in order and resolves the first bound exact
instance through canonical holdings.

When the playable player object is spawned,
`ProductionCanonicalWeaponGameplayBindingV2` now attaches one
`CanonicalPlayerWeaponSourceV2` directly to that player object. The source:

1. is bound once to the selected character and exact first-mount instance;
2. verifies the instance is owned by that character;
3. verifies its canonical `WeaponDefinitionId` against the catalogue mark;
4. resolves the retained live equipment projection using the same exact ID;
5. rejects unknown IDs and non-executable assignment states without a fallback;
6. cannot silently rebind to a different weapon during that player lifecycle.

The current `main` playable scene does not yet contain a player firing loop or an
`IInventoryWeaponEffectBatchSink`; it contains player movement and room traversal.
This change therefore establishes the authoritative spawned-player weapon source
and verifies its live executable projection without inventing a fake effect sink.
When the firing loop is composed, it must consume this player-local source or
`PlayerInventoryWeaponRuntimeCompositionRoot.CreateCanonical` rather than create a
scene weapon.

Migrated augment assignments may recover immutable V1 `AugmentInstance` payloads
by the same exact weapon identity. Overclock IDs are persisted and displayed, but
non-empty overclock assignments fail closed because the retained scheduler has no
overclock execution policy yet.

## Automated coverage

EditMode coverage includes:

- physical, active and locked mount counts for every class;
- unsupported profiles failing closed;
- exact starter counts and distinct opaque IDs;
- physical mount-keyed starter bindings;
- locked-center visibility and Equip rejection;
- empty active mounts;
- no Inventory-time grants or repairs;
- exact unequip, re-equip and replacement behavior;
- displaced instance retention;
- duplicate-definition cards remaining distinct;
- canonical-first reward ownership plus immutable receipt creation;
- deterministic V1 holdings and mount migration;
- holdings and mount codec round-trips;
- serialized V2 restart preserving the exact first physical mount;
- legacy V2-save weapon slots remaining null;
- character ownership isolation.

PlayMode coverage includes:

- production Inventory connected to the canonical mount authority;
- replacing the first physical mount with another exact held instance;
- binding that exact instance to the spawned player object;
- resolving the live compatibility equipment with the same exact ID;
- explicit proof that an unknown instance ID produces no fallback weapon.

## Execution evidence

This change was authored through the connected GitHub repository interface. That
environment does not provide a Unity checkout or Unity Editor executable, so the
following are intentionally **not claimed as run**:

| Evidence | Result |
|---|---|
| Unity script compilation | Not run in this environment |
| EditMode tests | Not run in this environment |
| PlayMode tests | Not run in this environment |
| Manual gameplay | Not run in this environment |

Static checks completed:

- exact starting and merge-base SHA retained;
- branch remains zero commits behind `main` at the latest comparison;
- new Unity C# files include stable `.meta` files;
- authored class profile IDs match the fail-closed layout resolver;
- all production ownership, mount, save, Inventory and spawned-player seams were
  traced after the self-audit repairs;
- focused tests were updated for Unity/CI execution.

## Manual acceptance runbook

For a fresh Aggressive, Healer and Defensive character:

1. Verify only the class's physical mounts appear.
2. Verify active and skill-locked states.
3. Verify starter count equals the active physical mount count.
4. Verify every starter has a distinct opaque ID.
5. Unequip one weapon and confirm it remains under Owned Weapons.
6. Confirm/save and reopen Inventory; verify no new weapon appears.
7. Re-equip the same exact instance.
8. Free a second instance and replace the first physical mount with it.
9. Verify the displaced instance remains owned.
10. Enter the playable scene and inspect the spawned player's
    `CanonicalPlayerWeaponSourceV2.ExactWeaponInstanceId`; it must equal the newly
    equipped first-mount instance.
11. Call `TryResolveLiveEquipment` and verify the returned equipment uses the same
    exact ID and canonical weapon definition.
12. Switch characters and verify exact IDs are isolated.
13. Restart the application and verify holdings, physical mount bindings,
    assignments and IDs are unchanged.
14. Reopen Inventory repeatedly and verify both canonical fingerprints remain
    stable.
