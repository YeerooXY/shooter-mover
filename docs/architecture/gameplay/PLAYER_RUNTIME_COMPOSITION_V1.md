# Player Runtime Composition V1

## Scope

`ShooterMover.PlayerRuntime` is a reusable Unity-side composition seam for one player actor. It does not migrate Stage 1 by itself and does not modify `Stage1VisibleSliceController`, scenes, or prefabs.

The composition reuses `PlayerActorAuthority` as the only health/death authority. It adds explicit adapter boundaries around the already-existing movement lifecycle, presentation, input ownership, trusted participant attribution, HUD projection, and run death facts.

## Ownership boundaries

| Capability | Owner | This component may do | This component must not do |
| --- | --- | --- | --- |
| Player identity, lifecycle generation, health, death, damage/healing replay | `PlayerActorAuthority` | Construct once, forward commands, read immutable snapshots | Create fallback health, mutate health directly, award rewards |
| Position, velocity, movement generation, thruster state | Existing `MovementActor2D` through `MovementActorPlayerRuntimeAdapter` | Read immutable movement snapshots, request explicit restart, dispose the owned lifecycle adapter | Duplicate locomotion or physics authority |
| Sprite, mounts, boost trail, visual reset | `IPlayerPresentationRuntime` implementation | Refresh continuous boost and project restart snapshots | Decide health, death, movement, attribution, or rewards |
| Local/future network input | `IPlayerInputRuntime` implementation | Acquire one explicit actor/participant ownership lease and release it exactly once | Become a global input singleton or infer participant ownership from a client claim |
| HUD health | `PlayerHudHealthProjector` | Read `PlayerActorSnapshot` and return a getter-only projection | Write health or retain an authority reference |
| Run statistics/death attribution | `IPlayerRunCoordinator` implementation | Observe immutable `GameplayEntityDeathFact` values | Award kills, XP, money, inventory, drops, or routing |

## Construction and failure policy

`PlayerRuntimeCompositionRoot` permits one successful runtime construction and contains no static registry. Failed validation does not create fallback truth or acquire input ownership. Construction requires all of the following:

- an immutable `PlayerActorDefinition` inside `PlayerRuntimeConfiguration`;
- an existing movement adapter;
- a presentation adapter;
- an input adapter;
- a trusted source-participant resolver;
- a run coordinator observer.

Missing configuration fails closed. No default actor identity, participant, character, faction, health, or generation is invented. The movement generation must exactly match the initial `PlayerActorAuthority` lifecycle generation before input ownership is acquired.

A second construction attempt is rejected without reacquiring input. Two separate composition roots cannot share one correctly implemented input adapter because the second ownership acquisition fails.

## Damage and attribution

`PlayerDamageRequest` preserves event identity, source actor, target actor, amount, combat channel, and lifecycle generation. Its `UntrustedSourceRunParticipantId` is diagnostic-only and is never forwarded to `DamageReceiverCommand`.

The adapter resolves trusted participant attribution from `ITrustedPlayerAttributionResolver` using the authoritative source actor identity. The resulting `DamageReceiverCommand` is processed by `PlayerActorAuthority`, which retains damage replay protection, generation validation, target validation, health mutation, and death-fact production. Healing uses the same resolver before creating `PlayerActorHealingCommand`; untrusted participant claims are never forwarded for either path.

The composition returns the original accepted/rejected `DamageReceiverResult`. A death fact is forwarded to the run coordinator only when the authority produces one. Duplicate lethal damage therefore does not emit a second death observation.

The component exposes no kill, XP, money, drop, reward, inventory, wallet, or routing API.

## Restart coordination

The player and movement generations are intentionally separate fields owned by separate authorities, but this seam keeps them synchronized during the Stage 1 player lifecycle.

A runtime restart command must:

1. target the composed actor;
2. identify the current retiring generation;
3. advance exactly one generation;
4. match both the current player lifecycle generation and current movement generation.

The composition validates the entire command before mutation. It then restarts movement, restarts `PlayerActorAuthority` with the same operation ID and generations, exports the combined snapshot, and asks presentation to reset from that snapshot. Exact restart replay is returned as a duplicate without restarting either capability again. Stale or future generations are rejected before either authority moves.

`MovementActorPlayerRuntimeAdapter` delegates to the existing `MovementActorLifecycle.RestartActor()` and verifies that the movement actor reached the requested generation.

## Disposal order

Disposal is idempotent. The composition:

1. releases explicit input ownership;
2. disposes presentation resources;
3. disposes the movement adapter/lifecycle;
4. disposes the input adapter resource.

No resource is disposed twice by the composition.

## Ordered Stage 1 cutover patch

The later Stage 1 integration should remain a small patch rather than replacing the controller API.

1. **Construct runtime**
   - Keep the existing Stage 1 movement actor and presentation objects.
   - Wrap `MovementActorLifecycle` with `MovementActorPlayerRuntimeAdapter`.
   - Implement the narrow presentation, input, trusted-attribution, and run-coordinator ports over existing Stage 1 references.
   - Build `PlayerActorDefinition` from the authoritative run participant/character/faction data and the current movement generation.
   - Construct one `PlayerRuntimeCompositionRoot`.

2. **Replace direct health reads/writes**
   - Route hazard and enemy projectile hits through `PlayerDamageRequest`.
   - Route healing through `PlayerHealingRequest`; the runtime resolves trusted participant attribution before constructing `PlayerActorHealingCommand`.
   - Replace HUD reads with `ExportHudHealth()`.
   - Keep death/rejection results visible to the existing coordinator; do not add reward logic here.

3. **Replace `BuildPlayer` ownership**
   - Keep current GameObject/component construction temporarily.
   - Move only the ownership of the assembled player references behind the runtime attachments.
   - Do not copy the large PR #211 `Stage1PlayerPresentationV1`; use it only as a checklist for current references.

4. **Delegate restart, boost, and disposal**
   - Replace local restart ordering with one `PlayerRuntimeRestartCommand`.
   - Call `RefreshContinuousPresentation()` from the retained presentation refresh point.
   - Dispose the composition root once from the current Stage 1 cleanup path.

5. **Remove obsolete fields after parity tests**
   - Remove `local playerHealth`, direct health mutation helpers, duplicate restart flags, boost refresh ownership, and duplicate input/presentation disposal only after focused EditMode and Stage 1 PlayMode parity tests pass.

## Current non-goals

- Stage 1 controller migration;
- scene or prefab edits;
- inventory, weapons, loadout, XP, money, drops, strongboxes, routing, or persistence;
- a global player singleton or service locator;
- a new health model;
- moving player health into a `MonoBehaviour`;
- copying PR #211 implementation.
