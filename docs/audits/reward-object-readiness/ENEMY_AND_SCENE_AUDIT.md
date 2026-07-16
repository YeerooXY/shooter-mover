# AUD-001 — Existing enemy and scene-readiness audit

**Repository:** `YeerooXY/shooter-mover`  
**Exact audited base:** `56a84838558fdfe67fb97254d832b2dd7cd5c018`  
**Audit mode:** read-only inspection of implementation, assets, scene glue, and focused tests  
**Owned output:** this file only

## Verdict legend

- **Retain** — the current package boundary and behavior are sound. Future work should consume it without a package rewrite.
- **Normalize** — preserve the working package/runtime, but replace a specific identity, scene-scope, authoring, or integration seam.
- **Replace** — discard the package implementation because its core authority or behavior is unsound.

No inspected package meets the threshold for **Replace**.

# 1. Executive findings

1. **The shared enemy foundation is healthy and must be protected.** `EnemyActorState` explicitly excludes Unity objects, encounter ownership, rewards, and persistence (`Assets/ShooterMover/Runtime/Domain/Enemies/EnemyActorState.cs:31-35`). `EnemyActorStepper` deterministically owns damage, disposable contact death, exactly-once destroyed notifications, and encounter-resolution notifications (`EnemyActorStepper.cs:37-98`, `100-184`, `186-289`). `EnemyTarget2DAdapter` translates only confirmed combat hits into EN-002 commands and stores no health truth (`Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyTarget2DAdapter.cs:94-188`, `253-395`). These are reusable contracts, not rewrite targets.

2. **Pursuer Drone, Ram Droid, and Mobile Blaster Droid are package-local, explicitly configured, and free of primary global lookup.** Their target and actor identities are injected by the caller. Their current gap is not gameplay correctness; it is the absence of a common placed-object identity/reward-source authoring layer. Verdict: **Retain**.

3. **Blaster Turret gameplay is strong, but its placement identity and context acquisition need narrow normalization.** The package has explicit health, contact, projectile, line-of-fire, cadence, tracking, wreck, and restart behavior. However, `BlasterTurretAuthoring2D.ResolveContext` falls back to `FindFirstObjectByType` (`BlasterTurretAuthoring2D.cs:230-239`), `BlasterTurretSceneContext2D.Configure` scans all authored turrets globally (`BlasterTurretSceneContext2D.cs:74-81`), and the actor ID is hashed from scene path, object names, hierarchy, and sibling indices (`BlasterTurretAuthoring2D.cs:265-289`). Verdict: **Normalize**, preserving the package runtime.

4. **Four-Blaster Elite is a tested deterministic combat/session model, not yet a complete placed Unity enemy package.** It has one EN-002 health model, one completion signal, ordered accepted Blaster execution plans, telegraph, bounded spread, and restart (`FourBlasterElitePackage.cs:129-299`, `335-629`). It has no prefab, collider/wreck policy, target adapter, physical projectile executor, scene context, or placed identity source. Verdict: **Normalize** by adding a bounded Unity composition/authoring layer; retain the deterministic session.

5. **Destructible prop authority is reward-source friendly, but Stage 1 discovery and identity are prototype glue.** `DestructiblePropAuthority` accepts only confirmed hits, deduplicates events, emits a rich exactly-once destruction result, and resets cleanly. `DestructibleProp2D` exposes `Destroyed` and `Restarted`, disables/restores collider and presentation, and keeps animation outside health authority (`DestructiblePropAuthority.cs:141-192`, `230-405`; `DestructibleProp2D.cs:10-28`, `122-198`). The Stage 1 integration still discovers variants from `Crate_*` / `Explosive_*`, locates colliders by `${visual.name}_Collision`, and derives IDs from visual name plus sibling index (`Stage1DestructiblePropIntegration.cs:171-221`, `230-250`). Verdict: **Normalize**, preserving the authority, events, and animation player.

