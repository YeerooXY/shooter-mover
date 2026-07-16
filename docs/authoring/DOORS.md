# Reusable doors

`DOOR-001` provides a scene-independent 2D door package under
`Assets/ShooterMover/ContentPackages/Environment/Doors/`.

## Package boundary

A door is a projection of authored configuration. It does not own durable room
or mission truth, scene loading, wallet balances, key inventory, encounter
completion, target destruction, or payment/consumption semantics.

The package consumes:

- `PlacedObjectAuthoring2D` for the authored placed `StableId`, family/variant
  resolution, explicit-or-nearest-parent `GameplaySceneScope2D` binding, and
  placed participant registration;
- `IRestartParticipant` for deterministic quick-restart phases;
- typed condition readers for encounter, target, wallet, and key facts;
- `RoomSocket` values for explicit transition endpoints; and
- `IDoorTransitionAuthorizationPort` for traversal admission.

There is no global lookup, object-name lookup, scene-name routing, Stage 1
controller dependency, direct wallet spend, key consumption, mission mutation,
or scene loading in the package.

## Required components

Add these components to the same reusable door root:

1. `PlacedObjectAuthoring2D`
2. `DoorController2D`

Configure `PlacedObjectAuthoring2D` with:

- one unique authored placed ID;
- a family/variant selecting the relevant presentation, collision, door, and
  lifecycle capabilities;
- an explicit compatible scope when needed, otherwise a compatible ancestor
  scope; and
- only capability-specific instance overrides.

Renaming, moving, or reparenting the door under another valid compatible parent
scope does not change its authored identity.

Configure `DoorController2D` with:

- at least one typed condition;
- one or more closed-state colliders;
- distinct closed and open presentation roots;
- a package-defined initial state;
- optional animator trigger names and Unity events;
- a one-way policy; and
- typed source/destination room sockets plus an authorization port when the
  door participates in a room transition.

## Conditions

Condition sets use deterministic `All` or `Any` composition. Every leaf returns
an indexed result, a typed diagnostic code, and a deterministic diagnostic
fingerprint.

Supported leaves are:

| Condition | Input |
|---|---|
| Always | no external input |
| Trigger entered | attempt-local trigger input |
| Interaction requested | attempt-local interaction input |
| Encounter resolved | `IDoorEncounterConditionReader` |
| Target destroyed | `IDoorTargetConditionReader` |
| Wallet amount at least | read-only `IDoorWalletReadPort` |
| Key owned | read-only `IDoorKeyReadPort` |

Wallet and key ports are deliberately read-only. A condition can make a
requirement visible and gate opening, but it cannot spend currency or consume a
key. Those operations require separately owned transaction services and an
approved product policy.

An empty set, malformed ID, impossible wallet threshold, or missing reader fails
closed with a deterministic diagnostic. It does not fabricate a false
authority or search the scene for one.

## Opening, presentation, and animation

When closed, every configured closed-state collider is enabled, the closed
presentation root is active, and the open root is inactive. Opening applies the
inverse projection.

`NotifyTriggerEntered`, `NotifyInteractionRequested`, `TryOpen`, `Close`, and
`ReevaluateAuthoritativeConditions` are explicit integration calls. State
changes expose:

- a C# `StateChanged` event with the placed ID and previous/current state;
- package-local `opened` and `closed` Unity events.

Animation components can subscribe through the Unity events or `StateChanged`
without requiring the reusable package to depend on a specific animation
module. Animation and presentation callbacks do not become door, mission, or
transition authority.

## One-way transitions

Transition authoring uses two explicit `RoomSocket` endpoints from distinct room
projections with compatible directions. The forward pair is reversed for a
reverse traversal request.

`TryRequestTransition` checks, in order:

1. the door is open;
2. the one-way policy allows the requested direction;
3. both typed sockets exist and are compatible; and
4. `IDoorTransitionAuthorizationPort` authorizes the request.

An authorized result is a typed admission result only. The door never loads a
scene or mutates route/mission state itself.

## Restart behavior

The controller registers as an OBJ-001 restart participant using the same
authored placed ID. Quick restart performs the shared phases:

1. retire attempt-local trigger and interaction facts and unbind the old placed
   projection;
2. release package transients;
3. restore the configured initial collider/presentation state; and
4. rebind with the replacement attempt generation, then reread authoritative
   conditions.

Thus an interaction from the retiring attempt cannot leak into the replacement
attempt. An authoritative encounter/target/wallet/key fact can reopen the
restored door after rebind. Durable route or room completion remains owned by
mission authority.

## Validation checklist

Before placing a package prefab in an integration-owned scene, confirm:

- [ ] the placed ID is canonical and unique;
- [ ] the family, variant, and required capabilities resolve;
- [ ] exactly one compatible explicit or nearest-parent scope is available;
- [ ] at least one condition exists and every required reader is connected;
- [ ] all closed colliders are assigned;
- [ ] open and closed presentation roots are assigned and distinct;
- [ ] transition doors have two compatible typed sockets;
- [ ] transition doors receive an authorization port;
- [ ] one-way direction matches the authored route;
- [ ] rename/reparent and quick-restart checks pass; and
- [ ] no scene, project setting, Stage 1 controller, wallet, key inventory, or
  mission implementation was added to the door package.

Final Stage 1 placement and serialized references remain exclusively owned by
`INT-001`.
