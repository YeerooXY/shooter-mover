# Enemy System Current State and Roadmap

## Status

Architecture and delivery-planning document.

This document summarizes the enemy-system audit performed against the current production paths. It does not change runtime behaviour, content, scenes, assets, or authority ownership.

## Purpose

The enemy subsystem has a strong engine-neutral foundation, but its current Unity gameplay integration is incomplete. The goal of this document is to make that boundary explicit and provide a small future plan that can be scheduled alongside other feature work without creating avoidable branch collisions.

## Executive summary

The repository currently contains:

- **five authored enemy definitions** in the schema-v2 enemy catalogue;
- **five definitions that can be composed by the pure runtime policy/factory layer**;
- **two enemies exposed through the room-object catalogue**;
- **two enemies placed in the current Level 1 room content**;
- **one shared placeholder presentation prefab** used by both current enemy presentation IDs;
- a live room binding path that owns enemy health, damage, death, lifecycle identity and room-clear reporting;
- no currently connected generic room enemy driver for AI ticking, movement and attack realization;
- rejected player-damage routing and no-op XP, drop and kill-stat consumers in the current room enemy composition.

The main conclusion is:

> The enemy definition, identity, factory and terminal-lifecycle layers are reusable and worth keeping. The next delivery should complete one generic capability-driven live enemy path rather than add more dormant enemy definitions.

## Current roster

| Enemy definition | Intended role | Runtime policy coverage | Room-object mapping | Current Level 1 placement |
|---|---|---:|---:|---:|
| `enemy.mobile-blaster-droid` | mobile ranged pressure | Yes | Yes, through `enemy.moving-droid` | Entry room |
| `enemy.ram-pouncer` | committed pursuit/pounce | Yes | No | No |
| `enemy.blaster-turret` | stationary ranged area pressure | Yes | Yes | Terminal room |
| `enemy.pursuer-drone` | fast contact pressure | Yes | No | No |
| `enemy.hybrid-sentinel` | mixed ranged/contact threat | Yes | No | No |

The current catalogue is therefore broader than the currently authorable and playable roster.

## Current architecture

The production route is split into clear identities and authorities:

```text
room JSON object ID
    -> room object catalogue
    -> enemy definition ID + presentation ID
    -> authored placement ID
    -> EnemyPlacementRuntimeFactoryV1
    -> generated run-local enemy actor ID
    -> RoomEnemyActor2D on the room-owned GameObject
    -> canonical damage/death transition
    -> room terminal report using the authored placement ID
```

### Strong foundations

The following boundaries are in good shape:

- enemy statistics and capability references are authored in JSON rather than duplicated in prefabs;
- catalogue validation fails closed for unsupported movement, decision, attack, presentation, projectile, damage, XP and drop references;
- the factory resolves mechanics by capability IDs rather than enemy-name switches;
- authored placement identity and generated runtime actor identity remain distinct;
- runtime actor identities are derived from run, room runtime and placement context;
- room composition validates definition, presentation, position, rotation, room and lifecycle facts before committing bindings;
- room enemy binding is transactional and rolls back actors bound during a failed attempt;
- enemy death validates actor identity, placement mapping, room identity and lifecycle before affecting room-clear state;
- required, optional, objective and non-participating room-clear roles are explicit.

### Current incomplete boundary

The current `RoomEnemySpawner2D` downstream composition connects:

- enemy terminal state to the room runtime;
- terminal collision shutdown to the bound Unity actor.

It currently leaves these gameplay outputs incomplete:

- attack effect output is unconnected and throws if invoked;
- player damage is rejected;
- XP facts are consumed by a no-op adapter;
- drop facts are consumed by a no-op adapter;
- kill-stat facts are consumed by a no-op adapter;
- the spawner does not own a generic AI tick, target acquisition, movement application or attack-effect realization.

The current scene does contain the JSON room bootstrap, room presentation, enemy catalogue and `RoomEnemySpawner2D`, so the spawn/bind/death route is genuinely present in production. The missing part is the reusable live combat driver after binding.

## Presentation state

The current Level 1 presentation catalogue maps both enemy presentation IDs to the same `GenericRuntimePresentation` prefab. That prefab has a `SpriteRenderer` but no assigned sprite and no authored enemy-specific silhouette, collider, animation, telegraph or attack origin.

