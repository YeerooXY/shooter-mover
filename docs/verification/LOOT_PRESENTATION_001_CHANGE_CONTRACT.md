# LOOT-PRESENTATION-001 change contract

## Starting point

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/loot-presentation-001`
- Exact refreshed `main` SHA: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`
- Primary planning evidence:
  - `docs/verification/STRONGBOX_SYSTEM_AUDIT.md` from PR #341
  - `docs/architecture/rewards/DROP_SYSTEM_CURRENT_STATE_AND_FORWARD_PLAN.md` from PR #345

The audits are planning evidence. Current source remains authoritative.

## Requested visible behavior

Provide reusable visual presentation for:

- cash/credits, scrap/metal and tier-specific physical pickups;
- projection-only attraction and accepted-collection feedback;
- authoritative run-total display inputs;
- grouped owned strongboxes that preserve every exact instance identity;
- exact strongbox selection;
- closed -> opening -> reveal -> continue presentation;
- immutable money, scrap and equipment reward cards;
- a development-only showcase covering all production tiers, grouped quantities, Open 1/Open 5 layout states, one immutable sample result, reward variants and animation skip/fast-forward.

## Authority ledger

| Concept | Authoritative owner | This lane may do |
|---|---|---|
| Reward generation and tier choice | Existing reward-generation and strongbox policy authorities | Read immutable tier/result projections only |
| Run-local pickup availability and collection | `RunLocalPickupAuthorityV1` / canonical collection route | Render exported snapshots and retire views only after accepted collection state |
| Run totals | Existing Run Session collected-reward projection | Display supplied immutable totals |
| Owned unopened boxes | Existing player holdings authority | Group exact immutable instance projections; never mutate counts |
| Strongbox opening transaction | `StrongboxOpeningServiceV1` and durable executor | Submit one immutable command through existing port; animate committed result |
| Reward application and permanent holdings | Existing RAP/holdings/save authorities | Display immutable result cards only |
| Presentation timing and local selection | This lane | Own animation stage, speed, selected exact instance ID and purely visual state |
| Development fixture state | Development-only fixture | Own disposable immutable sample projections; never bind to production holdings |

## Expected files and systems

Expected ownership is limited to:

- `Assets/ShooterMover/UI/StrongboxOpening/` presentation models/controllers;
- `Assets/ShooterMover/Scenes/LootPresentation/` development showcase scene;
- focused existing StrongboxOpening PlayMode test assembly;
- authoring/verification documentation and required Unity `.meta` files.

## Forbidden files and systems

This lane must not modify:

- reward generation or reward-source catalogues;
- strongbox opening transaction internals;
- player holdings or reward application;
- save schema or migration;
- weapon/equipment catalogues;
- XP, skills or progression;
- shared gameplay composition, room-clear, selected-character or navigation authorities;
- Run Session mutation or final-exit payout;
- production scene composition.

## Stable identities

The following identities must survive grouping, selection, animation skip, presenter rebuild and replay:

- exact run-local pickup StableId;
- exact reward instance StableId;
- exact strongbox instance StableId;
- production strongbox tier StableId;
- exact equipment instance StableId;
- immutable opening/result identity supplied by the production transaction.

No identity may be inferred from display name, hierarchy, index, slot, path, world position or screen coordinates.

## Transaction and failure behavior

This lane has no durable commit point. Its only local commit is visual selection/animation state.

The production commit points remain unchanged:

- pickup collection truth: acceptance by the canonical pickup authority;
- opening truth: the existing strongbox opening/RAP/consumption transaction;
- permanent reward truth: existing RAP/holdings/save acceptance.

Required behavior:

| Condition | Result |
|---|---|
| Presenter or scene object is destroyed | Authoritative uncollected pickup remains available and can be reconstructed |
| Collection is rejected or throws | View remains available; no total changes |
| Accepted collection or exact accepted replay | Matching exact pickup view retires |
| Unknown tier or duplicate exact instance identity | Fail closed with a diagnostic; do not group or select a substitute |
| Group selection changes | Only the selected exact instance ID changes |
| Open 1/Open 5 layout changes | No holdings or quantity mutation |
| Skip/fast-forward | Same immutable result and item identities become visible sooner |
| Replay presentation | Reuses the same immutable result; no new opening call or reward |
| Preview interaction | Cannot enter production holdings and cannot invoke BOX/RAP/save |
| Cleanup failure after production commit | Presentation may report a visual diagnostic but cannot make the committed result appear uncommitted |

## Runtime, editor, persistence and assembly boundaries

- Runtime code may reference existing Application, Contracts, Domain, UnityAdapters and StrongboxOpening presentation assemblies only through existing public contracts.
- No `UnityEditor` dependency may enter runtime assemblies.
- No serialized save data or schema is added.
- No generated assets are required; the showcase scene and Unity metadata are tracked source assets.
- The development fixture must be visibly labelled preview-only and use disposable identities with a dedicated development namespace.

## Validation tools available

Available in this environment:

- GitHub source inspection and branch/file publication;
- static full-file review;
- caller/search review through repository history and known paths;
- structural checks over authored C# and Unity YAML text.

Unavailable in this environment:

- local repository checkout and network clone;
- `gh` CLI;
- Unity `6000.3.19f1` import/compilation;
- EditMode/PlayMode execution;
- manual gameplay acceptance;
- performance testing.

## Exact manual Unity acceptance route

1. Open Unity `6000.3.19f1` and import the branch.
2. Open `Assets/ShooterMover/Scenes/LootPresentation/LootPresentationShowcase.unity`.
3. Enter Play Mode.
4. Inspect cash, scrap and every production strongbox tier presentation.
5. Confirm grouped counts expose and retain exact selectable instance IDs.
6. Toggle Open 1/Open 5 layout states without changing grouped quantities.
7. Play one complete opening from the immutable fixture result.
8. replay, skip and fast-forward it; confirm all reward identities and quantities remain identical.
9. Exercise the authoritative pickup fixture: reject once, then accept collection; verify the view disappears only after acceptance.
10. rebuild the presenter before acceptance and confirm the same exact uncollected pickup reappears.
11. exit Play Mode and confirm no selected-character wallet, holdings, save or inventory state changed.
