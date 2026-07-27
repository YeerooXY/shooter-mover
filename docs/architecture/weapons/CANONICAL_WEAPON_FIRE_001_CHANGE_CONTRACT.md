# CANONICAL-WEAPON-FIRE-001 Change Contract

## Baseline

- Repository: `YeerooXY/shooter-mover`
- Refreshed base branch: `main`
- Exact starting SHA: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`
- Primary planning evidence: `docs/architecture/weapons/WEAPON_SHOOTING_SYSTEM_AUDIT_AND_FORWARD_PLAN.md` from draft PR #346
- Audit status: planning evidence only; current source was inspected independently before implementation.

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
| effective weapon derivation | `EffectiveWeaponFactory` |
| cadence, trigger edges, cooldown and burst scheduling | `WeaponFiringScheduler` |
| pending launch delivery | `InventoryWeaponRuntimeComposition` |
| accepted presentation receipts | scene-local canonical projectile effect sink |
| projectile movement/contact lifecycle | scene-local canonical projectile component using canonical projectile domain state |
| enemy health, death and terminal state | existing `RoomEnemyActor2D` canonical enemy runtime |
| room clear and exit unlock | existing room occupant terminal and room-clear route |

Compatibility `WeaponCatalog`, route-slot projections and UI models remain projections. They do not become mechanics or equipped-state write authorities.

## Expected files and responsibilities

- `InventoryWeaponEffectiveResolver.cs`
  - add an explicit canonical-blueprint resolver seam;
  - use the canonical blueprint directly for production composition;
  - retain the flat-catalogue mapping path only for compatibility callers.
- `ProductionCanonicalWeaponFireControllerV1.cs`
  - bounded scene/player composition;
  - exact source, character, mount, instance and definition validation;
  - mouse aim and held/released input;
  - simulation-tick advancement into the retained runtime;
  - lifecycle cleanup.
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
- run participant identity;
- fire-operation identity;
- canonical effect, launch and projectile identities;
- enemy actor identity and lifecycle generation;
- exact enemy damage-operation identity and retained occurrence timestamp during retry.

No identity may be derived from display name, hierarchy position, route slot index, scene path, object coordinates or authored presentation name.

## Transaction and replay model

### Canonical firing composition

- Before commit: resolve the current character graph, bound canonical player source, exact V2 mount binding, exact live equipment projection and exact canonical blueprint.
- Commit point: the controller publishes its bound runtime only after all exact identities agree, the sink accepts the exact source binding, and the runtime and mounted execution entry are fully constructed.
- Failure before commit: retire staged scene presentation and leave all persistent authorities unchanged.
- Retry: a later bounded composition attempt may retry the same exact context; it must reuse an already matching controller and reject conflicting duplicates.

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
- The Unity-adapter assembly owns effect presentation and enemy-contact translation.
- Domain/application assemblies remain the authorities for effective-weapon creation, scheduling, projectile decisions and enemy state.

## Failure-mode decisions

| Condition | Decision |
|---|---|
| missing canonical player source | remain pending during bounded startup, then fail closed |
| duplicate player sources/controllers/sinks | reject conflicting duplicates; reuse only exact matching composition |
| unknown weapon definition | reject; no fallback |
| equipment/blueprint identity mismatch | reject before runtime commit |
| stale mount binding | reject before runtime commit |
| unsupported projectile mechanics | sink rejects batch and pending delivery remains retryable |
| exact accepted-batch replay | acknowledge without another projectile |
| distinct burst emission | distinct full effect identity; do not collapse into the first receipt |
| conflicting batch fingerprint | reject without mutation |
| player collider contact | ignore explicitly by source hierarchy |
| duplicate enemy collider callback | suppress by exact enemy actor/lifecycle identity |
| downstream enemy exception/null/retryable lethal transition | retain exact command and timestamp for retry |
| stale enemy identity/lifecycle | terminate impact as permanently invalid |
| scene unload | dispose controller/sink and scene-owned presentation |
| player defeat | clear input; retire uncommitted presentation; committed damage retry cannot resume travel |

## Exact manual Unity acceptance route

1. Open the project in Unity and allow import/compilation to finish.
2. Confirm the Console has no missing-script, missing-assembly or compilation errors.
3. Start from the production menu flow.
4. Select a real persisted character whose first active physical mount owns an exact canonical Rattler instance.
5. Enter the authored production combat room through the existing level-selection route.
6. Inspect the spawned player and confirm exactly one canonical weapon source, firing controller and effect sink are present.
7. Hold primary fire while aiming with the mouse; observe cadence from the authored fire mode.
8. Release primary fire; confirm automatic emission stops through scheduler trigger handling.
9. Confirm one visible projectile exists for each newly accepted canonical launch and that no projectile collides with the player hierarchy.
10. Inspect a projectile and confirm its source projection retains exact character actor, lifecycle, mount, equipment-instance and definition identity.
11. Hit the enemy and confirm canonical enemy health changes.
12. Kill the enemy and confirm the existing terminal fact, room-clear state and authored exit unlock occur without direct weapon-side unlock logic.
13. Leave and re-enter the level; confirm no duplicate controller, sink or stale projectile remains.
14. Force exact weapon-definition resolution failure and confirm no fallback weapon or projectile appears.

## Invariant ledger

- Each responsibility has one write authority.
- Canonical weapon mechanics come directly from the exact canonical blueprint.
- `WeaponFiringScheduler` is the sole firing cadence authority.
- Compatibility projections never become write authorities.
- Stable identity never depends on names, hierarchy order, slots, paths or coordinates.
- Unknown, stale, unsupported and ambiguous state fails closed.
- Exact replay cannot duplicate projectiles or enemy damage.
- Scene objects are projections over authoritative state.
- Existing enemy terminal and room-clear authorities remain authoritative.

## Scope checkpoint

The requested vertical slice crosses the preferred 500-production-line threshold because it includes three inseparable runtime boundaries: production input/composition, replay-safe scene launch presentation, and canonical projectile-to-enemy contact. Rocket, Orb, homing, damage-over-time and multi-projectile spread remain explicitly excluded. The pull request must remain draft until Unity compilation and the complete manual gameplay route are executed.
