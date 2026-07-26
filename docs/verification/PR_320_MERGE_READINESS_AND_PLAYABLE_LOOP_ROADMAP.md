# PR #320 merge readiness and playable-loop roadmap

## Purpose

PR #320 removes the retired five-weapon starter catalogue, makes the authored eighteen-weapon catalogue the sole production source, and creates fresh exact starter equipment per character mount layout.

This document records:

1. the safety correction applied to the save migration;
2. the remaining evidence required before merge;
3. the shortest development path from catalogue cleanup to a repeatable loot-to-combat gameplay loop.

## Merge decision

PR #320 should remain a draft until the Unity validation gates below have been executed against its final head.

The prior code-level blocker has been corrected: retired equipment is removed from current holdings without reconstructing the holdings authority from scratch. The migration now retains the existing ledger snapshot and transaction history, then lets normal onboarding append only the required fresh starter grants.

This preserves accepted operation identities and prevents historical rewards from becoming replayable after migration.

## Corrected migration behaviour

For each existing character, the migration now performs this sequence:

```text
Decode exact holdings and loadout snapshots
    -> identify retired equipment instances
    -> remove only retired current equipment entries
    -> preserve the original holdings ledger and transactions
    -> clear invalid or retired loadout bindings
    -> preserve current equipment, strongboxes and stack holdings
    -> remove generated augment signatures for retired instances
    -> fill required empty weapon mounts through normal onboarding
    -> validate the complete holdings/loadout pair
    -> return one migrated account snapshot
    -> atomically save before runtime restore
```

The migration does not translate retired weapons into modern equivalents. Required empty mounts receive fresh authored Rattler MK1 instances through the same policy used for new characters.

## Added regression proof

The focused replay-safety suite covers:

- an accepted pre-migration holdings operation remains an exact duplicate after migration;
- conflicting reuse of the same operation identity does not apply;
- holdings sequence does not move backwards;
- an existing strongbox retains its exact instance fingerprint;
- retired equipment is removed;
- migration failure returns the original account unchanged;
- deterministic instance-ID failure cannot partially mutate the account.

These tests are source-authored but must still be executed in Unity before a merge-ready claim is made.

## Required merge gates

### Source and branch gates

- [x] Retired starter catalogue removed.
- [x] Authored eighteen-weapon catalogue is the production provider.
- [x] Fresh starter instances are character-local and exact.
- [x] Two-, three- and four-mount class layouts are supported.
- [x] Retired save IDs are isolated in one migration boundary.
- [x] Holdings ledger and transaction history are preserved during cleanup.
- [x] Replay and atomic-failure regression tests are present.
- [ ] Review the final diff after all concurrent branch updates have settled.
- [ ] Confirm the PR is still mergeable with current `main`.
- [ ] Perform a repository sweep for production references to retired definition and instance IDs outside migration/tests/documentation.

The branch may be behind `main` while still mergeable. Because current `main` contains the room-enemy binding work from PR #319, validation must use the actual final merge candidate rather than an older checkout.

### Unity gates

Use the repository's pinned Unity editor version.

1. Open the project and wait for a complete script reload.
2. Confirm zero compiler errors and zero missing-script errors.
3. Run focused EditMode tests:
   - `ProductionWeaponOnboardingAndMigrationTests`
   - `RetiredWeaponSaveMigrationReplayTests`
   - `ProductionPlayerLoadoutRuntimeV1Tests`
   - `ProductionWeaponMountPolicyV1Tests`
   - `ProductionExactWeaponInstanceLoadoutTests`
   - `CharacterCompositionCoordinatorV1Tests`
   - `CharacterActivationAndStrongboxRegressionTests`
   - `CharacterCreationTransactionRegressionTests`
4. Run focused PlayMode tests:
   - inventory/loadout authority connection;
   - inventory-backed live weapon runtime;
   - mounted aim and concurrent mount execution.
5. Save the XML results or attach the exact passing counts to the PR description.

### Manual acceptance gates

1. Create one Striker character and verify exactly two owned/equipped starter instances.
2. Create one Combat Medic character and verify exactly three.
3. Create one Juggernaut character and verify exactly four.
4. Confirm no two characters share an equipment-instance ID.
5. Switch characters, restart the application and verify holdings/loadouts remain isolated.
6. Open an existing strongbox and verify the granted item belongs to the authored catalogue.
7. Enter gameplay and verify every equipped exact instance resolves to a live weapon.
8. Load a save containing retired weapon data and verify:
   - migration occurs once;
   - retired equipment disappears;
   - valid current equipment remains;
   - strongboxes remain;
   - XP, money, scrap and skills remain;
   - the application restarts with the same migrated result;
   - no reward or starter grant duplicates.

## Merge recommendation

Merge PR #320 only when:

```text
final branch is mergeable
AND Unity compiles cleanly
AND focused EditMode tests pass
AND focused PlayMode tests pass
AND retired-save manual migration passes
AND the PR description contains the actual evidence
```

