# ARCH-AUDIT-001 — OOP, ownership, coupling, and extensibility assessment

## Audit boundary

- **Launch SHA:** `f0f7d5e2519bc7b5fb9afb096da41889cd02cc0f`
- **Launch branch:** `agent/stage1-weapon-001-extensible-execution`
- **Audit branch:** `agent/arch-audit-001-oop-ownership-assessment`
- **Primary repository state inspected:** current `main` through `923a2bcdb3b8c9a2c80d8154a9d65291d71a514c`, production-flow PR #211 through `1ab0323df7dd6cfab2beed032a3573ea993f5ef7`, and weapon-execution PR #212 through the launch SHA above.
- **Method:** static repository and PR inspection through the GitHub connector. No Unity editor, compilation, PlayMode execution, scene traversal, profiler, or XML test execution was available. Runtime conclusions are therefore explicitly qualified.
- **Production changes:** none. This branch adds documentation and a machine-readable inventory only.

## 1. Executive summary

### Overall architectural health

The codebase is **mixed but recoverable**. The engine-independent authorities and immutable/replay-aware application contracts are the strongest part of the repository. Inventory, concrete equipment identity, loadout resolution, reward/run-result work, skills, crafting, and related presentation ports generally show deliberate source-of-truth boundaries. The current live Stage 1 scene, however, still depends on a retained prototype controller that acts as composition root, object factory, input loop, combat coordinator, player-vital owner, enemy/room observer, HUD projection, camera owner, restart coordinator, projectile factory, VFX coordinator, and test surface.

The repository does **not** justify a broad rewrite. Stable authorities and content definitions should be preserved. Incremental extraction behind existing contracts is safer because the major problem is not that every contract is unusable; it is that the retained scene path bypasses or duplicates newer boundaries.

### Strongest existing design decisions

1. Stable IDs and concrete equipment-instance identity are represented explicitly rather than inferred from display names.
2. Transactional systems increasingly use immutable commands/results, deterministic fingerprints, and duplicate-operation handling.
3. Domain/application logic is frequently separated from Unity presentation, especially in run results, inventory/loadout, crafting, skills, rewards, and room definitions.
4. Composition roots and route contexts are explicit rather than hidden entirely behind static globals.
5. The new weapon registry/dispatcher removes the need for a central weapon-name switch and fails closed for unknown runtime IDs.

### Largest risks

1. **High:** `Stage1VisibleSliceController` remains a scene god object and the practical live owner of several generic systems.
2. **High:** the production weapon boundary is not yet the live firing path; demo ID branching/fallback remains in the retained controller.
3. **High:** several newly extracted reusable concepts are named and located as Stage 1 production code, making Stage 2 reuse likely to require copying or disruptive renaming.
4. **High:** restart and initialization are temporally coupled across many independently owned Unity objects.
5. **Medium:** duplicated migration state exists while extracted components capture objects that the retained controller still constructs and owns.

### Rewrite versus refactor conclusion

1. **Complete rewrite required?** No.
2. **Parts that may eventually be replaced:** the retained Stage 1 controller after its responsibilities have been delegated and protected by focused tests; the current Stage1-specific weapon execution request types should be redesigned behind their emerging contract before broad content growth.
3. **Parts to preserve behind new boundaries:** domain authorities, stable IDs, concrete equipment identities, run-result contracts, room definitions, content importers, deterministic generation/economy services, and focused reusable Unity adapters.
4. **Parts needing extraction or naming correction:** player presentation/construction, camera/HUD composition, generic weapon execution, projectile/effect spawning, enemy-destruction relays, room runtime, restart lifecycle, and production composition roots.
5. **Safest path:** delegate one live responsibility at a time, delete the corresponding retained duplicate immediately after proof, and keep Stage 1 runnable throughout.

### Top five recommended actions

1. Wire the selected production equipment instance into the new weapon dispatcher and a Unity effect sink; then remove the retained weapon-ID branches and silent blaster fallback.
2. Finish player delegation: make one component own construction, movement projection, boost presentation, restart, and disposal; remove duplicate player fields/helpers from the retained controller.
3. Introduce a reusable gameplay-session restart contract and move restart orchestration out of the controller without creating a global service locator.
4. Extract reusable room runtime/clear detection from Stage 1 definitions; keep room sequence, placements, environment, and mission scripting level-specific.
5. Split the remaining controller by ownership seams—combat/effects, enemies, room flow, HUD/camera—rather than by arbitrary file size.

## 2. Architecture map

