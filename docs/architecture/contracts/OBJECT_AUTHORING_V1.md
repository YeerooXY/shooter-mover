# Object Authoring v1

## Status and scope

`OBJECT_AUTHORING_V1` is the implementation contract for `OBJ-001`. It provides
the generic placed-object identity, family/variant, capability, instance
override, scene-scope registration, and restart-participation foundation locked
by `PLACED_OBJECT_LIFECYCLE_V1`.

This contract is intentionally independent of existing enemy, prop, door,
hazard, reward, mission, and Stage 1 packages. Those packages may consume this
foundation through later owner tasks. They do not gain permission to migrate or
edit serialized content through `OBJ-001`.

The implementation is contained in:

- `ShooterMover.Domain.Authoring` for immutable values and deterministic
  resolution;
- `ShooterMover.Contracts.Authoring` for definition-source, registration,
  binding-result, runtime-spawn, and restart ports;
- `ShooterMover.UnityAdapters.Authoring` for explicit/ancestor scene binding;
  and
- `ShooterMover.Content.Definitions.Objects` for immutable authored
  ScriptableObject inputs.

## Identity

Every persistent placement serializes one canonical `StableId`. The runtime
adapter parses that exact text without normalization or repair.

Authored identity is independent of:

- scene and object names;
- hierarchy paths;
- parent and sibling order;
- transform values;
- prefab path;
- Unity instance ID;
- load order; and
- runtime projection or restart generation.

A duplicate copied by normal Unity duplication remains invalid until a
deliberate later editor action assigns a new ID. Runtime registration rejects
the second owner and reports both diagnostic locations. It never hashes,
renames, or regenerates the authored ID.

A runtime-spawned object does not invent a durable authored placement. Its
spawner must supply a `RuntimeSpawnIdentityInput` containing both:

- the stable runtime object identity; and
- the stable spawn-operation identity.

Generated names, frame numbers, transforms, sibling indices, and Unity instance
IDs are not accepted spawn inputs.

## Family and variant model

`ObjectFamilyDefinition` contains:

- one family `StableId`;
- display metadata;
- one default variant `StableId`;
- zero or more family capability defaults; and
- an authored ordered list of variants.

`ObjectVariantDefinition` contains:

- one authoritative variant `StableId`;
- optional designer-facing `objectLevel`; and
- only the capability selections relevant to that variant.

Variant count is represented by an ordinary collection. It has no enum, fixed
array size, tier ceiling, or implicit numeric identity. `objectLevel` is
metadata and never replaces the stable variant ID.

## Composable capabilities

One `CapabilityDefinition` owns one stable capability ID and a deterministic
ordered set of typed fields. The v1 field vocabulary supports:

- Boolean;
- signed integer;
- finite decimal;
- text; and
- canonical `StableId`.

A crate variant can select presentation, collision, destructible, lifecycle,
and reward-source capabilities without exposing combat or door fields. A door
can select presentation, collision, condition, transition, and lifecycle
capabilities without exposing weapon fields.

There is deliberately no universal object-type enum and no giant
ScriptableObject containing every future gameplay field. Package-specific tasks
own the meaning and validation of their capability modules.

## Inheritance and instance overrides

Resolution order is:

```text
explicit instance override
    ?? selected variant value
    ?? family default
```

Both variant selections and placed-instance overrides use one explicit mode:

- `Inherit`
- `Override`

An inherited entry carries no alternate definition. An overridden entry must
carry a definition with the exact same capability ID. An instance override can
target only a capability selected by the active variant. Duplicate overrides
or overrides for unselected capabilities fail resolution.

Clearing an override means restoring `Inherit`; it does not mutate the family,
variant, capability asset, prefab, or another placement.

Resolved capability sets are immutable, ordered by stable capability ID, and
carry a deterministic FNV-1a fingerprint over canonical field data. Input
collection order does not change the result or fingerprint.

## Definition source boundary

Unity-facing definition assets implement narrow engine-independent contracts:

```csharp
public interface IObjectCapabilityDefinitionSource
{
    StableId CapabilityId { get; }
    CapabilityDefinition BuildDefinition();
}

public interface IObjectFamilyDefinitionSource
{
    StableId FamilyId { get; }
    ObjectFamilyDefinition BuildDefinition();
}
```

`PlacedObjectAuthoring2D` stores the source as `ScriptableObject` and consumes
only these interfaces. This preserves the accepted assembly direction:
`UnityAdapters` does not reference `Content.Definitions`.

ScriptableObjects contain immutable authored configuration only. They never
store health, destruction state, source claims, mission facts, wallet balances,
holdings, restart progress, or runtime registrations.

## Scene-scope binding

`GameplaySceneScope2D` is a generic projection and composition boundary. A
placed object binds in exactly this order:

1. a serialized explicit scope reference, when present;
2. otherwise the nearest compatible ancestor scope.

