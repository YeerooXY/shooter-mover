# PLAYER-VITALS-LIVE-001 — authored-level player vitals

## Branch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact starting SHA: `6ddc52244235861df54a88e3c98694c6d4570388`
- Task branch: `agent/player-vitals-live-001`
- Pull-request target: `agent/first-combat-room-001-integration`
- Draft only; never target `main`, merge, or enable auto-merge from this task branch.

The target branch was refreshed during the final audit. At that point it was eight commits
ahead of the shared starting SHA, with changes confined to room-door content and
presentation files. No player-vitals file overlapped, and no merge or rebase was performed.

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

`PlayablePlayerVitals2D` is only a scene-local Unity presentation/flow adapter around that
existing authority. It does not store a second mutable health value and does not touch
persistent character holdings, equipment, loadout, money, XP, skills, strongboxes, or
progression.

No ready production projection currently exposes the selected character's derived
`combat.maximum-health` value to the authored-level controller. This task therefore uses a
clearly provisional run-local maximum health of `100`. It is not final balance.

## Run-local lifecycle identity

`CharacterInstanceStableId` remains the persistent selected-character ownership identity. It
is not used as the canonical damage actor identity.

Every successful `PlayablePlayerVitals2D.Bind(...)` creates one new run-entry token and
uses it in both canonical identities:

- `actor.playable-level-<run-entry-token>`;
- `participant.playable-level-<run-entry-token>`.

The compact token-only values stay below `StableId.MaxValueLength`; the selected character
remains canonically represented by `GameplayEntityOwnership.SourceCharacterId`.

The authority starts at lifecycle generation `0`, but the `(actor identity, generation)`
pair is different for every level entry. Destroying and recreating vitals for the same
character therefore creates a new actor and participant rather than a fresh replay table
behind the previous externally visible identity.

A delayed command captured from an earlier entry retains the earlier actor in
`TargetActorId`. The new authority rejects it with `TargetMismatch`; health and accepted
sequence remain unchanged.

## Neutral external-contact mapping

Integration consumes `IPlayablePlayerDamageReceiverV1`, which extends the existing
package-neutral `IDamageReceiver` boundary.

An external character-target contact identifies the marker's persistent
`CharacterInstanceStableId`. That value is **not** the canonical damage actor identity, and
must never be assigned directly to `DamageReceiverCommand.TargetActorId`.

`PlayablePlayerDamageCommandFactoryV1.TryCreateForCharacterContact(...)` supplies the narrow,
external-type-independent mapping seam:

1. validate the external target against `receiver.CharacterInstanceStableId`;
2. preserve the external event identity as `DamageReceiverCommand.EventId`;
3. preserve the source actor as `SourceActorId`;
4. preserve the source run participant as `SourceRunParticipantId`;
5. use `receiver.Identity.EntityInstanceId` as `TargetActorId`;
6. preserve exact damage and the canonical combat channel;
7. use `receiver.LifecycleGeneration` as the target lifecycle generation.

The integration branch must connect its neutral external hit fact through this factory. A
focused integration test must verify one accepted hit, exact replay with no second mutation,
and rejection of a contact carrying another character identity.

Replay behavior after command construction remains canonical:

- first valid command: `Applied`;
- exact retained replay: `Duplicate`, no second health mutation or defeat fact;
- conflicting reuse of the same event identity: `RejectedInvalid / ConflictingDuplicate`;
- stale/future generation or post-death commands: rejected by lifecycle;
- a command from a previous level entry: `RejectedInvalid / TargetMismatch` because its
  run-local target actor no longer exists.

## Lifecycle binding

`PlayablePlayerVitalsInstallerV1` installs only for
`ProductionPlayableLevelCatalogV1.PlayableLevelScenePath` and attaches once to the existing
`ProductionPlayableLevelControllerV1` object. At `Start`, after the production controller
has spawned the selected-character player, it:

1. resolves the existing `PlayablePlayerMarker2D` under that controller;
2. reuses its exact character, route, holdings, and loadout references;
3. binds the existing `Rigidbody2D` and `PlayableTopDownMovement2D`;
4. creates one fresh run-local `PlayerActorAuthority` and unique actor/participant identity;
5. binds the production Hub-return request adapter;
6. refuses duplicate binding rather than replacing the authority.

There is no per-frame global player search and no edit to the D-owned
`PlayableLevel.unity` or `ProductionPlayableLevelControllerV1.cs`.

## HUD and hit feedback

The HUD reads one immutable `PlayerActorSnapshot` from the canonical authority and projects
current health, maximum health, and a normalized health bar. Every newly accepted damage
result gives the existing player sprite a short white hit flash. Duplicate or rejected
commands produce no second flash.

## Exactly-once defeat and retryable safe return

When the authority emits its first lethal death fact, the adapter:

1. accepts defeat once;
2. disables `PlayableTopDownMovement2D`;
3. zeroes Rigidbody2D linear and angular velocity;
4. publishes one neutral `PlayablePlayerDefeatedFactV1`;
5. begins the retryable Hub-return path.

`ProductionPlayablePlayerHubReturnRequestV1` resolves the current production graph and
profile on every attempt. `PlayablePlayerHubReturnAuthorityGuardV1` verifies exact selected
character, class, graph/profile route, holdings reference, and loadout reference without
mutating them.

