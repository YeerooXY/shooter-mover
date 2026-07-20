# PROP-RUNTIME-001 — Generic prop definitions and runtime capabilities

## Launch provenance

- Exact launch SHA: `af83d72e80d216dbe78678754d6a66189967127f`
- Branch: `agent/prop-runtime-001-data-driven-props`
- Target: `main`
- Production Stage 1 composition is intentionally unchanged.

## Boundary

The implementation separates three immutable or independently owned concepts:

1. `PropDefinitionV1` describes one reusable content definition and presentation ID.
2. `PropPlacementV1` supplies one exact `PlacedObjectIdentity` plus the selected definition ID.
3. `PropRuntimeV1` owns only run-local state for that one placement.

`PropRuntimeFactoryV1` is the generic placement-to-runtime port. `ROOM-JSON-LIVE-001`
can later adapt imported room placements into this factory without introducing room-, prop-,
or prefab-name branches.

## Capability model

Definitions use immutable `PropCapabilityV1` descriptors while placements reuse the existing
`PlacedObjectIdentity` authoring contract. A `PropCapabilityRegistryV1` maps stable capability
IDs to validators. The built-in registry supports:

- solid or non-solid collision;
- indestructible or health-based destructibility;
- per-damage-channel multipliers;
- explode-on-destroy facts;
- drop-on-destroy request facts;
- interaction facts;
- switch state/facts;
- objective facts;
- neutral or hostile damage alignment plus an open damage-policy ID;
- room-clear participation;
- decorative-only presentation.

Unknown capability IDs fail closed. Combination validation rejects examples such as an
exploding indestructible prop, room-clear participation without health-based destruction,
a switch without interaction, or decorative-only content that also owns combat/reward state.

Adding another prop that only uses registered mechanics requires a definition, a presentation
mapping, and a placement. No runtime class or controller branch is required.

## Authority and replay rules

`PropRuntimeV1` owns the health and switch state of exactly one placement. Two placements using
one definition therefore have separate state and snapshot fingerprints.

Damage and interaction commands use stable operation IDs:

- exact retries return `DuplicateNoChange`;
- conflicting reuse rejects without mutation;
- duplicate retries emit no terminal, explosion, drop, objective, interaction, or switch facts.

On accepted terminal destruction, the runtime emits immutable attributed facts. It does not
roll rewards, mutate inventory, complete objectives, or execute explosion damage. Those remain
downstream consumers.

## Friendly fire and faction behavior

The definition records neutral/hostile alignment and an open damage-policy ID. The runtime does
not decide friendly-fire semantics. Every combat-capable prop requires an injected
`IPropDamageEligibilityPolicyV1`, which receives source participant/faction, target participant,
alignment, policy ID, and damage channel before health can change.

## Focused test command

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Props.PropRuntimeV1Tests -testResults Temp/prop-runtime-001-editmode.xml -logFile Temp/prop-runtime-001-editmode.log
```

The focused suite covers decorative props, independent placement health, one-shot barrel
terminal/explosion/drop facts, exact and conflicting replay, authored damage resistance,
indestructible props, unknown capabilities, invalid combinations, injected friendly-fire
policy, replay-safe switches, and deterministic catalog ordering.
