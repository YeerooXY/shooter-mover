# Placed object lifecycle v1

## Status

This document is the ADR-001 architecture lock for placed identity, scene-scope
binding, object families and variants, capability composition, instance
overrides, restart/reload behavior, doors, hazards, destructible props, and
reward-source integration.

It is normative for `OBJ-001`, `SRC-001`, `PROP-001`, `NORM-001`, `DOOR-001`,
`VOID-001`, `VAL-001`, and `INT-001`. It defines contracts and ownership only; it
does not implement components, prefabs, scenes, assets, inspectors, or balance.

It consumes rather than replaces:

- [StableId v1](../contracts/STABLE_ID_V1.md);
- [Combat Messages v1](../contracts/COMBAT_MESSAGES_V1.md);
- [Mission Messages v1](../contracts/MISSION_MESSAGES_V1.md);
- [Room Projection v1](../contracts/ROOM_PROJECTION_V1.md);
- [Encounter Lifecycle v1](../contracts/ENCOUNTER_LIFECYCLE_V1.md);
- [Reward and equipment architecture v1](../rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md);
- [File ownership rules](../FILE_OWNERSHIP.md); and
- [Stage 1 integration ownership](STAGE1_INTEGRATION_OWNERSHIP.md).

The words **must**, **must not**, **required**, and **only** identify locked
architecture requirements.

## 1. Named owners

| Concept | Sole owner task | Consumers |
|---|---|---|
| Generic placed identity, capability, variant, override, and scene-scope contracts | `OBJ-001` | All authored object packages |
| Reward profiles and placed reward-source override authoring | `SRC-001` | Props, enemies, boxes, integration |
| Existing destructible-prop migration | `PROP-001` | `INT-001` consumes the migrated package |
| Existing Blaster Turret identity/registration normalization | `NORM-001` | `INT-001` consumes the normalized package |
| Reusable door package | `DOOR-001` | Level authors and `INT-001` |
| Reusable void/fall-hazard package | `VOID-001` | Level authors and `INT-001` |
| Authoring inspectors and project/scene validators | `VAL-001` | Designers and integration review |
| Final Stage 1 serialized placement and references | `INT-001` after `DEMO-001` release | No concurrent serialized consumer |

An object package owns its implementation and package-local serialized assets.
It does not own the shared scene scope, reward application, combat authority,
mission state, checkpoint authority, wallets, holdings, or final Stage 1 scene.

## 2. Core object model

### 2.1 Family and variant identity

A placed object selects one family and one variant by stable identity:

```text
ObjectFamilyDefinition
- family_id
- display metadata
- default_variant_id
- ordered variants[]
- validation policy

ObjectVariantDefinition
- variant_id
- optional object_level
- selected capability definitions[]
```

`variant_id` is authoritative. `object_level` is optional designer metadata and
must not impose an array size, enum cap, or hardcoded number of variants.

Families provide defaults and organization. Variants select only capabilities
that apply. A family or variant does not become a mutable runtime state object.

### 2.2 Composable capabilities

Capabilities are bounded modules with their own definitions, resolved values,
validation, and runtime ports. Initial capability families may include:

- presentation;
- collision;
- destructible/vital behavior;
- reward source;
- movement;
- combat;
- targeting/tracking;
- lifecycle/restart;
- door behavior;
- condition expression;
- room transition;
- hazard volume;
- checkpoint/respawn response; and
- future environment-specific capabilities.

A crate variant may select presentation, collision, destructible, reward-source,
and lifecycle capabilities. A shooter turret may additionally select combat and
tracking. A door may select presentation, collision, condition, transition, and
lifecycle. A void region may select hazard and checkpoint response.

There must not be a giant universal object asset containing irrelevant combat,
movement, door, hazard, reward, shop, crafting, and equipment fields. Consumers
must not branch on one universal object-type enum to discover unrelated fields.

Cross-capability dependencies are explicit. For example, a damage response needs
a combat/vital port; a respawn response needs a checkpoint port; a reward source
needs a terminal source fact. A missing required capability is a validation error,
not an invitation to search the scene.

## 3. Resolved values and instance overrides

Every placed instance resolves a capability value in this order:

```text
explicit instance override
    ?? selected variant value
    ?? family default
```

Overrides are capability-specific groups. Only relevant groups appear for the
selected variant. An enabled group exposes:

- inherited value;
- explicit override value;
- validation result;
- resolved preview; and
- reset-to-inherited behavior.

Reward overrides use one explicit mode such as `Inherit`, `ForceNone`,
`ReplaceProfile`, `AppendGuaranteed`, or another versioned typed mode. Interacting
booleans must not create ambiguous reward behavior.