```text
Persistent profile/session authorities
  holdings / inventory / loadout / money / scrap / XP / skills / rewards / run results
                              |
                              v
Engine-independent application coordinators and immutable route contexts
                              |
                              v
Unity adapters and scene presentation
                              |
                              v
Stage or mode composition + data-authored level definitions
```

Current Stage 1 reality:

```text
Bootstrap and route contexts
        |
        v
Stage1ProductionCompositionRootV1 / host / run binding
        |
        +------ newer production authorities and projections
        |
        v
Stage1VisibleSliceController (~2,194 lines)
  constructs player, rooms, camera, UI, projectiles and enemies
  polls input and presentation state
  owns player health and restart ordering
  interprets selected weapon through retained demo execution
```

The intended dependency direction is mostly visible in the newer folders, but the retained controller crosses application, Unity adapter, presentation, composition, level-definition, and test-support boundaries. Its namespace `ShooterMover.TestSupport.VisibleSlice` is especially inaccurate for a production-routed live scene.

## 3. Responsibility hotspots

| Hotspot | Approx. size | Observed responsibilities | Key dependencies/state | Severity | Recommended target responsibility |
|---|---:|---|---|---|---|
| `Assets/ShooterMover/Production/Stage1/Stage1VisibleSliceController.cs` | 2,194 | Serialized validation, session construction, player construction, input polling, firing, projectile templates, enemy setup/observation, player health, void damage, room switching, room clear, doors, camera, HUD snapshots, VFX, restart, cleanup, test hooks | More than 30 direct namespaces; large object graph; many sequence counters/flags; `Awake`, `Update`, `FixedUpdate`, destruction lifecycle | **High** | Thin Stage 1 scene coordinator: validate level definition, invoke reusable installers, connect stage-specific bindings, and expose no gameplay truth. Expected final range roughly 150–350 coherent lines, not a mandatory limit. |
| `Production/Stage1/Stage1PlayerPresentationV1.cs` | 486 | Player construction, migration capture, movement lifecycle, combat input, target/hazard adapters, fallback sprite/gun mounts, boost trail, restart, disposal | Unity/Input System + movement/domain adapters; mutable capture/ownership flags | **Medium** | Reusable `PlayerRuntimeView`/installer owned by gameplay runtime, with Stage 1 supplying spawn/configuration. Separate fallback prototype visuals from normal production construction. |
| `Production/Stage1/Weapons/Stage1WeaponExecutionV1.cs` | 449 | Result/request contracts, registry, dispatcher, effect contracts, tuning, base executor, four weapon implementations | Depends directly on Unity `Vector3`, uses `object` for equipment/shooter, stores cooldown and accepted operations | **High** | Game-wide weapon execution application boundary with typed equipment/shooter references; weapon behaviours registered once in composition/content bootstrap; Unity vectors adapted at the edge. |
| `Runtime/Bootstrap/ProductionSessionAuthorityOwnerV1.cs` | inventory row | Persistent authority bundle and cross-scene ownership | Long-lived authorities and lifecycle | **Medium** | Keep as persistent authority owner, but enforce clear disposal/session reset and prevent references to destroyed scene objects. |
| `UI/LevelSelection/Stage1ProductionCompositionRootV1.cs` | inventory row | Route validation, authority projection, scene composition | UI namespace owns gameplay composition name/scope | **Medium** | Stage-specific composition belongs under gameplay/scene composition, while Level Selection should only route. |
| `Runtime/UnityAdapters/Missions/Run/Stage1ProductionRunSceneAdapterV1.cs` and binding/relay types | inventory rows | Scene-to-run reporting, enemy destruction, selected slot input | Stage-specific names around partly generic events | **Medium** | Keep a small Stage 1 adapter only for Stage 1 mission semantics; move generic enemy-destruction and weapon-slot adapters to game-wide runtime namespaces. |

### Stage 1 retained controller responsibility map

**State groups**

- Session-created object ownership: `sessionObjects`, runtime material/sprites/templates.
- Room and environment: room definitions, projections, graph/layout, doors, void hazard, current room.
- Player runtime: transform, rigidbody, collider, movement lifecycle/tuning, input assets, target/hazard adapters, renderer, boost trail, spawn.
- Player vital and combat observation: health, hit adapter, damage/firing flags, sequences.
- Weapon selection/execution: selected demo fixture, fire interval, projectile template, next-shot timestamp.
- Enemies: turret package/context/definition/presenter, mobile droid and definition.
- UI/camera: loadout selector, HUD, weapon strip, camera/rig, GUI styles.
- Restart/session lifecycle: generation, active/initialized/complete flags, object resets.

