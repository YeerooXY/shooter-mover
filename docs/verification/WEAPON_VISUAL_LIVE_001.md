# WEAPON-VISUAL-LIVE-001 — positional concurrent weapon mounts

## Current production path

`Stage1WeaponPresentationRepairV1` remains the Level 1 effect-presentation consumer, but it does not own equipment selection.

- Every character owns separate holdings and positional exact-instance bindings.
- Striker has two mounts: Outer Left and Outer Right.
- Combat Medic has three mounts: Outer Left, Center, and Outer Right.
- Juggernaut has four mounts: Outer Left, Inner Left, Inner Right, and Outer Right.
- Inventory exposes only the selected character's physical mounts.
- One equipment instance cannot occupy two mounts simultaneously.
- Different owned instances may resolve to the same authored weapon definition.
- Confirm applies the bindings through `ProductionInventoryLoadoutAuthorityV1` and returns the exact route projection to Hub.
- Level 1 adopts the selected character holdings/loadout authorities rather than rebuilding them.
- One fire input executes every enabled mount. Each mount retains its own cooldown, replay state, deterministic seed, exact equipment ID, and muzzle origin.
- Number keys do not select a weapon in the production mounted path.
- Results freezes the same normalized profile payload and holdings authority used by weapon execution.

## Position behaviour

Physical position is authoritative rather than decorative.

- Outer mounts use wider lateral muzzle offsets.
- Inner mounts use narrower lateral muzzle offsets.
- The Combat Medic center mount uses the centerline.
- Swapping two exact Ironwake instances between outer and inner mounts changes their physical origins while preserving their equipment identities.

## Authored catalogue presentation

Every equipped instance resolves through:

```text
EquipmentInstance
    -> EquipmentCatalog definition
    -> runtime weapon reference
    -> WeaponCatalog definition
    -> live execution
```

Representative presentation cases are:

- Rattler: normal projectile and automatic/semi-auto/burst cadence;
- Ironwake: simultaneous spread;
- Voltspike: seeking projectile;
- Prismata: orb delivery;
- Crownfall: contact explosion;
- Nullstar: direct hit and damage over time.

The live effect layer may use shared projectile, trail, explosion, guidance, and damage-over-time presenters. Those presenters are mechanics adapters, not additional weapon definitions.

## Starter onboarding and migration

Fresh characters receive exactly one fresh Rattler MK1 instance for each required mount. They do not receive the complete catalogue and do not use global equipment-instance IDs.

Retired save equipment is deleted rather than translated. Invalid bindings are cleared, then the same onboarding policy fills required empty mounts with fresh exact instances. The migrated holdings/loadout pair is atomically saved before runtime restore.

## Automated coverage

### EditMode

- authored catalogue contains eighteen current equipment definitions and no retired definitions;
- every current weapon projects into equipment data;
- Striker, Combat Medic, and Juggernaut receive exactly 2, 3, and 4 starter instances;
- every equipped ID is owned and distinct;
- separate characters do not share generated IDs;
- inventory opens after fresh creation;
- exact loadout survives restore;
- retired holdings and bindings are removed;
- unrelated character components and valid current equipment are preserved;
- migration fills empty required mounts and is idempotent;
- character switching keeps holdings/loadouts separate;
- strongbox equipment grants continue to use current catalogue definitions.

### PlayMode

- one fire command executes mounted weapons together;
- lateral mount offsets produce distinct effect origins;
- each mount receives a distinct fire-operation identity;
- exact replay does not emit effects twice;
- inventory authority connection confirms and returns the exact runtime payload.

## Required runtime verification

1. Open the project with its pinned Unity editor and confirm zero compilation errors.
2. Run the focused Hub and persistence EditMode suites.
3. Run the focused inventory and live-weapon PlayMode suites.
4. Create Striker, Combat Medic, and Juggernaut characters and inspect exact starter counts.
5. Confirm a loadout, switch characters, restart, and verify separate exact bindings persist.
6. Open a strongbox that grants equipment and verify the granted definition is in the authored catalogue.
7. Enter Level 1 and verify every equipped exact instance resolves and fires from its physical mount.
8. Load a retired-equipment save and verify migration completes once without losing XP, wallets, scrap, skills, strongboxes, or valid current equipment.

No Unity compile, EditMode, PlayMode, XML, or manual runtime result is claimed until those commands are actually run.