6. **The Stage 1 scene is deliberately thin but the controller is an integration hotspot.** The serialized scene contains one root `Stage1VisibleSliceController` and package asset references (`Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity:56-114`). The controller instantiates the room, builds prop colliders, player, hit adapter, prop set, turret scene context, projectile templates, turret, UI, and restart wiring (`Stage1VisibleSliceController.cs:424-469`, `543-585`, `652-720`). This is acceptable for the current demo but must remain under one serial scene owner.

7. **Physical projectile semantics are already real and should not be replaced.** `BoundedProjectile2D` uses Rigidbody2D/Collider2D contact, owner-collider exclusion, finite lifetime, exactly-once completion, and `CombatHit2DAdapter` translation; it never owns damage (`Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/BoundedProjectile2D.cs:22-29`, `103-163`, `224-305`, `308-351`). Mobile Blaster Droid and Blaster Turret execute that accepted shell. Stage 1 tests prove damage occurs only after physical travel/contact (`Stage1VisibleSliceIntegrationTests.cs:84-157`).

8. **Reward integration should subscribe to existing terminal facts, not add reward logic to enemy health code.** EN-002 emits one `EnemyDestroyedNotification` and one `EnemyEncounterResolutionNotification` on lethal damage or disposable impact (`EnemyActorStepper.cs:167-183`, `273-289`). Props expose one rich `DestructiblePropDestructionResult`. `SRC-001` should adapt these facts to a source operation identity; quick restart must restore session state without re-granting an already claimed durable reward.

# 2. Evidence table by package

