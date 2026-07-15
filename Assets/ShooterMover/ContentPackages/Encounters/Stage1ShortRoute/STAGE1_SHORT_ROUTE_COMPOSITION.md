# Stage 1 Short-Route Encounter Composition

EN-011 projects deterministic encounter and hazard inputs onto the marker order
owned by the read-only EH-005 short-route shell. The package contains no scene
reference, scene loader, prefab, durable mission state, reward, economy, save, or
registry output.

## Frozen route map

| Order | EH-005 marker | Projected pressure | Hazard | Retreat | Lockdown |
| ---: | --- | --- | --- | --- | --- |
| 0 | `route.start` | 2 × Pursuer Drone | none | no previous room | released |
| 1 | `route.arena-entry` | Ram Droid + Mobile Blaster Droid | `CHEVRON SWEEP`, full-width chevron footprint, 45-tick warning, one hit maximum | allowed | released |
| 2 | `route.connector` | Pursuer Drone + Blaster Turret | `DOUBLE BAR GATE`, double-bar lane footprint, 60-tick warning, one hit maximum | allowed | released |
| 3 | `route.review-end` | 1 × Four-Blaster Elite | none | rejected | engaged on entry; released before completion |
| 4 | `route.restart` | projection-only route endpoint | none | not applicable | released |

The three ordinary encounters cover every validated ordinary Stage 1 role. The
elite endpoint contains only `enemy.four-blaster-elite`. No room exceeds two
enemies and no room projects more than one hazard.

## Hazard boundary

The two temporary hazards are geometry-and-text warnings rather than color-only
cues. Each definition has:

- one explicit warning glyph and uppercase warning text;
- a stable footprint identity;
- positive telegraph, active, and cooldown windows;
- exactly one permitted hit per activation;
- finite damage no greater than eight.

They are encounter inputs only. They do not alter collision authority, damage
contracts, enemy behavior, movement, or the route scene.

## Projection and lifecycle boundary

`Stage1ShortRouteComposition` is immutable. It mirrors the five EH-005 marker
IDs and creates a fresh Room Projection v1 identity for each session generation.
`Stage1ShortRouteSession` is disposable attempt state that delegates start,
retreat, withdrawal, lockdown, combat resolution, and completion behavior to
Encounter Lifecycle v1.

Room completion is represented through the accepted `EncounterCompletionMessage`
and matching Mission Messages v1 `RoomClearedEvent`; this package does not store
or apply durable mission truth. Repeated completion returns no change and does
not emit a second completion count.

## Restart contract

Restart preserves the composition fingerprint and marker order while issuing a
new projection generation. It clears:

- current encounter runtime and participant identities;
- active hazard identities;
- registered projectile identities;
- current marker/cursor and attempt-local completion count.

Entering the route after restart creates fresh actor and encounter runtime IDs,
so no stale object token can be mistaken for the new attempt.

## Route-shell read-only audit

The implementation is confined to:

- `Assets/ShooterMover/ContentPackages/Encounters/Stage1ShortRoute/`
- `Assets/ShooterMover/Tests/PlayMode/Encounters/Stage1ShortRouteEncounterTests.cs`
- inseparable Unity metadata.

The owned source contains no `UnityEngine`, `SceneManager`, scene loading,
instantiation, persistence, or `MissionRunState` reference. The EH-005 scene
`Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1ShortRouteShell.unity`
remains read-only.

## Required manual proof

Using either accepted WP-008 fixed four-slot loadout:

1. Load the unmodified EH-005 short-route shell through the accepted harness.
2. Traverse markers in the frozen order above.
3. Confirm each ordinary pressure group and its geometric/text hazard warning is readable.
4. Retreat from `route.arena-entry` and `route.connector`; confirm the current room is discarded and re-entry starts it cleanly.
5. Confirm retreat is blocked during the Four-Blaster Elite lockdown.
6. Defeat the elite; confirm lockdown releases and completion is recorded once.
7. Restart while enemies, a hazard, and projectiles are active; confirm the route returns to `route.start` with no stale objects.

This playable proof is intentionally pending until run in Unity 6000.3.19f1.
