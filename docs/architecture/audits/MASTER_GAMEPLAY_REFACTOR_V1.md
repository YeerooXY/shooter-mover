# MASTER_GAMEPLAY_REFACTOR_V1 — Current-main migration tracker

## Document authority and boundary

- **Repository:** `YeerooXY/shooter-mover`
- **Unity baseline:** `6000.3.19f1`
- **Official baseline:** `main` at `b2bf4348ab6f827a737add53278d57568684f552`
- **Prepared:** 2026-07-19
- **Live Stage 1 controller:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`
- **Controller size:** approximately 2,192 lines on the baseline above
- **Machine-readable mirror:** `MASTER_GAMEPLAY_REFACTOR_V1_inventory.csv`
- **Production behavior changed:** none
- **Source audit:** PR #213 was used as read-only evidence. This tracker does not merge #213, copy its branch history, or treat its stacked base as current main.

This file is the official current-main migration tracker. It replaces PR #213 as the planning authority because #213 is stacked on unmerged PR #212 and predates the merged entity/enemy foundations in PRs #215–#217.

## Status vocabulary

| Status | Meaning |
|---|---|
| **Not Started** | No merged current-main foundation specific enough to execute this migration area, or the required source-of-truth transition has not begun. |
| **Foundation** | Reusable contracts, authorities, policies, or projections are merged, but the live Stage 1 path does not use them. |
| **Partially Live** | A meaningful portion is used by the live Stage 1 path, while legacy ownership or duplicated truth remains. |
| **Live** | The authoritative reusable path owns live behavior and the corresponding legacy owner has been removed. |
| **Retired** | The old path/type is no longer required and has been deleted or reduced to an explicitly allowed compatibility shell. |

Status is assigned to the migration area, not to the quality of an isolated foundation.

## Non-negotiable architecture invariants

1. Stable definition IDs, concrete equipment-instance IDs, entity-instance IDs, run-participant ownership, operation IDs, and lifecycle generations remain separate concepts.
2. Participant attribution is explicit and resolved by trusted composition/network authority. Client-supplied attribution is never accepted as authoritative evidence by itself.
3. Commands and results are immutable, deterministic, and replay-safe. Idempotency applies to the same operation or concrete instance only; it must not suppress independent boxes, actors, equipment instances, or effects that legitimately share a definition.
4. Engine-neutral domain/application code does not depend on `UnityEngine`, scenes, `GameObject`, physics queries, UI, or static mutable gameplay state.
5. Unity adapters may gather transforms, physics, line-of-sight, input, and scene facts, then pass immutable values into deterministic policies.
6. Prefer small capabilities and composition. Do not introduce `EntityBase`, `ActorBase`, `EnemyBase`, `PlayerBase`, a service locator, or a global mutable restart bus.
7. Existing canonical authorities remain canonical. Migrations adapt and delegate; they do not create parallel health, inventory, reward, room, weapon, or lifecycle truth.
8. The approximately 2,192-line controller must be patched incrementally. No task should reconstruct or replace the whole file through a broad API overwrite.

## Current-main reconciliation of PR #213

PR #213 correctly identified the retained controller, local player health, non-authoritative live weapon path, non-atomic multi-effect risk, broad restart transaction, room-clear ownership, and Stage1/demo naming pressure. The following statements are now current:

- PR #215 is merged. `PlayerActorAuthority` and shared gameplay identity/ownership capabilities exist on main, but Stage 1 still uses local `playerHealth`.
- PR #216 is merged. Reusable enemy projections, decisions, generic attack intents, room-clear roles, attributed death facts, and truthful debug snapshots exist on main.
- PR #217 is merged. Geometric perception, distinct vision and attack arcs, and the representative Mobile Blaster Droid projection factory exist on main.
- PR #210 is merged. The live scene has a working two-room loop, retained room state, locked exits, a moving droid sprite, and killable droid behavior.
- PR #202's typed JSON weapon catalog is merged and is the official weapon-content source. It is not a firing implementation.
- PRs #211 and #212 remain large open drafts. Their code and claims are evidence, not current-main production truth.
- PR #206 remains an open conflicting alternative. It is not the official runtime path.
- PR #213 remains an open stacked documentation draft and is superseded by this current-main tracker.
- The live controller path is `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`. PR #213's inventory recorded a stale `Assets/ShooterMover/Production/Stage1/...` path for that file.
- The controller still constructs the player; owns local health, fixture-based weapon firing, package-specific enemies, room occupancy/clear truth, HUD/camera assembly, and broad restart ordering.

## Foundations are not live migration

A merged contract, authority, projection, policy, or test suite is **not** equivalent to live Stage 1 migration.

A migration area advances to **Partially Live** only when the playable Stage 1 path delegates a meaningful responsibility to the merged reusable boundary. It advances to **Live** only when:

1. the authoritative live call path uses that boundary;
2. the former duplicate truth is removed;
3. rejection, replay, lifecycle, and exact-identity behavior are covered;
4. focused Unity proof exists when runtime behavior changed; and
5. the controller no longer owns the migrated responsibility.

By this rule, `PlayerActorAuthority`, enemy decision/perception foundations, and the Mobile Blaster Droid projection are valuable merged foundations but are not yet the live Stage 1 player/enemy migration.

## Migration status overview

| # | Migration area | Status | Current live result |
|---:|---|---|---|
| 1 | Authoritative live weapon handoff | **Not Started** | Stage 1 does not resolve the authoritative active equipment instance into live execution. The controller iterates a fixture and branches on weapon/package IDs. |
| 2 | Exact equipment-instance weapon resolution | **Foundation** | No current-main handoff carries equipment-instance ID, definition ID, runtime weapon ID, owner participant, operation ID, and lifecycle generation together into live firing. |
| 3 | Atomic effect runtime | **Not Started** | Shotgun pellets and future multi-effect attacks can only be emitted as independent side effects. No current-main batch reserve/validate/commit boundary exists. |
| 4 | Reusable player runtime/presentation | **Not Started** | Current main has no reusable player installer/view that is the sole owner of construction, restart, presentation refresh, and disposal. |
| 5 | Player vital/damage authority integration | **Foundation** | No Stage 1 adapter constructs or invokes PlayerActorAuthority. The merged foundation explicitly states that Stage 1 migration is incomplete. |
| 6 | Enemy package migration | **Foundation** | The representative projection is not the live Stage 1 decision path. Other packages still need projection/adapters, and the controller still branches on Stage 1 enemy IDs. |
| 7 | Reusable room occupancy/clear runtime | **Partially Live** | Graph/traversal is live, but occupant registration, exact occupant identity, terminal reporting, clear transitions, and door projection are still controller-local. |
| 8 | Restart lifecycle | **Partially Live** | Subsystem restart calls are live, but registration, phases, ordering, failure behavior, and disposal are not owned by a reusable lifecycle coordinator. |
| 9 | HUD/camera projection | **Partially Live** | Presentation is extracted in shape, not in truth ownership. The controller remains snapshot assembler, refresh coordinator, camera factory, and duplicate HUD owner. |
| 10 | Stage1 controller retirement | **Not Started** | The controller is still required for the playable two-room slice and cannot yet be deleted or reduced to composition-only wiring. |
| 11 | Naming/path normalization | **Not Started** | No current-main normalization has occurred. Renaming before ownership migration would hide rather than solve coupling and could create Unity GUID/assembly churn. |

## Detailed migration records

### 1. Authoritative live weapon handoff

- **Status:** Not Started
- **Current source of truth:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`: `FireSelectedLoadout`/`FireWeapon`/`FireProjectile` are the live firing path.
- **Completed foundation:** Merged holdings/loadout/equipment contracts preserve concrete equipment identity; the merged JSON catalog provides validated weapon content. PR #212 demonstrates an unmerged runtime-ID registry/dispatcher seam.
- **Live integration status:** Stage 1 does not resolve the authoritative active equipment instance into live execution. The controller iterates a fixture and branches on weapon/package IDs.
- **Remaining legacy owner:** `Stage1VisibleSliceController` owns firing cadence, projectile construction, weapon branching, and the silent blaster fallback.
- **Dependencies:** Exact equipment-instance resolution; atomic effect runtime; current-main scene adapter; production run/loadout authority chosen from merged main.
- **Relevant PRs:** #180, #202, #206, #211, #212, #213
- **Acceptance tests:** PlayMode proves each starter weapon is selected from the exact active equipment instance and produces distinct live behavior; unknown/preview-only/unmapped IDs fail closed; no controller ID branch or fallback remains; exact replay and restart are deterministic.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogJsonImporter.cs`; `docs/architecture/weapons/WEAPON_CATALOG_V1.md`

### 2. Exact equipment-instance weapon resolution

- **Status:** Foundation
- **Current source of truth:** Merged holdings/inventory/loadout authorities own concrete equipment instances; the live controller still consumes `Stage1WeaponLoadoutFixture` weapon IDs rather than the exact active equipment instance.
- **Completed foundation:** Concrete equipment-instance identity and ordered loadout projection are merged. PR #211 describes a production active-slot resolver, but that stack is not on main.
- **Live integration status:** No current-main handoff carries equipment-instance ID, definition ID, runtime weapon ID, owner participant, operation ID, and lifecycle generation together into live firing.
- **Remaining legacy owner:** `Stage1VisibleSliceController.SelectedLoadout` and `FireSelectedLoadout` collapse selection to package/weapon IDs.
- **Dependencies:** Authoritative active-slot snapshot; trusted participant attribution; merged catalog mapping; live weapon execution contract.
- **Relevant PRs:** #180, #201, #202, #211, #212
- **Acceptance tests:** Two independent equipment instances sharing one weapon definition remain distinguishable; firing forwards the selected instance unchanged; changing only the active instance changes attribution without changing definition behavior; stale or missing instance references reject without fallback.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogJsonImporter.cs`