**Lifecycle hooks**

- `Awake`: validates serialized dependencies, constructs the entire session, marks initialized.
- `Update`: handles loadout/restart/effect toggles, combat input, HUD, turret presentation, arena flow, boost visuals, traces, transient flags.
- `FixedUpdate`: observes turret cadence/hits and player hit adapter counters.
- Cleanup later in the file owns runtime object/material/input disposal.

**Coupling between extraction candidates**

- Player health is consumed by HUD, void hazards, enemy projectile callbacks, restart, and completion presentation.
- Enemy destruction feeds doors, room clear, HUD, run reporting, XP/rewards, and tests.
- Weapon firing depends on player transform/input, active selection, projectile templates, enemy targets, trace VFX, and cooldown state.
- Restart touches nearly every subsystem in a hand-maintained order.
- Room switching changes enemies, player spawn, doors, objective text, and content activation.

**Recommended ownership destinations**

| Responsibility | Natural owner | Scope |
|---|---|---|
| Player movement/input runtime | reusable player runtime component | global gameplay |
| Player health/damage acceptance | player combat/vital authority + Unity adapter | global gameplay |
| Weapon execution | typed game-wide dispatcher/behaviours | global gameplay |
| Projectile/explosion/arc/pool realization | reusable effect runtime adapters | global gameplay |
| Enemy health/destruction | enemy authority; presentation observes | global gameplay |
| Room clear detection | reusable room runtime over registered occupants | mode-level/global level runtime |
| Room sequence, placements, environment | level definition | Stage 1 specific |
| HUD/camera | projection-driven scene presentation | scene-specific but reusable configuration |
| Restart | gameplay-session lifecycle coordinator with explicit participants | mode-level |
| Final wiring | thin composition root | Stage 1 specific |

## 4. Naming and scope audit

| Name/path pattern | Classification | Evidence and direction |
|---|---|---|
| `Stage1VisibleSliceController` | 3 — historical/demo naming that should be retired | Production-routed scene component remains in `ShooterMover.TestSupport.VisibleSlice`; it owns generic gameplay and level-specific composition simultaneously. Preserve only a thin `Stage1SceneCoordinator` after delegation. |
| `Stage1PlayerPresentationV1` | 2 — temporarily Stage-specific, should become generic | Constructs movement/input/target/hazard/visual components reusable by Stage 2 and survival mode. Stage 1 should provide spawn and visual/configuration data. |
| `Stage1EnemyDestructionRelayV1` | 2 unless it translates Stage 1-specific encounter IDs | Enemy destruction reporting is game-wide; only encounter/room mapping should remain Stage-specific. |
| `Stage1ProductionCompositionRootV1` under `UI/LevelSelection` | 2/5 | Composition is correctly Stage-specific, but UI ownership is inaccurate. Level Selection should route; scene composition should live with Stage 1 runtime. |
| `Stage1ProductionPresentationHostV1` | 5 — unclear | Host may be a legitimate temporary migration gate, but “Production” only contrasts retained demo code and should disappear when there is one path. |
| `Stage1ProductionWeaponInputV1` and slot selection | 2 | Four-slot selection/input is reusable across stages and likely modes. Stage-specific adapter should only bind scene controls if bindings differ. |
| `Level1RoomGraphDefinitionV1` and authored room content | 1 — correctly Stage-specific | Room sequence, IDs, placements, environment and encounters belong to Level 1. |
| `VisibleSlice*` HUD/camera/loadout names | 2/3 | General combat HUD and camera behavior are reusable; demo-only selection UI may remain prototype-specific until production UI replaces it. |
| `ProductionSessionAuthorityOwnerV1` | 4 — generic despite suspicious naming | “Production” is transitional, but persistent authority ownership is genuinely game-wide. Rename later only when migration stabilizes. |
| `DemoRoomProjection` / IDs such as `scope.demo002-arena` in live controller | 3 | Historical fixtures leak into production-routed runtime and should be replaced by stage definition/session IDs, not cosmetically renamed. |

A repository-path inventory of suspicious names is included in `ARCH_AUDIT_001_inventory.csv`. Classification is based on implementation or PR patch evidence where available; entries inferred only from names are marked lower confidence.

## 5. Authority-boundary audit

