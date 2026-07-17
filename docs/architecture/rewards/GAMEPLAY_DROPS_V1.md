# Gameplay Drops V1

## Scope and launch provenance

DROP-001 was launched from `main` at commit
`45b276a8c415508ac8c0ddd8283234a6bbb2948e`.

This package owns gameplay-drop authoring and source-operation projection. It does
not own random generation, reward commitment/claim truth, physical pickup
collection, money, scrap, strongbox holdings, equipment inventory, or stack
inventory.

## Architecture

A gameplay host references two reusable pieces:

1. `GameplayDropProfileDefinitionAsset` describes what may drop.
2. `GameplayDropSource2D` binds that profile to a stable placed-object identity
   and submits one immutable source operation to `IRewardSourceOperationSink`.

The sink is normally the existing PICK-001 drop factory. That path composes:

`Gameplay fact -> DROP source -> GEN-001 -> PICK-001 -> RAP-001 -> child authorities`

DROP-001 never calls a wallet, inventory, scrap, or strongbox service.

## Profile semantics

Profiles use the shared REW-001 reward vocabulary and support:

- guaranteed entries;
- independent probability rolls in integer millionths;
- exclusive weighted alternatives;
- weighted explicit no-drop outcomes;
- whole-profile explicit no-drop;
- money, scrap, strongbox, premium-ammunition, and miscellaneous grants;
- any mixed combination of the above.

Sampling is not implemented here. `RewardGenerationServiceV1` remains the sole
deterministic generator.

## Manual override semantics

Each placed source has exactly one explicit mode:

- **Default**: use the authored profile unchanged.
- **Forced none**: replace the profile with explicit no-drop.
- **Forced specific reward**: replace the profile with one guaranteed grant.
- **Append guaranteed reward**: retain the profile and append one guaranteed
  grant.

Overrides resolve through the existing immutable `RewardSourceOverrideV1`
model. They do not perform generation or application.

## Identity and replay safety

The stable source instance comes from `PlacedObjectAuthoring2D`. For a given run
and source instance, DROP derives deterministic:

- source-operation identity;
- commitment identity;
- restart-participant identity.

Names, tags, hierarchy positions, Unity instance IDs, frame numbers, and callback
counts do not participate.

Repeated terminal callbacks submit the exact same
`RewardOperationRequestV1`. Existing sinks classify that as
`ExactDuplicateNoChange`; PICK reuses the same projection and RAP retains the
exactly-once commitment and claim lifecycle. A reused source-operation identity
with a different profile fingerprint is rejected as a conflicting duplicate.

## Host integration

`IGameplayDropSourceV1` is host-agnostic. Destructible props, turrets, mobile
droids, pursuers, ram units, bosses, and future objects attach or reference the
same component and interface. DROP contains no checks for concrete gameplay
types.

A host should invoke `SubmitGameplayDrop()` only from its authoritative terminal
destruction/death fact. Duplicate invocations are safe, but callers should still
avoid treating transient collision callbacks as terminal facts.

## Authoring checklist

1. Give the host a stable `PlacedObjectAuthoring2D` identity and gameplay scope.
2. Create or select a `GameplayDropProfileDefinitionAsset`.
3. Add `GameplayDropSource2D`.
4. Assign the profile and the shared reward operation sink.
5. Select a manual override mode; leave **Default** for normal profile behavior.
6. Trigger `SubmitGameplayDrop()` from the host's terminal fact bridge.

## Verification policy

EditMode coverage locks profile shapes, all supported reward families, explicit
no-drop, override resolution, per-instance operation identity, and conflict
classification.

PlayMode coverage locks repeated callback identity and the shared host-agnostic
contract across prop, turret, droid, and boss-shaped hosts. A pipeline test also
routes a money drop through GEN, PICK, and RAP and asserts one child-authority
application across repeated death and collection callbacks.

These tests are authored in the repository. Unity execution is not claimed
unless a Unity test-result XML reports zero failures.
