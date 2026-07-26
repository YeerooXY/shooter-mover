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
5. binds the production Hub-return request adapter;
6. refuses duplicate binding rather than replacing the authority.

There is no per-frame global player search, no scene/prefab/name branch, and no edit to the
D-owned `PlayableLevel.unity` or `ProductionPlayableLevelControllerV1.cs`.

## HUD and hit feedback

The adapter projects the authority snapshot through a small runtime HUD showing:

- current health;
- maximum health;
- a normalized health bar.

Every newly accepted damage result gives the existing player sprite a short white hit
flash. Duplicate or rejected commands produce no second flash.

## Exactly-once defeat and retryable safe return

When the authority emits its first lethal death fact, the adapter:

1. accepts defeat once;
2. disables `PlayableTopDownMovement2D`;
3. zeroes `Rigidbody2D.linearVelocity` and angular velocity;
4. publishes one neutral `PlayablePlayerDefeatedFactV1` event for integration to disable
   the Task A weapon bridge;
5. begins the retryable Hub-return path.

`ProductionPlayablePlayerHubReturnRequestV1` resolves the current production graph and
profile on every attempt, then uses `PlayablePlayerHubReturnAuthorityGuardV1` to verify:

- selected character identity;
- class identity;
- graph route payload;
- profile route payload;
- exact holdings authority reference;
- exact loadout authority reference.

The guard is read-only. It cannot mutate character, holdings, loadout, money, or
progression state.

The retry coordinator obeys these rules:

- a rejected context lookup, authority guard, or transition request does **not** set the
  accepted latch;
- the first return attempt happens immediately after defeat;
- while defeated and not accepted, the scene-local adapter retries at a throttled
  deterministic interval;
- `TryRetryHubReturn()` exposes the same immediate retry boundary for focused validation;
- only `TryReturnToHub(...) == true` marks the return accepted;
- after acceptance, Update/retry/replay processing cannot call the transition again;
- a permanent authority mismatch fails closed with a diagnostic and never reports false
  success or substitutes another character authority.

This means a transient rejected transition can recover without relying on another damage
or death fact. Exact lethal replay still cannot duplicate defeat or an accepted Hub
transition.

## Observer exception policy

Defeat observers are invoked individually:

- ordinary observer exceptions are logged and later observers still run;
- `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException` are
  rethrown rather than converted into observer failures.

This matches the fatal-exception policy used by the surrounding production runtime.

Destroying the gameplay player destroys the run-local authority adapter, HUD projection,
return request binding, and defeat subscriptions. Re-entering the level creates a fresh
health lifecycle rather than a persistent replacement character authority.

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
- first Hub-return attempt may reject and a later retry may accept;
- repeated processing after acceptance cannot request a second transition;
- authority mismatch cannot report success, invoke the accepted transition, or mutate
  holdings/loadout/route references;
- ordinary defeat-observer failure does not block later observers or Hub return;
- fatal defeat-observer failure is rethrown;
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

- confirmed the task branch retains the required exact merge base;
- confirmed the retained `PlayerActorAuthority` still owns health, replay, conflict,
  death, and lifecycle semantics;
- confirmed the Hub-return accepted latch is written only after the retained transition
  returns true;
- confirmed rejected attempts remain eligible for deterministic retry independently of
  later damage/death facts;
- confirmed the authority guard is read-only and exact-reference based;
- confirmed fatal defeat-observer exceptions are rethrown;
- confirmed no Task A weapon type or Task B projectile type is referenced;
- confirmed no scene, gameplay controller, inventory, reward, XP, or room-clear authority
  file is changed.

## Validation not performed in this environment

This connected environment has no Unity Editor or runnable repository checkout. Therefore:

- Unity script import/compilation was not run;
- EditMode XML was not produced;
- PlayMode validation was not run;
- manual authored-level entry, HUD, hit flash, rejected-return recovery, defeat, Hub return,
  and persistent character/loadout verification were not performed.

No passing Unity or manual result is claimed.