| Authority/source of truth | Current assessment | Risk |
|---|---|---|
| Player holdings/equipment inventory | Strong in newer application services; concrete identities retained | Low on authority design; medium at scene integration |
| Four-slot loadout and active slot | Production coordinator resolves exact equipment instance | Low/medium; live retained firing still bypasses the resolved behaviour boundary |
| Weapon execution | New registry/dispatcher is a good seam, but not live and Stage1/Unity-coupled | High |
| Enemy health/destruction | Enemy packages expose authority state; controller still asks/coordinates broadly and owns downstream interpretation | Medium/high |
| Player health | Retained controller owns an integer and direct mutations from hazards/projectiles | High for extensibility and class/augment support |
| Room completion | Room graph/runtime exists, but controller coordinates occupant state, doors, switching and objectives | High for Stage 2/mode reuse |
| Run completion/results | New application/run contracts are comparatively strong and replay-aware | Low/medium until physical extraction and route wiring are complete |
| Rewards/XP | Persistent owner and production run context appear authoritative | Medium because destruction forwarding is incomplete in PR #211 |
| Strongbox ownership/opening | Authorities exist elsewhere; Stage 1 bridge is still incomplete/empty in PR #211 | High for end-to-end correctness, not necessarily core contract quality |
| Money/scrap/crafting/shop/skills | Recent PRs consistently delegate mutation to authorities with duplicate-operation handling | Low/medium; verify merged state and integration per subsystem |
| HUD/UI | Intended as read-only projection, but retained controller constructs snapshots from its own gameplay truth | Medium |

## 6. Extension-cost matrix

| Scenario | Current files touched | Unrelated files touched | Central branching | Duplication risk | Current difficulty | Target difficulty |
|---|---|---:|---|---|---|---|
| A — fifth weapon `weapon.test-burst-rifle` | Definition/catalog; executor; registry composition; tests; **today also retained controller/effect adapter until integration completes** | Controller is unrelated to content registration | Yes in current live path; no in new dispatcher | High while paths coexist | High | Low: definition + executor + one registration + tests |
| B — Stage 2 | New level definitions/scenes plus likely copy/rename of controller, player presentation, HUD/camera setup, room/enemy wiring | Many generic Stage1-prefixed files | Existing controller encodes Level 1 IDs and room/enemy assumptions | Very high | Very high | Medium/low: Stage 2 definition + composition + unique encounters |
| C — melee charging enemy | Enemy definition/runtime/prefab/tests; room placement; currently controller may need fields/build/refresh/restart/clear edits | Central Stage 1 controller | Likely | High | High | Medium: enemy package + registration + stage placement |
| D — survival mode | New completion/wave authority plus scene; combat currently intertwined with room traversal/controller | Stage 1 room/HUD/controller code | Yes | Very high | Very high | Medium: reuse combat/player/effects; new mode coordinator and wave definition |
| E — activated ability | Input asset creation, player/controller state, targeting/effects, HUD, restart | Weapon/player/Stage 1 controller likely touched | Likely | High | High | Medium: reusable ability slots/cooldowns + one effect adapter + HUD projection |

### Experiment A details

The new PR #212 dispatcher demonstrates the desired open/closed seam, including a test-only fifth executor without dispatcher modification. However, the file still packages contracts, registry, tuning and four behaviours together, and the real controller retains `FireWeapon` branching and fallback. Today the conceptual edit set is therefore broader than the PR description’s target state.

### Experiment B details

Reusable: persistent authorities, holdings/loadout, stable IDs, run-result contracts, movement domain/lifecycle, projectile base adapters, enemy packages, authored-room foundations, and likely general HUD/camera components.

Copy/rename pressure today: `Stage1PlayerPresentationV1`, production weapon types, weapon input/selection adapters, run scene adapters/relays, composition host/root, and large portions of the controller. Stage 2 should not clone `Stage1VisibleSliceController`; that would freeze prototype ownership as the game architecture.

## 7. Detailed findings

### ARCH-001 — Retained scene controller is the primary architectural blocker

- **Severity:** High
- **Confidence:** High
- **Affected path:** `Assets/ShooterMover/Production/Stage1/Stage1VisibleSliceController.cs`
- **Evidence:** approximately 2,194 lines; 30+ direct namespaces; implements HUD, turret-presentation, door-condition and hazard-combat ports; owns player health, movement/input fields, weapon selection and firing timing, enemies, rooms, doors, camera, HUD and restart; `Update` refreshes five unrelated concerns.
- **Why it matters:** every new stage, enemy, weapon effect, ability or mode risks editing a central scene object.
- **Likely failure mode:** Stage 2 clones the controller, then fixes and features diverge between stages.
- **Recommended direction:** delegate one coherent ownership seam at a time and delete corresponding fields/helpers immediately after proof.
- **What not to do:** split into partial classes while leaving the same ownership graph; that improves navigation but not architecture.