### 3. Atomic effect runtime

- **Status:** Not Started
- **Current source of truth:** The retained controller directly instantiates individual projectiles. Existing projectile/hit/enemy authorities handle their own transitions, but there is no authoritative all-or-nothing weapon-effect batch.
- **Completed foundation:** Reusable projectile and combat adapters exist. PR #212 defines effect requests but submits them one at a time; PR #206 carries advanced effect data but does not realize a complete atomic effect transaction.
- **Live integration status:** Shotgun pellets and future multi-effect attacks can only be emitted as independent side effects. No current-main batch reserve/validate/commit boundary exists.
- **Remaining legacy owner:** `Stage1VisibleSliceController.FireWeapon`/`FireProjectile` and any per-effect adapter that mutates before the whole operation is accepted.
- **Dependencies:** Engine-neutral effect-batch contract; exact operation/equipment/participant/generation context; adapters over existing projectile, hit, explosion, area, chain, pool, and damage authorities.
- **Relevant PRs:** #206, #212, #213
- **Acceptance tests:** A multi-effect request either commits every effect once or commits none; rejection after validation leaves no projectile/damage residue and permits exact retry; conflicting duplicate operations reject; independent operations may produce identical effects.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `PR #212: Assets/ShooterMover/Production/Stage1/Weapons/Stage1WeaponExecutionV1.cs`

