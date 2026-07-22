using System;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal static class Stage1ProductionFingerprintV1
    {
        public static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }

    internal sealed class Stage1EnemyTerminalSourceContextResolverV1 :
        IEnemyTerminalSourceContextResolverV1
    {
        private readonly Func<RunSessionAggregateV1> run;

        public Stage1EnemyTerminalSourceContextResolverV1(
            Func<RunSessionAggregateV1> run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public bool TryResolve(
            EnemyDeathFactV1 terminalFact,
            out EnemyTerminalSourceContextV1 context,
            out string diagnostic)
        {
            context = null;
            diagnostic = string.Empty;
            RunSessionAggregateV1 current = run();
            if (terminalFact == null
                || terminalFact.Identity == null
                || current == null
                || terminalFact.Identity.RunStableId != current.RunStableId
                || terminalFact.LifecycleGeneration != current.LifecycleGeneration)
            {
                diagnostic =
                    "stage1-enemy-terminal-source-context-mismatch";
                return false;
            }

            context = new EnemyTerminalSourceContextV1(
                current.RunStableId,
                current.LifecycleGeneration,
                terminalFact.Identity.EntityInstanceId,
                terminalFact.Identity.PlacementStableId,
                terminalFact.LifecycleGeneration,
                Stage1ProductionFingerprintV1.Hash(
                    terminalFact.DeathEventStableId
                    + "|"
                    + terminalFact.Identity.EntityInstanceId
                    + "|"
                    + terminalFact.Identity.PlacementStableId));
            return true;
        }
    }

    internal sealed class Stage1PickupTerminalDropRunContextResolverV1 :
        ITerminalDropRunContextResolverV1
    {
        private RunSessionAggregateV1 boundRun;
        private TerminalDropRunGenerationContextV1 frozenContext;

        public void Bind(RunSessionAggregateV1 run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.LifecycleState != RunSessionLifecycleStateV1.Active
                || run.FrozenInputs == null
                || run.FrozenInputs.CharacterStats == null)
            {
                throw new InvalidOperationException(
                    "An active Run Session with frozen character inputs is required.");
            }

            LevelSelectionDefinitionV1 levelDefinition;
            LevelSelectionCatalogV1 levelCatalog =
                LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog();
            if (run.StartCommand.MissionLayoutStableId
                    != Level1AuthorableRoomDefinitionV1.LayoutStableId
                || !levelCatalog.TryGet(
                    StableId.Parse(
                        LevelSelectionCatalogDefinitionV1.Level1StableIdText),
                    out levelDefinition)
                || levelDefinition == null)
            {
                throw new InvalidOperationException(
                    "The Run Session mission has no canonical drop-level projection.");
            }

            int frozenPlayerLevel = Math.Max(
                1,
                run.FrozenInputs.CharacterStats.Level);
            int frozenMissionLevel = Math.Max(
                1,
                levelDefinition.Recommendation.RecommendedPlayerLevel);
            frozenContext = new TerminalDropRunGenerationContextV1(
                run.RunStableId,
                run.LifecycleGeneration,
                unchecked((ulong)run.StartCommand.DeterministicSeed),
                1,
                ProgressionContext.Create(
                    frozenPlayerLevel,
                    frozenMissionLevel,
                    run.StartCommand.DifficultyStableId,
                    0),
                run.StartCommand.EventModifierContextFingerprint);
            boundRun = run;
        }

        public bool TryResolve(
            StableId runStableId,
            long expectedLifecycleGeneration,
            out TerminalDropRunGenerationContextV1 context,
            out TerminalDropRejectionCodeV1 rejectionCode,
            out string diagnostic)
        {
            context = null;
            rejectionCode = TerminalDropRejectionCodeV1.None;
            diagnostic = string.Empty;
            TerminalDropRunGenerationContextV1 current = frozenContext;
            if (current == null || runStableId != current.RunStableId)
            {
                rejectionCode = TerminalDropRejectionCodeV1.MissingRun;
                diagnostic = "stage1-pickup-run-context-missing";
                return false;
            }
            if (expectedLifecycleGeneration != current.LifecycleGeneration)
            {
                rejectionCode = TerminalDropRejectionCodeV1.WrongRunLifecycle;
                diagnostic =
                    "stage1-pickup-run-context-lifecycle-mismatch";
                return false;
            }
            if (boundRun == null
                || boundRun.LifecycleGeneration != current.LifecycleGeneration)
            {
                rejectionCode = TerminalDropRejectionCodeV1.WrongRunLifecycle;
                diagnostic =
                    "stage1-pickup-run-context-lifecycle-not-current";
                return false;
            }
            if (boundRun.LifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                rejectionCode = TerminalDropRejectionCodeV1.RunEnded;
                diagnostic = "stage1-pickup-run-context-ended";
                return false;
            }
            context = current;
            return true;
        }

    }

    internal sealed class Stage1MissingPropTerminalSourceContextResolverV1 :
        IPropTerminalSourceContextResolverV1
    {
        public bool TryResolve(
            PropTerminalFactV1 terminalFact,
            out PropTerminalSourceContextV1 context,
            out string diagnostic)
        {
            context = null;
            diagnostic =
                "stage1-production-prop-terminal-source-not-registered";
            return false;
        }
    }
}
