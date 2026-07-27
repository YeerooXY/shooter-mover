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

## Commit, rollback and retry

The pending-admission authority is the reward transaction commit point. Generation rejection creates no pending entry. Accepted admission creates one operation-keyed record. Exact replay returns the existing record; conflicting operation reuse rejects.

For the proof source, `RequiredProofPendingAdmissionEnemyConsumerV1` requires exactly one accepted admission containing exactly one cash, one scrap and one strongbox reward. Missing, rejected or malformed proof admission throws. The enemy runtime retains its exact pending terminal transition, so the same canonical death operation can retry without applying damage or generating another reward.

The HUD stores only observed operation IDs and re-reads every authoritative pending record before projecting totals. Repeated delivery cannot inflate the projection.

Enemy binding remains transactional in `RoomEnemySpawner2D`: temporary bindings commit only after the complete room batch binds, and failure rolls back newly bound actors. Run/downstream configuration is immutable after the first accepted configuration. A second writer or post-bind reconfiguration rejects.

Scene teardown discards the transient Run Session and pending authority. Re-entry creates fresh run, operation, actor and participant identities. No permanent rollback path is introduced because this slice does not mutate permanent character state.

## Targeted self-audit repairs

The branch includes these repairs identified by its hostile self-audit:

1. composition moved from a late `Start()` hook to the synchronous accepted-room-build boundary;
2. the invalid `FrozenInputs.Character.CharacterLevel` read now uses authoritative frozen derived stats;
3. progression is captured once instead of read live during reward generation;
4. pacing and personal-delivery state are run-owned;
5. canonical production overrides are preserved beneath the proof placement overlay;
6. failed proof admission throws and remains retryable;
7. Solo play mode maps explicitly to Campaign reward mode;
8. proof identity is explicitly authored and coordinate-independent;
9. focused EditMode tests cover synchronous callback ordering, fail-closed callback rejection and exact authored proof import.

## Boundaries

- runtime only; no `UnityEditor` dependency in production code;
- no persistence schema or save-adapter changes;
- no physical pickups, payout, Results, Skills, strongbox opening or prop rewards;
- no permanent wallet, scrap, holdings or unopened-strongbox mutation;
- no fallback character, weapon, reward, catalogue entry or damage caller;
- scene objects remain projections over room, enemy and Run Session authorities.

## Validation boundary and known dependency

The exact starting SHA does not contain `ProductionPlayablePlayerWeaponControllerV1` or another production player-to-enemy damage caller. The catalogue mentions `COMBAT LOOP TEST`, but its room resource is also absent at the exact starting SHA. This branch intentionally does not recreate either system.

Level 1's current doors are authored `always` open, so it does not visually prove a room-clear-gated unlock. The shared room-terminal path remains unchanged, but full kill-to-room-gate acceptance must run after the existing combat integration reaches this branch or `main`.

## Manual Unity acceptance route

Available immediately on this branch:

1. Open the project and allow Unity to import and compile.
2. From the Hub, retain an exact selected production character.
3. Open Play, choose Solo, select `LEVEL 1`, and enter `PlayableLevel.unity`.
4. Confirm the level remains loaded, one Run Session composition exists, and the entry enemy binds once instead of returning immediately to the Hub.
5. Confirm no duplicate `ProductionRunRewardSceneCompositionV1` or enemy bindings appear after re-entry.

After the canonical combat caller is integrated:

1. Kill the authored `run-reward-proof` enemy through the canonical damage/terminal route.
2. Observe one accepted pending admission and HUD totals Cash 1, Scrap 1, Strongboxes 1.
3. Confirm permanent money, scrap, holdings and unopened strongboxes do not change.
4. Replay the exact enemy terminal delivery and confirm pending count and HUD totals do not increase.
5. Exercise a room-complete-gated combat room and confirm the existing room-clear/door route still commits once.
6. Return and re-enter; confirm fresh run identity and no stale pending projection.
