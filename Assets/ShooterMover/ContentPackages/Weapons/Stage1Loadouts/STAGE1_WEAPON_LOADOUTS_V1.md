# Stage 1 Weapon Loadout Fixtures v1

## Purpose

WP-008 provides evidence-only four-slot comparisons for the five approved Stage 1
weapon packages. It does not create a production inventory, loadout screen, reward,
economy, save, shop, or weapon-modifier system.

The accepted package roster is loaded from the real package descriptors and passed
through `Stage1WeaponPackageValidator.Validate`. The catalog therefore fails closed
unless exactly the Blaster Machine Gun, Shotgun, Rocket Launcher, Arc Gun, and
Ricochet Gun descriptors validate with the Blaster Machine Gun as the sole default.

## Fixed comparisons

`Stage1WeaponLoadoutCatalog.Approved` exposes two immutable fixtures in stable HUD
slot order:

| Fixture ID | Mount 1 | Mount 2 | Mount 3 | Mount 4 |
| --- | --- | --- | --- | --- |
| `loadout.stage1-default-comparison` | Blaster Machine Gun | Shotgun | Rocket Launcher | Arc Gun |
| `loadout.stage1-ricochet-comparison` | Blaster Machine Gun | Ricochet Gun | Shotgun | Rocket Launcher |

Together the matrix exposes all five approved identities. Every fixture contains
exactly four distinct weapons and all IDs resolve back to the validated descriptor
roster.

Custom four-mount selections may repeat an approved weapon identity. Each stable
mount must still appear exactly once, so combinations such as four Blaster Machine
Guns are valid while duplicate mount slots and unknown weapon IDs remain invalid.

## Manual selection

A test or evidence harness selects a comparison by stable ID without changing a
serialized scene:

```csharp
Stage1WeaponLoadoutFixture fixture =
    Stage1WeaponLoadoutCatalog.Approved.GetFixedFixture(
        StableId.Parse("loadout.stage1-default-comparison"));
```

The returned fixture is already ordered by `WeaponMountSlot`. A harness may read
`GetByHudIndex(0..3)` or `GetBySlot(...)` and compose its own test-owned objects.
No serialized scene edit is required, and this task adds no scene or UI asset.

## Seeded evidence wrapper

`Stage1WeaponLoadoutEvidenceSession.Create` consumes:

- the positive EH-002 evidence run seed;
- one immutable `BuildIdentity`;
- one immutable `ContentVersion`.

The build and content fingerprints must match. Mount 1 remains the default Blaster
Machine Gun. A deterministic SHA-256-derived index stream orders the other four
approved identities and selects the first three. The same seed and identity inputs
therefore produce the same ordered fixture and checksums on every replay.

The canonical tracked proof uses EH-002 seed `104729`. Its expected output and
checksums are recorded in
`Tests/PlayMode/Combat/Fixtures/Stage1WeaponLoadouts/stage1-weapon-loadouts-v1.json`.

## State and authority boundary

The wrapper exposes immutable identity data only. It has:

- no persistent inventory;
- no reward persistence;
- no randomized weapon modifiers;
- no filesystem, `PlayerPrefs`, scene, registry-output, or package-mutation path.

Normal and empowered values continue to come directly from the five package
descriptors. Selection never copies or changes package tuning.

## Verification

Run the focused EditMode fixture:

```text
ShooterMover.Tests.EditMode.Combat.Stage1WeaponLoadoutFixtureTests
```

The fixture covers roster validation, four-slot order/count, descriptor resolution,
duplicate and unknown IDs, all-five matrix coverage, canonical seed replay,
identity/content drift, no-persistence boundaries, source-surface restrictions, and
the tracked manifest/checksums.

## Ownership, rollback, and prototype debt

Owned paths are only `ContentPackages/Weapons/Stage1Loadouts`, the paired PlayMode
fixture directory, and `Stage1WeaponLoadoutFixtureTests.cs`, plus inseparable Unity
metadata. Rollback removes those three areas.

This evidence-only seeded wrapper expires when Stage 2 inventory and loadout state
becomes authoritative. It must not be promoted into a mature random-loadout system.
