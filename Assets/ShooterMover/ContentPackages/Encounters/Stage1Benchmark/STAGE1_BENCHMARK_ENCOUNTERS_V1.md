# Stage 1 benchmark enemy encounters v1

## Authority and scope

EN-010 owns seven temporary, resettable encounter fixtures and the runtime
selector/projection loader under this folder. The fixtures consume Encounter
Lifecycle v1, the EH-004 arena marker contract, the EH-006 restart boundary, the
two accepted WP-008 fixed loadouts, and the EN-009 five-enemy roster.

The loader never edits or saves
`Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1BenchmarkArena.unity`.
It adds package-owned runtime children only after the arena is loaded. Enemy
labels are temporary verification projections; enemy packages remain the source
of movement, combat, health, telegraph, prefab, and tuning authority.

**No pacing or quality acceptance is claimed by these fixtures.** EN-013 owns the
later human review of pressure, readability, and voluntary replay.

## Fixture matrix

| Fixture ID | Kind | Ordered roles | Arena sockets | Fixed loadout | Concurrent cap |
|---|---|---|---|---|---:|
| `encounter.stage1-benchmark-pursuer` | isolated | Pursuer Drone | north | default comparison | 1 |
| `encounter.stage1-benchmark-ram-droid` | isolated | Ram Droid | north | default comparison | 1 |
| `encounter.stage1-benchmark-mobile-blaster` | isolated | Mobile Blaster Droid | north | ricochet comparison | 1 |
| `encounter.stage1-benchmark-blaster-turret` | isolated | Blaster Turret | north | default comparison | 1 |
| `encounter.stage1-benchmark-four-blaster-elite` | elite | Four-Blaster Elite | north | ricochet comparison | 1 |
| `encounter.stage1-benchmark-close-pressure` | mixed | Pursuer Drone, Ram Droid | west, east | default comparison | 2 |
| `encounter.stage1-benchmark-crossfire` | mixed | Blaster Turret, Mobile Blaster Droid, Pursuer Drone | north, east, west | ricochet comparison | 3 |

Every fixture has contiguous zero-based spawn order, unique actor/entry/socket
identities, no reinforcement queue, a maximum of three concurrent actors, a
32-combat-message-per-tick verification cap, and a 16.667 ms observation budget.
The performance values are evidence comparison limits; they do not change
simulation behavior.

## Runtime selection and replay

1. Open `Bootstrap`, enter Play Mode, and load the canonical EH-004 benchmark
   arena.
2. While still in Play Mode, add
   `Stage1BenchmarkEnemyEncounterArenaLoader` to the
   `Stage1 Benchmark Arena` root, or invoke
   `Stage1BenchmarkEnemyEncounterArenaLoader.AttachToLoadedArena()` from a
   task-owned runner.
3. Use the on-screen buttons to select all seven fixtures in sequence.
4. Use **Replay selected fixture** twice for each fixture.
5. Confirm actor labels return to their original sockets and the active count
   returns to the fixture cap.
6. For lifecycle proof, call `ResolveProjectedActor(actorId)` in spawn order and
   confirm completion appears once; replay must restore all participants.

The projection tokens `[P]`, `[R]`, `[M]`, `[T]`, and `[4B]` are deliberately
plain, non-final labels. They exist only to make selection and socket placement
visible without copying package presentation or adding scene-owned references.

## Automated proof

Focused PlayMode fixture:

```text
ShooterMover.Tests.PlayMode.Encounters.Stage1BenchmarkEnemyEncounterTests
```

Coverage:
- exact five-role isolated coverage, two mixed fixtures, and one standalone elite;
- deterministic spawn order and byte-equal replay snapshot;
- Encounter Lifecycle completion once and duplicate-resolution no-change;
- reset clearing completion/resolution state;
- fixture count and performance-budget evaluation;
- missing fixture/enemy ID rejection; and
- read-only arena source audit with the observed scene SHA-256 printed to the log.

Connector-only authoring cannot produce the pinned-editor log. Run the focused
fixture in Unity `6000.3.19f1` and attach its XML/log to the draft PR before
merge.

## Ownership, limitations, and rollback

- The EH-004 arena scene, shared encounter contracts, enemy packages, weapon
  packages, generated registries, mission persistence, rewards, and saves remain
  read-only.
- Runtime labels do not instantiate or replace package AI. They make the
  encounter composition selectable before EN-012 performs combined evidence.
- No reward, route, Stage 2, final-art, or durable-state behavior is present.
- Remove this folder, its leaf metadata, and
  `Stage1BenchmarkEnemyEncounterTests.cs` plus metadata to roll back. No save,
  registry, scene, package, or project-setting migration is required.