| Package / integration | Identity and role | Target acquisition / scope | Health and damage authority | Projectile / contact semantics and fairness | Placement, duplicates, hierarchy, collider / wreck | Restart, reward readiness, definition split | Tests and concrete gaps | Verdict |
|---|---|---|---|---|---|---|---|---|
| **Pursuer Drone** | `enemy.pursuer-drone`; ordinary direct-pursuit contact enemy. Descriptor and tuning live in `PursuerDroneDefinition` (`PursuerDroneDefinition.cs:28-35`, `124-171`). | Caller injects player target source, player collider, actor ID, and mover ID; no scene search (`PursuerDronePackage.cs:273-359`; `PACKAGE.md:41-43`). | `PursuerDroneAuthority` delegates every transition to `EnemyActorStepper` (`PursuerDronePackage.cs:12-62`). `EnemyTarget2DAdapter` is confirmed-hit intake. | Direct normalized pursuit, stopping distance, ordinary contact cadence/grace, target-loss stop, color-independent warning (`PursuerDronePackage.cs:95-194`; `PACKAGE.md:45-83`). Contact never writes player velocity. | Prefab has explicit Rigidbody2D/collider/adapters, but there is no placed authoring component or serialized stable instance ID. Multiple instances are possible only if a caller supplies unique IDs. No package-specific destroyed-wreck collider policy is exposed; tests prove terminal contact/movement rejection, not wreck passability. Arbitrary hierarchy is otherwise irrelevant after explicit configuration. | `EnemyActor2DAdapter.Restart` resets authority, decision sequence, fixed-step/contact generation (`PursuerDronePackage.cs:407-429`; `PACKAGE.md:37-39`). EN-002 terminal notifications are suitable source facts, but no reward profile/override or once-only source operation exists. Definition asset is separate from runtime/prefab. | `PursuerDronePackageTests.cs:60-248` covers pursuit, cadence, target loss, death, disable, restart; `:250-335` protects 2D/no-global boundaries. Missing: placed-ID stability, duplicate prefab authoring, arbitrary map placement, wreck collider behavior, reward-source claim/restart tests. | **Retain** |
| **Ram Droid** | `enemy.ram-droid`; small, fast, light disposable-impact attacker (`RamDroidDefinition.cs:28-52`, `192-218`). | `ConfigureSession` requires an explicit stable actor ID, configured player target, player colliders, player ID, and weight (`RamDroidRuntime2D.cs:67-177`). No lookup. | EN-002 state and stepper own health and disposable death (`RamDroidDefinition.cs:170-190`; `RamDroidRuntime2D.cs:254-278`). | Direct pursuit; first accepted player impact requests bounded mover damage and destroys the Ram Droid; contact grace and simultaneous-window deduplication; explicit `RAM!`/shape warning (`RamDroidDefinition.cs:39-52`, `114-168`). | Prefab/runtime require explicit components, but no placed stable-ID authoring, duplicate validation, or inherited instance metadata. Collider remains a package collider; death stops movement/contact, but no authored wreck/passability mode was found. Explicit injection makes arbitrary parent hierarchy safe once configured. | `RestartSession` delegates to EN-003 restart and restores presentation/state (`RamDroidRuntime2D.cs:221-235`, `325-342`). EN-002 emits one terminal fact. Definition asset is reusable, but no reward source/default/override. | `RamDroidPackageTests.cs:55-167` proves identity, readable warning, exactly-one disposable impact and no player-velocity write; `:169-336` covers grace, non-player collision rejection, projectile death, target loss, disable, restart. Missing: placed duplicate/hierarchy identity, wreck mode, reward source and durable-claim restart tests. | **Retain** |
| **Mobile Blaster Droid** | `enemy.mobile-blaster-droid`; ordinary moving ranged enemy (`MOBILE_BLASTER_DROID_PACKAGE.md:3-15`). | Explicit package definition, actor ID, player target, player colliders, player ID/weight, and accepted projectile prefab are injected (`MobileBlasterDroidRuntime2D.cs:139-273`). No scene search. | EN-002 owns health/death/restart; EN-003 owns target/movement/contact projection (`MobileBlasterDroidRuntime2D.cs:23-32`, `371-399`). | Maintains a preferred distance band, locks a non-predictive direction during wind-up, fires one accepted normal Blaster physical projectile, and has bounded recovery. Target loss cancels pending fire; death/deactivation/restart cancels projectiles (`MobileBlasterDroidRuntime2D.cs:400-457`, `552-744`; package doc `:17-32`). | Runtime/prefab have no placed identity component or duplicate validation. Unique injected IDs permit multiple instances, but no test proves scene-authored duplicates or ID stability. No explicit destroyed-wreck collider option was found. | Restart restores EN-002 health, clears hit replay state, cadence, counters, and active projectiles, and increments generation (`MobileBlasterDroidRuntime2D.cs:331-362`). Definition asset is separate; no reward defaults/override/source operation. | `MobileBlasterDroidPackageTests.cs:80-216` covers movement, wind-up/recovery, accepted projectile reuse, and target loss; `:218-295` covers death and restart. Missing: actual placed duplicate/hierarchy tests, physical impact against arbitrary map blockers in package fixture, wreck collider policy, reward claim/restart. | **Retain** |
| **Blaster Turret** | `enemy.blaster-turret`; fixed-position ranged ordinary enemy with ScriptableObject definition, prefab, package, presentation, and authoring (`PACKAGE.md:3-26`, `49-61`). | Core `BlasterTurretPackage.Configure` takes an explicit target source/collider/ID (`BlasterTurretPackage.cs:748-852`). Authoring optionally accepts overrides but otherwise calls `FindFirstObjectByType<BlasterTurretSceneContext2D>` (`BlasterTurretAuthoring2D.cs:230-239`). The context itself scans all authored turrets with `FindObjectsByType` (`BlasterTurretSceneContext2D.cs:74-81`). Scope is therefore mixed: explicit inside the package, global at placement/bootstrap. | `BlasterTurretAuthority` delegates to EN-002 (`BlasterTurretPackage.cs:280-330`). Confirmed player shots are routed by scene context into `TargetAdapter.ApplyHit` (`BlasterTurretSceneContext2D.cs:140-162`). | Real bounded projectile; explicit line-of-fire Physics2D obstruction query; range, point-blank, cone, warning/recovery, optional bounded tracking, non-homing fired direction (`BlasterTurretPackage.cs:388-436`, `530-852`; `PACKAGE.md:28-41`, `63-73`). | Authoring snaps position/facing and supports `keepColliderWhenDestroyed` (`BlasterTurretAuthoring2D.cs:31-78`, `291-304`). Actor ID hashes scene path plus hierarchy names and sibling indices (`:265-289`), so rename/reparent/reorder changes identity. Duplicate test proves two current instances register independently, but not identity persistence. Destroyed collider can remain cover or disable; restart restores authored state (`BlasterTurretPackage.cs:1288-1362`). | Restart clears cadence/projectiles, resets health, restores anchor/facing/collider/presentation and increments generation (`BlasterTurretPackage.cs:1100-1124`). EN-002 terminal facts are usable, but the scene context has no reward source/override. Definition and authoring are separated, but placed identity is derived rather than authored. | `BlasterTurretPackageTests.cs:33-195` checks type/descriptor/cadence/facing and source-surface assertions. Stage 1 integration proves physical travel, tracking, wreck passability/restart, duplicate registration, and 50 restarts (`Stage1VisibleSliceIntegrationTests.cs:50-276`). Missing: additive/multiple-context scope, parent-scope resolution, authored-ID duplicate rejection, rename/reparent/reorder identity stability, explicit reward source and claim idempotency. | **Normalize** |
| **Four-Blaster Elite** | `enemy.four-blaster-elite`; easy first boss, one 160-HP heavy EN-002 state, four ordered origins, bounded spread, telegraph and recovery (`FourBlasterElitePackage.cs:129-233`). | `FourBlasterEliteSession.Advance` receives center and target coordinates directly; no scene lookup (`FourBlasterElitePackage.cs:388-403`). There is no Unity target adapter or placed context. | One `EnemyActorState`; `ApplyDamage` delegates to `EnemyActorStepper`; one completion flag/count follows one encounter-resolution notification (`FourBlasterElitePackage.cs:504-552`). | Produces four immutable accepted Blaster execution plans with ordered origins and mild spread (`FourBlasterElitePackage.cs:235-299`). It does **not** execute physical projectiles itself. Telegraph and recovery are deterministic and reduced-effects readable. | No prefab, Rigidbody2D, Collider2D, wreck handling, hierarchy behavior, authoring component, or placed identity source exists in the package. Duplicate sessions are safe only when callers supply unique actor IDs. | `RestartSession` resets one health/cadence/completion model (`FourBlasterElitePackage.cs:554-629`). EN-002 completion is a good source fact, but no placed operation identity/reward authoring exists. Tuning is static constants, not a reusable definition asset/instance split. | `FourBlasterElitePackageTests.cs:42-177` covers ordered accepted plans and cadence; `:180-295` covers exactly-once completion, 25 restarts, and readability. Missing: Unity composition, actual projectile execution/collision, target loss, collider/wreck, arbitrary placement/duplicates, stable identity, reward source. | **Normalize** |
| **Destructible props + Stage 1 integration** | Generic `DestructiblePropAuthority`/`DestructibleProp2D`; Stage 1 currently creates three crates and one explosive from room visual names. | Generic prop component is explicitly configured. Stage 1 scans only supplied presentation/collider roots, but variant discovery is name-prefix based and collider lookup is exact-name based (`Stage1DestructiblePropIntegration.cs:115-120`, `171-210`). | Prop authority owns health and confirmed-hit deduplication; projectile relay applies fixed confirmed-hit damage. It is independent from EN-002 but similarly deterministic (`DestructiblePropAuthority.cs:230-405`). | Physical projectile contact is translated by the shared hit adapter, then relayed. Destruction disables blocker and presentation. Optional ordered-sprite animation is presentation-only and cancels on restart (`DestructibleProp2D.cs:122-198`; `DestructiblePropDestructionPlayer2D.cs`). | Stage 1 recognizes `Crate_*` / `Explosive_*`; finds `${name}_Collision`; creates ID `stage1-{siblingIndex}-{normalizedName}` (`Stage1DestructiblePropIntegration.cs:171-250`). Rename/reorder changes ID; duplicate names can collide semantically while `Transform.Find` resolves only one direct child. `DestructiblePropAuthoring2D` provides HP/collider/animation only, with no family definition, explicit ID, inheritance, or reward override (`DestructiblePropAuthoring2D.cs:8-42`). | `DestructiblePropSet2D` observes controller restart generation and restores all props (`Stage1DestructiblePropIntegration.cs:12-113`). `Destroyed` carries source/channel/damage/state detail suitable for `SRC-001`; `Restarted` is separate. Current animation assets are configured but empty (`CrateDestructionAnimation.asset:15-22`), so the player safely no-ops until frames are authored. | Authority tests cover confirmed/nonconfirmed hits, duplicates, exactly-one destruction, and restart (`DestructiblePropAuthorityTests.cs:24-203`). PlayMode tests cover collider/presentation disable/restore and animation cleanup (`DestructibleProp2DTests.cs:29-212`). Missing: inherited variants, explicit placed IDs, duplicate-ID validation, arbitrary nested hierarchy/collider binding, per-instance reward override, reward claim across restart, populated production destruction frames. | **Normalize** |

