# LOOT-PRESENTATION-001D — Canonical Pickup Feedback

## Starting point

- Repository: `YeerooXY/shooter-mover`
- Refreshed `main`: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`
- Parent stack head: `fdc42b1f8c034eb34876e18ef0e5c1144f277818`
- Feature branch: `agent/loot-presentation-001-pickup-feedback`

## Requested visible behavior

After the canonical run-local pickup authority accepts an exact pickup collection,
the pickup should visibly move and fade toward the player before its GameObject is
destroyed. Rejected collection must leave the pickup available and visible.

## Authority and ownership

- `RunLocalPickupState` remains the sole collection authority.
- `RunPickupPresenter2D` owns the registry of currently visible physical projections.
- `RunRewardPickup2D` submits the typed collection command and coordinates retirement
  only after receiving an accepted canonical result.
- Optional projection and feedback components own visual state only.
- `LootPickupVisual2D` never reports collection, grants rewards, changes Run Session
  facts, or destroys authoritative state.

## Stable identities

The pickup, reward instance, run, lifecycle, collector entity, collector participant,
and collection operation identities are unchanged. No identity is derived from a
GameObject name, transform, hierarchy, UI slot, or screen position.

## Commit and rollback behavior

The transaction commit point remains canonical collection acceptance. Once accepted:

1. the view is removed from the presenter's visible identity registry immediately;
2. its collection trigger is disabled;
3. optional feedback runs against the already-committed result;
4. the GameObject is destroyed after feedback completion.

A presentation exception cannot rewrite an accepted collection as rejected. It records
a presentation diagnostic and completes retirement immediately. A rejected authority
result starts no feedback and changes no presentation ownership.

## Runtime and assembly boundaries

`ShooterMover.RunPickupUnity` defines narrow presentation lifecycle interfaces and a
generic transform-based fallback. `ShooterMover.UI.StrongboxOpening` optionally adapts
canonical snapshots into `LootPickupVisual2D`. The runtime assembly does not reference
the UI assembly, preventing an assembly cycle.

## Production composition status

The generic fly/fade feedback is automatically available to every canonical
`RunRewardPickup2D` created by `RunPickupPresenter2D`.

The richer credits, scrap, and strongbox visual requires a presentation prefab carrying
`LootPickupRunView2D`. The current live production registry composition was not
located confidently in this connector pass, so no retired Stage 1 bootstrap was recreated
or guessed. Unsupported or unadapted content retains the existing registry sprite and
generic feedback.

Enemy reward generation remains outside this lane. The JSON-room enemy spawner still
uses unconnected reward ports and must be connected in a separate authority-focused
change.

## Validation authored

- accepted collection removes the pickup from the visible registry immediately;
- the GameObject remains alive while feedback is pending;
- repeated trigger callbacks do not duplicate collection;
- destruction occurs after feedback completion;
- a rich money-pickup projection retains exact identity and quantity;
- feedback failure preserves the accepted canonical result;
- presenter teardown owns visible and retiring views;
- generated sprite caches reset at Unity subsystem registration and discard destroyed
  cached sprites.

## Validation not executed

- Unity `6000.3.19f1` import and compilation;
- PlayMode test execution;
- manual gameplay acceptance;
- production prefab/registry authoring;
- JSON-room enemy drop generation.

## Exact manual Unity acceptance route

1. Check out the full stack through this branch in Unity `6000.3.19f1`.
2. Confirm `ShooterMover.RunPickupUnity`, `ShooterMover.UI.StrongboxOpening`, and the
   RunPickups PlayMode test assembly compile.
3. Run `RunPickupPresentationPlayModeTests`.
4. Run `LootPickupRunViewPlayModeTests`.
5. In a scene with a configured canonical pickup presenter and player collector, collect
   one available pickup.
6. Confirm the authoritative available count changes immediately and exactly once.
7. Confirm the physical object moves and fades toward the player for approximately
   0.24 seconds before destruction.
8. Trigger collection repeatedly during that interval and confirm no duplicate Run
   Session record or reward occurs.
9. Reject a collection and confirm the pickup stays available, collidable, and visible.
10. Exit and re-enter Play Mode twice with domain reload disabled and confirm generated
    sprites remain valid.