An instance override changes only that placed instance. It does not mutate the
family, variant, package prefab, shared definition, or another placement.

## 4. Authored placed identity

### 4.1 Serialized identity

Every persistent placed object has one serialized authored `StableId` instance
identity. The ID is independent of:

- scene path or scene name;
- hierarchy path;
- GameObject or Transform name;
- sibling index;
- prefab asset path;
- Unity instance ID;
- load order;
- component order; and
- current room projection/runtime ID.

Renaming, reparenting, reordering, moving between valid parent scopes, or
reloading a room must not change the authored placed identity.

A definition/family/variant ID does not replace the placed instance ID. Ten
placements of one crate definition have ten unique instance IDs.

### 4.2 Duplication and validation

Duplicating a prefab or scene object may initially copy its serialized ID because
that is ordinary Unity behavior. The duplicate is invalid until deliberately
assigned a new ID.

`VAL-001` must provide duplicate validation at least within the relevant scene or
project validation scope and a deliberate **Generate new instance ID** action.
The validator must report both/all conflicting object locations. Runtime
registration must fail closed or reject the later duplicate; it must not silently
rename, hash, or regenerate IDs during play.

Automatic runtime repair would make authored references unstable and is
forbidden.

### 4.3 Runtime-spawned identity

A runtime-spawned object receives identity through an explicit spawn request or
spawn context owned by the spawning system. The request supplies or deterministically
derives the runtime identity from stable inputs and documents its lifetime.

A spawned object must not infer identity from its generated Unity name, frame,
position, instance ID, or sibling index. Spawned runtime identity does not grant
permission to invent a durable authored placement identity.

## 5. Gameplay scene scope

### 5.1 Purpose

`OBJ-001` defines the generic scene-scope contracts. A room or integration root
may project them through a Unity component such as `GameplaySceneScope2D`, but
the contract is not Stage-1-specific and is not owned by an enemy package.

The scope is a composition/registration boundary. It is not a second mission,
combat, reward, wallet, holdings, encounter, checkpoint, or persistence authority.

The scope may expose typed ports for:

- placed participant registration and duplicate-ID rejection;
- current runtime projection and attempt/restart generation;
- combat target/hit registration;
- accepted terminal source-resolution submission;
- reward pickup projection service lookup;
- room/encounter condition readers;
- mission command submission;
- checkpoint/respawn lookup;
- door transition authorization;
- typed wallet/holdings read ports when a condition requires them; and
- lifecycle begin/complete notifications.

Packages depend on the narrowest port they require, not the entire concrete scope
component.

### 5.2 Binding precedence

A placed object binds through exactly this precedence:

1. an explicit serialized scene-scope reference override; otherwise
2. the nearest compatible parent scope.

The explicit reference must be reachable and compatible with the object's room
projection. The parent search is bounded to ancestors and stops at the nearest
compatible scope.

Global `Find*` APIs, static registries discovered by search, scene names, object
names, tags, hierarchy prefixes, and Stage 1 controller lookups are not primary or
fallback integration paths for new authoring.

A migration-only legacy fallback, if separately approved, must be time-bounded,
emit validation debt, and never be used by new prefabs. ADR-001 does not approve
such a fallback by itself.

### 5.3 Missing or conflicting scope

A missing required scope, incompatible explicit scope, cross-room scope reference,
or duplicate placed identity is an invalid configuration.

The object must fail closed: it may remain visibly diagnostic, disabled, or
unregistered according to its package policy, but it must not create a private
replacement authority, search the whole scene, or continue with fabricated IDs.

## 6. Registration and lifecycle ports

A placed runtime participant has separate authored and runtime identities:

```text
PlacedParticipantRegistration
- placed_instance_id
- definition/family/variant identity
- runtime_projection_id
- run_id
- attempt_or_restart_generation
- declared capabilities
```

Registration is idempotent for an exact equal registration in the same runtime
projection. Reusing a placed ID for a different object, capability set, projection,
or conflicting definition is rejected.

A lifecycle participant consumes typed begin/complete boundaries rather than
assuming Unity callback order. The minimum conceptual boundaries are:

- bind/register;
- activate/project current state;
- begin quick restart or unload;
- release attempt-local/transient resources;
- apply reset/reprojection state;
- complete restart/rebind; and
- unregister/unbind.

Exact method names are owned by `OBJ-001`; the state/authority separation is
locked here.

Unregistration removes runtime projection and subscriptions. It does not delete
authored identity, mission facts, source claims, reward commitments, applied
transactions, shop inventory identity, or player holdings.