### 4. Reusable player runtime/presentation

- **Status:** Not Started
- **Current source of truth:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` constructs and owns the player `GameObject`, `Rigidbody2D`, collider, input actions, movement adapters, target/hazard adapters, sprite, mounts, boost trail, restart, and disposal.
- **Completed foundation:** Movement, input, combat-target, hazard, and shared entity capabilities are merged. PR #211 contains an unmerged `Stage1PlayerPresentationV1` extraction.
- **Live integration status:** Current main has no reusable player installer/view that is the sole owner of construction, restart, presentation refresh, and disposal.
- **Remaining legacy owner:** `Stage1VisibleSliceController.BuildPlayer`, `RefreshBoostPresentation`, player fields, `inputActions` cleanup, and player restart block.
- **Dependencies:** Stable player identity; class/loadout configuration; movement lifecycle coordination; later player vital adapter; explicit ownership/disposal contract.
- **Relevant PRs:** #211, #213, #215
- **Acceptance tests:** Exactly one player runtime owner and one `InputActionAsset` exist; movement/input/collision/boost behavior matches current Stage 1; restart preserves stable identity while advancing lifecycle generation; Stage 2/survival fixtures reuse the same installer without Stage1 dependencies.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs`; `docs/architecture/gameplay/PLAYER_ACTOR_FOUNDATION_V1.md`

### 5. Player vital/damage authority integration

