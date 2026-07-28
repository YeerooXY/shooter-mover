# LOOT-PRESENTATION-001 showcase

## Purpose

`LootPresentationShowcase.unity` is a development-only visual acceptance scene for immutable loot projections. It demonstrates presentation behavior without generating rewards, mutating a wallet, adding holdings, consuming a strongbox, applying a reward, or saving character state.

Open it in Unity `6000.3.19f1`:

`Assets/ShooterMover/Scenes/LootPresentation/LootPresentationShowcase.unity`

The showcase controller disables itself outside the Editor and development builds. Disposable pickup, reward-child, strongbox-instance and equipment-instance identities use a `development-*` namespace. Production content and tier identities remain canonical catalogue references.

## Reusable production-facing views

The showcase now composes the same bindable projection views intended for later production callers:

- `LootRunHudViewV1` binds one immutable `RunLootTotalsPresentationV1`.
- `OwnedStrongboxGroupsViewV1` binds grouped exact instances, owns only local exact selection, and rejects Open 5 when fewer than five exact instances exist.
- `StrongboxOpeningPresentationViewV1` binds one existing `StrongboxOpeningSceneSessionV1` and renders closed, opening, reveal and complete visual stages.
- `StrongboxRewardCardsViewV1` binds one immutable committed result and renders only the currently visible immutable reward items.
- `LootPickupVisual2D` binds one exact pickup projection and owns only sprite, glow, bob and accepted-collection feedback.

The development controller supplies fixture projections; it does not own the reusable rendering logic.

## Production binding contracts

The reusable presentation consumes immutable inputs:

- `LootPickupPresentationV1` keeps the exact pickup and reward-instance identities for credits, scrap, or one production strongbox tier.
- `RunLootTotalsProjectorV1` derives HUD totals from exported `RunSessionCollectedRewardV1` facts. The HUD model and view expose no mutation methods.
- `StrongboxGroupingProjectorV1.TryProjectUnopened(...)` consumes immutable `MissionRunStrongboxResultV1` objects and rejects opened, unknown, null, or duplicate instances.
- `OwnedStrongboxGroupsViewV1` preserves every exact instance beneath grouped counts and resolves exact Open 1/Open 5 batches without mutating holdings.
- `StrongboxOpeningSceneSessionV1` and `StrongboxPresentationPlaybackV1` animate an already-frozen `StrongboxOpeningPresentationResultV1`. Skip and replay reuse that result and never invoke reward generation.
- `StrongboxRewardCardsViewV1` displays the immutable money, scrap or exact equipment result projection without applying it.

A future production composition should adapt an available `RunPickupSnapshotV1` into `LootPickupPresentationV1` using its exact pickup ID, generated reward child ID, reward kind, content ID, and quantity. Collection must continue through `RunLocalPickupAuthorityV1`; only accepted collection or accepted exact replay may start the visual attraction/retire feedback.

## Showcase controls

- **Pickup gallery:** credits, scrap, and every tier currently authored by `ProductionStrongboxCatalogV1`. No fixture-owned tier list exists.
- **Owned groups:** Steel × 10 and two examples of every later production tier. Expand a group to select exact instance IDs.
- **Open 1 / Open 5:** changes only the exact-ID batch layout. Open 5 fails closed when the selected group has fewer than five exact instances.
- **Play:** starts closed → opening → reveal → continue against one immutable sample result.
- **Replay same result:** recreates presentation timing around the same result object.
- **Skip / fast-forward:** changes visual time only.
- **Authoritative pickup fixture:** reject once, accept collection, destroy the view, and reconstruct an uncollected view from fixture truth.

## Manual acceptance

1. Enter Play Mode in the showcase scene.
2. Inspect distinct credits, scrap, and all production-tier pickup visuals. Confirm higher tiers have progressively stronger glow.
3. Inspect `Steel x 10`; select several exact IDs and verify the group quantity does not change.
4. Switch between Open 1 and Open 5 on Steel and verify the displayed batch contains the exact requested number of unique IDs.
5. Select a two-instance later tier and verify Open 5 is unavailable with an insufficient-instance diagnostic.
6. Play the full opening. Confirm the box visibly moves through closed, opening, reveal and complete states.
7. Record the reward titles, quantities, content IDs, and equipment instance ID.
8. Replay, skip, and fast-forward. Confirm those immutable facts are unchanged.
9. In the pickup fixture, press **Reject Next**, then **Collect**. Confirm the view remains after rejection.
10. Destroy and reconstruct the still-uncollected view. Confirm the same exact pickup returns.
11. Collect successfully. Confirm the feedback starts only after acceptance and the fixture reports no available pickup.
12. Exit Play Mode and verify no permanent character wallet, holdings, unopened boxes, inventory, or save changed.

## Failure behavior

- Unsupported reward kinds and unknown strongbox tiers fail closed.
- Duplicate exact strongbox IDs reject the entire grouping projection.
- Opened strongbox results cannot be projected as selectable unopened boxes.
- Open 5 rejects instead of silently resolving fewer exact instances.
- A pickup visual cannot be rebound to another exact pickup identity or conflicting facts.
- Destroying a visual does not change fixture or production collection truth.
- Skip is unavailable before an immutable result exists or while a transaction is pending.
