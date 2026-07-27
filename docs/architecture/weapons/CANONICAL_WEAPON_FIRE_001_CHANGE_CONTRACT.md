# CANONICAL-WEAPON-FIRE-001 Change Contract

## Baseline

- Repository: `YeerooXY/shooter-mover`
- Refreshed base branch: `main`
- Exact starting SHA: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`
- Primary planning evidence: `docs/architecture/weapons/WEAPON_SHOOTING_SYSTEM_AUDIT_AND_FORWARD_PLAN.md` from draft PR #346
- Audit status: planning evidence only; current source was inspected independently before implementation.
- Self-audit status: completed against the combined branch after initial publication; repairs are recorded below.

## Requested visible behavior

The first active exact equipped canonical Rattler instance must accept mouse aim and held/released primary-fire input, execute cadence only through `WeaponFiringScheduler`, create one visible canonical Normal projectile for each newly accepted launch batch, ignore the firing player's collider hierarchy, damage the canonical enemy runtime through canonical projectile contact/effect resolution, and preserve the existing enemy terminal and room-clear route.

Exact-resolution failure must create no fallback weapon, controller-owned substitute, fabricated equipment instance, or catalogue entry.

## Authoritative owners

| Concept | Authoritative owner |
|---|---|
| selected character | `ProductionCharacterRuntimeGraphV1` / existing production account composition |
| weapon ownership | `ProductionWeaponHoldingsAuthorityV2` via `WeaponEquipmentInstance.InstanceId` |
| equipped mount state | `ProductionWeaponMountLoadoutAuthorityV2` via `MountId -> InstanceId` |
| weapon definition and mechanics | `ProductionWeaponCatalogueV1` canonical `WeaponBlueprint` |
| compatibility equipment projection | `CanonicalWeaponEquipmentProjectionLookupV2`, read-only over canonical holdings |
| effective weapon derivation | `EffectiveWeaponFactory` |
| cadence, trigger edges, cooldown and burst scheduling | `WeaponFiringScheduler` |
| pending launch delivery | `InventoryWeaponRuntimeComposition` |
| accepted presentation receipts | scene-local canonical projectile effect sink |
| projectile movement/contact lifecycle | scene-local canonical projectile component using canonical projectile domain state |
| enemy health, death and terminal state | existing `RoomEnemyActor2D` canonical enemy runtime |
| room clear and exit unlock | existing room occupant terminal and room-clear route |

Compatibility `WeaponCatalog`, generic reward receipts, route-slot projections and UI models remain projections. They do not become mechanics, ownership or equipped-state write authorities.

## Expected files and responsibilities

- `InventoryBackedWeaponExecutionAdapter.cs`
  - preserve fatal exception propagation through effective resolution, scheduling and sink delivery;
  - retain ordinary rejection/retry containment unchanged.
- `InventoryWeaponEffectiveResolver.cs`
  - add an explicit canonical-blueprint resolver seam;
  - use the canonical blueprint directly for production composition;
  - retain the flat-catalogue mapping path only for compatibility callers.
- `ProductionCanonicalWeaponFireControllerV1.cs`
  - bounded scene/player composition;
  - exact source, character, mount, instance and definition validation;
  - resolve execution equipment through `CanonicalWeaponEquipmentProjectionLookupV2`, never the generic receipt ledger;
  - revalidate current graph, ownership, mount and live projection before every firing tick;
  - mouse aim and held/released input;
  - simulation-tick advancement into the retained runtime;
  - rollback and lifecycle cleanup.
- `ProductionCanonicalProjectileEffectSink2D.cs`
  - bind once to exact actor/lifecycle/mount/equipment/definition identity;
  - reject mismatched batches;
  - retain replay-safe accepted-batch receipts keyed by full canonical effect identity;
  - stage one supported canonical Normal launch per accepted batch;
  - attach a read-only exact source-identity projection to each projectile object.
- `ProductionCanonicalNormalProjectile2D.cs`
  - explicit player-hierarchy collider suppression;
  - canonical movement, contact and effect resolution;
  - exact retryable enemy damage command delivery;
  - retirement of uncommitted scene presentation.
- `CanonicalWeaponProjectileSourceIdentityTests.cs`
  - prove a fresh V2 starter is absent from the generic receipt ledger but resolves through canonical holdings into the exact canonical blueprint and `EffectiveWeapon`;
  - narrow exact-replay/conflicting-mount identity guard;
  - fail-closed unbound sink guard.
- Unity `.meta` files for new source files.
- This change-contract document.

## Forbidden systems

No changes to:

- strongbox loot generation;
- inventory ownership or persistence;
- augment or overclock transactions;
- enemy reward composition;
- Skills;
- level-editor infrastructure;
- room-clear authority;
- selected-character authority;
- canonical holdings or mount-loadout mutation;
- navigation ownership;
- authored encounter content.

## Stable identities that must survive

- selected `CharacterInstanceStableId`;
- exact `WeaponEquipmentInstance.InstanceId`;
- exact `WeaponEquipmentInstance.WeaponDefinitionId`;
- exact active `MountStableId`;
- canonical `WeaponBlueprint.DefinitionId`;
- firing actor identity and lifecycle generation;
- firing participant execution identity;
- fire-operation identity;
- canonical effect, launch and projectile identities;
- enemy actor identity and lifecycle generation;
- exact enemy damage-operation identity and retained occurrence timestamp during retry.

No identity may be derived from display name, hierarchy position, route slot index, scene path, object coordinates or authored presentation name.

## Transaction and replay model

### Canonical firing composition

- Before commit: resolve the current character graph, bound canonical player source, exact V2 mount binding, exact canonical-holdings equipment projection and exact canonical blueprint.
- Commit point: the controller publishes its bound runtime only after all exact identities agree, the sink accepts the exact source binding, and the runtime and mounted execution entry are fully constructed.
- Failure before commit: dispose staged runtime state, deactivate staged actor state, retire and destroy the staged sink, and leave persistent authorities unchanged.
- Retry: a fresh scene composition may retry the same authoritative context; conflicting pre-existing controller or sink state fails closed.

### Runtime authority revalidation

- Before every simulation tick, the controller verifies the same current graph reference, non-disposed graph, exact canonical ownership, exact first active mount and live equipment projection.
- Any stale or changed authority retires the scene-local execution path before another scheduler request is created.
- Revalidation reads authoritative state and never mutates holdings or loadout.

### Launch-effect delivery

- Before commit: validate exact source identity, full canonical effect identity, batch fingerprint and supported canonical Normal launch; create and configure the projectile inactive.
- Commit point: the projectile begins emission and the accepted receipt is retained only after successful staging.
- Failure before commit: destroy the staged projectile and retain no receipt.
- Repeated exact delivery: return `AlreadyAccepted` and create no second projectile.
- Distinct emissions from one fire operation remain distinct through shot sequence and projectile ordinal.
- Conflicting identity reuse: reject without mutating presentation state.

### Enemy impact

- Before commit: canonical contact resolution and effect emission produce one enemy-impact damage emission; construct one exact `EnemyRuntimeDamageCommandV1` and capture one occurrence timestamp.
- Commit point: the projectile marks impact committed, disables further travel/contact and stores the exact command and timestamp.
- Retryable failure: retain the same command object and timestamp and retry from `FixedUpdate`.
- Accepted or exact replay: continue or terminate according to canonical projectile lifecycle state.
- Permanent rejection or stale target identity: terminate the projectile without constructing a substitute target or command.

### Cleanup

- Scene unload or controller destruction disposes the firing runtime and clears held input.
- Sink retirement rejects new batches and terminates uncommitted projectiles.
- A projectile with a committed pending enemy-damage retry remains only as the retry executor; it cannot resume physical travel.
- Cleanup never mutates holdings, loadout, character, weapon definition or room-clear state.

## Runtime, editor, persistence and assembly boundaries

- Runtime-only production implementation; no editor API dependency.
- EditMode-only regression coverage remains in the existing test assembly.
- No serialized schema or persistence envelope changes.
- No generated asset output beyond required Unity `.meta` files.
- The UI production-flow assembly owns player input/composition.
- The Unity-adapter assembly owns exact equipment projection, scheduling adaptation, effect presentation and enemy-contact translation.
- Domain/application assemblies remain the authorities for effective-weapon creation, scheduling, projectile decisions and enemy state.

## Failure-mode decisions

| Condition | Decision |
|---|---|
| missing canonical player source | remain pending during bounded startup, then fail closed |
| duplicate player sources/controllers/sinks | reject conflicting duplicates; reuse only an already-bound exact controller |
| unknown weapon definition | reject; no fallback |
| canonical ownership missing | reject before runtime commit or retire before the next tick |
| generic receipt absent for a fresh starter | expected; resolve from canonical holdings instead |
| equipment/blueprint identity mismatch | reject before runtime commit |
| stale graph or mount binding | retire the scene execution path before another scheduler request |
| unsupported projectile mechanics | sink rejects batch and pending delivery remains retryable |
| exact accepted-batch replay | acknowledge without another projectile |
| distinct burst emission | distinct full effect identity; do not collapse into the first receipt |
| conflicting batch fingerprint | reject without mutation |
| player collider contact | ignore explicitly by source hierarchy |
| duplicate enemy collider callback | suppress by exact enemy actor/lifecycle identity |
| downstream enemy exception/null/retryable lethal transition | retain exact command and timestamp for retry |
| stale enemy identity/lifecycle | terminate impact as permanently invalid |
| fatal runtime exception | rethrow through resolver, scheduler and sink adapter boundaries |
| scene unload | dispose controller/sink and scene-owned presentation |
| player defeat | disable/destroy cleanup retires input and uncommitted presentation; no separate authoritative defeat callback exists in this slice |

## Self-audit findings and repairs

### Blocker repaired: wrong equipment lookup

The initially published controller passed `graph.LoadoutRuntime.Holdings` into the retained adapter. That overload resolves equipment from the generic reward-receipt ledger. Fresh V2 starter weapons are intentionally created only in `ProductionWeaponHoldingsAuthorityV2`, so the player could bind an exact Rattler yet fail the first actual firing request as unresolved equipment.

The controller now supplies `CanonicalWeaponEquipmentProjectionLookupV2`, keyed by the exact canonical instance. The generic receipt ledger is used only as immutable augment payload support where applicable. Focused test coverage encodes the fresh-starter boundary explicitly.

### Authority repair: cached scene state

The initially published controller validated ownership and mount state only during binding. It now revalidates the exact current graph, ownership, first active mount and live projection before each simulation tick and shuts down on stale state.

### Rollback repair: partially staged sink

The initially published composition could leave a source-bound retired sink after a later construction failure. Staged runtime, actor and sink state are now explicitly rolled back; an unexplained pre-existing sink fails closed.

### Exception-policy repair

Three inherited broad catches in `InventoryBackedWeaponExecutionAdapter` swallowed fatal runtime exceptions. The exact previously reviewed three-boundary patch is applied: fatal exceptions propagate, while ordinary resolver, scheduler and sink failures retain their existing rejection/retry behavior.

### Investigated and dismissed: sink receipt capacity

A suspected mismatch between scheduler replay-record capacity and sink batch receipts was traced through `InventoryWeaponPendingDeliveryState`. Delivered emissions are retained by the authoritative outbox while the scheduler replay remains, so exact schedule replay does not re-enter the sink. If downstream acceptance succeeds but the pending-state commit fails, the same first due entry blocks later deliveries until the sink returns `AlreadyAccepted`. No duplicate-projectile defect was found at this boundary.

## Exact manual Unity acceptance route

1. Open the project in Unity and allow import/compilation to finish.
2. Confirm the Console has no missing-script, missing-assembly or compilation errors.
3. Run `CanonicalWeaponProjectileSourceIdentityTests` in EditMode.
4. Start from the production menu flow.
5. Select a real persisted character whose first active physical mount owns an exact canonical Rattler instance.
6. Enter the authored production combat room through the existing level-selection route.
7. Inspect the spawned player and confirm exactly one canonical weapon source, firing controller and effect sink are present.
8. Hold primary fire while aiming with the mouse; observe cadence from the authored fire mode.
9. Release primary fire; confirm automatic emission stops through scheduler trigger handling.
10. Confirm one visible projectile exists for each newly accepted canonical launch and that no projectile collides with the player hierarchy.
11. Inspect a projectile and confirm its source projection retains exact character actor, lifecycle, mount, equipment-instance and definition identity.
12. Hit the enemy and confirm canonical enemy health changes.
13. Kill the enemy and confirm the existing terminal fact, room-clear state and authored exit unlock occur without direct weapon-side unlock logic.
14. Leave and re-enter the level; confirm no duplicate controller, sink or stale projectile remains.
15. Force exact weapon-definition resolution failure and confirm no fallback weapon or projectile appears.
16. During Play Mode, remove or replace the first active exact mount through a debug hook and confirm firing retires before another projectile is scheduled.

## Invariant ledger

- Each responsibility has one write authority.
- Canonical weapon mechanics come directly from the exact canonical blueprint.
- `WeaponFiringScheduler` is the sole firing cadence authority.
- Compatibility projections never become write authorities.
- Fresh canonical weapons do not depend on generic receipt ownership.
- Stable identity never depends on names, hierarchy order, slots, paths or coordinates.
- Unknown, stale, unsupported and ambiguous state fails closed.
- Exact replay cannot duplicate projectiles or enemy damage.
- Scene objects are projections over authoritative state.
- Existing enemy terminal and room-clear authorities remain authoritative.

## Scope checkpoint

The requested vertical slice crosses the preferred 500-production-line threshold because it includes three inseparable runtime boundaries: production input/composition, replay-safe scene launch presentation, and canonical projectile-to-enemy contact. The self-audit adds one nine-line inherited-adapter repair and one focused regression boundary but does not expand supported weapon mechanics. Rocket, Orb, homing, damage-over-time and multi-projectile spread remain explicitly excluded. The pull request must remain draft until Unity compilation, EditMode execution and the complete manual gameplay route are executed.