- **Status:** Foundation
- **Current source of truth:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs.playerHealth` is live truth; hazards and enemy projectile callbacks mutate it directly; HUD and restart read/reset it.
- **Completed foundation:** PR #215 merged `PlayerActorAuthority`, shared `GameplayEntityIdentity`/ownership, generation-scoped damage/healing deduplication, death attribution, and deterministic restart commands/results.
- **Live integration status:** No Stage 1 adapter constructs or invokes `PlayerActorAuthority`. The merged foundation explicitly states that Stage 1 migration is incomplete.
- **Remaining legacy owner:** `Stage1VisibleSliceController.RequestDamage`, `RequestInstantDeath`, `ApplyTurretProjectileDamageToPlayer`, HUD snapshot creation, and `QuickRestart` health reset.
- **Dependencies:** Trusted participant/source attribution; Unity hit/hazard adapters; movement/restart generation coordination; read-only HUD projection; run/death observers.
- **Relevant PRs:** #210, #213, #215
- **Acceptance tests:** Hazard and enemy projectile damage enter one authority; exact replay does not damage twice; conflicting reuse rejects; death emits once with source actor/participant attribution; HUD reads immutable snapshots; restart rejects stale generation and removes local `playerHealth`.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs`; `docs/architecture/gameplay/PLAYER_ACTOR_FOUNDATION_V1.md`

### 6. Enemy package migration

- **Status:** Foundation
- **Current source of truth:** Existing enemy packages remain canonical for enemy health/runtime behavior. `Stage1VisibleSliceController` selects package types, constructs definitions/contexts, activates/deactivates packages, applies some hits, and interprets destruction.
- **Completed foundation:** PRs #216 and #217 merged `EnemyRuntimeProjection`, generic attack intents, deterministic decision/debug snapshots, room-clear roles, geometric perception, separate vision/attack arcs, and a representative Mobile Blaster Droid projection factory.
- **Live integration status:** The representative projection is not the live Stage 1 decision path. Blaster Turret, Pursuer Drone, Ram Droid, and Four Blaster Elite still need package-specific projection/adapters; the controller still branches on Stage 1 enemy IDs.
- **Remaining legacy owner:** `Stage1VisibleSliceController.BuildAuthoredRooms`, `BuildMobileBlasterDroid`, `BuildTurret`, hit forwarding, activation/projection switching, and enemy-specific HUD interpretation.
- **Dependencies:** Per-package projection factories/adapters; Unity perception facts; generic attack executors; attributed terminal facts; room occupancy registration.
- **Relevant PRs:** #210, #213, #215, #216, #217
- **Acceptance tests:** Each package projects canonical actor state without copying health; vision and attack arcs remain distinct; decisions are deterministic; attack intents execute through capability IDs; required/optional roles project correctly; controller contains no enemy package construction or decision ownership.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyDecisionPolicy.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyPerceptionBuilder.cs`; `docs/architecture/gameplay/ENEMY_RUNTIME_FOUNDATION_V1.md`

### 7. Reusable room occupancy/clear runtime

- **Status:** Partially Live
- **Current source of truth:** `RoomMissionLayoutV1` is live for graph progress and traversal. `Stage1VisibleSliceController.DemoRoomProjection` and `enemyDestroyedReaders` remain the live occupancy/clear truth and drive doors/objectives.
- **Completed foundation:** PR #181 merged immutable room graph definitions and `RoomMissionLayoutV1`. PR #210 merged the working two-room loop and retained room state. PR #216 merged `EnemyRuntimeProjection.BlocksRoomClear` role semantics.
- **Live integration status:** Graph/traversal is live, but occupant registration, exact occupant identity, terminal reporting, clear transitions, and door projection are still controller-local.
- **Remaining legacy owner:** `Stage1VisibleSliceController.DemoRoomProjection`, `BuildAuthoredRooms` enemy branches, `RefreshArenaFlow`, door condition reading, and room activation logic.
- **Dependencies:** Enemy runtime projections/terminal facts; exact room and occupant IDs; deterministic registration/removal; layout completion command; door/read-only objective projections.
- **Relevant PRs:** #181, #210, #213, #216, #217
- **Acceptance tests:** Zero/one/many required occupants; optional/non-blocking occupants; duplicate terminal notifications; independent actors sharing a definition; leave/revisit retained state; locked exits; restart; no controller-owned occupancy list or clear calculation.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayoutV1.cs`; `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1RoomGraphDefinitionV1.cs`; `docs/architecture/missions/ROOM_GRAPH_V1.md`; `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs`