## 7. State classification

Each object field belongs to one explicit lifetime:

| Lifetime | Examples | Owner |
|---|---|---|
| Authored definition | family, variant, capabilities, default values, references | Definition/package task |
| Authored placement | placed StableId, selected variant, override flags/values, explicit scope reference | Exact serialized scene/prefab owner |
| Attempt-local transient | effects, projectile views, temporary subscriptions, animation progress, pickup projection objects | Runtime package/presentation |
| Room-projection-local | runtime registration, projection ID, current visual/collider projection | Room/scene scope and package adapter |
| Run-durable | source claim identity, generated commitments, stable shop inventory for the run, accepted mission facts | Owning application/mission services |
| Profile/durable authority | applied money, scrap, holdings, applied operation IDs according to approved persistence model | Money/scrap/holdings/application owners |

A quick restart may rebuild the first two runtime categories. It must not infer a
reset of run-durable or applied authority state from that rebuild.

## 8. Restart and reload behavior

### 8.1 Quick restart

Quick restart preserves the parent run and authored placement identities while
using a fresh attempt/restart generation. Participants must:

1. stop producing new callbacks for the retiring attempt;
2. unsubscribe attempt-local listeners;
3. remove projectiles and transient effects they own;
4. unregister or reset runtime projection state;
5. restore package-defined initial health/collider/presentation/door/hazard state;
6. rebind using the same authored placed ID; and
7. reconcile durable source/reward facts before projecting pickups or enabled
   interactions.

An already-resolved source remains resolved. An unclaimed commitment may be
re-projected according to the explicit reward policy. A claimed commitment remains
retryable. An applied commitment remains applied.

### 8.2 Room reload

Room reload replaces one runtime projection. The new projection may receive a new
projection/runtime ID while retaining the same durable room ID and authored
placed IDs.

The new projection reads authoritative room/mission/reward state through typed
ports. Unknown projection keys fail closed. Reload does not itself clear a room,
refresh shop stock, regenerate rewards, reset a source claim, deactivate a
checkpoint, or roll back an applied transaction.

### 8.3 Mission restart

Mission restart is an explicit application/domain operation, not scene cleanup.
It resets authored gameplay projections according to the mission restart policy.
The policy must state whether unclaimed commitments are retained/re-projected or
cancelled and whether a replacement run identity is created.

Regardless of policy, mission restart must not:

- duplicate or partially roll back an operation;
- reuse a run/source/transaction ID for changed payload;
- infer reward loss from destroyed GameObjects; or
- permit same-run source farming by clearing the source-claim ledger.

### 8.4 New run

A new run receives a new `run_id`, source-claim namespace, runtime projection
identities, and run-seeded shop identities. Authored placed IDs and content IDs
remain stable. Profile-level wallet/holdings behavior follows the approved
persistence model; it is not decided by object cleanup.

The complete cross-system lifecycle matrix is in
[`STAGE1_INTEGRATION_OWNERSHIP.md`](STAGE1_INTEGRATION_OWNERSHIP.md).

## 9. Reward-source capability

A reward-source capability subscribes to one accepted terminal source fact. It
must not modify damage, health, projectile, encounter, or mission authority.

For enemy-backed sources, the adapter consumes the existing terminal combat/enemy
fact and, where policy requires durable completion, the matching encounter/mission
fact. For destructible props, the adapter consumes the package's once-only
confirmed destruction event. It does not infer destruction from missing objects,
animation completion, disabled colliders, names, or scene reload.

The source submits one immutable source-resolution request containing the run,
authored source instance, source definition/profile, progression context, and
operation identity. Reward generation/application owners decide duplicate,
claim, and application outcomes.

Restarting or re-enabling the placed component cannot create a second logical
source operation for the same source in the same run.

## 10. Destructible props and turret normalization

`PROP-001` and `NORM-001` migrate existing packages without replacing accepted
combat/lifecycle behavior.

### 10.1 Destructible props

The migrated prop authoring must explicitly reference:

- family and variant definition;
- placed instance ID;
- presentation root;
- collider/collision capability;
- accepted confirmed-hit/destructible authority;
- destruction presentation hooks;
- lifecycle/restart capability; and
- inherited reward source plus optional typed instance override.

Name prefixes such as `Crate_`, collider names derived from visual names, sibling
indices, and Stage 1 controller scans must not determine gameplay type, collider,
identity, or reward behavior.

### 10.2 Blaster Turret