### ARCH-002 — Player health is presentation/controller-local gameplay truth

- **Severity:** High
- **Confidence:** High
- **Affected paths:** retained controller and hazard/enemy projectile callbacks.
- **Evidence:** `playerHealth` is an integer mutated directly by hazard and projectile methods; HUD snapshots read it; restart resets it.
- **Why it matters:** armor, classes, augments, abilities, damage effects and persistence cannot consistently participate.
- **Likely failure mode:** separate damage paths apply different mitigation/death/reporting logic.
- **Recommended direction:** introduce one player vital/damage authority or application service, with a Unity adapter and read-only HUD projection.
- **What not to do:** move the integer into `Stage1PlayerPresentationV1`; presentation must not become the new authority.

### ARCH-003 — Production weapon execution is not yet authoritative live behaviour

- **Severity:** High
- **Confidence:** High
- **Affected paths:** retained controller; `Production/Stage1/Weapons/Stage1WeaponExecutionV1.cs`; production loadout resolver/input.
- **Evidence:** PR #212 states the final call-site handoff is unperformed and retained ID branches/fallback remain.
- **Why it matters:** selecting Rocket Launcher does not guarantee rocket execution in the actual scene.
- **Likely failure mode:** tests validate the new dispatcher while players still exercise demo logic.
- **Recommended direction:** make the exact active equipment instance the only source for runtime weapon resolution and effect requests; remove fallback after integration.
- **What not to do:** keep both paths behind a feature flag for an extended period.

### ARCH-004 — Weapon boundary is reusable in concept but incorrectly scoped and typed

- **Severity:** High
- **Confidence:** High
- **Affected path:** `Production/Stage1/Weapons/Stage1WeaponExecutionV1.cs`
- **Evidence:** all types are Stage1-prefixed; requests depend on `UnityEngine.Vector3`; equipment and shooter are `object`; contracts, registry, tuning and concrete behaviours share one file.
- **Why it matters:** survival mode and Stage 2 either depend on Stage 1 or duplicate the system; `object` weakens compile-time invariants.
- **Likely failure mode:** adapters perform casts and validation inconsistently, or Stage2 versions fork.
- **Recommended direction:** preserve registry/dispatcher semantics but move to a game-wide application contract with typed immutable references and engine-independent vectors/aim data where practical.
- **What not to do:** introduce reflection scanning or a DI framework solely for registration.

### ARCH-005 — Batch effect submission is not atomic

- **Severity:** Critical
- **Confidence:** Medium
- **Affected path:** `Stage1ConfiguredWeaponExecutorV1.TryExecute`.
- **Evidence:** effects are submitted one at a time; if a later pellet/effect is rejected, earlier sink requests may already have been accepted while the executor does not record the operation/cooldown.
- **Why it matters:** retry can duplicate a partial shotgun burst or multi-effect weapon.
- **Likely failure mode:** partial side effects followed by retry produce more projectiles/damage than intended.
- **Recommended direction:** effect sink should accept an immutable batch atomically or return a committed operation result; alternatively reserve/validate then commit.
- **What not to do:** merely add the operation ID before sending effects, because that would make rejected operations non-retryable and may lose shots.

### ARCH-006 — Player extraction still has dual ownership during migration

- **Severity:** Medium
- **Confidence:** High
- **Affected paths:** retained controller and `Stage1PlayerPresentationV1`.
- **Evidence:** extracted component can construct or capture; retained controller still constructs and stores the same player components; PR #211 lists delegation/deletion as remaining work.
- **Why it matters:** restart, disposal and input ownership can diverge.
- **Likely failure mode:** leaked InputAction assets, duplicate polling, or one component restarting stale references.
- **Recommended direction:** complete delegation in one focused PR and remove capture mode once migration is complete.
- **What not to do:** keep capture as a permanent general discovery mechanism.

### ARCH-007 — Player runtime is generic despite Stage 1 ownership

- **Severity:** High
- **Confidence:** High
- **Affected path:** `Stage1PlayerPresentationV1`.
- **Evidence:** movement, input, target, void-hazard participation, sprite, mounts, boost trail and restart are not Level 1 rules.
- **Why it matters:** every stage and mode needs the same implementation.
- **Likely failure mode:** Stage 2 adds `Stage2PlayerPresentationV1` with copied code.
- **Recommended direction:** game-wide player runtime view/installer with data/configuration provided by class/loadout/stage composition.
- **What not to do:** rename only; first remove Stage 1 IDs, fallback visuals and migration assumptions.