### 8. Restart lifecycle

- **Status:** Partially Live
- **Current source of truth:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs.QuickRestart` manually advances `restartGeneration` and resets player health, shot/damage counters, loadout, room layout, movement, enemies, gameplay scope, hit adapter, HUD, camera, and transient visuals in one ordered transaction.
- **Completed foundation:** Movement, room, enemy packages, gameplay scope, HUD/camera, and `PlayerActorAuthority` expose bounded restart/reset seams; shared restart vocabulary and lifecycle generation exist.
- **Live integration status:** Subsystem restart calls are live, but registration, phases, ordering, failure behavior, and disposal are not owned by a reusable lifecycle coordinator.
- **Remaining legacy owner:** `Stage1VisibleSliceController.QuickRestart` and `OnDestroy` cleanup order.
- **Dependencies:** Explicit restart participant registry; stable participant IDs; deterministic phases only where required; rollback/fail-closed policy; weapon/effect/player/enemy/room adapters.
- **Relevant PRs:** #210, #211, #212, #213, #215
- **Acceptance tests:** Initial run and restarted run satisfy identical construction invariants; stable identities remain stable while generations advance; stale commands/effects reject; no old projectiles/cooldowns/subscriptions survive; participant order is explicit; controller has no broad restart transaction.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs`

### 9. HUD/camera projection