The normalized turret retains existing tracking, cadence, physical projectiles,
damage, destruction, destroyed-collision, and restart behavior. It replaces:

- global scene-context discovery;
- scene-wide concrete turret scans; and
- hierarchy/name-derived identity.

The turret binds through the explicit/nearest-parent generic scope, registers its
authored placed ID, and depends only on required typed ports. It does not gain a
private scene context or reward authority.

## 11. Door package contract

Doors consume typed lifecycle, condition, room, encounter, mission, wallet/holding
read, and transition ports. A reusable door definition may contain:

- opening mode;
- initial state;
- typed condition expression;
- one-way policy;
- open/closed collider states;
- presentation/animation hooks;
- optional room-transition reference; and
- restart policy.

Conditions compose `All` and `Any` over typed leaves such as:

- always;
- trigger entered;
- interaction requested;
- encounter resolved;
- target destroyed;
- currency available;
- key owned;
- direction allowed; and
- room transition authorized.

A condition reads typed facts. It does not mutate mission state, spend money,
consume an item, clear an encounter, or load a scene by itself. Payment, key
consumption, and mission transitions require their owning transaction/command
services.

Door state restoration on quick restart is package-defined and explicit. Durable
route/room completion remains mission-owned. A door GameObject being open or
closed is not durable mission truth.

The first door increment may expose a read-only currency condition port without
implementing payment. Full payment semantics remain an unresolved product
choice.

## 12. Void/fall-hazard package contract

A hazard definition declares explicit responses for each category:

- player;
- enemy;
- projectile; and
- prop.

Responses are typed, for example `Ignore`, `Damage`, `Destroy`, `Respawn`,
`RemoveProjectile`, or `KeepSupported`. Unsupported or contradictory combinations
are validation errors.

Hazards consume:

- accepted combat/contact ports for damage or destruction;
- accepted enemy lifecycle ports for terminal facts;
- a projectile-removal port;
- a checkpoint/respawn destination port;
- typed category/classification data; and
- restart lifecycle boundaries.

A hazard must not create a second health authority, directly mutate an unrelated
component, discover checkpoints by object name, or treat a trigger callback as a
durable kill/reward fact.

Whether an enemy destroyed or withdrawn by a hazard counts as a kill, encounter
resolution, or reward-bearing source is a human policy decision. The hazard emits
or requests the typed fact selected by that policy; it does not decide reward
balance.

## 13. Authoring and runtime validation

At minimum, focused validation must detect:

- missing or malformed placed `StableId`;
- duplicate placed IDs;
- missing family or variant;
- duplicate family/variant IDs;
- variant selecting incompatible capabilities;
- override enabled without a complete value;
- unresolved required capability dependency;
- missing, incompatible, or cross-room explicit scope;
- no reachable parent scope when one is required;
- global/name-based integration remaining in normalized production paths;
- invalid door condition graph;
- transition door without a valid typed transition reference;
- respawn hazard without a valid checkpoint destination;
- hazard response incompatible with the target category;
- reward override inconsistent with its selected mode; and
- a serialized path with more than one active owner.

Runtime duplicate/missing-scope checks fail closed even when editor validation was
not run. Runtime checks do not silently repair authored data.

Required behavioral proof includes rename/reparent stability, arbitrary hierarchy
placement, duplicate rejection, exact registration retry, fifty quick restarts,
room reload reprojection, door collider/state restoration, hazard filtering, and
no duplicate reward source operation.

## 14. Unresolved human decisions

These remain explicit and must not be chosen by package implementers:

- Mission-restart treatment of generated/projected rewards and whether restart
  creates a replacement run.
- Source-claim lifetime beyond one run and future save/resume behavior.
- Door payment and key-consumption semantics.
- Whether ordinary enemies falling into a void grant rewards, count as kills, or
  withdraw from an encounter.
- Boss reward timing relative to destruction, encounter resolution, and durable
  room-clear acceptance.
- Exact collider, health, movement, door, hazard, and reward balance values.
- Whether any legacy lookup fallback is needed for one migration release. Such a
  fallback requires a separate explicit approval and removal condition.

These choices do not block Wave 1 because the object model exposes typed ports and
policies without selecting product tuning.

## 15. Non-goals

This architecture lock does not:

- implement object, door, hazard, reward, or editor code;
- create or edit scenes, prefabs, assets, ScriptableObjects, or `.meta` files;
- change accepted enemy, combat, encounter, mission, room, or restart authority;
- add a global service locator or scene scan;
- authorize a universal all-purpose object definition;
- decide balance or persistence values; or
- grant package tasks ownership of Stage 1 serialized integration paths.