### ARCH-008 — Restart lifecycle is manually ordered and broadly coupled

- **Severity:** High
- **Confidence:** High
- **Affected path:** retained controller plus player, enemies, gameplay scope, room layout, hit adapter, UI and camera.
- **Evidence:** `QuickRestart` resets numerous counters and invokes many subsystems in a specific sequence.
- **Why it matters:** new abilities, pools, augments, objectives or effects can be forgotten.
- **Likely failure mode:** initial run works but second run retains cooldowns, subscriptions, projectiles or completion state.
- **Recommended direction:** explicit restart participants registered by composition, with deterministic phases only where order is genuinely required.
- **What not to do:** broadcast a global static restart event with hidden subscribers.

### ARCH-009 — Room runtime and level definition are not cleanly separated in live composition

- **Severity:** High
- **Confidence:** Medium
- **Affected paths:** retained controller, room application/definition packages, `Level1RoomGraphDefinitionV1`, room content assets.
- **Evidence:** authored definitions exist, but the controller still finds Level 1 IDs, switches rooms, activates content, observes kills, controls doors and creates objective text.
- **Why it matters:** Stage 2 and survival mode need different progression without duplicating combat.
- **Likely failure mode:** room-clear logic becomes conditional on stage/mode inside a central controller.
- **Recommended direction:** reusable room runtime owns occupant registration and clear transitions; Stage 1 definition owns graph/placements; mode coordinator owns completion semantics.
- **What not to do:** put all stages into one giant room graph switch.

### ARCH-010 — UI composition namespace owns gameplay composition

- **Severity:** Medium
- **Confidence:** High
- **Affected path:** `Assets/ShooterMover/UI/LevelSelection/Stage1ProductionCompositionRootV1.cs` and host.
- **Evidence:** Level Selection UI path contains Stage 1 gameplay composition.
- **Why it matters:** dependency direction becomes ambiguous and future scenes may depend on UI assemblies to start gameplay.
- **Likely failure mode:** headless simulation or alternate routing cannot compose a run without UI.
- **Recommended direction:** Level Selection emits an immutable route command; Stage 1 scene composition consumes it under runtime/composition ownership.
- **What not to do:** let UI directly mutate session authorities.

### ARCH-011 — Polling and global object discovery obscure ownership

- **Severity:** Medium
- **Confidence:** High
- **Affected path:** retained controller.
- **Evidence:** frame polling refreshes HUD/turret/arena/boost/traces; test properties use `FindObjectsByType` for projectiles/HUD/camera.
- **Why it matters:** dependencies are hidden, tests require scenes, and object lifetime becomes expensive to reason about.
- **Likely failure mode:** duplicate HUD/camera/projectile objects are detected late or only in tests.
- **Recommended direction:** explicit references and event/projection updates for meaningful state changes; keep frame updates only for genuinely continuous visuals.
- **What not to do:** replace all polling indiscriminately—aim/camera smoothing may remain frame-based.

### ARCH-012 — Data-driven content and code-defined behaviour are only partially aligned

- **Severity:** Medium
- **Confidence:** High
- **Affected paths:** JSON weapon catalog, Stage 1 weapon tuning/executors, retained controller constants and Level 1 IDs.
- **Evidence:** weapon definitions are imported from data, but executor tuning currently hardcodes values; retained controller hardcodes damage, projectile speed/lifetime, actor IDs and demo scope IDs.
- **Why it matters:** balance data can diverge from live behaviour.
- **Likely failure mode:** UI/catalog reports one weapon profile while executor/controller fires another.
- **Recommended direction:** behaviour remains code-defined; tuning/content comes from validated definitions and is mapped once during composition.
- **What not to do:** data-drive algorithms or arbitrary class names/reflection.

## 8. Proposed target architecture

### Game-wide systems

- Player movement runtime and player vital/damage authority.
- Equipment-instance resolution, loadout and active-slot projection.
- Weapon execution dispatcher, behaviour registry and atomic effect batch contract.
- Projectile, explosion, area, chain, pool and damage Unity adapters.
- Enemy vital/destruction authority and common movement/targeting contracts.
- General combat HUD projections and camera follow components.
- Restart/session lifecycle contracts.

### Mode-level systems

- Room-traversal mission coordinator.
- Survival wave coordinator.
- Extraction/completion policy.
- Mode-specific objective and HUD projection extensions.

### Stage-specific definitions

