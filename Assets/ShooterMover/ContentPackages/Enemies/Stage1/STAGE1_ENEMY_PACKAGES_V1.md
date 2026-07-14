# Stage 1 Enemy Packages v1

## Status and ownership

EN-001 defines the immutable authoring boundary for the amended Stage 1 enemy roster.
It owns no concrete enemy package, prefab, scene, balance asset, runtime state, generated
registry output, mission state, save data, reward data, or Stage 2 selection.

The boundary is implemented by:

- `Stage1EnemyPackageDescriptor`, an immutable engine-independent package declaration;
- `Stage1EnemyPackageValidator`, the deterministic exact-roster validator; and
- `Stage1EnemyPackageValidationResult`, the ordered proof and registry-input projection.

Concrete package tasks EN-004 through EN-008 create package-owned definitions and assets.
CS-011 remains the sole writer of `Assets/ShooterMover/Generated/` outputs.

## Consumed accepted contracts

This boundary composes accepted contracts rather than introducing a parallel enemy
architecture:

- **StableId v1** supplies every package, module, provenance, actor, and encounter-entry
  identity.
- **ContentVersion v1** is produced downstream from the complete validated descriptor
  catalog by Generated Registry v1; a package does not author its own catalog fingerprint.
- **Combat Messages v1** supplies `CombatChannel` and `CombatWeightClass` classifications.
- **Encounter Lifecycle v1** consumes the package StableId as the generic
  `EncounterParticipantEntry.RoleId`; spawning and lifecycle remain encounter-owned.
- **Typed Content References v1** supplies `ContentDefinitionDescriptor` and exact typed
  movement, attack, and telegraph references.
- **Generated Registry v1** accepts the validated `ContentDefinitionDescriptor` inputs.
  EN-001 never serializes or edits a central registry or review snapshot.

The descriptor contains no health, speed, cadence, damage amount, spread angle, reward,
spawn, scene, or persistence values. Those belong to later package tuning and accepted
runtime adapters.

## Exact v1 roster

| Stable ID | Classification | Required package behavior | Combat/reference boundary |
|---|---|---|---|
| `enemy.pursuer-drone` | ordinary | direct pursuit; ordinary contact damage | Contact channel; shared movement, attack, and telegraph modules |
| `enemy.ram-droid` | ordinary | direct pursuit; one disposable impact attack | Contact channel; shared movement, attack, and telegraph modules |
| `enemy.mobile-blaster-droid` | ordinary | mobile positioning; accepted Blaster projectile; safe recovery | Kinetic channel; attack reference must be `weapon.blaster-machine-gun` |
| `enemy.blaster-turret` | ordinary | stationary positioning; accepted Blaster projectile; line-of-fire telegraph; safe recovery | Kinetic channel; immovable weight; attack reference must be `weapon.blaster-machine-gun` |
| `enemy.four-blaster-elite` | elite | four Blaster origins; mild bounded spread; safe recovery | Kinetic channel; attack reference must be `weapon.blaster-machine-gun` |

Exactly these five IDs validate. The first four must be `Ordinary`; only
`enemy.four-blaster-elite` may be `Elite`. A sixth or unknown ID does not replace a
missing accepted role.

The weight boundary is structural, not final balance: the stationary turret is immovable,
and moving or elite packages cannot claim immovable behavior. Numeric mass, health,
speed, damage, cadence, and recovery remain later tuning.

## Descriptor fields

A v1 descriptor declares:

1. descriptor version `1`;
2. one release-eligible Content Definitions v1 enemy descriptor with provenance;
3. ordinary or elite classification;
4. accepted Combat Messages v1 damage channel and weight class;
5. one typed movement reference;
6. one typed attack reference;
7. one typed telegraph reference; and
8. the closed Stage 1 capability set for that exact StableId.

Movement and telegraph references are Shared Module v1 references. Contact-role attacks
are Shared Module v1 references. The three ranged roles reference the accepted Blaster
Machine Gun definition as their attack input. Every named reference must also appear in
the package's `ContentDefinitionDescriptor.References` collection.

