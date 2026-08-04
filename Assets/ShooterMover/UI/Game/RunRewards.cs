using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.LootDropBinding;
using ShooterMover.RunLoot;
using ShooterMover.UI.Game.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Rewards.RunLoots;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Definition-driven compact-enemy reward composition. Enemy JSON selects an existing
    /// production drop-source profile; accepted rewards become run-local physical pickups.
    /// Victory transfers collected rewards to the selected character, resolves exact
    /// Strongbox loot, saves it, and only then opens the Results flow.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class RunRewards : MonoBehaviour
    {
        public const int GenerationAlgorithmVersion = 1;
        private const string CashDropSkillId = "generic.cash_drop_size";

        private static RunRewards active;
        private static Texture2D moneyTexture;
        private static Texture2D scrapTexture;
        private static Texture2D strongboxTexture;
        private static Sprite moneySprite;
        private static Sprite scrapSprite;
        private static Sprite strongboxSprite;

        private RunSessionState runSessions;
        private RunSessionAggregate run;
        private CharacterLiveGraph characterGraph;
        private CharacterSetupFlow characterComposition;
        private LevelPorts levelPorts;
        private ProgressionContext frozenProgression;
        private LevelRooms rooms;
        private LootBridge pickupBridge;
        private LootDropGenerationState dropGeneration;
        private PendingLootDropAdmissionState pendingAdmission;
        private RunLootView pickupView;
        private StableId playerParticipantStableId;
        private StableId playerDamageSourceStableId;
        private EndRunSessionCommand victoryEndCommand;
        private RewardClaimPreparedTransfer victoryAwaitingTransfer;
        private RewardClaimPreparedTransfer victoryPreparedTransfer;
        private RewardClaimAtomicPlan victoryPlan;
        private RunSessionEndResult victoryEndResult;
        private RewardClaimTransferResult victoryTransferResult;
        private MissionResultPayload victoryMissionResult;
        private ResultsContext victoryResultsContext;
        private bool victoryAwaitingSaved;
        private bool victoryBlocked;
        private bool configured;
        private string diagnostic = "compact-enemy-rewards-not-configured";

        public bool IsConfigured { get { return configured; } }
        public string Diagnostic { get { return diagnostic; } }
        public StableId RunStableId { get { return run == null ? null : run.RunStableId; } }
        public int VisiblePickupCount
        {
            get { return pickupView == null ? 0 : pickupView.VisiblePickupCount; }
        }

        public void Configure(
            PlayableLevelDefinition level,
            StableId gameModeStableId,
            CharacterLiveGraph graph,
            CharacterSetupFlow composition,
            LevelRooms configuredRooms,
            PlayerMarker player)
        {
            if (configured)
                throw new InvalidOperationException(
                    "compact-enemy-rewards-duplicate-configuration");
            if (active != null && active != this)
                throw new InvalidOperationException(
                    "compact-enemy-rewards-active-instance-conflict");
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (gameModeStableId == null)
                throw new ArgumentNullException(nameof(gameModeStableId));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            rooms = configuredRooms ?? throw new ArgumentNullException(nameof(configuredRooms));
            if (player == null) throw new ArgumentNullException(nameof(player));
            characterGraph = graph;
            characterComposition = composition;
            RewardClaimLiveRegistry.BindRuntime(graph, composition);

            ProgressionContext currentProgression =
                graph.ExperienceAuthority.CurrentContext;
            if (currentProgression == null || currentProgression.CharacterLevel < 1)
                throw new InvalidOperationException(
                    "compact-enemy-reward-progression-context-missing");
            StableId difficultyId = currentProgression.DifficultyId;
            frozenProgression = ProgressionContext.Create(
                currentProgression.CharacterLevel,
                currentProgression.RegionLevel,
                difficultyId,
                currentProgression.DifficultyValue,
                currentProgression.ProgressionTags);

            string token = Guid.NewGuid().ToString("N");
            StableId runId = StableId.Create("run", "playable-level-" + token);
            long seed = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0)
                & long.MaxValue;
            levelPorts = new LevelPorts(rooms, graph);
            runSessions = new RunSessionState(
                new CharacterRunSessionStartSource(
                    composition,
                    new LevelStats(level, frozenProgression),
                    levelPorts));
            RunSessionStartResult start = runSessions.Start(
                new StartRunSessionCommand(
                    StableId.Create("operation", "start-playable-run-" + token),
                    runId,
                    "playable-level-run-v2|" + level.LevelStableId + "|"
                        + graph.Character.CharacterInstanceStableId + "|" + token,
                    graph.Character.CharacterInstanceStableId,
                    graph.Character.Revision,
                    graph.Character.Fingerprint,
                    level.LevelStableId,
                    difficultyId,
                    seed,
                    0L,
                    Hash(
                        "playable-level-event-context-v2|"
                        + level.LevelStableId + "|" + gameModeStableId + "|"
                        + frozenProgression.Fingerprint)));
            if (start == null
                || start.Status != RunSessionStartStatus.Started
                || !runSessions.TryGetRun(runId, out run)
                || run == null
                || run.LifecycleState != RunSessionLifecycleState.Active)
            {
                throw new InvalidOperationException(
                    "compact-enemy-reward-run-start-rejected:"
                    + (start == null ? "result-null" : start.RejectionCode));
            }
            levelPorts.BindRun(run);

            RankedSkillAllocationSnapshot allocation;
            if (!graph.SkillAuthority.TryGet(graph.SkillProfileId, out allocation)
                || allocation == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-reward-skill-context-missing");
            }
            run.ConfigureRewardEnvironment(new RunRewardEnvironmentSnapshot(
                gameModeStableId,
                Array.Empty<StableId>(),
                checked(1000 + allocation.RankOf(CashDropSkillId) * 1000),
                1000,
                RunDropPacingCatalog.Default));

            ConfigurePickupRuntime();
            ConfigureDropGeneration(graph);
            RunPlayerSnapshot playerSnapshot = run.RuntimePorts.Player.ExportSnapshot();
            playerParticipantStableId = playerSnapshot.ParticipantStableId;
            playerDamageSourceStableId = graph.Character.CharacterInstanceStableId;
            RunLootCollector collector = player.GetComponent<RunLootCollector>();
            if (collector == null)
                collector = player.gameObject.AddComponent<RunLootCollector>();
            collector.Configure(
                playerSnapshot.ActorInstanceStableId,
                playerSnapshot.ParticipantStableId);

            rooms.CurrentRoomPresentationRebuilt += SynchronizeCurrentRoom;
            active = this;
            configured = true;
            diagnostic = string.Empty;
            SynchronizeCurrentRoom();
        }

        /// <summary>
        /// Completes one victorious run. Collected rewards are transferred exactly once,
        /// every collected Strongbox stores its exact generated loot, and the character is
        /// saved before Results becomes visible.
        /// </summary>
        public bool TryFinishVictory()
        {
            if (victoryBlocked)
            {
                return false;
            }
            if (!configured
                || run == null
                || characterGraph == null
                || characterGraph.IsDisposed
                || characterComposition == null
                || levelPorts == null)
            {
                return RejectVictory(
                    "playable-run-victory-context-unavailable");
            }

            try
            {
                if (!TryTransferVictoryRewards())
                {
                    return false;
                }
                if (!VictoryStrongboxesArePrepared())
                {
                    return false;
                }

                if (victoryResultsContext == null)
                {
                    victoryResultsContext = new ResultsContext(
                        victoryMissionResult,
                        characterGraph.StrongboxAuthority,
                        CreateStrongboxOpenCommand,
                        characterGraph.LoadoutRuntime.EquipmentCatalog,
                        characterGraph.LoadoutRuntime.GunCatalog,
                        RefreshVictoryMissionResult);
                }
                if (!GameFlow.PresentResults(victoryResultsContext))
                {
                    return RejectVictory(
                        "playable-run-results-transition-rejected");
                }

                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                return RejectVictory(
                    "playable-run-victory-threw-"
                    + exception.GetType().Name.ToLowerInvariant());
            }
        }

        private bool TryTransferVictoryRewards()
        {
            if (victoryTransferResult != null
                && victoryTransferResult.Succeeded)
            {
                return true;
            }

            RewardApplicationActions rewardApplication;
            RewardClaimPreparedTransferStore preparedTransfers;
            RewardClaimTransferReceiptState receipts;
            if (!RewardClaimLiveRegistry.TryResolve(
                    characterGraph.Character.CharacterInstanceStableId,
                    out rewardApplication,
                    out preparedTransfers,
                    out receipts))
            {
                return RejectVictory(
                    "playable-run-reward-transfer-unavailable");
            }

            if (victoryEndCommand == null)
            {
                victoryEndCommand = new EndRunSessionCommand(
                    StableId.Create(
                        "operation",
                        "finish-playable-run-"
                        + Hash(
                            "finish-playable-run-v1|"
                            + run.RunStableId
                            + "|"
                            + run.LifecycleGeneration)
                            .Substring(0, 32)),
                    run.RunStableId,
                    run.LifecycleGeneration,
                    MissionRunCompletionState.Completed,
                    run.AuthoritativeTick);
            }

            var persistence = new RewardClaimPersistence(
                characterComposition,
                preparedTransfers,
                receipts,
                characterGraph.Character.CharacterInstanceStableId);

            if (victoryAwaitingTransfer == null)
            {
                string preparationDiagnostic;
                if (!RewardClaimTransferPreparationFactory
                    .TryCreateAwaitingAcceptedEnd(
                        victoryEndCommand,
                        run.ExportRewardClaims(),
                        characterGraph,
                        rewardApplication,
                        receipts,
                        preparedTransfers,
                        new RewardClaimGenerationContext(
                            checked((ulong)run.StartCommand.DeterministicSeed),
                            GenerationAlgorithmVersion,
                            frozenProgression,
                            run.StartCommand
                                .EventModifierContextFingerprint),
                        new RejectingCollectedRunEquipmentPayloadSource(),
                        out victoryAwaitingTransfer,
                        out preparationDiagnostic))
                {
                    return RejectVictory(
                        "playable-run-reward-preparation-rejected:"
                        + preparationDiagnostic);
                }
            }

            if (!victoryAwaitingSaved)
            {
                RewardClaimTransferPersistenceResult custody =
                    persistence.PersistPreparedCustody(
                        victoryAwaitingTransfer);
                if (custody == null || !custody.Succeeded)
                {
                    if (custody != null
                        && custody.DurableStateUncertain)
                    {
                        victoryBlocked = true;
                    }
                    RewardClaimResultsBridge.PublishPreparationFailure(
                        victoryAwaitingTransfer,
                        custody == null
                            ? "victory-custody-save-result-null"
                            : custody.Diagnostic);
                    return RejectVictory(
                        "playable-run-reward-custody-save-rejected:"
                        + (custody == null
                            ? "result-null"
                            : custody.Diagnostic));
                }
                victoryAwaitingSaved = true;
            }

            if (victoryEndResult == null)
            {
                victoryEndResult = run.End(victoryEndCommand);
            }
            if (victoryEndResult == null
                || !victoryEndResult.Succeeded
                || victoryEndResult.Receipt == null
                || victoryEndResult.Receipt.MissionResult == null)
            {
                return RejectVictory(
                    "playable-run-end-rejected:"
                    + (victoryEndResult == null
                        ? "result-null"
                        : victoryEndResult.RejectionCode));
            }

            if (victoryPreparedTransfer == null || victoryPlan == null)
            {
                string planDiagnostic;
                if (!RewardClaimTransferPreparationFactory
                    .TryAcceptEndAndBuildPlan(
                        victoryEndResult,
                        victoryAwaitingTransfer,
                        characterGraph,
                        rewardApplication,
                        out victoryPreparedTransfer,
                        out victoryPlan,
                        out planDiagnostic))
                {
                    RewardClaimResultsBridge.PublishPreparationFailure(
                        victoryAwaitingTransfer,
                        planDiagnostic);
                    return RejectVictory(
                        "playable-run-reward-plan-rejected:"
                        + planDiagnostic);
                }
            }

            var authority = new RewardClaimAtomicState(
                characterGraph,
                rewardApplication,
                preparedTransfers,
                receipts);
            victoryTransferResult = new RewardClaimTransferActions(
                victoryPlan,
                authority,
                persistence).Apply();
            if (victoryTransferResult == null)
            {
                return RejectVictory(
                    "playable-run-reward-transfer-rejected:result-null");
            }
            RewardClaimResultsBridge.Publish(
                victoryPreparedTransfer,
                victoryTransferResult);
            if (!victoryTransferResult.Succeeded)
            {
                if (victoryTransferResult != null
                    && (victoryTransferResult.Status
                            == RewardClaimTransferStatus
                                .FatalCompensationFailure
                        || victoryTransferResult.Persistence
                            .DurableStateUncertain))
                {
                    victoryBlocked = true;
                }
                return RejectVictory(
                    "playable-run-reward-transfer-rejected:"
                    + victoryTransferResult.Diagnostic);
            }

            victoryMissionResult =
                victoryEndResult.Receipt.MissionResult;
            return true;
        }

        private bool VictoryStrongboxesArePrepared()
        {
            if (victoryMissionResult == null)
            {
                return RejectVictory(
                    "playable-run-victory-result-missing");
            }

            StrongboxOpeningSnapshot snapshot = characterGraph
                .StrongboxAuthority.ExportSnapshot();
            for (int boxIndex = 0;
                 boxIndex < victoryMissionResult
                     .UnopenedStrongboxes.Count;
                 boxIndex++)
            {
                MissionRunStrongboxResult strongbox =
                    victoryMissionResult
                        .UnopenedStrongboxes[boxIndex];
                StrongboxOpenCommand expected =
                    CreateStrongboxOpenCommand(strongbox);
                StrongboxOpeningRecordSnapshot found = null;
                for (int openingIndex = 0;
                     openingIndex < snapshot.Openings.Count;
                     openingIndex++)
                {
                    StrongboxOpeningRecordSnapshot candidate =
                        snapshot.Openings[openingIndex];
                    if (candidate.Command.StrongboxInstanceStableId
                        != strongbox.InstanceStableId)
                    {
                        continue;
                    }
                    if (found != null)
                    {
                        return RejectVictory(
                            "playable-run-strongbox-preparation-duplicated:"
                            + strongbox.InstanceStableId);
                    }
                    found = candidate;
                }

                if (found == null
                    || found.Stage != StrongboxOpeningStage.Prepared
                    || found.GeneratedOutcome == null
                    || !found.Command.Equals(expected))
                {
                    return RejectVictory(
                        "playable-run-strongbox-preparation-missing-or-mismatched:"
                        + strongbox.InstanceStableId);
                }
            }
            return true;
        }

        private StrongboxOpenCommand CreateStrongboxOpenCommand(
            MissionRunStrongboxResult strongbox)
        {
            if (strongbox == null)
            {
                throw new ArgumentNullException(nameof(strongbox));
            }
            return StrongboxOpenCommand.CreateForCollectedRun(
                run.RunStableId,
                strongbox.InstanceStableId,
                characterGraph.Character.CharacterInstanceStableId,
                MoneyWalletIds.AuthorityStableId,
                characterGraph.ScrapWallet.AuthorityStableId,
                characterGraph.LoadoutRuntime.Holdings.AuthorityStableId);
        }

        private MissionResultPayload RefreshVictoryMissionResult()
        {
            victoryMissionResult = levelPorts.RefreshMissionResult(
                victoryMissionResult);
            return victoryMissionResult;
        }

        private bool RejectVictory(string code)
        {
            diagnostic = string.IsNullOrWhiteSpace(code)
                ? "playable-run-victory-rejected"
                : code;
            Debug.LogError(diagnostic, this);
            return false;
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        internal static void ReportCompactEnemyDefeat(
            CompactEnemy enemy,
            StableId triggeringEventStableId,
            StableId killerEntityStableId,
            StableId killerParticipantStableId,
            EnemyActorDeathCause cause,
            Vector2 position)
        {
            RunRewards runtime = active;
            if (runtime == null || !runtime.configured)
            {
                Debug.LogError("compact-enemy-reward-runtime-unavailable", enemy);
                return;
            }
            runtime.ReportDefeat(
                enemy,
                triggeringEventStableId,
                killerEntityStableId,
                killerParticipantStableId,
                cause,
                position);
        }

        private void ReportDefeat(
            CompactEnemy enemy,
            StableId triggeringEventStableId,
            StableId killerEntityStableId,
            StableId killerParticipantStableId,
            EnemyActorDeathCause cause,
            Vector2 position)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            if (triggeringEventStableId == null)
                throw new ArgumentNullException(nameof(triggeringEventStableId));
            if (run == null || run.LifecycleState == RunSessionLifecycleState.Ended)
                throw new InvalidOperationException(
                    "compact-enemy-reward-run-unavailable");
            if (killerEntityStableId == null
                || killerEntityStableId != playerDamageSourceStableId
                || killerParticipantStableId == null)
            {
                diagnostic = "compact-enemy-reward-killer-not-player";
                return;
            }

            StableId roomStableId = enemy.RoomStableId;
            StableId placementStableId = enemy.PlacementStableId;
            CompactEnemyDefinition definition = enemy.Definition;
            if (roomStableId == null
                || placementStableId == null
                || definition == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-reward-source-incomplete");
            }

            StableId dropProfile = EnemyDropProfiles.Resolve(
                definition.drops,
                enemy.DefinitionStableId);
            RoomLiveView roomProjection = rooms.CurrentProjection;
            if (roomProjection == null)
                throw new InvalidOperationException(
                    "compact-enemy-reward-room-projection-missing");
            EnemyLiveIdentity identity = new DeterministicEnemyLiveIdentityDeriver()
                .Derive(
                    run.RunStableId,
                    roomProjection.RuntimeInstanceStableId,
                    roomStableId,
                    placementStableId);
            StableId deathEventStableId = StableId.Create(
                "enemy-death",
                Hash(
                    run.RunStableId + "|" + run.LifecycleGeneration + "|"
                    + roomStableId + "|" + placementStableId + "|"
                    + enemy.DamageableLifecycleGeneration + "|"
                    + triggeringEventStableId).Substring(0, 32));
            var fact = new EnemyDropFact(
                deathEventStableId,
                triggeringEventStableId,
                run.RunStableId,
                run.LifecycleGeneration,
                checked((int)Math.Max(1L, roomProjection.LifecycleGeneration)),
                identity.EntityInstanceId,
                placementStableId,
                enemy.DamageableLifecycleGeneration,
                roomStableId,
                enemy.DefinitionStableId,
                enemy.EnemyLevel,
                killerEntityStableId,
                playerParticipantStableId,
                dropProfile,
                cause);
            string positionFingerprint = Hash(
                "compact-enemy-terminal-position-v1|" + run.RunStableId + "|"
                + run.LifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + roomStableId + "|" + placementStableId + "|"
                + position.x.ToString("R", CultureInfo.InvariantCulture) + "|"
                + position.y.ToString("R", CultureInfo.InvariantCulture));
            pickupBridge.RegisterFixedSource(
                run.RunStableId,
                run.LifecycleGeneration,
                identity.EntityInstanceId,
                placementStableId,
                roomStableId,
                position,
                positionFingerprint);

            TerminalPersonalRewardBatch batch = dropGeneration.GenerateBatch(fact);
            if (batch == null || !batch.IsAccepted)
            {
                diagnostic = batch == null
                    ? "compact-enemy-reward-generation-null"
                    : batch.Diagnostic;
                Debug.LogError(diagnostic, enemy);
                return;
            }
            for (int index = 0; index < batch.Results.Count; index++)
            {
                PendingLootDropAdmissionResult admission =
                    pendingAdmission.Admit(batch.Results[index]);
                if (admission == null || !admission.IsAccepted)
                {
                    diagnostic = admission == null
                        ? "compact-enemy-reward-admission-null"
                        : admission.Diagnostic;
                    Debug.LogError(diagnostic, enemy);
                    continue;
                }
                pickupBridge.Consume(admission);
            }
            pickupBridge.ProcessPending();
            diagnostic = pickupBridge.LastDiagnostic;
        }

        private void ConfigureDropGeneration(CharacterLiveGraph graph)
        {
            pendingAdmission = new PendingLootDropAdmissionState();
            Func<RunSessionAggregate> runResolver = delegate { return run; };
            var binding = LootDropBindingSetup.Create(
                new PropCatalog(
                    PropCapabilityRegistry.CreateBuiltIns(),
                    Array.Empty<PropDefinition>()),
                new NoPropRewardSource(),
                new RunSessionLootDropContextResolver(
                    runSessions,
                    new FrozenRunProgression(
                        graph.Character.CharacterInstanceStableId,
                        frozenProgression),
                    GenerationAlgorithmVersion),
                null,
                null,
                pendingAdmission,
                new ILootDropFactBridge[]
                {
                    new EnemyDropBridge(),
                },
                pickupBridge,
                new PersonalRewardGenerationActions(
                    new ParticipantDropPacing(
                        new RunSessionParticipantDropPacingStateStore(run))),
                new RunSessionTerminalRewardParticipantResolver(
                    runResolver,
                    new TerminalRewardEligibilityPolicy(true, false, false)),
                new RunSessionTerminalRewardEnvironmentResolver(runResolver),
                new RunSessionTerminalRewardOverrideResolver(runResolver),
                null,
                true);
            dropGeneration = binding.Authority;
        }

        private void ConfigurePickupRuntime()
        {
            RunLootPositions positions = GetComponent<RunLootPositions>();
            if (positions == null)
                positions = gameObject.AddComponent<RunLootPositions>();
            RunLootLiveSetup live = RunLootLiveSetup.Create(run, positions);
            RunLootSession session = GetComponent<RunLootSession>();
            if (session == null)
                session = gameObject.AddComponent<RunLootSession>();
            session.Configure(live.Authority);
            RunLootViews views = GetComponent<RunLootViews>();
            if (views == null)
                views = gameObject.AddComponent<RunLootViews>();
            views.Configure(new[]
            {
                Presentation(RewardGrantKind.Money, MoneySprite(), 0.34f, "Cash"),
                Presentation(RewardGrantKind.Scrap, ScrapSprite(), 0.34f, "Scrap"),
                Presentation(
                    RewardGrantKind.Strongbox,
                    StrongboxSprite(),
                    0.5f,
                    "Strongbox"),
            });
            pickupView = GetComponent<RunLootView>();
            if (pickupView == null)
                pickupView = gameObject.AddComponent<RunLootView>();
            pickupView.Configure(session, views, transform);
            pickupBridge = new LootBridge();
            pickupBridge.ConfigureRuntime(positions, live.PendingConsumer, pickupView);
        }

        private static RunLootPresentationEntry Presentation(
            RewardGrantKind kind,
            Sprite sprite,
            float scale,
            string label)
        {
            var entry = new RunLootPresentationEntry();
            entry.Configure(
                kind,
                null,
                null,
                sprite,
                new Vector3(scale, scale, 1f),
                0.75f,
                label);
            return entry;
        }

        private void SynchronizeCurrentRoom()
        {
            if (!configured || rooms == null || pickupView == null) return;
            pickupView.Synchronize(rooms.CurrentRoomStableId);
            if (pickupBridge != null) pickupBridge.ProcessPending();
        }

        private static Sprite MoneySprite()
        {
            if (moneySprite == null)
                moneySprite = CreateSprite(
                    ref moneyTexture,
                    new Color(1f, 0.82f, 0.18f, 1f),
                    "Run Reward Money");
            return moneySprite;
        }

        private static Sprite ScrapSprite()
        {
            if (scrapSprite == null)
                scrapSprite = CreateSprite(
                    ref scrapTexture,
                    new Color(0.2f, 0.86f, 0.9f, 1f),
                    "Run Reward Scrap");
            return scrapSprite;
        }

        private static Sprite StrongboxSprite()
        {
            if (strongboxSprite == null)
                strongboxSprite = CreateSprite(
                    ref strongboxTexture,
                    new Color(0.7f, 0.34f, 1f, 1f),
                    "Run Reward Strongbox");
            return strongboxSprite;
        }

        private static Sprite CreateSprite(
            ref Texture2D texture,
            Color color,
            string name)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = name + " Texture";
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = name;
            return sprite;
        }

        internal static string Hash(string material)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(
                    Encoding.UTF8.GetBytes(material ?? string.Empty));
                return BitConverter.ToString(digest)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private void OnDestroy()
        {
            if (rooms != null)
                rooms.CurrentRoomPresentationRebuilt -= SynchronizeCurrentRoom;
            if (pickupBridge != null) pickupBridge.ReleaseRuntime();
            if (active == this) active = null;
            configured = false;
        }
    }

    internal sealed class FrozenRunProgression :
        IRunRewardProgressionContextProvider
    {
        private readonly StableId characterStableId;
        private readonly ProgressionContext frozenProgression;

        public FrozenRunProgression(
            StableId characterStableId,
            ProgressionContext frozenProgression)
        {
            this.characterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            this.frozenProgression = frozenProgression
                ?? throw new ArgumentNullException(nameof(frozenProgression));
        }

        public bool TryResolve(
            RunSessionAggregate run,
            out ProgressionContext progressionContext,
            out string diagnostic)
        {
            progressionContext = null;
            if (run == null
                || run.FrozenInputs.Character.CharacterInstanceStableId
                    != characterStableId
                || run.FrozenInputs.CharacterStats.Level
                    != frozenProgression.CharacterLevel
                || run.StartCommand.DifficultyStableId
                    != frozenProgression.DifficultyId)
            {
                diagnostic = "compact-enemy-reward-progression-mismatch";
                return false;
            }
            progressionContext = frozenProgression;
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class NoPropRewardSource : IPropTerminalSourceContextResolver
    {
        public bool TryResolve(
            PropTerminalFact terminalFact,
            out PropTerminalSourceContext context,
            out string diagnostic)
        {
            context = null;
            diagnostic = "prop-rewards-not-composed";
            return false;
        }
    }
}