- Room graph and placements.
- Spawn plan and encounter IDs.
- Environment and mission scripting.
- Unique bosses/encounters and presentation bindings.

### Scene-specific presentation

- Serialized prefabs/assets.
- Camera/HUD bindings.
- Animation/VFX adapters.
- Input adapter installation where platform/scene-specific.

### Persistent authorities

- Profile, holdings, inventory, loadout, currencies, XP, skills, crafting transactions, rewards, strongboxes and immutable run results.
- Must not retain scene-object references.

### Composition roots

- Thin, explicit and stage/mode-specific.
- Resolve route payload and authority bundle.
- Construct reusable systems and register content behaviours.
- Bind adapters; own disposal.
- Contain no gameplay branching by weapon/enemy display name.

### Example naming conventions

- `PlayerRuntimeViewV1` — Unity projection, not health authority.
- `WeaponExecutionDispatcherV1` — game-wide application behaviour routing.
- `Stage1DefinitionV1` / `Level1RoomGraphDefinitionV1` — actual stage data.
- `Stage1SceneCompositionRootV1` — stage-specific wiring only.
- `RoomTraversalMissionCoordinatorV1` — mode-level progression.
- `UnityWeaponEffectSinkV1` — adapter realizing accepted effect batches.

## 9. Migration roadmap

| Step | Objective | Preconditions | Owned paths | Behaviour to preserve | Tests | Risk | Parallel work |
|---|---|---|---|---|---|---|---|
| 1 | Complete production weapon live handoff | PR #211/#212 compile-ready | Stage1 composition, weapon adapters, retained fire call site, focused tests | exact active equipment, four controls, cooldown/retry semantics | EditMode dispatcher + PlayMode per weapon and restart | High | Definition/catalog work can continue |
| 2 | Finish player ownership transfer | Step 1 optional; current player component present | player runtime, retained controller, tests | movement feel, input, collision, boost, restart | EditMode construction invariants + PlayMode movement/restart | Medium | Enemy and room design work can continue |
| 3 | Introduce player vital/damage authority | Damage contracts understood | combat domain/application, Unity adapter, HUD projection | current health/damage/death behaviour | damage ordering, duplicate hit, mitigation fixture, restart | High | UI art and content unaffected |
| 4 | Extract generic effect runtime | Weapon batch contract stable | projectile/explosion/arc/pool adapters | collisions, lifetimes, damage, deterministic target ordering | focused EditMode + PlayMode effect suites | High | Stage 2 definition can proceed |
| 5 | Extract room runtime/clear detection | enemy destruction events stable | room application/runtime, Stage1 definition/composition | two-room traversal, doors, clear rules | zero/one/many occupants, duplicate destruction, restart | High | Survival mode coordinator can proceed after combat separation |
| 6 | Move HUD/camera to projection-driven components | player/enemy/room projections available | presentation/UI/camera | current visuals and reduced-effects/grayscale controls | projection tests + scene ownership checks | Medium | Content work parallel |
| 7 | Reduce retained controller to composition then retire it | Steps 1–6 | Stage1 production path | complete playable Stage 1 route | end-to-end PlayMode/manual route + XML | Medium | Stage 2 implementation begins safely |
| 8 | Normalize names/paths | one authoritative path exists | targeted moves only | GUIDs and assembly references | compile + focused tests | Low/medium | Do in small PRs, not one sweep |

## 10. Keep / refactor / replace

| Subsystem | Classification | Reason |
|---|---|---|
| Stable IDs and immutable domain payloads | Keep as-is | Strong base for determinism and authority boundaries. |
| Holdings/inventory/loadout authorities | Keep with minor cleanup | Correct identity model; focus on consistent integration. |
| Run results and replay-aware transaction patterns | Keep as-is / minor cleanup | Appropriate authoritative model. |
| JSON content importers and validated definitions | Keep as-is | Correct separation of content from behaviour. |
| Movement domain/lifecycle | Keep with minor cleanup | Reusable; move construction out of Stage 1 ownership. |
| `Stage1PlayerPresentationV1` | Extract incrementally | Good seam, wrong scope and mixed fallback/migration concerns. |
| Weapon registry/dispatcher design | Redesign behind existing contract | Preserve registry semantics; fix scope, typing, Unity coupling and atomic batches. |
| Retained controller | Replace incrementally | Replace only after delegated seams are live and tested. |
| Room definitions | Keep as-is / minor cleanup | Correctly stage-specific data. |
| Live room orchestration | Extract incrementally | Generic progression mixed with Level 1 scene logic. |
| Enemy packages/authorities | Keep with minor cleanup | Reusable foundation; reporting/composition needs separation. |
| HUD/camera packages | Extract incrementally | Reusable presentation currently coordinated centrally. |
| Production session owner | Keep with minor cleanup | Correct persistent scope; audit cleanup and scene-reference isolation. |
| ECS rewrite | Do not pursue | No evidence that current problems require replacing the object/component model. |