## Stage 1 integration evidence

- The scene serializes one root controller and references the room, turret, presentation, shot sprites, and two destruction-animation assets; it does not serialize placed enemies or props (`Stage1VisibleSlice.unity:56-114`).
- `BuildSession` creates all runtime ownership: room, prop obstacles, walls, player, player hit adapter, prop set, turret context, projectile template, turret, camera, and UI (`Stage1VisibleSliceController.cs:424-469`).
- `BuildPropObstacles` adds `DestructiblePropAuthoring2D` to matching room visuals and creates collider objects named `${visual.name}_Collision`; the later integration then rediscovers those pairs by name (`Stage1VisibleSliceController.cs:652-720`; `Stage1DestructiblePropIntegration.cs:171-221`).
- `BuildTurret` instantiates exactly one turret at a controller-owned position, creates a runtime definition, applies overrides, then forces immediate context binding (`Stage1VisibleSliceController.cs:543-585`). This is direct Stage 1 controller dependency, not reusable level authoring.
- `QuickRestart` resets player/session state and turret directly, while props watch `RestartGeneration` and restore in `LateUpdate` (`Stage1VisibleSliceController.cs:272-315`; `Stage1DestructiblePropIntegration.cs:91-106`). Fifty-restart integration coverage proves current counts and projectiles remain bounded (`Stage1VisibleSliceIntegrationTests.cs:250-276`).

