# Stage 1 Visible-Slice Planning Amendment

**Status:** selected by the human lead; authoritative when this amendment pull request merges  
**Scope:** a bounded screen-visible Stage 1 prototype before deeper evidence execution  
**Architecture impact:** no replacement of accepted movement, combat, enemy, encounter, mission, collision, registry, or persistence authority

## Decision

Prioritize one screen-visible Stage 1 prototype that reads as a dark industrial game room rather than placeholder/debug geometry. The prototype must let the human lead select a fixed loadout, enter one readable room, move, aim, fire, damage and destroy an enemy, read the Blaster Turret warning, observe HUD feedback, and restart to a clean initial state.

This is a presentation and composition amendment, not a second game architecture. It does not authorize inventory, rewards, unlocks, progression, mission persistence, campaign saves, registry edits, final art acceptance, or a new collision/movement/combat authority.

The accepted Stage 1 aggregate cap remains **50 focused lead days / 12 calendar weeks**. The amendment is funded by explicit resequencing and use of existing S1.2/S1.3 reserve; it does not silently increase either cap.

## Current-main verification and lifecycle gate

At the planning branch point, current `main` is `103e6fdc3ba8024662137f660507ce6102e0a76c` (merged PR #101, EN-007). No merged visible-slice amendment, `VS-*` task card, generated visible-slice batch, or canonical-backlog entry exists on that commit.

WP-010 is an open draft implementation PR, not a merged dependency. Therefore this pull request performs the **planning-amendment stage only**. It must not create a `VS-*` task batch, modify `assembly/generated/task_backlog.json`, initialize slots, dispatch agents, or implement Unity assets. After this amendment merges, a fresh Task Splitter branch and separate PR may materialize the seven proposed tasks.

## Temporary art intake boundary

The human lead identified these local prototype inputs:

- industrial floor: `tile_concept_1.jfif`;
- room reference: `level_idea_1.png`;
- standing turret/droid: `standing_turret_weak.png`;
- additional candidate props under `C:\Users\Yeeroo\Desktop\sprites`.

These files are local inputs unavailable through GitHub. This planning agent has **not** inspected, opened, converted, licensed, counted, or checksummed them. No claim about dimensions, alpha, color space, import suitability, provenance, or visual quality is made here.

VS-001 is the sole local-intake task. It must copy only selected inputs, compute checksums after intake, record source/provenance and prototype-only status, create Unity-ready derivatives when required, preserve originals under the task-owned source path, and reject any asset whose rights or origin cannot be recorded. Consumers depend on VS-001 and may not recreate, rename, re-export, or independently import the same local files.

## Authority and presentation rules

1. Accepted movement remains owned by MT-007/MT-008/MT-009/MT-010, with MT-011 as read-only thruster status.
2. Accepted player weapon execution remains owned by CB-004/CB-005/CB-006/CB-008/CB-009, with CB-010 as read-only four-mount status.
3. Enemy health, damage, contact, lifecycle, Unity 2D bridging, and turret behavior remain owned by EN-002, EN-003, and EN-007.
4. EN-009 remains the enemy-package validation gate. EN-010 and EN-011 remain the accepted encounter-composition inputs for benchmark/short-route behavior.
5. Scenes and presentation components consume immutable/read-only state or submit accepted messages. They do not decide durable room completion, reward, inventory, mission, or save truth.
6. The visible slice may keep one deterministic **session-only** player-vital value inside the VS-007 TestSupport composition so incoming accepted damage can be shown and reset. It is prototype debt, cannot persist, cannot alter movement/collision authority, and cannot become a campaign-health or MissionRunState substitute.
7. Reduced-effects behavior preserves timing, text/shape/count warnings, and essential HUD information. Color is always supplemental.
8. WP-010 retains exclusive ownership of its four-slot weapon-status strip, temporary weapon audiovisual identity, folder, and focused test. VS-004 may position a reserved viewport region for that strip but may not duplicate, wrap, modify, restyle, or test its four slots.

## Serialized ownership and concurrency

Only one task may own a serialized scene at a time.

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity` is owned **only by VS-007**.
- EH-004 `Stage1BenchmarkArena` and EH-005 `Stage1ShortRouteShell` remain read-only.
- VS-002 authors a task-local room-presentation prefab/package, not the integration scene.
- VS-003 authors task-local turret presentation assets, not EN-007 assets or the integration scene.
- VS-004 and VS-005 author task-local UI prefabs/assets, not the integration scene.
- VS-006 authors a task-local camera/readability rig/configuration, not the integration scene.
- Parallel VS-002 through VS-006 branches must contain no `.unity` scene change.
- VS-007 starts only after its dependencies merge, composes their immutable assets into the one integration scene, and is the serial conflict-resolution owner. It does not copy another task's assets into a second folder.

Inseparable Unity `.meta` files are allowed only beside the exact task-owned paths. Parent-folder metadata must be assigned to the task that first creates that exact task-owned folder; no two tasks may create or rewrite the same parent `.meta`.

## Proposed stable task split

The following identities and ownership boundaries are proposed for the later Task Splitter PR. They are **not** generated or dispatchable by this planning PR.

### VS-001 — Intake temporary visible-slice art

**Purpose:** bring the named local assets and a small selected prop subset into Git with prototype-only provenance, original-file preservation, deterministic checksums, and Unity import settings.

**Allowed paths:**

- `source-assets/prototype/stage1-visible-slice/`
- `Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/`
- `docs/provenance/prototype/stage1-visible-slice/`

**Dependencies:** accepted repository layout/provenance rules; no other VS task.  
**Blocks:** VS-002, VS-003, VS-004, VS-005, VS-006, VS-007.  
**Focused lead days:** **0.30**, allocated to S1.3 reserve.

**Acceptance boundary:** exact originals and any Unity-ready derivatives are inventoried and checksummed after local intake; prototype-only use and removal path are explicit; unknown-rights assets are rejected; no gameplay, scene, prefab, UI, test, or generated-registry edit.

### VS-002 — Build the dark industrial room presentation shell

**Purpose:** create a visual room package with dark tiled floor, walls, doors, selected props, room markers, sorting/lighting presentation, and a readable play-space mask while leaving collision and room/mission truth with accepted owners.

**Allowed paths:**

- `Assets/ShooterMover/ContentPackages/Rooms/Stage1VisibleSlicePresentation/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceRoomPresentation/`

**Dependencies:** VS-001; read-only architectural inputs from EH-004/EH-005 and accepted 2D rendering settings.  
**Blocks:** VS-007.  
**Focused lead days:** **0.50**, allocated to S1.3 reserve.

**Acceptance boundary:** task-local prefab(s) contain presentation/sorting/lighting/marker data only; no `.unity` edit, durable room state, mission rule, collision-authority rewrite, scene search, or duplicated EH-004/EH-005 geometry authority.

### VS-003 — Present the accepted Blaster Turret

**Purpose:** apply `standing_turret_weak.png` to EN-007 through a detachable presentation layer that reads enemy current/max health, damage reaction, firing warning, destroyed/deactivated state, and reset generation.

**Allowed paths:**

- `Assets/ShooterMover/Runtime/Presentation/VisibleSliceBlasterTurret/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceBlasterTurretPresentation/`

**Dependencies:** VS-001, EN-002, EN-003, EN-007, EN-009.  
**Blocks:** VS-007.  
**Focused lead days:** **0.45**, allocated to S1.3 reserve.

**Acceptance boundary:** EN-002/EN-007 remain authoritative; the presentation consumes immutable state/events and never applies damage, advances cadence, chooses a target, owns projectile execution, or edits `ContentPackages/Enemies/BlasterTurret/`. Warning timing remains EN-007-owned and readable without color or full effects.

### VS-004 — Add the general combat HUD

**Purpose:** provide player-health, thruster, reticle, hit-confirmation, focused-enemy-health, and objective/room-clear presentation around a reserved WP-010 four-slot region.

**Allowed paths:**

- `Assets/ShooterMover/UI/VisibleSliceGeneralCombatHud/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceGeneralCombatHud/`

**Dependencies:** VS-001, CS-004, MT-011, CB-009, EN-002/EN-003; WP-010 is a layout input but remains separately owned.  
**Blocks:** VS-007.  
**Focused lead days:** **0.45**, allocated to S1.2 reserve.

**Acceptance boundary:** injected read-only status sources only; no player-vital mutation, enemy mutation, objective completion decision, scene edit, persistence, or WP-010 folder/test change. Reticle/hit confirmation must correspond to accepted aim/confirmed-hit facts. Critical health/thruster/objective information cannot depend on color.

### VS-005 — Add the temporary fixed-loadout selector

**Purpose:** present the fixed WP-008 comparisons before room entry and hand one selected immutable fixture to the final composition.

**Allowed paths:**

- `Assets/ShooterMover/UI/VisibleSliceLoadoutSelector/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceLoadoutSelector/`

**Dependencies:** VS-001, WP-008.  
**Blocks:** VS-007.  
**Focused lead days:** **0.15**, allocated to S1.2 reserve.

**Acceptance boundary:** exactly the approved fixed fixture IDs/ordered four slots; no inventory, reward, unlock, shop, reroll, random modifier, save, PlayerPrefs, persistence, package tuning, or scene edit. Restart returns to the deterministic pre-entry selection state.

### VS-006 — Apply the orthographic camera and readability pass

**Purpose:** provide orthographic room framing, bounded follow/room clamp, safe HUD margins, screen-edge warning checks, reduced-effects response, and color-independent readability constraints.

**Allowed paths:**

- `Assets/ShooterMover/Runtime/Presentation/VisibleSliceCameraReadability/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceCameraReadability/`

**Dependencies:** VS-001, UF-003, MT-010, MT-011; consumes injected reduced-effects state without owning global settings or persistence.  
**Blocks:** VS-007.  
**Focused lead days:** **0.35**, allocated to S1.3 reserve.

**Acceptance boundary:** one task-local camera rig/configuration; no `.unity` edit, movement/Rigidbody write, combat simulation change, global quality mutation, settings persistence, or removal of essential warnings. Tests cover 16:9 reference framing, safe margins, reduced-effects parity, grayscale/color-independent warnings, and restart.

### VS-007 — Compose and prove the final Stage 1 visible slice

**Purpose:** serially compose the accepted runtime, encounter fixtures, imported/presentation assets, general HUD, WP-010 strip, fixed-loadout selector, and camera rig into one playable prototype scene.

**Allowed paths:**

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`
- `Assets/ShooterMover/TestSupport/VisibleSlice/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/`
- `docs/verification/visible-slice/STAGE1_VISIBLE_SLICE_PLAYABLE.md`

**Dependencies:** VS-001 through VS-006; WP-008; merged WP-010; EN-009; EN-010; EN-011; EH-006; MT-010/MT-011; CB-006/CB-008/CB-009/CB-010; EN-002/EN-003/EN-007.  
**Blocks:** later visible-slice-dependent human/evidence execution; it does not automatically accept WP-012, EN-012, EN-013, AR, or GATE tasks.  
**Focused lead days:** **0.95**, allocated to S1.3 reserve.

**Acceptance boundary:** select a fixed loadout, enter the room, move, aim, fire, receive/reflect session-only health feedback, damage and kill at least one enemy, observe a complete EN-007 warning/fire/recovery/deactivation path, see room-clear/objective presentation, and quick-restart cleanly. Fifty restart cycles leave no duplicate scene objects, callbacks, projectiles, enemies, HUD owners, camera owners, selected-loadout state, or session-health state. No durable state, build-settings edit, MissionRunState, inventory, reward, save, registry, or persistence output is created.

## Dependency and dispatch order

1. Merge this planning amendment.
2. In a **separate Task Splitter PR**, create a dedicated `stage1-visible-slice` batch, materialize VS-001 through VS-007, update the canonical backlog/index/collaboration artifacts, and validate exact path ownership and an acyclic graph.
3. Do not dispatch any VS task from the planning PR.
4. Dispatch VS-001 first through a local worktree that can access the named files.
5. After VS-001 merges, VS-002 through VS-006 may run concurrently when their non-VS dependencies are merged. None may edit `Stage1VisibleSlice.unity`.
6. WP-010 must merge with its own focused proof before VS-007; its open draft does not satisfy the dependency.
7. EN-009 validates the enemy roster; EN-010 and EN-011 provide accepted encounter inputs before VS-007.
8. VS-007 runs last, on fresh current `main`, as the sole integration-scene owner.
9. After VS-007 proof, resume the deeper WP-011/WP-012, EN-012/EN-013, AR, and Stage 1 gate evidence sequence against the materially visible prototype. This is resequencing, not evidence removal.

## Focused-day and cap impact

The seven tasks total **3.15 focused lead days**:

| Milestone allocation | Current planned direct spend | Visible-slice allocation | Amended direct spend | Remaining reserve |
|---|---:|---:|---:|---:|
| S1.2 Four-weapon combat | 9.10 / 10.00 | +0.60 | **9.70 / 10.00** | **0.30** |
| S1.3 Enemies and short route | 7.45 / 10.00 | +2.55 | **10.00 / 10.00** | **0.00** |

The Stage 1 aggregate cap remains **50 focused lead days** because the 3.15 days consume previously unallocated S1.2/S1.3 review/iteration reserve. This deliberately uses all remaining S1.3 reserve and leaves only 0.30 day in S1.2. Any estimate growth, additional visible-slice task, retained S1.3 reserve, or new polish requirement cannot be absorbed silently.

If the human lead does **not** approve this reserve consumption/resequencing, the exact alternative decision is to raise the Stage 1 aggregate cap from **50.00 to 53.15 focused lead days** and the S1.3 cap from **10.00 to 12.55 focused lead days**, with a revised calendar cap and written cap-review record. This amendment does not select that cap increase.

## Required Unity and human evidence

The later task cards must require, at minimum:

- pinned Unity `6000.3.19f1` import/compilation;
- focused PlayMode tests for every VS task;
- static path audit proving WP-010 exclusivity and no parallel `.unity` edits;
- intake inventory, provenance records, and post-intake SHA-256 values from VS-001;
- room screenshot at the reference orthographic framing;
- default/reduced-effects and grayscale/color-independent capture pairs;
- turret warning/fire/recovery/damage/deactivation capture tied to EN-007 state;
- general HUD capture with the WP-010 region present but unmodified;
- loadout-selection trace proving WP-008 fixture identity and no persistence;
- VS-007 end-to-end PlayMode log and fifty-restart leak summary;
- manual playable check for loadout selection, room readability, movement, aiming, firing, hit confirmation, enemy health/damage/death, turret warning, objective/room clear, and clean restart;
- explicit statement that the evidence demonstrates a prototype composition, not final art, final balance, Stage 1 gate acceptance, or durable game state.

Where EH-008/EH-009/EH-010 manifested evidence entrypoints are available, VS-007 should be captured through them after the direct focused test passes. Technical validity remains separate from visual/feel observations.

## Non-goals

- no Unity implementation in this amendment;
- no generated backlog/batch/slot/collaboration update in this amendment;
- no local-asset inspection or checksum claim in this amendment;
- no final art, animation, VFX, audio, shader, or lighting pipeline acceptance;
- no inventory, rewards, unlocks, shops, progression, save, mission persistence, or durable room-clear state;
- no collision, movement, combat, enemy-health, turret-cadence, encounter, registry, or input-authority redesign;
- no edit to WP-010-owned paths or tests;
- no Stage 2 generation or dispatch.

## Rollback

Before task generation, rollback is one planning revert: remove this amendment and restore the prior milestone/handoff wording. After a later Task Splitter PR materializes VS tasks, rollback must remove the dedicated visible-slice batch and VS-001 through VS-007 from generated artifacts as one graph change. Implementation rollback remains task-local; VS-007 scene/composition removal must not require reverting accepted movement, combat, enemy, WP-010, EN-009, EN-010, or EN-011 work.