The retry coordinator obeys these rules:

- rejected context, guard, exception, or transition attempts do not close the latch;
- the first attempt happens immediately after defeat;
- `Update()` retries after `0.25` unscaled seconds while defeated and not accepted;
- `TryRetryHubReturn()` exposes the same immediate retry boundary for deterministic tests;
- only `TryReturnToHub(...) == true` marks the return accepted;
- after acceptance, Update/retry/replay processing cannot request another transition;
- permanent authority mismatch fails closed with a diagnostic and no false success.

## Observer exception policy

Defeat observers are invoked individually. Ordinary observer exceptions are logged and
later observers continue. `OutOfMemoryException`, `StackOverflowException`, and
`AccessViolationException` are rethrown.

## Files changed

- `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs`
- `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs.meta`
- `Assets/ShooterMover/UI/ProductionFlow/ShooterMover.UI.ProductionFlow.asmdef`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow.meta`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/PlayablePlayerVitalsV1Tests.cs`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/PlayablePlayerVitalsV1Tests.cs.meta`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/ShooterMover.Tests.EditMode.ProductionFlow.asmdef`
- `Assets/ShooterMover/Tests/EditMode/ProductionFlow/ShooterMover.Tests.EditMode.ProductionFlow.asmdef.meta`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow.meta`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/PlayablePlayerVitalsRetryPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/PlayablePlayerVitalsRetryPlayModeTests.cs.meta`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/PlayablePlayerVitalsAcceptedFeedbackPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/PlayablePlayerVitalsAcceptedFeedbackPlayModeTests.cs.meta`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/ShooterMover.Tests.PlayMode.ProductionFlow.asmdef`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/ShooterMover.Tests.PlayMode.ProductionFlow.asmdef.meta`
- `docs/verification/PLAYER_VITALS_LIVE_001.md`

## Validation authored

Focused EditMode coverage asserts:

- valid damage, exact replay, and conflicting duplicate semantics;
- provisional maximum health `100`;
- exact holdings/loadout reference preservation;
- same-character re-entry creates a different actor identity;
- replaying the old entry's command is rejected with `TargetMismatch`, no health mutation,
  and no accepted sequence;
- character-target contact mapping produces the current actor target and generation;
- mismatched character contact cannot produce a command;
- lethal movement shutdown and one defeat fact;
- rejected Hub return can later be accepted and accepted return cannot duplicate;
- authority mismatch cannot report success or mutate character references;
- ordinary observer isolation and fatal observer propagation;
- duplicate runtime binding rejection.

Focused PlayMode coverage is authored for:

- the real MonoBehaviour `Update()` timer: first immediate attempt rejects, automatic retry
  accepts, and later frames cannot transition twice;
- `PlayablePlayerVitalsInstallerV1.Start()`: an already-spawned player child is discovered,
  bound once, and retains the same authority/component on later frames;
- accepted-only presentation: one accepted non-lethal command changes health and starts the
  white hit flash, while exact replay and conflicting identity reuse produce no second health
  mutation and no second flash.

Suggested Unity commands using the repository's Unity `6000.3.19f1` baseline:

```text
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.ProductionFlow.PlayablePlayerVitalsV1Tests \
  -testResults Temp/player-vitals-live-001-editmode.xml \
  -logFile Temp/player-vitals-live-001-editmode.log
```

```text
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform PlayMode \
  -testFilter ShooterMover.Tests.PlayMode.ProductionFlow \
  -testResults Temp/player-vitals-live-001-playmode.xml \
  -logFile Temp/player-vitals-live-001-playmode.log
```

## Validation performed in this environment

Static inspection performed:

- required exact merge base retained;
- current target-branch changes do not overlap player-vitals files;
- canonical `PlayerActorAuthority` still owns health/replay/death/lifecycle state;
- every binding creates a fresh run-local actor and participant identity within StableId
  limits;
- previous-entry commands retain the old target and fail against the new actor;
- contact-command factory validates character identity before projecting actor/generation;
- HUD reads the authority snapshot and hit feedback runs only for `Applied` results;
- accepted Hub-return latch is written only after a true transition result;
- authority guard is read-only and exact-reference based;
- fatal observer exceptions are rethrown;
- no concrete external projectile or hit class is referenced;
- no D-owned scene/controller, inventory, reward, XP, or room-clear authority file changed.

## Validation not performed in this environment

This connected environment has no Unity Editor or runnable repository checkout. Therefore:

- Unity asset import and C# compilation were not run;
- authored EditMode tests were not executed and no XML exists;
- authored PlayMode tests were not executed and no XML exists;
- the synthetic installer test does not replace loading the real production scene and
  observing the actual controller-spawned player sequence;
- the final neutral-contact-to-canonical-command integration test does not exist on this
  isolated task branch and must be implemented on the integration branch;
- manual authored-level repeated entry, delayed contact replay, HUD, defeat, rejected return
  recovery, and persistent character/loadout verification were not performed;
- no CI workflow or commit status exists for this head.

No passing Unity, CI, or manual result is claimed. Keep the PR draft until those paths are
genuinely validated.