This is sufficient for structural binding tests, but not for visually proving:

- which enemy type spawned;
- facing and movement;
- projectile or melee origin;
- dangerous-action telegraphs;
- hit reaction and terminal presentation;
- collision size and readability.

## Active legacy and duplication debt

### Dual enemy-terminal routes

`RoomPresentationScene2D` still adds the older `EnemyActorTerminalFactSource2D` and `RoomOccupantTerminalRelay2D` to enemy presentations. That route polls an `IEnemyActor2DAuthority` and reports room terminal state.

The factory-backed `RoomEnemyActor2D` reports canonical death through a typed room terminal port. While bound, it disables the legacy relay and restores the old enabled state on unbind.

This prevents double terminal mutation, but it leaves two terminal architectures attached to the same enemy object. The legacy route should be retired once every production enemy is guaranteed to use the factory-backed runtime.

### Historical temporary combat path

Earlier first-combat-room work contained a mechanics-driven `EnemyAttack2D`/`EnemyShot2D` route. Those historical components are not the current production enemy driver. Their replay, cancellation and mechanics-based design can inform the new implementation, but the old temporary integration should not be restored as a second authority without reconciling it with the current room runtime and downstream systems.

### Schema compatibility

The importer still understands schema-v1 attack shapes while current production content uses schema v2. This is contained compatibility rather than a second runtime authority, but it should eventually be removed if no supported content requires it.

### Special-capability metadata

The catalogue accepts special-capability IDs such as locked commitment and rotating aim. The current factory resolves movement, decision, perception, aim and attack capability registrations, but there is no equivalent special-capability materialization registry in the inspected production path. Special-capability IDs should therefore not be treated as self-executing mechanics.

## Five-step future plan

## 1. Add one enemy-readiness gate

Create one automated cross-catalogue report that evaluates every enemy definition against:

- enemy catalogue validation;
- runtime movement policy;
- runtime decision policy;
- runtime attack capability and aim policy;
- room-object mapping;
- presentation registration and prefab;
- production-level placement;
- live-mechanics support.

Each enemy should have an explicit status such as:

- `production-ready`;
- `runtime-only`;
- `planned`;
- `retired`.

This prevents the current five-definition/two-placeable-enemy drift from being mistaken for completion.

## 2. Build one generic live enemy driver

Add a capability-driven Unity adapter for factory-created room enemies. It should:

- bind to `RoomEnemyActor2D` without creating a second runtime;
- use one authoritative run/lifecycle time source;
- gather validated target context;
- evaluate the existing runtime decision authority;
- apply supported movement intents to the Unity body;
- execute supported attack requests;
- realize projectile, contact, pounce and instantaneous area mechanics through typed adapters;
- cancel pending effects on lifecycle end;
- fail closed for unsupported mechanics.

The first acceptance target should be the Mobile Blaster Droid from placement through movement, projectile, player damage, enemy damage and room clear.

## 3. Connect canonical downstream authorities

Replace the current placeholder ports with adapters to existing owners:

- enemy hits -> combat eligibility -> player damage authority;
- enemy death -> XP authority;
- enemy death -> drop-source resolution;
- enemy death -> kill-stat journal;
- enemy death -> existing room terminal authority.

The enemy runtime must emit facts and requests only. It must not directly mutate player health, persistent XP, inventory, strongboxes or permanent statistics.

Required proof includes exact replay, conflicting duplicate rejection, stale-lifecycle rejection and retry-safe terminal transitions.

## 4. Replace placeholder presentation and promote the roster

Create distinct presentation prefabs for the two current room enemies, then promote the remaining catalogue enemies in increasing mechanical complexity:

1. Pursuer Drone;
2. Ram Pouncer;
3. Hybrid Sentinel.

The Blaster Turret remains part of the initial roster but should receive a real stationary presentation and telegraph before its area attack is considered production-ready.

Presentation work must preserve authority boundaries: prefabs may own visuals, colliders, Rigidbody configuration, animation, audio and attachment points, but not a second health, AI or reward model.

## 5. Retire compatibility paths and harden production proof

After all production enemies use the factory-backed live route:

