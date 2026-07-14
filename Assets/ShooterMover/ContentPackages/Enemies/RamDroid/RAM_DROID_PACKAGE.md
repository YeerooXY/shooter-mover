# Ram Droid package

## Identity and role

- Stable package role: `enemy.ram-droid`
- Ordinary Stage 1 enemy.
- Small, fast, light-weight, low-health disposable impact attacker.
- Direct pursuit only; no pathfinding, ranged attack, spawning, area damage, reward, mission, save, or encounter ownership.

## Accepted boundaries consumed

- EN-001 `Stage1EnemyPackageDescriptor` for the package definition and registry input.
- EN-002 `EnemyActorState`, `EnemyContactPolicy`, and `EnemyActorStepper` for health, grace, disposable-impact death, and idempotency.
- EN-003 `EnemyActor2DAdapter`, `EnemyTarget2DAdapter`, and `EnemyContact2DAdapter` for fixed-step movement, confirmed hits, contact deduplication, restart, and the MT-009 movement-contact projection.

The package emits the accepted immutable mover-damage request on the first valid player impact. It never reads or writes player velocity and does not invent player health authority.

## Package-owned serialized assets

- `RamDroidDefinition.asset` — identity, speed, health, radius, bounded impact damage, grace, capacity, and warning tuning.
- `RamDroid.prefab` — one dynamic `Rigidbody2D`, one `CircleCollider2D`, the unchanged EN-003 adapters, package runtime, and temporary warning presentation.
- `WARNING_RAM_TEXT_AND_PULSE` — literal `RAM!` text plus a geometric pulse. Color may support the cue but is not required to understand it.

## Tuning snapshot

| Property | Ram Droid | Pursuer comparison guardrail | Relation |
|---|---:|---:|---|
| Maximum speed | 7.5 | 4.5 | faster |
| Maximum health | 24 | 80 | lower-health |
| Collider radius | 0.28 | 0.48 | smaller |
| Impact damage request | 16 | n/a | bounded, once |
| Contact grace | 0.35 s | n/a | EN-002-owned rule |
| Simultaneous callback window | 0.02 s | n/a | EN-002-owned rule |

EN-004 is a parallel task, not an EN-005 dependency. The Pursuer values above are package-local acceptance guardrails only; they do not serialize or own the Pursuer package's final tuning.

## Readability review

The warning is understandable in grayscale and with color disabled because it displays the explicit text `RAM!` and changes shape/scale before close impact. It is temporary prototype presentation, contains no borrowed-IP elements, and is expected to be replaced by final package art later.

## Rollback

Remove the `RamDroid` folder and `RamDroidPackageTests.cs` with their paired Unity metadata as one unit. No scene, generated registry, save, player, shared movement, shared combat, or shared enemy-adapter migration is required.