## 11. Suggested follow-up tasks

### WPN-LIVE-001 — Authoritative live weapon handoff

- **Scope:** wire exact active equipment to game behaviour dispatcher and atomic Unity effect sink; remove retained branches/fallback.
- **Dependencies:** PR #211 and #212.
- **Non-goals:** weapon balance redesign, new UI, catalog rewrite.
- **Acceptance:** all four starter weapons execute distinct live effects; fifth executor requires no controller/dispatcher edit; duplicate/retry and restart proof.

### PLAYER-RUNTIME-001 — Complete reusable player runtime ownership

- **Scope:** delegate construction/restart/boost/disposal; remove duplicate controller fields/helpers; relocate toward game-wide scope.
- **Dependencies:** current player presentation extraction.
- **Non-goals:** player health, classes, armor, abilities.
- **Acceptance:** one owner of InputAction asset/player projection; Stage 1 behaviour unchanged.

### PLAYER-VITAL-001 — Authoritative player health and damage

- **Scope:** one engine-independent vital/damage transition boundary and Unity adapter.
- **Dependencies:** damage contracts and player runtime seam.
- **Non-goals:** final armor/augment balance.
- **Acceptance:** hazards and enemy projectiles use the same authority; duplicate hits are deterministic; HUD is read-only.

### EFFECT-RUNTIME-001 — Reusable projectile/effect realization

- **Scope:** atomic effect batches for projectile, explosion, area and arc requests.
- **Dependencies:** WPN-LIVE-001 contract.
- **Non-goals:** every future effect type.
- **Acceptance:** no Stage1 namespace; typed equipment/shooter context; deterministic chain ordering; lifecycle tests.

### ROOM-RUNTIME-001 — Occupancy and room-clear authority

- **Scope:** reusable occupant registration, destruction reporting, clear state and door projection.
- **Dependencies:** stable enemy destruction notifications.
- **Non-goals:** Stage 2 content or survival waves.
- **Acceptance:** Stage 1 uses authored graph/placements without controller-owned enemy branching.

### MODE-SURVIVAL-001 — Combat reuse proof

- **Scope:** minimal survival-mode fixture using shared player/combat/equipment/effects with wave completion.
- **Dependencies:** WPN-LIVE-001, EFFECT-RUNTIME-001, player runtime.
- **Non-goals:** polished scene/content.
- **Acceptance:** no dependency on Stage 1 room traversal or controller.

### STAGE2-SPIKE-001 — Stage 2 architecture proof

- **Scope:** static/PlayMode fixture with different room graph, same player/weapons/results, one new enemy.
- **Dependencies:** room runtime extraction.
- **Non-goals:** final Stage 2 art/balance.
- **Acceptance:** no cloning of retained controller or Stage1 generic implementations.

### LIFECYCLE-001 — Explicit gameplay restart participants

- **Scope:** deterministic restart phases and ownership/disposal contract.
- **Dependencies:** player/weapon/effect seams.
- **Non-goals:** global event bus or service locator.
- **Acceptance:** initial and restarted session use the same construction invariants; no stale cooldowns/projectiles/subscriptions.

## Validation performed

- Retrieved repository metadata and current main commit.
- Inspected open and historical PR metadata relevant to production flow, weapon data/runtime, rooms, run results, UI, crafting, skills and simulator work.
- Inspected the full changed-file inventory for PR #211.
- Read targeted source from the PR #211/#212 stacked branch, including the retained controller, extracted player presentation and weapon execution implementation.
- Compared implementation claims with PR descriptions and explicitly recorded unperformed runtime handoffs.
- Performed static responsibility, dependency, lifecycle, state-ownership, naming, extensibility and replay-safety analysis.

## Areas not inspected or not proven

- Unity scene serialization beyond changed-file/path evidence.
- Runtime object hierarchy, frame behaviour, physics outcomes, animation/VFX correctness and actual scene transitions.
- Complete line-by-line review of every C# production file; the CSV records the significant files and confidence of classification.
- Unity compilation, EditMode/PlayMode XML, profiler output and manual acceptance.
- Closed/stacked branches not represented in current main or the active production-flow stack may contain newer alternatives.

No production behaviour was changed by this audit.