- stop adding the legacy terminal source and polling relay;
- remove relay disable/restore compatibility from `RoomEnemyActor2D`;
- remove obsolete temporary enemy integration components and source references;
- remove schema-v1 import support if no supported content remains;
- add focused EditMode, PlayMode and manual acceptance evidence;
- add a multi-enemy performance and cleanup test for room traversal/restart.

## Parallel work recommendation

The enemy roadmap can safely support **three concurrent workstreams**, but only **two should be code-heavy**.

| Lane | Scope | Safe to start | Shared-file risk |
|---|---|---|---|
| A — Live runtime | generic driver, target context, movement, attack realization, player hit routing | After Step 1 contracts are frozen | High around `RoomEnemySpawner2D`, Unity enemy adapters and gameplay composition |
| B — Death integrations | XP, drop and kill-stat adapters from canonical death facts | After Step 1; can run beside Lane A if composition ownership is agreed | Medium around downstream port construction and run/progression composition |
| C — Content and presentation | readiness report, prefabs, presentation catalogue, room mappings and future placements | Immediately after Step 1 | Low if it avoids scene/composition code until integration |

### Recommended concurrency limit

- **Maximum:** three enemy branches at once: two implementation lanes plus one content/tooling lane.
- **Preferred during critical integration:** two branches at once: one runtime/integration owner and one presentation/content owner.
- **Avoid:** two branches simultaneously editing `RoomEnemySpawner2D`, `PlayableLevel.unity`, the same presentation catalogue asset or the same downstream composition root.

A single integration owner should sequence shared-file changes and publish small public seams for the other lanes. This keeps parallel work useful instead of producing repeated rebases around the room spawner and gameplay scene.

## Dependency shape

```text
Step 1 readiness gate
    -> Step 2 generic live driver
        -> Step 3 downstream authority integration
            -> Step 5 legacy retirement and hardening

Step 1 readiness gate
    -> Step 4 presentation asset work can begin in parallel

Step 2 + Step 3
    -> Step 4 enemy enablement and new room placements
```

Presentation asset preparation may run early, but enabling additional enemy definitions in production should wait until the live driver and downstream authority routes support their mechanics.

## Suggested first visible milestone

The highest-value next milestone is:

> One Mobile Blaster Droid in an authored room visibly acquires the player, moves to its preferred range, telegraphs and fires a reusable projectile, damages the canonical player authority, accepts canonical player-weapon damage, dies once, emits XP/drop/kill facts through real adapters, and unlocks the room through the existing placement-based room terminal route.

This milestone exercises the reusable architecture rather than proving only one isolated animation or one additional JSON definition.

## Completion criteria for the enemy foundation

The enemy foundation should be considered sustainable when:

- every production enemy has an explicit readiness status;
- adding an enemy that uses existing mechanics requires definition, presentation registration and placement rather than controller branching;
- one generic driver owns Unity realization for supported mechanics;
- player damage, XP, drops and kill statistics route to their canonical authorities;
- room clear still uses authored placement identity;
- legacy polling terminal components are absent from production enemies;
- traversal/restart creates fresh lifecycles without duplicate actors or effects;
- current Unity compilation, focused tests and manual gameplay evidence exist.

## Verification boundary

This audit was based on static source, asset and production-composition inspection. It did not execute:

- Unity import or C# compilation;
- EditMode tests;
- PlayMode tests;
- manual combat gameplay;
- asset missing-script validation;
- performance profiling.

The roadmap therefore separates architecture findings from runtime proof. Future implementation PRs should include their actual executed evidence and must not treat authored tests as passed tests.

## Primary inspected sources

- `Assets/ShooterMover/Resources/EnemyCatalog/enemy_catalog_v2.json`
- `Assets/ShooterMover/Content/Definitions/Enemies/BuiltInEnemyCatalogRegistryV1.cs`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonImporterV1.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/BuiltInRoomContentObjectCatalogV1.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemyActor2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemySpawner2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomPresentationScene2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimePresentationInstances2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/RoomPresentationCatalog2D.cs`
- `Assets/ShooterMover/Resources/ProductionLevels/Level1PresentationCatalog.asset`
- `Assets/ShooterMover/Resources/ProductionLevels/GenericRuntimePresentation.prefab`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1/`
- `Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity`