# 3. Confirmed strengths to retain

1. **EN-002 immutable health/lifecycle truth and terminal notifications.** Keep `EnemyActorState`, `EnemyContactPolicy`, and `EnemyActorStepper` unchanged unless a separately owned contract task proves a defect. They already provide deterministic ordering, duplicate-event handling, one destroyed vital, and one encounter-resolution notification.

2. **EN-003 explicit Unity projection.** Keep `EnemyTarget2DAdapter`, `EnemyContact2DAdapter`, and `EnemyActor2DAdapter` as the target/contact/movement boundaries. Ordinary packages correctly inject dependencies rather than finding the player.

3. **Shared physical projectile path.** Keep `WeaponMount2DAdapter` → `ProjectileExecutionPlanAdapter` → `BoundedProjectile2D` → `CombatHit2DAdapter`. It provides finite, non-homing, physical contact and exactly-once completion without owning damage.

4. **Ordinary package behavior and tuning separation.** Pursuer, Ram, and Mobile Blaster each have package-local definitions, prefabs, runtime composition, and focused tests. Do not migrate their gameplay into a new generic enemy controller.

5. **Turret combat runtime.** Retain stationary anchor restoration, cone/range/line-of-fire checks, warning/recovery cadence, optional bounded tracking, physical projectile execution, configurable wreck collider, and restart cleanup. Normalize only identity/context acquisition.