Do not mark it ready based only on source inspection.

---

# Immediate development roadmap after PR #320

## Product goal

Build one complete, visible and repeatable loop:

```text
Enter level
-> kill enemy
-> receive and collect strongbox
-> complete mission
-> open box
-> keep or sell item
-> equip kept exact weapon
-> replay level
-> fire that exact weapon
```

Until this works, armour, deep augment progression, rotating shops, masteries, collections and procedural geometry should remain secondary.

## Milestone 1 - playable JSON level boot

### Deliverable

A selected character can enter one authored JSON level at its defined spawn, move with the camera following, and return through an authored completion/exit path.

### Visible acceptance

```text
Character Select -> Hub -> Level Select -> JSON level -> move -> exit -> Results/Hub
```

### Guardrail

Do not introduce a level-specific gameplay controller. The level entry should select validated content and compose existing room/runtime adapters.

## Milestone 2 - live enemy combat

Connect the merged JSON enemy placement path to the minimum live encounter:

- runtime movement or stationary policy;
- target detection;
- attack cadence;
- player damage;
- enemy damage and death;
- room-clear participation.

Prove one ranged enemy first. A second enemy should be addable through definition, presentation registration and room placement without enemy-type branches.

## Milestone 3 - equipped weapon in gameplay

Connect the selected character's exact loadout to visible live firing in the JSON level:

```text
EquipmentInstance
-> equipment definition
-> authored weapon definition
-> inventory-backed scheduler
-> visible effect
-> collision
-> canonical damage
-> enemy death
```

Prove one simple projectile weapon and one nontrivial weapon such as Ironwake or Crownfall.

## Milestone 4 - deterministic strongbox drop and collection

Use a guaranteed Tier 1 box for the first integration test.

```text
accepted enemy death
-> registered drop profile
-> exact StrongboxInstance
-> physical pickup
-> run-local collected box
-> successful mission result
-> character-owned unopened box
-> durable save
```

Random drop probability can follow after the loop is reliable.

## Milestone 5 - production Keep/Sell disposition

The opening authority currently grants the exact equipment item. The safest first implementation is:

```text
Open exact box
-> exact equipment is granted and persisted
-> reveal item
-> Keep: terminal no-op decision
-> Sell: remove that exact equipment instance
         + credit exact money value
         + persist one exactly-once disposition receipt
```

Requirements:

- repeated Keep/Sell input cannot apply twice;
- a conflicting decision rejects;
- Sell removes only the revealed exact instance;
- holdings and money change atomically;
- save failure cannot leave the item removed without payment;
- closing/reopening cannot reroll or resell the item.

Scrap salvage should be a later, separate action.

## Milestone 6 - usable inventory and loadout

Inventory must visibly support:

- listing owned exact weapon instances;
- item details;
- equip;
- unequip where policy permits;
- replace occupied slot;
- swap slots;
- unavailable/locked slot presentation;
- persistence after confirmation.

Always present four physical positions:

| Class | Baseline available mounts | Future expansion |
|---|---:|---|
| Striker/Assault | 2 | skill may enable a third |
| Combat Medic/Healer | 3 | none required initially |
| Juggernaut | 4 | none required initially |

Capacity remains data-defined; do not add class-specific UI branches.

## Milestone 7 - end-to-end loop proof

The integration acceptance script is:

1. Select a Striker character.
2. Enter the JSON test level.
3. Kill the test enemy.
4. Collect its guaranteed strongbox.
5. Complete the mission.
6. Open the strongbox.
7. Keep the item.
8. Equip it in the second weapon mount.
9. Re-enter the level.
10. Fire the newly obtained exact weapon.
11. Repeat with another box and choose Sell.
12. Confirm money increases and the sold item is absent.
13. Restart and verify holdings, loadout, money and box state.

## Milestone 8 - content extensibility proof

Immediately after the loop works:

### Add a second level

Expected changes:

- new level/room JSON;
- content catalogue entry;
- focused content validation;
- no new production gameplay class.

### Add a different enemy

Expected changes:

- new enemy definition JSON;
- presentation/prefab registration;
- room placement with identity, position, rotation and level;
- focused content validation;
- no room-clear or factory changes.

## Later feature order

1. Player death, lives, respawn and mission failure.
2. XP display and level-up loop.
3. A small initial skill set and Assault third-mount unlock.
4. Weapon augments and overclock cores.
5. Four-slot armour: helmet, chestplate, leggings and boots.
6. Crafting with money plus one initial scrap currency.
7. Rotating shop with deterministic offers and item pinning.
8. Per-character masteries.
9. Account-global overlapping collections.
10. Generic mission objectives.
11. Seeded procedural level manifests built from validated static room templates.

## Motivation rule

Every implementation task must end with a visible acceptance statement:

> After this change, I can launch the game and visibly do X.

A non-visible architecture task is acceptable only when it directly unblocks the next visible task. This keeps the project moving towards a game rather than accumulating disconnected systems.
