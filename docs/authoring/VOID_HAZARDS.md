# Void hazards

## Scope

`VOID-001` supplies a reusable 2D void/fall-region package. It owns only typed
classification, authored response policy, contact-local duplicate suppression,
OBJ-001 placed-object/restart participation, editor gizmos, and presentation
notifications. It does not own health, enemy terminal truth, projectile lifetime,
prop lifetime, checkpoints, respawn execution, rewards, missions, maps, or scenes.

## Components

### `VoidHazardAuthoring2D`

Place this component on the same GameObject as:

- one trigger `Collider2D` defining the drag-anywhere region; and
- one configured `PlacedObjectAuthoring2D` with a unique authored `StableId` and
  a reachable compatible `GameplaySceneScope2D`.

The hazard binds through OBJ-001's explicit-scope/nearest-parent rules and
registers itself as an `IRestartParticipant` using the placed instance ID. A
missing scope, malformed/duplicate placed ID, invalid collider, invalid policy,
or rejected restart registration leaves the hazard disabled and fail-closed.

The component draws a wireframe gizmo from the collider bounds. This is authoring
visibility only; it carries no runtime truth.

### `VoidHazardTarget2D`

Every participating target explicitly declares:

- a stable target ID;
- exactly one category: player, enemy, projectile, or prop;
- whether a prop is currently supported; and
- only the typed authority ports required by the selected hazard policy.

Tags, GameObject names, scene names, hierarchy prefixes, sibling indices, and
global discovery are never classification or authority inputs. A child collider
may resolve a classifier from its explicit ancestor chain.

## Per-category policy

Each hazard authors four independent responses:

| Category | Responses |
|---|---|
| Player | `Ignore`, `Damage`, `InstantDeath`, `Respawn` |
| Enemy | `Ignore`, `RequestFall` |
| Projectile | `Ignore`, `RemoveProjectile` |
| Prop | `Ignore`, `Remove`, `KeepSupported` |

`Damage` and `InstantDeath` submit typed environmental requests through
`IVoidHazardCombatPort`; they never mutate a health component. `RequestFall`
submits to `IVoidHazardEnemyFallPort`; the receiving enemy/encounter authority
chooses terminal, withdrawal, kill, and reward semantics. `RemoveProjectile` and
prop removal use their respective typed removal ports.

`KeepSupported` preserves a prop whose classifier explicitly reports support. An
unsupported prop falls through to the typed removal request. The hazard does not
inspect rigidbodies, colliders, names, or presentation to infer support.

## Checkpoint and respawn

A respawn policy requires both:

1. a canonical authored checkpoint `StableId` and explicit
   `IVoidHazardCheckpointPort` on the hazard; and
2. an `IVoidHazardRespawnPort` on the contacted player classifier.

The checkpoint port resolves the typed destination at contact time. An absent
port, malformed ID, unresolved destination, or rejected respawn request produces
no fallback search and no movement. This is the required fail-closed behavior.

## Contacts and duplicate safety

One logical target produces at most one authority request while any of its
colliders remain inside the region. Additional trigger-enter callbacks increment
contact depth but return `DuplicateContactIgnored`. Matching exits decrement the
depth; a later fresh entry receives a new deterministic event ID derived from the
hazard ID, target ID, attempt generation, and contact ordinal.

Authority ports may return `Accepted`, `DuplicateNoChange`, or `Rejected`.
`DuplicateNoChange` is treated as an accepted idempotent outcome. The hazard does
not cache or reinterpret authority-owned health, death, respawn, removal, or
reward state.

## Presentation

`IVoidHazardPresentationPort` and the `PresentationRequested` event receive a
read-only summary after contact routing. Presentation may show warning, fall,
impact, blocked, or ignored feedback. It must not decide whether damage, death,
respawn, enemy fall, or removal occurred.

## Restart behavior

OBJ-001 restart phases are handled as follows:

1. `RetireAttempt`: stop accepting contacts and disable the trigger.
2. `ReleaseTransientResources`: clear contact-depth and event-ordinal state.
3. `ApplyResetProjection`: restore the authored initial collider state.
4. `CompleteRebind`: resume the authored initial acceptance policy.

The authored placed ID, category policies, checkpoint ID, and port references do
not change. No contact or event identity leaks into the replacement attempt.

## Integration boundary

`INT-001` is the sole owner that may place and wire this package in the final
Stage 1 scene. It supplies the accepted scene scope, family/variant definition,
unique placed ID, checkpoint provider, and concrete gameplay authority adapters.
The package does not edit or call `Stage1VisibleSliceController`, load scenes,
search maps, or create private replacement authorities.

## Assembly boundary

The package compiles into the package-local
`ShooterMover.ContentPackages.Environment.VoidHazards` assembly. It references
only the accepted inward dependencies used by production source:

- `ShooterMover.Domain`;
- `ShooterMover.Contracts`; and
- `ShooterMover.UnityAdapters`.

Focused tests compile in separate package-local test assemblies:

- `ShooterMover.Tests.EditMode.Environment.VoidHazards`; and
- `ShooterMover.Tests.PlayMode.Environment.VoidHazards`.

Those test assemblies explicitly reference the runtime package and preserve the
same shared test dependencies as their repository-wide parent assemblies. They
do not require changes to any shared asmdef.

## Focused proof

Authored tests cover:

- independent category filters and ignored behavior;
- damage versus instant-death requests through environmental combat ports;
- checkpoint-backed respawn and unresolved/missing checkpoint failures;
- projectile removal, optional enemy fall, supported/unsupported prop behavior;
- duplicate contact suppression;
- arbitrary nested placement under the nearest compatible scope; and
- restart cleanup and policy/collider restoration.

Cold compilation plus both focused test assemblies must pass in Unity
`6000.3.19f1` before this draft is proof-complete. The repository's three
unrelated baseline PlayMode failures in `assembly/dispatch/wave2/VALIDATION.md`
are outside VOID-001 ownership and must not be repaired here.

## Rollback

Remove the `VoidHazards` runtime, EditMode, and PlayMode subtrees plus this document
and their paired metadata. No scene, checkpoint, health, enemy, projectile, prop,
mission, reward, registry, project-setting, or persistence migration is required.