6. **Elite deterministic session.** Retain one health model, ordered origins, accepted Blaster plans, telegraph, spread cap, generous recovery, one completion emission, and deterministic restart trace.

7. **Prop authority, events, and animation separation.** Retain confirmed-hit-only mutation, rich destruction result, separate restart event, collider/presentation restoration, and presentation-only animation player.

8. **Thin serialized Stage 1 scene.** The one-root scene avoids a large fragile YAML graph. Preserve single-owner scene editing while replacing controller-only authoring assumptions incrementally.

# 4. Confirmed normalization requirements

## 4.1 Common placed identity and capabilities — OBJ-001

Create one reusable placed-object identity/capability component and definition contract outside enemy/prop package folders. It must provide an authored stable ID, explicit duplicate validation, scene/parent scope, object family/variant reference, and resolved per-instance overrides. Do not derive durable identity from object name, hierarchy, sibling index, transform, or scene load order.

Evidence requiring this:

- Turret actor ID is hierarchy-derived (`BlasterTurretAuthoring2D.cs:265-289`).
- Stage 1 prop ID is sibling-index/name-derived (`Stage1DestructiblePropIntegration.cs:230-250`).
- Pursuer, Ram, Mobile, and Elite accept actor IDs but have no common placed authoring source.

## 4.2 Turret context and identity — NORM-001

Preserve `BlasterTurretPackage` combat behavior. Change only the placement seams:

- replace global `FindFirstObjectByType` with explicit serialized or nearest-parent scene scope;
- remove context-wide global `FindObjectsByType` registration as the primary path;
- consume the OBJ-001 authored placed identity;
- reject duplicate stable IDs deterministically;
- test additive scenes and two independent scopes;
- keep duplicate prefab instances independent when they have distinct authored IDs;
- keep existing tracking, line-of-fire, collider/wreck, projectile, and restart behavior unchanged.

Exact likely ownership:

- `Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretAuthoring2D.cs`
- `Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretSceneContext2D.cs`
- `Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurret.prefab`
- focused turret and scene-scope tests only

`BlasterTurretPackage.cs` should be edited only if the normalized identity/context port cannot be injected through its existing `Configure` method.

## 4.3 Destructible prop definition/instance migration — PROP-001

Preserve `DestructiblePropAuthority`, `DestructibleProp2D`, destruction result/event quality, restart restoration, and animation player. Replace Stage 1 conventions with explicit authoring:

- reusable family/variant definition with inherited defaults;
- placed identity from OBJ-001;
- explicit collider binding, including nested hierarchy support;
- explicit health/collider/animation resolved values;
- per-instance reward-source override reference/mode owned by SRC-001;
- validation for missing collider, duplicate identity, invalid override, and ambiguous binding;
- compatibility bridge for the four current Stage 1 props until INT-001 migrates the scene.

Exact ownership:

- `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/**`
- `Assets/ShooterMover/Tests/EditMode/Props/**`
- `Assets/ShooterMover/Tests/PlayMode/Props/**`

No Stage 1 scene/controller edit belongs in PROP-001.

## 4.4 Four-Blaster Elite Unity composition

Add a bounded package-local composition layer; do not rewrite the deterministic session:

- definition asset or authoring data separate from placed instance identity;
- Rigidbody2D/collider/target adapter and destroyed-wreck behavior;
- physical execution of each accepted `WeaponFireExecutionPlan` through existing weapon/projectile adapters;
- target loss/deactivation/restart cleanup;
- explicit placed identity and duplicate tests;
- no scene edit.

Recommended new task ownership:

- `Assets/ShooterMover/ContentPackages/Enemies/FourBlasterElite/**`
- `Assets/ShooterMover/Tests/PlayMode/Enemies/FourBlasterElitePackageTests.cs`
- a new package-local PlayMode fixture if needed

## 4.5 Reward source adaptation — SRC-001

Reward logic must remain outside enemy and prop health authorities. SRC-001 should adapt:

- `EnemyDestroyedNotification` / `EnemyEncounterResolutionNotification` for enemies;
- `DestructiblePropDestructionResult` for props;
- OBJ-001 placed identity plus reusable reward defaults and explicit override modes;
- a once-only source operation identity that survives quick restart and claim retry;
- no reward grant on `Restarted`, no grant from presentation destruction, and no polling object names.

# 5. Suspected issues requiring tests before changes

These are risks, not confirmed defects. Add focused tests before changing behavior.

1. **Hierarchy identity drift:** place a turret, record ID, then rename, reparent, and reorder siblings. Confirm the current ID changes; use that proof to lock the migration behavior and any compatibility mapping.

2. **Multiple scene contexts:** load two additive scenes, each with a turret context and authored turrets. Determine whether `FindFirstObjectByType` or the global context scan cross-binds packages. Do not infer the failure from code alone; capture it in a focused test.

3. **Duplicate authored IDs after OBJ-001:** prove duplicate IDs fail closed before combat/reward registration and do not silently share dictionary entries.

4. **Ordinary enemy destroyed-collider semantics:** current tests prove stopped movement/contact, but not whether Pursuer, Ram, or Mobile colliders remain blocking after destruction. Record current behavior before adding a general wreck policy.

5. **Prop nested hierarchy and duplicate visual names:** place two same-named visuals or move collider objects below nested parents. Confirm current direct-child `Transform.Find` behavior and failure mode before PROP-001 changes binding.

6. **Prop reward claim across restart:** destroy, project/claim/apply a reward, quick restart, destroy again, and prove the intended durable policy. The current prop authority deliberately clears event replay history on restart (`DestructiblePropAuthorityTests.cs:178-203`), so durable reward idempotency must live above it.

7. **Enemy reward claim across restart:** EN-002 restarts clear processed session events. Test that a source operation identity/claim ledger, not health state, prevents duplicate durable grants when required by the chosen run policy.

8. **Elite physical execution:** prove four plans spawn four finite projectiles with owner exclusion and terminate on collision/lifetime; the current tests validate plans, not Physics2D execution.

9. **Turret package fixture depth:** the package test primarily checks cadence/facing/source surfaces (`BlasterTurretPackageTests.cs:33-195`); physical behavior is currently proven through Stage 1 integration. Add package-local physical tests before decoupling the turret from Stage 1.

10. **Destruction animation content:** both scene references are valid, but the current crate asset has `frames: []` (`CrateDestructionAnimation.asset:15-22`). Verify the no-frame path remains safe and separately gate production readiness on populated sprites.

# 6. Recommended follow-up task splits and exact ownership

| Task | Scope | Exact ownership / exclusions |
|---|---|---|
| **DEMO-001** | Publish/protect the current playable baseline before normalization. | Own only the Stage 1 scene/controller/integration tests and exact demo presentation assets granted by its packet. No enemy/prop foundation rewrite. |
| **OBJ-001** | Authored placed identity, scene/parent scope, capabilities, family/variant reference, and override model. | `Assets/ShooterMover/Runtime/Domain/Authoring/**`, `Runtime/Contracts/Authoring/**`, `Runtime/UnityAdapters/Authoring/**`, `Content/Definitions/Objects/**`, focused tests. No existing enemy/prop/scene edits. |
| **NORM-001** | Normalize turret identity/context only. | Exact turret authoring/context/prefab and focused tests listed in §4.2. Protect `BlasterTurretPackage` behavior and shared adapters. No scene edit. |
| **ELITE-AUTH-001** *(recommended new bounded task)* | Add placed Unity composition for the Four-Blaster Elite around the retained deterministic session. | `ContentPackages/Enemies/FourBlasterElite/**` and focused tests. No scene, shared adapter, weapon, or reward edits. |
| **SRC-001** | Reusable reward definitions and placed source adapter using OBJ-001 identity. | `Content/Definitions/Rewards/**`, `Runtime/UnityAdapters/Rewards/Sources/**`, focused reward-authoring/source tests and workflow doc. No existing package or scene edits. |
| **PROP-001** | Migrate destructible prop family/variant/instance authoring while retaining authority/events/restart/animation. | Existing `ContentPackages/Props/DestructibleProps/**` and focused prop tests only. Consume OBJ-001/SRC-001; no Stage 1 edit. |
| **INT-001** | Final serialized integration of normalized identities, sources, props, selected enemies, rewards, and validators. | Sole owner of `Stage1VisibleSlice.unity`, `Stage1VisibleSliceController.cs`, and Stage 1 integration tests after DEMO-001 releases ownership. No new domain authority in scene glue. |

