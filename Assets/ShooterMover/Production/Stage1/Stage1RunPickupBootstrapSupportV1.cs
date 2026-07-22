using System;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
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
                RunSessionFingerprintV1.Hash(
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
        private readonly Func<RunSessionAggregateV1> run;
        private readonly Func<int> playerLevel;

        public Stage1PickupTerminalDropRunContextResolverV1(
            Func<RunSessionAggregateV1> run,
            Func<int> playerLevel)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.playerLevel = playerLevel
                ?? throw new ArgumentNullException(nameof(playerLevel));
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
            RunSessionAggregateV1 current = run();
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
            if (current.LifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                rejectionCode = TerminalDropRejectionCodeV1.RunEnded;
                diagnostic = "stage1-pickup-run-context-ended";
                return false;
            }

            context = new TerminalDropRunGenerationContextV1(
                current.RunStableId,
                current.LifecycleGeneration,
                unchecked((ulong)current.StartCommand.DeterministicSeed),
                1,
                ProgressionContext.Create(
                    Math.Max(1, playerLevel()),
                    1,
                    StableId.Parse("difficulty.normal"),
                    0),
                current.StartCommand.EventModifierContextFingerprint);
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
