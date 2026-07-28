# RUN-REWARD-COMPOSITION-001 change contract

Starting `main` SHA: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`

## Requested visible behavior

Entering the authored Level 1 production route creates one selected-character Run Session. The authored `run-reward-proof` enemy routes its canonical death through one typed downstream fan-out, admits one deterministic pending reward batch, and shows an authoritative development HUD:

```text
Pending rewards:
Cash: 1
Scrap: 1
Strongboxes: 1
```

The display is derived from pending-admission records. It does not infer success from destroyed scene objects, room state, logs, or presentation objects.

## Authorities

- selected character, owned equipment, loadout, wallets, holdings and strongboxes: existing account-backed `ProductionCharacterRuntimeGraphV1` authorities;
- transient run identity, frozen selected-character inputs, participant roster, reward environment, pacing state and personal-delivery outbox: one `RunSessionAuthorityV1` aggregate owned by the gameplay scene;
- room terminal state, room clear and door state: existing `RoomRuntimeComposition2D` route through `RoomEnemySpawner2D.EnemyRoomPort`;
- enemy terminal fact: existing `EnemyPlacementRuntimeInstanceV1` canonical death publication;
- reward profile resolution and generation: existing production override catalogue and terminal personal-reward authority;
- deterministic proof behavior: one placement-only overlay over the canonical production override result;
- pending admission and replay: one scene-local `PendingTerminalDropAdmissionAuthorityV1`;
- HUD: read-only projection over admitted pending records;
- XP and kill statistics: explicit typed no-op consumers until their dedicated lanes integrate.

## Synchronous production composition

`ProductionRunRewardSceneCompositionV1` is serialized on `PlayableLevel.unity`. Its `Awake()` subscribes to `JsonRoomRuntimeBootstrap2D.BuildAccepted` before the production controller starts the selected route.

The exact ordering is:

```text
ProductionPlayableLevelControllerV1.Begin(...)
-> JsonRoomRuntimeBootstrap2D.BuildFromJson()
-> room authority and imported bundle commit
-> synchronous BuildAccepted callback
-> selected-character Run Session starts
-> exact run/downstream ports freeze on RoomEnemySpawner2D
-> BuildFromJson() returns
-> controller performs its first enemy synchronization
```

A missing scene composition, rejected Run Session, mismatched selected character, unsupported play mode, missing proof identity or downstream configuration throws before the controller can continue to enemy binding. The controller's existing failure route returns to the Hub.

## Frozen reward context

At Run Session start the composition freezes:

- selected character identity, revision and fingerprint;
- derived character level and combat profile;
- exact equipment snapshot;
- Level 1 mission identity;
- `difficulty.normal`;
- the Solo route mapped explicitly to `game-mode.campaign`;
- empty active-event modifiers;
- 1.0 money and scrap multipliers;
- the production default pacing policy.

Terminal reward generation reads the immutable `ProgressionContext` captured at Run Session start. It does not read `graph.ExperienceAuthority.CurrentContext` after the run has started.

`RunSessionParticipantDropPacingStateStoreV1` and `RunSessionPersonalRewardDeliveryOutboxV1` keep pacing and delivery state on the exact run lifecycle. Reconstructing a reward service for the same run therefore cannot silently reset participant luck or discard pending delivery state.

## Stable identities

Run and start-operation identities are fresh GUID-backed `StableId` values per scene entry.

The proof enemy has the explicit authored ID `run-reward-proof`. Its imported instance identity is derived from room, section and authored ID rather than display text, hierarchy, array order, path, coordinates or rotation. Composition fails closed unless exactly one imported enemy in `room.level1-entry` carries that authored ID.

Each reward transaction is keyed by the canonical enemy death-event identity. A successful transaction retains its exact generation consumer and replay ledger for that death event. A failed transaction discards that attempt-owned ledger before retry.

## Reward transaction commit, compensation and retry

The Level 1 composition explicitly opts into strict terminal-reward publication. Other callers of `TerminalDropBindingCompositionV1.Create(...)` retain the previous lenient behavior unless they also opt in.

For every enemy reward delivery in this production route:

1. export the complete pre-attempt `RunRewardRuntimeSnapshotV1`;
2. create a fresh personal-generation authority backed by the exact Run Session pacing store and delivery outbox;
3. generate the participant batch;
4. admit every generated participant result to pending state;
5. validate exact operation and batch-fingerprint correspondence for every result/admission pair;
6. validate the proof enemy's exact Cash 1, Scrap 1 and Strongboxes 1 contract when applicable;
7. publish accepted operation IDs to the HUD projection;
8. retain the successful generation consumer for exact replay.

`NoEligibleParticipants` is the only accepted empty batch. `Generated` and `ExplicitNoDrop` batches contain participant results and require exactly one accepted or exact-replay pending admission for each result. Generation rejection, missing results, rejected admission, conflicting operation reuse, mismatched identity or malformed proof totals throw and keep the enemy terminal transition retryable.

If any step before commit fails:

- HUD operation IDs added by that attempt are removed;
- only pending records created by an exact `Accepted` receipt from that attempt are compensated;
- `ExactReplay` receipts are never eligible to delete earlier committed pending state;
- the complete Run Session reward snapshot is restored, including participants, pacing state, personal-delivery outbox and reward environment;
- the failed attempt's generation/replay authority is discarded;
- the original exception is preserved unless compensation itself is incomplete, in which case both failures are reported.

Repeated compensation of the same exact accepted receipt is idempotent. A conflicting compensation receipt fails closed.

The enemy runtime publishes room terminal state before the reward consumer in its existing terminal fan-out. A reward failure therefore does not roll the room authority backward. Instead, the exact enemy terminal transition remains pending and retry replays the already accepted room report idempotently before retrying reward publication. This PR does not claim atomic rollback across room and reward authorities.

The HUD stores only committed operation IDs and re-reads every authoritative pending record before projecting totals. Repeated delivery cannot inflate the projection.

Enemy binding remains transactional in `RoomEnemySpawner2D`: temporary bindings commit only after the complete room batch binds, and failure rolls back newly bound actors. Run/downstream configuration is immutable after the first accepted configuration. A second writer or post-bind reconfiguration rejects.

Scene teardown discards the transient Run Session and pending authority. Re-entry creates fresh run, operation, actor and participant identities. No permanent rollback path is introduced because this slice does not mutate permanent character state.

## Truthful level availability

`ProductionPlayableLevelCatalogV1` now exposes only Level 1 through the production catalogue. The stable `AuthoredCombatLoopTestLevelStableId` constant remains available to the separate level/compiler lane, but the missing Combat Loop Test resource is no longer advertised as an unlocked live route and does not resolve through the production catalogue.

## Targeted self-audit repairs

The branch includes these repairs identified by repeated hostile audit:

1. composition moved from a late `Start()` hook to the synchronous accepted-room-build boundary;
2. the invalid `FrozenInputs.Character.CharacterLevel` read now uses authoritative frozen derived stats;
3. progression is captured once instead of read live during reward generation;
4. pacing and personal-delivery state are run-owned;
5. canonical production overrides are preserved beneath the proof placement overlay;
6. strict generation/admission validation applies to every enemy reward delivery in this production route, not only the proof placement;
7. failed reward attempts compensate pending, HUD, pacing and outbox state before exact retry;
8. failed attempts discard their internal replay authority while successful attempts retain it;
9. Solo play mode maps explicitly to Campaign reward mode;
10. proof identity is explicitly authored and coordinate-independent;
11. unavailable Combat Loop Test content is removed from production selection/resolution;
12. focused EditMode tests cover synchronous composition ordering, duplicate-writer rejection, strict publication failure/retry, exact pending compensation, replay-protection and truthful level availability.

## Boundaries

- runtime only; no `UnityEditor` dependency in production code;
- no persistence schema or save-adapter changes;
- no physical pickups, payout, Results, Skills, strongbox opening or prop-reward scene composition;
- no permanent wallet, scrap, holdings or unopened-strongbox mutation;
- no fallback character, weapon, reward, catalogue entry or damage caller;
- scene objects remain projections over room, enemy and Run Session authorities.

## Validation boundary and known dependency

The exact starting SHA does not contain `ProductionPlayablePlayerWeaponControllerV1` or another production player-to-enemy damage caller. This branch intentionally does not recreate either system. The separate combat stack must supply the canonical player-to-enemy damage route before the pending reward can be reached through ordinary gameplay.

Level 1's current doors are authored `always` open, so it does not visually prove a room-clear-gated unlock. The shared room-terminal path remains unchanged, but full kill-to-room-gate acceptance must run after the existing combat integration reaches this branch or `main`.

The Run Session mission-results port remains unsupported. This slice does not end the run, present Results, transfer pending rewards into permanent account authorities, award XP, update kill statistics or open strongboxes.

Unity compilation/import, EditMode execution, PlayMode execution and manual gameplay have not been performed in this connector-only environment. The authored tests are evidence targets, not passing-test claims.

## Manual Unity acceptance route

Available immediately on this branch:

1. Open the project and allow Unity to import and compile.
2. From the Hub, retain an exact selected production character.
3. Open Play, choose Solo, and confirm only `LEVEL 1` is offered by the production catalogue.
4. Enter `PlayableLevel.unity` and confirm the level remains loaded instead of immediately returning to Hub.
5. Confirm one Run Session composition exists and the entry enemy binds once.
6. Return and re-enter; confirm no duplicate `ProductionRunRewardSceneCompositionV1`, stale run or duplicate enemy binding.

After the canonical combat caller is integrated:

1. Kill the authored `run-reward-proof` enemy through the canonical damage/terminal route.
2. Observe one accepted pending admission and HUD totals Cash 1, Scrap 1, Strongboxes 1.
3. Confirm permanent money, scrap, holdings and unopened strongboxes do not change.
4. Replay the exact enemy terminal delivery and confirm pending count and HUD totals do not increase.
5. Inject one reward-generation or pending-admission rejection; confirm no pending record, HUD operation, pacing change or delivery-outbox entry survives the failed attempt.
6. Retry the exact lethal damage operation; confirm one reward commits and the room report replays without duplication.
7. Exercise a room-complete-gated combat room after that level exists and confirm the existing room-clear/door route still commits once.
8. Return and re-enter; confirm fresh run identity and no stale pending projection.
