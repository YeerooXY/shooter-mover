# PLAYER-VITALS-LIVE-001 — authored-level player vitals

## Branch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact starting SHA: `6ddc52244235861df54a88e3c98694c6d4570388`
- Task branch: `agent/player-vitals-live-001`
- Pull-request target: `agent/first-combat-room-001-integration`
- Draft only; never target `main`, merge, or enable auto-merge from this task branch.

## Health authority

The authored gameplay player reuses the retained engine-neutral
`ShooterMover.GameplayEntities.PlayerActorAuthority` as the sole authority for:

- current and maximum health;
- alive/dead lifecycle state;
- lifecycle generation;
- accepted damage sequence;
- exact damage replay;
- conflicting operation rejection;
- immutable lethal `GameplayEntityDeathFact` production.

`PlayablePlayerVitals2D` is only a run-local Unity presentation/flow adapter around that
existing authority. It does not store a second mutable health value and does not touch
persistent character holdings, equipment, loadout, money, XP, skills, strongboxes, or
progression.

No ready production projection currently exposes the selected character's derived
`combat.maximum-health` value to the new authored-level controller. This task therefore
uses a clearly provisional run-local maximum health of `100`. It is not final balance and
can be replaced by a future authored-stat adapter without changing the neutral damage
receiver contract.

## Neutral damage receiver

Integration consumes `IPlayablePlayerDamageReceiverV1`, which extends the existing
package-neutral `IDamageReceiver` boundary. Enemy contacts can provide a normal
`DamageReceiverCommand` containing:

- stable event/operation identity;
- stable source actor identity;
- exact target actor identity;
- positive damage amount;
- canonical combat channel;
- target lifecycle generation.

The adapter delegates the command unchanged to `PlayerActorAuthority`.

Replay behavior remains canonical:

- first valid command: `Applied`;
- exact retained replay: `Duplicate`, no second health mutation or defeat fact;
- conflicting reuse of the same event identity: `RejectedInvalid` with
  `ConflictingDuplicate`;
- stale/future generation or post-death commands: rejected by lifecycle;
- multiple colliders reporting the same exact contact cannot apply damage twice when they
  retain the same event identity.

## Lifecycle binding

`PlayablePlayerVitalsInstallerV1` installs only for
`ProductionPlayableLevelCatalogV1.PlayableLevelScenePath` and attaches once to the
existing `ProductionPlayableLevelControllerV1` object. At `Start`, after the production
controller has spawned the selected-character player, it:

1. resolves the existing `PlayablePlayerMarker2D` under that controller;
2. reuses its exact character, route, holdings, and loadout references;
3. binds the existing `Rigidbody2D` and `PlayableTopDownMovement2D`;
4. creates one fresh run-local `PlayerActorAuthority` for that scene entry;
5. refuses duplicate binding rather than replacing the authority.

There is no per-frame global player search, no scene/prefab/name branch, and no edit to the
D-owned `PlayableLevel.unity` or `ProductionPlayableLevelControllerV1.cs`.

## HUD and hit feedback

The adapter projects the authority snapshot through a small runtime HUD showing:

- current health;
- maximum health;
- a normalized health bar.

Every newly accepted damage result gives the existing player sprite a short white hit
flash. Duplicate or rejected commands produce no second flash.

## Exactly-once defeat flow

When the authority emits its first lethal death fact, the adapter:

1. accepts defeat once;
2. disables `PlayableTopDownMovement2D`;
3. zeroes `Rigidbody2D.linearVelocity` and angular velocity;
4. publishes one neutral `PlayablePlayerDefeatedFactV1` event for integration to disable
   the Task A weapon bridge;
5. verifies that selected character ID, class ID, route payload, exact holdings authority,
   and exact loadout authority are unchanged;
6. requests one Hub return through the existing
   `ProductionFlowCoordinatorV1.Transitions.TryReturnToHub(...)` boundary.

Repeated lethal facts or exact lethal command replays cannot request another transition.
The flow receives the existing route payload, so failure does not replace or mutate the
selected character's persistent authorities.

Destroying the gameplay player destroys the run-local authority adapter, HUD projection,
and defeat subscriptions. Re-entering the level creates a fresh health lifecycle rather
than a persistent replacement character authority.

## Files changed

- `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs`
- `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs.meta`
- `Assets/ShooterMover/UI/ProductionFlow/ShooterMover.UI.ProductionFlow.asmdef`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow.meta`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/PlayablePlayerVitalsV1Tests.cs`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/PlayablePlayerVitalsV1Tests.cs.meta`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/ShooterMover.Tests.EditMode.ProductionFlow.asmdef`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/ShooterMover.Tests.EditMode.ProductionFlow.asmdef.meta`
- `docs/verification/PLAYER_VITALS_LIVE_001.md`

## Validation authored

Focused EditMode coverage asserts:

- valid damage changes health once;
- exact replay returns `Duplicate`;
- conflicting event-ID reuse returns `ConflictingDuplicate` without mutation;
- provisional maximum health is projected as `100`;
- exact holdings and loadout marker references are not replaced;
- lethal damage disables movement and zeroes velocity;
- lethal replay raises one defeat event only;
- duplicate runtime binding is rejected without replacing the authority.

Suggested Unity command using the repository's Unity `6000.3.19f1` baseline:

```text
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.ProductionFlow.PlayablePlayerVitalsV1Tests \
  -testResults Temp/player-vitals-live-001-editmode.xml \
  -logFile Temp/player-vitals-live-001-editmode.log
```

## Validation performed in this environment

Static inspection performed:

- confirmed the task branch started at the required exact SHA;
- confirmed the retained `PlayerActorAuthority` already owns the required health,
  replay, conflict, death, and lifecycle semantics;
- confirmed the selected-character marker retains exact holdings/loadout references;
- confirmed defeat uses the existing production Hub transition;
- confirmed no Task A weapon type or Task B projectile type is referenced;
- confirmed no scene, gameplay controller, inventory, reward, XP, or room-clear authority
  file is changed.

## Validation not performed in this environment

This connected environment has no Unity Editor or runnable repository checkout. Therefore:

- Unity script import/compilation was not run;
- EditMode XML was not produced;
- PlayMode validation was not run;
- manual authored-level entry, HUD, hit flash, defeat, Hub return, and persistent
  character/loadout verification were not performed.

No passing Unity or manual result is claimed.