Additional registry references are permitted only when they remain supported v1 shared
modules. A package cannot smuggle in a second weapon, room, encounter, environment, enemy,
unsupported version, or undeclared adapter dependency.

## Four-Blaster Elite boundary

The elite is intentionally an approachable first boss. Validation rejects each of these
capabilities explicitly and independently:

- phase transition;
- denial pulse;
- mortar attack;
- reinforcement call;
- teleport;
- complex repositioning; and
- bullet-hell behavior.

It has one package classification and one encounter participant identity. Four firing
origins do not create four enemy actors, four health models, player-style mount authority,
or a second encounter architecture. Completion and lockdown remain in Encounter Lifecycle
v1 and mission contracts.

## Deterministic validation

Validation copies its input, groups by StableId, and sorts output independently of caller
order. Errors use this fixed family order before ordinal package-ID and detail tie-breakers:

1. null, missing, duplicate, and unknown package identity;
2. descriptor and content-definition version/shape;
3. provenance and release eligibility;
4. ordinary/elite, combat-channel, and weight classification;
5. movement, attack, telegraph, and registry-reference shape; and
6. unknown, missing, out-of-bound, and elite-forbidden capabilities.

No invalid descriptor is repaired, normalized, selected from a duplicate group, silently
dropped, or converted into registry input. `GetRegistryInputs()` fails closed unless the
complete five-package roster is valid.

## Accepted and rejected fixtures

The EditMode fixture accepts:

- four ordinary descriptors and one elite descriptor;
- package order shuffled arbitrarily;
- shared-module reuse where the typed identity is identical; and
- a complete catalog containing the five enemy definitions plus their referenced shared
  modules and the accepted Blaster weapon.

The fixture intentionally rejects:

- a duplicated Pursuer Drone with the Ram Droid missing;
- an unknown sixth enemy ID;
- an ordinary role marked elite or the elite marked ordinary;
- null or malformed content definitions;
- wrong kinds, unsupported versions, missing provenance, or prototype-only definitions;
- a missing telegraph reference or a named reference absent from registry input;
- behavior outside the exact role mask; and
- every forbidden Four-Blaster Elite capability listed above.

## Contract review note

The package boundary fits the accepted adapters without adding enemy-specific envelopes:

- `CreateEnemyReference()` emits a normal Typed Content References v1 enemy reference;
- `CreateEncounterParticipantEntry()` emits the existing generic Encounter Lifecycle v1
  participant entry with the package ID as `RoleId`;
- damage channel and weight use Combat Messages v1 enums; and
- validated content definitions can be supplied directly to
  `GeneratedMachineRegistry.Create` alongside referenced definitions.

Runtime health, contact-once behavior, destruction, movement decisions, Unity 2D bridging,
projectile execution, encounter completion, and durable mission truth remain with EN-002,
EN-003, accepted combat/movement adapters, Encounter Lifecycle v1, and mission contracts.

## Verification

Run the focused EditMode fixture with the pinned Unity editor and capture the test result:

```text
Stage1EnemyPackageValidatorTests
```

The fixture covers exact roster acceptance, input-order stability, duplicate and missing
IDs, unknown IDs, classification, malformed definitions, missing telegraphs,
out-of-bound ordinary behavior, all seven elite exclusions, immutability, encounter-entry
projection, and Generated Registry/ContentVersion compatibility.

Manual review confirms:

- only the four EN-001-owned product/test/document paths and inseparable Unity metadata
  changed;
- no generated output, shared contract, scene, package implementation, or project setting
  changed; and
- the boundary consumes existing adapters rather than creating a second enemy runtime.

## Limitations and non-goals

EN-001 deliberately does not choose final balance, serialized package assets, presentation,
AI algorithms, pathfinding, projectile implementation, encounter composition, reward
behavior, evidence scenes, Stage 2 enemies, or registry output bytes. Package tasks may add
package-owned shared-module references inside this v1 boundary but may not change the exact
roster or forbidden elite behavior without a reviewed versioned amendment.

## Rollback

Before consumers land, rollback is removal of the two Stage 1 source files, the focused
test file, this document, and their inseparable Unity metadata. There is no save migration,
scene cleanup, package-lock change, generated-output repair, or serialized-asset rollback.
