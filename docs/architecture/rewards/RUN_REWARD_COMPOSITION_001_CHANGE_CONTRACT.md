# RUN-REWARD-COMPOSITION-001 change contract

Starting `main` SHA: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`

## Requested visible behavior

Entering the authored Level 1 production route creates one selected-character Run Session. Killing the single authored entry-room proof enemy routes its canonical death through one typed downstream fan-out, admits one deterministic pending reward batch, and shows an authoritative development HUD:

```text
Pending rewards:
Cash: 1
Scrap: 1
Strongboxes: 1
```

The display is derived from pending-admission records. It does not infer success from destroyed scene objects, room state, logs, or presentation objects.

## Authorities

- selected character, owned equipment, loadout, wallets, holdings and strongboxes: existing account-backed `ProductionCharacterRuntimeGraphV1` authorities;
- transient run identity, frozen selected-character inputs and participant roster: one `RunSessionAuthorityV1` aggregate owned by the gameplay-scene composition;
- room terminal state, room clear and door unlock: existing `RoomRuntimeComposition2D` route through `RoomEnemySpawner2D.EnemyRoomPort`;
- enemy terminal fact: existing `EnemyPlacementRuntimeInstanceV1` canonical death publication;
- reward generation: existing terminal personal-reward authority;
- pending admission and replay: one scene-local `PendingTerminalDropAdmissionAuthorityV1`;
- HUD: read-only projection over admitted pending records;
- XP and kill statistics: explicit typed no-op consumers until their dedicated lanes integrate.

## Expected files and systems

- `RoomEnemySpawner2D`: accept exactly one frozen run/downstream composition and use it when constructing enemy runtimes;
- production playable-level composition: start the selected-character Run Session, construct terminal-drop adapters and project pending admissions;
- `ShooterMover.UI.ProductionFlow.asmdef`: reference the existing enemy-runtime and terminal-drop assemblies;
- focused EditMode regression coverage for duplicate configuration and pending projection replay when practical.

## Forbidden scope

No physical pickups, wallet/scrap/holdings/strongbox mutation, payout, Results, Skills, strongbox opening, weapon firing, prop rewards, economy modifiers, fallback character/weapon/reward/catalog entries, or final balance work.

## Stable identities

Run and operation identities are fresh GUID-backed `StableId` values per scene entry. The proof source is the exact imported placement stable ID and room stable ID from the accepted authored bundle. No identity depends on display text, hierarchy, slot index, path, coordinates, or list position. Composition fails closed unless the expected proof room contains exactly one imported enemy placement.

## Commit, rollback and retry

The pending-admission authority is the reward transaction commit point. Generation rejection produces no pending entry. Accepted admission adds one operation-keyed record. Exact replay returns the existing record and cannot add another. Conflicting operation reuse is rejected. The HUD stores only observed operation IDs and re-reads each authoritative pending record before projecting it.

Enemy binding remains transactional in `RoomEnemySpawner2D`: temporary bindings are committed only after the full room batch binds; failure rolls back newly bound actors. Run/downstream configuration is immutable after the first synchronization attempt. Missing, stale or duplicate composition fails closed.

Scene teardown discards the transient Run Session and pending authority. Re-entry creates fresh run, operation and actor identities. No permanent rollback is required because this slice has no permanent mutation path.

## Boundaries

- runtime only; no `UnityEditor` dependency;
- no persistence schema or save adapter changes;
- no generated gameplay assets;
- UI assembly consumes existing Application, Domain, UnityAdapters, EnemyRuntimeComposition and TerminalDropBinding assemblies;
- scene objects remain projections over room/enemy/run authorities.

## Manual Unity acceptance route

1. From the Hub, keep an exact selected production character.
2. Open Play, select `LEVEL 1`, and enter `PlayableLevel.unity` through the production route.
3. Confirm one run composition is active and the entry-room enemy is bound.
4. Kill the single authored entry-room proof enemy through the available canonical damage/terminal route.
5. Observe one pending admission and the HUD values Cash 1, Scrap 1, Strongboxes 1.
6. Confirm money, scrap, holdings and unopened strongboxes did not change.
7. Confirm normal room clear and door unlock.
8. Replay the exact enemy terminal delivery and confirm the pending count/HUD do not increase.
9. Return and re-enter; confirm a fresh run identity and no duplicate composition component.