An explicit reference has precedence and must be compatible and in the same
loaded scene as the object. It never silently falls back to a parent when
invalid.

Ancestor lookup is bounded to `Transform.parent`. At the nearest ancestor:

- exactly one compatible scope is accepted;
- more than one compatible scope is a conflict and fails closed; and
- no compatible scope continues the bounded ancestor walk.

Missing scope, malformed identity, invalid family/variant data, conflicting
scope, duplicate identity, and conflicting retry all fail closed. A failed
placed adapter remains unregistered and exposes a typed
`SceneScopeBindingResult`.

Production authoring code does not use global `Find*`, tags, scene names, object
names, or a Stage 1 controller as integration authority. Names and hierarchy
may appear only in diagnostic location text.

## Registration

`PlacedParticipantRegistration` separates:

- stable placed identity;
- family and variant identity;
- runtime projection ID;
- run ID;
- attempt/restart generation;
- declared capability IDs; and
- resolved capability fingerprint.

One `GameplaySceneScope2D` owns one non-static
`SceneScopeRegistrationRegistry`.

Registration rules:

- a new stable ID is registered;
- an exact retry from the same runtime owner is `DuplicateNoChange`;
- the same owner with a changed immutable payload is rejected as conflicting;
- a different owner with the same stable ID is rejected as a duplicate and
  reports both locations; and
- the same stable ID may exist in another intentionally separate scope because
  each scope owns an isolated registry.

Unregistration removes only runtime projection state. It does not delete or
alter authored identity or any external durable authority.

## Restart participation

`IRestartParticipant` is a typed package-owned port:

```csharp
public interface IRestartParticipant
{
    StableId RestartParticipantId { get; }
    void OnRestartPhase(RestartContext context, RestartLifecyclePhase phase);
}
```

The generic scope invokes the deterministic phases:

1. `RetireAttempt`
2. `ReleaseTransientResources`
3. `ApplyResetProjection`
4. `CompleteRebind`

The context names the run, runtime projection, retiring generation, and
replacement generation. The replacement generation must advance
monotonically.

The scope sequences callbacks only. It does not decide or mutate health,
damage, projectiles, enemy behavior, rewards, claims, doors, hazards,
checkpoints, mission truth, wallets, holdings, or persistence. Package owners
implement their own participant behavior against accepted authorities.

Exact participant re-registration is no-change. A different participant using
the same restart ID is rejected. Repeated restart/unbind/rebind cycles keep one
placed registration and one restart participant.

## Public Unity components

### `GameplaySceneScope2D`

Provides:

- canonical scope, compatibility, projection, and run IDs;
- attempt generation;
- isolated participant and restart registries;
- typed registration/unregistration;
- deterministic ordered snapshots; and
- typed restart phase sequencing.

It is generic and not Stage-1-specific.

### `PlacedObjectAuthoring2D`

Provides:

- serialized authored placed ID;
- explicit runtime-spawn identity input;
- family definition source;
- selected variant ID;
- grouped capability-specific overrides;
- explicit scope override;
- required compatibility ID;
- nearest-parent fallback;
- immutable resolved definition/capability output; and
- typed fail-closed binding diagnostics.

It does not implement a gameplay package or custom inspector.

### Definition assets

`ObjectCapabilityDefinitionAsset` translates one capability-specific authored
field set into one immutable domain definition.

`ObjectFamilyDefinitionAsset` translates family defaults and an arbitrary
ordered list of variant entries into one immutable domain family.

No production family, prefab, scene placement, or tuning catalog is created by
this task.

## Required proof

Focused tests cover:

- arbitrary variant counts;
- relevant-capability-only composition;
- deterministic inheritance, override, reset, and fingerprinting;
- rejection of overrides targeting unselected capabilities;
- distinct registration;
- exact retry no-change;
- conflicting retry;
- duplicate identity rejection with both locations;
- isolated compatible scopes;
- explicit-scope precedence;
- missing, incompatible, cross-scope, and conflicting binding failures;
- arbitrary nested hierarchy placement;
- identity stability after rename, transform, sibling, and parent changes;
- explicit runtime-spawn identity;
- typed restart ordering;
- fifty restart/unbind/rebind cycles without duplicate registration;
- no global-search API in production authoring adapters; and
- no `UnityEngine` dependency in Domain or Contracts authoring paths.

## Non-goals

Object Authoring v1 does not:

- edit or migrate an existing enemy, turret, prop, door, hazard, or reward
  package;
- edit a scene, prefab, Stage 1 controller, or integration test;
- provide custom inspectors or automatic duplicate-ID repair;
- create a global registry or service locator;
- add health, combat, reward, wallet, inventory, mission, checkpoint, or
  persistence authority;
- create production balance data;
- add shared assembly references or modify project/package settings; or
- select unresolved restart, reward, door-payment, or hazard-kill policy.