Ordinary enemy packages should not receive separate normalization tasks unless OBJ-001 integration proves a concrete package-local need. Their explicit `Configure` seams already accept stable IDs and targets.

# 7. Risks to downstream tasks

## DEMO-001

- The current playable baseline depends on one controller-created turret context, one runtime-instantiated turret, name-paired prop colliders, and restart generation. A premature NORM-001/PROP-001/INT-001 edit could destabilize the demo.
- Protect the physical travel/contact tests, tracking/wreck test, duplicate turret test, and 50-restart test as baseline acceptance.
- The scene is currently a shooting sandbox with one serialized controller (`Stage1VisibleSlice.unity:56-114`); DEMO-001 should document that exact baseline rather than imply all five enemy packages are placed.

## OBJ-001

- It must not absorb health, damage, projectile, encounter, reward-claim, or persistence authority.
- It must support both current explicit-ID packages and future placed authoring.
- Identity must be stable across rename/reparent/reorder and validated within explicit scene/parent scope.
- Capabilities/variants/overrides must be resolvable without requiring package-specific global searches.

## PROP-001

- A broad rewrite risks losing exactly-once destruction, rich source/channel evidence, collider/presentation restoration, and animation cancellation.
- Migration must remove `Crate_*` / `Explosive_*` and `${name}_Collision` as authority without breaking the four existing Stage 1 objects before INT-001.
- Restart-cleared session event history cannot be treated as durable reward idempotency.

## NORM-001

- Its target is narrow and evidence-backed: global context acquisition/scanning and hierarchy-derived IDs.
- It must retain duplicate independence, tracking, cone/range/line-of-fire fairness, physical projectiles, wreck configuration, and restart behavior.
- Additive-scene and duplicate-ID tests are mandatory before changing registration behavior.

## SRC-001

- Enemy and prop terminal facts are sufficient inputs; source logic must not be inserted into EN-002 or prop health authority.
- Source operation identity must be independent from session event replay history and must define quick-restart behavior explicitly.
- Per-instance overrides require OBJ-001 identity and resolved inheritance; current `DestructiblePropAuthoring2D` has no reward field and should not grow an ad hoc one before SRC-001.

## INT-001

- INT-001 is the only final Stage 1 serialized owner. It must consume merged OBJ-001, NORM-001, SRC-001, PROP-001, validators, and selected content packages rather than reimplement them in the controller.
- The controller should stop constructing identities from names/sibling order and stop being the only place where prop/turret authoring becomes functional.
- Keep one explicit scene scope, one restart coordinator, and one reward-claim/application path. Do not use global `Find*` as the primary path.
- Preserve the current physical projectile and restart regression tests, then add coverage for normalized prop IDs, reward claim idempotency, multiple placed enemy instances, and inherited/per-instance reward overrides.

## Final audit conclusion

The repository does not need an enemy-system rewrite. The safe path is:

1. retain EN-002, EN-003, combat-hit translation, bounded physical projectiles, the three ordinary packages, turret gameplay, elite session logic, and prop authority/events;
2. introduce common placed identity/scope/variant contracts in OBJ-001;
3. normalize only turret placement/context in NORM-001;
4. add a package-local Unity shell around the elite;
5. migrate prop authoring in PROP-001 and adapt terminal facts to reward sources in SRC-001; and
6. let INT-001 perform the only final Stage 1 scene/controller integration.
