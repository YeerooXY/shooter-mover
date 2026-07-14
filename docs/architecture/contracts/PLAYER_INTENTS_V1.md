# Player Intents v1

## Status and scope

Player Intents v1 is the immutable, engine-independent boundary between input
adapters and gameplay/application consumers. It describes what the player intends
for one sampling interval without exposing the physical control, binding,
platform, or Unity Input System action that produced it.

The implementation lives in `ShooterMover.Contracts.Input` and must not reference
`UnityEngine`. This contract does not implement adapters, bindings, rebinding,
aim assistance, gameplay behavior, or an InputAction asset.

## Contract types

`PlayerIntentFrame` contains one complete sample:

| Field | Meaning |
|---|---|
| `Move` | Planar movement intent. |
| `Aim` | Planar aim intent, independent from movement. |
| `Fire` | Shared fire intent consumed by every ready mounted weapon. |
| `PowerModifier` | Shared request to empower ready mounted weapons. |
| `Thruster` | Directional-thruster activation intent. |
| `Interact` | Context interaction intent. |
| `Map` | Map-view intent. |
| `PauseMenu` | Pause or menu-boundary intent. |
| `UiNavigation` | Two-axis UI navigation intent. |
| `WasFocusLost` | Marks the synthetic safe sample emitted at focus loss. |

All properties are get-only. `NormalizedIntentVector2`, `ButtonIntent`, and
`PlayerIntentFrame` are readonly value types. Publishing one frame therefore does
not require a managed allocation, and consumers must not mutate or reinterpret a
frame after publication.

## Normalized vectors

`NormalizedIntentVector2.Create(x, y)` accepts finite `float` components only.
NaN and positive or negative infinity are rejected with
`ArgumentOutOfRangeException`.

A vector whose magnitude is at most one preserves its analogue magnitude. A
finite vector outside the unit circle is normalized to the unit circle. Zero is
valid. Move and aim are separate values and are never derived from one another;
for example, moving east while aiming north is represented directly. UI
navigation uses the same validated vector primitive so an adapter cannot pass
non-finite values through a different route.

Floating-point comparisons at the unit boundary use normal IEEE-754 tolerance;
the semantic invariant is a direction and magnitude bounded to the unit circle.

## Button state and transitions

Every button-like action uses `ButtonIntent`, which records:

- `IsHeld`: final held state at the end of the sample;
- `WasPressed`: at least one press edge occurred during the sample;
- `WasReleased`: at least one release edge occurred during the sample.

The valid canonical states are:

| State | Held | Pressed | Released | Meaning |
|---|---:|---:|---:|---|
| `Inactive` | false | false | false | No active state or edge. |
| `Held` | true | false | false | Continued hold. |
| `Pressed` | true | true | false | Pressed and held at sample end. |
| `Released` | false | false | true | Released and not held at sample end. |
| `Tap` | false | true | true | Press then release within one sample. |
| `ReleaseThenPress` | true | true | true | Release then press within one sample. |

A lone press ending unheld and a lone release ending held are rejected because
they contradict the final state. Recording both edges plus the final held state
preserves a fast tap or release/repress without inventing an ambiguous generic
"changed" flag.

## Simultaneous actions

Each action has its own `ButtonIntent` in the same frame. No exclusive mode or
single-action union discards concurrency. Fire, power modifier, thruster,
interaction, map, pause/menu, and UI navigation may therefore coexist. In
particular, a frame can carry both `Fire = Pressed` and `PauseMenu = Pressed`;
the receiving application decides ordering and policy without losing either
intent at the boundary.

The shared fire and power values are not mount-specific. Combat consumers fan
one shared request out to all ready mounts while each mount retains its own
cadence, heat, charge, recovery, and power-bank rules.

## Focus-loss safety

An adapter must retain the last published frame. When application focus is lost,
it emits exactly one `PlayerIntentFrame.FromFocusLoss(previous)` sample before
emitting neutral unfocused samples.

The focus-loss sample:

1. sets move, aim, and UI navigation to zero;
2. converts every action that was held at the end of the previous sample to one
   `Released` edge;
3. clears prior press/release edges for actions that were not held; and
4. sets `WasFocusLost` to true.

While focus remains absent, adapters emit `PlayerIntentFrame.Neutral` and do not
reconstruct held state from stale observations. After focus returns, new samples
begin from neutral and only newly observed state may become held. This prevents
stuck movement, firing, power, thruster, map, interaction, or pause/menu state
without naming or depending on any physical device.

## Adapter mapping

Adapters own physical and platform details and publish only these contracts.
Representative mappings are:

- a keyboard/mouse adapter converts movement bindings to `Move`, cursor or
  pointer direction to `Aim`, and binding edges to `ButtonIntent` values;
- a gamepad adapter converts stick values to the same normalized vectors and
  button edges to the same action fields;
- a later touch adapter converts virtual controls or gestures to those same
  vectors and button transitions.

These adapters may use Unity Input System actions internally, but action names,
control paths, key codes, binding IDs, device IDs, and `InputAction` references
must stop at the adapter boundary. Gameplay and application code consume only
`PlayerIntentFrame`, `NormalizedIntentVector2`, and `ButtonIntent`.

Different adapters must produce equivalent frames for equivalent player intent.
Adapter-specific dead zones, pointer projection, sensitivity, and rebinding are
outside this v1 contract and require their own owned implementation and proof.

## Validation and fixtures

EditMode contract tests cover:

- normalization outside the unit circle and preservation inside it;
- rejection of NaN and infinity;
- independent move and aim values;
- canonical held, pressed, released, tap, and release/repress states;
- rejection of contradictory single-edge states;
- simultaneous fire, power, thruster, pause/menu, movement, and navigation;
- focus-loss neutralization and synthesized releases;
- a neutral allocation-free value frame; and
- absence of a UnityEngine assembly reference from the contracts assembly.

Exact Unity execution remains pending when the pinned editor is unavailable.
Static review can still verify that runtime source contains no Unity type and no
physical-control identifier.

## Versioning and non-goals

Changing vector normalization, transition semantics, focus-loss behavior, action
membership, or simultaneous-action representation requires a new player-intent
contract version and an explicit consumer migration.

Player Intents v1 does not add an InputAction asset, bindings, adapter code,
control-scheme selection, aim-assist tuning, gameplay command ordering, pause
policy, weapon behavior, UI behavior, persistence, or serialized state.