- **Status:** Partially Live
- **Current source of truth:** `VisibleSliceGeneralCombatHud` and `VisibleSliceCameraRig` are live components, but `Stage1VisibleSliceController` constructs/configures them, synthesizes gameplay snapshots from its own health/enemy/room truth, refreshes them each frame, and also draws a second compact `OnGUI` HUD.
- **Completed foundation:** Dedicated HUD/camera presentation components and read-only source interfaces exist.
- **Live integration status:** Presentation is extracted in shape, not in truth ownership. The controller remains snapshot assembler, refresh coordinator, camera factory, and duplicate HUD owner.
- **Remaining legacy owner:** `Stage1VisibleSliceController.BuildUi`, `BuildCamera`, `TryRead`, `TryReadSnapshot`, `RefreshHud`, `RefreshTurretPresentation`, `OnGUI`, grayscale/reduced-effects coordination.
- **Dependencies:** Player/enemy/room immutable projections; scene-bound configuration; explicit continuous-vs-event update policy; one HUD owner and one camera owner.
- **Relevant PRs:** #210, #211, #213
- **Acceptance tests:** HUD and camera consume immutable projections only; health/objective/enemy state matches authorities; one HUD and one camera owner exist; reduced-effects/grayscale/restart behavior is preserved; no gameplay mutation or authority lookup occurs in presentation.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`

### 10. Stage1 controller retirement

- **Status:** Not Started
- **Current source of truth:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` remains the approximately 2,192-line live scene composition root and practical owner/coordinator for player construction, local health, weapons, enemies, rooms, HUD, camera, and restart.
- **Completed foundation:** Several independent authorities and projections are merged, but none of the required ownership seams has been fully removed from the controller on current main.
- **Live integration status:** The controller is still required for the playable two-room slice and cannot yet be deleted or reduced to composition-only wiring.
- **Remaining legacy owner:** The entire `Stage1VisibleSliceController` ownership graph and its `ShooterMover.TestSupport.VisibleSlice` namespace/path.
- **Dependencies:** All live migration rows above; end-to-end Stage 1 route/PlayMode/manual proof; one-controller-editor sequencing.
- **Relevant PRs:** #210, #211, #213, #215, #216, #217
- **Acceptance tests:** All measurable retirement gates in this document pass; Stage 1 remains playable; controller contains scene composition and stage-specific bindings only; generic behavior is reusable by a second fixture without controller cloning.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`

### 11. Naming/path normalization

- **Status:** Not Started
- **Current source of truth:** Live production-routed code still uses TestSupport/VisibleSlice/Demo naming, while some unmerged alternatives use Production/Stage1 for partly generic behavior.
- **Completed foundation:** The architecture audit identified scope mismatches; merged gameplay-entity paths demonstrate a suitable game-wide home for engine-neutral identity/decision contracts.
- **Live integration status:** No current-main normalization has occurred. Renaming before ownership migration would hide rather than solve coupling and could create Unity GUID/assembly churn.
- **Remaining legacy owner:** `ShooterMover.TestSupport.VisibleSlice`, VisibleSlice-prefixed generic HUD/camera types, demo IDs, and Stage1-prefixed generic concepts in unmerged drafts.
- **Dependencies:** One authoritative live path per subsystem; serialized GUID preservation; assembly dependency audit; no concurrent controller migration.
- **Relevant PRs:** #211, #212, #213, #215, #216, #217
- **Acceptance tests:** No production-routed gameplay lives under TestSupport; generic systems do not depend on Stage1 namespaces; stage-specific definitions retain Stage1/Level1 names; Unity GUIDs and serialized references remain valid; focused compile/tests pass after each narrow move.
- **Evidence paths:** `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs`; `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs`

## ADR-WPN-001 — Weapon runtime alternatives remain evidence, not an approved implementation

### Context

Four distinct sources currently describe weapon behavior:

| Alternative | Evidence-backed strengths | Gaps or risks |
|---|---|---|
| **PR #206** | Reads the merged typed catalog; derives firing profiles without weapon-name switches; rejects invalid/preview-only definitions; carries deterministic per-mount cooldown/shot-sequence/spread behavior. | Open and conflicting; edits the retained controller directly; bridges fixture/package selection to definition IDs rather than the production exact equipment instance; does not provide a complete atomic advanced-effect transaction; Unity proof is not recorded as complete. |
| **PR #212** | Carries the exact active equipment object; resolves a runtime weapon ID through a registry/dispatcher; fails closed for unknown IDs; demonstrates adding a fifth executor without dispatcher/controller branching; separates transient cooldown/operation state. | Open and stacked on #211; not live; application contracts depend on `UnityEngine.Vector3`; equipment and shooter contexts are typed as `object`; tuning duplicates catalog data; the sink accepts effects one at a time, so a later rejection can leave earlier effects committed. |
| **Merged JSON catalog / PR #202** | Official validated source for weapon definitions, live/preview status, tuning/content fields, deterministic ordering, and fingerprints. | Deliberately owns no live firing, effect realization, equipment selection, or cooldown state. |
| **Retained controller firing** | It is the current playable source of truth and preserves today's visible-slice behavior. | Iterates a fixture rather than authoritative active equipment; branches on weapon/package IDs; silently falls back to blaster behavior; owns projectile construction/cadence in the scene controller; cannot be the target architecture. |

### Decision

This tracker does **not** select #206 or #212 for merge and does not approve either branch wholesale.

A fresh current-main successor must be implemented behind evidence-backed contracts. PRs #206 and #212 may be mined for tests, algorithms, and narrowly reusable code only after revalidation against current main. The merged JSON catalog remains the content authority. The retained controller remains temporary live behavior only until a focused handoff PR replaces it.

### Properties that must be preserved

- exact concrete equipment-instance identity from authoritative active-slot selection;
- explicit shooter entity and trusted run-participant attribution;
- separate operation ID and lifecycle generation;
- runtime behavior resolution that fails closed for unknown, missing, invalid, or preview-only content;
- tuning/content read from the merged validated catalog rather than a second hardcoded catalog;
- an open/closed registry or equivalent composition seam: a fifth weapon normally adds a definition, behavior, registration, and tests—not controller or dispatcher branching;
- deterministic cooldown, burst/shot sequence, spread, target ordering, and replay behavior;
- one immutable atomic effect batch carrying exact equipment/shooter/operation/generation context;
- reuse of existing projectile, hit, explosion, area, chain, pool, enemy-damage, and room authorities rather than parallel state;
- restart/disposal that clears only transient execution state while preserving authoritative equipment identity.

### Behavior that must be superseded

- `Stage1VisibleSliceController.FireSelectedLoadout` / `FireWeapon` ID branching;
- the silent blaster fallback;
- definition-only identity when a concrete equipment instance exists;
- Stage1-prefixed engine-neutral application contracts;
- `object` equipment/shooter identity;
- `UnityEngine` vectors inside deterministic application policy;
- one-by-one effect submission with partial-commit risk;
- duplicated tuning that can disagree with the merged catalog;
- prolonged coexistence of two live firing paths.

### Evidence gate before choosing implementation details

The successor PR must first prove, on current main:

1. typed engine-neutral request/result and atomic-batch contracts;
2. catalog mapping for the current starter set;
3. exact equipment/participant/operation/generation propagation;
4. all-or-nothing multi-effect commit and exact retry;
5. a fifth fixture behavior without dispatcher/controller edits;
6. focused EditMode proof before the sole controller handoff PR begins.

## Recommended PR sequence

### Controller-edit mutex

`Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` is a serialized live-scene hotspot. At most **one open/in-progress task may modify it at a time**. A controller-touching task must start from the latest main after the preceding controller task is merged or closed, record its launch SHA, and make a small mechanical delegation/deletion patch. Foundation-only tasks may overlap when their owned paths and contracts do not conflict.

| Order | Proposed task | Controller edit | Purpose and exit condition |
|---:|---|---|---|
| 0 | `ARCH-TRACKER-002` | No | Publish this current-main tracker and inventory. |
| 1 | `WPN-CONTRACT-001` | No | Add engine-neutral typed weapon execution, catalog mapping, atomic effect batch, and focused tests. No Stage 1 call-site change. |
| 2 | `WPN-LIVE-001` | **Yes — exclusive** | Resolve authoritative active equipment, bind the Unity effect adapter, delete controller weapon-ID branches/fallback, and prove all starter behaviors/restart. |
| 3 | `PLAYER-RUNTIME-001` | No | Build the reusable player runtime/view installer with explicit ownership, configuration, restart, and disposal. |
| 4 | `PLAYER-RUNTIME-LIVE-001` | **Yes — exclusive** | Delegate player construction/boost/restart/disposal and delete duplicate controller fields/helpers. |
| 5 | `PLAYER-VITAL-ADAPTER-001` | No | Adapt hit/hazard facts and trusted attribution into merged `PlayerActorAuthority`; expose immutable projections. |
| 6 | `PLAYER-VITAL-LIVE-001` | **Yes — exclusive** | Replace direct health writes/HUD health truth and delete local `playerHealth`. |
| 7 | `ENEMY-PACKAGES-001` | No | Add/complete package projections, perception adapters, attack executors, and attributed terminal facts without editing Stage 1 composition. |
| 8 | `ENEMY-LIVE-001` | **Yes — exclusive** | Register packages through reusable composition and remove controller enemy construction/decision ownership. |
| 9 | `ROOM-OCCUPANCY-001` | No | Add exact occupant registration, role-aware clear state, duplicate terminal handling, and door/objective projections. |
| 10 | `ROOM-OCCUPANCY-LIVE-001` | **Yes — exclusive** | Delegate live occupancy/clear truth and delete `DemoRoomProjection`/enemy reader logic. |
| 11 | `LIFECYCLE-001` | No | Add explicit restart participants/phases, failure policy, and transient effect cleanup. |
| 12 | `LIFECYCLE-LIVE-001` | **Yes — exclusive** | Replace the controller's broad restart transaction with one lifecycle coordinator command. |
| 13 | `HUD-CAMERA-001` | No | Define projection-driven HUD/camera bindings and ownership tests over player/enemy/room snapshots. |
| 14 | `HUD-CAMERA-LIVE-001` | **Yes — exclusive** | Remove controller snapshot assembly, duplicate compact HUD, and camera/HUD refresh ownership. |
| 15 | `STAGE1-RETIRE-001` | **Yes — exclusive** | Reduce the controller to scene composition and Stage 1-specific bindings; prove end-to-end playable flow. |
| 16 | `NAME-PATH-001` | No controller behavior change | Move/rename only after ownership is stable, preserving Unity GUIDs and assembly direction in narrow batches. |

No later controller task should begin merely because its foundation exists. It begins only when the preceding controller patch is merged/closed and the current live behavior is green.

## Measurable controller retirement gates

The controller is not considered retired based on line count alone. It is retired only when all gates below are true:

1. **No local player health:** no mutable `playerHealth` field, direct health subtraction, instant-death assignment, or restart health reset exists in the controller.
2. **No weapon-ID branch or silent fallback:** the controller compares no weapon/package/runtime IDs to choose behavior; an unmapped weapon fails closed through the execution boundary.
3. **No enemy package construction or decision ownership:** the controller creates no enemy definitions/contexts, contains no package-specific build/decision branches, and applies no package-specific hit logic.
4. **No room occupancy or clear truth:** the controller stores no occupant readers/list, calculates no `AreAllEnemiesDestroyed`, and does not decide room clear from package state.
5. **No broad restart transaction:** the controller does not manually reset player, weapon, effect, enemy, room, HUD, camera, counters, and subscriptions. It may issue one explicit lifecycle command to a composed coordinator.
6. **Composition-only scope:** remaining code validates Stage 1 data, constructs/registers reusable components, supplies scene-specific serialized bindings, connects stage-specific route/completion callbacks, and disposes owned composition.
7. **No duplicate HUD/camera truth:** presentation consumes immutable projections and there is one HUD owner and one camera owner.
8. **No generic production gameplay under TestSupport:** production-routed generic behavior no longer depends on `ShooterMover.TestSupport.VisibleSlice`.
9. **Cross-stage reuse proof:** a minimal second stage or mode fixture reuses player, weapon/effects, enemy, room, HUD/camera, and lifecycle components without cloning the controller.
10. **Proof:** focused EditMode/PlayMode suites and an end-to-end Stage 1 manual route pass at the final controller head. Unity proof is claimed only when actual XML/manual evidence exists.

A smaller file is expected after these gates, but no arbitrary target line count substitutes for ownership proof.

## Reference verification register

### Current-main paths verified at `b2bf4348ab6f827a737add53278d57568684f552`

- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`
- `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs`
- `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs`
- `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyDecisionPolicy.cs`
- `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyPerceptionBuilder.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayoutV1.cs`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1RoomGraphDefinitionV1.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogJsonImporter.cs`
- `docs/architecture/gameplay/PLAYER_ACTOR_FOUNDATION_V1.md`
- `docs/architecture/gameplay/ENEMY_RUNTIME_FOUNDATION_V1.md`
- `docs/architecture/weapons/WEAPON_CATALOG_V1.md`
- `docs/architecture/missions/ROOM_GRAPH_V1.md`

### Branch-only evidence paths verified through their PR changed-file inventories

- PR #206: `Assets/ShooterMover/Runtime/Application/Weapons/Firing/WeaponDefinitionFiringRuntime.cs`
- PR #206: `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1WeaponRuntimeLoadoutAdapter.cs`
- PR #206: `docs/architecture/weapons/WPN_RUNTIME_STAGE1_V1.md`
- PR #212: `Assets/ShooterMover/Production/Stage1/Weapons/Stage1WeaponExecutionV1.cs`
- PR #212: `docs/authoring/STAGE1_WEAPON_EXECUTION_V1.md`
- PR #213: `docs/architecture/audits/ARCH_AUDIT_001.md`
- PR #213: `docs/architecture/audits/ARCH_AUDIT_001_inventory.csv`

### PR state verified while preparing this tracker

- Merged: #180, #181, #201, #202, #210, #215, #216, #217.
- Open draft/unmerged: #206, #211, #212, #213.
- #212 is stacked on #211; #213 is stacked on #212.
- No PR was merged, closed, or retargeted by this task.

## Validation contract for this documentation PR

Run and record:

```text
git diff --check
```

Also verify:

- the diff contains only:
  - `docs/architecture/audits/MASTER_GAMEPLAY_REFACTOR_V1.md`
  - `docs/architecture/audits/MASTER_GAMEPLAY_REFACTOR_V1_inventory.csv`
- the CSV parses with exactly 11 unique migration areas;
- every CSV status is one of `Not Started`, `Foundation`, `Partially Live`, `Live`, or `Retired`;
- all required CSV fields are non-empty;
- every referenced current-main path exists at the recorded starting SHA;
- every referenced PR exists and its merged/draft/stacked state is accurately described.

This task requires no Unity compilation or test execution because it changes no C#, scene, prefab, asset, package, or generated file. It must not claim Unity proof.
