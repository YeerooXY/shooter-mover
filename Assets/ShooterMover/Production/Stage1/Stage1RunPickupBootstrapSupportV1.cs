using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal sealed class Stage1EnemyTerminalDropObserver2D : MonoBehaviour
    {
        private IEnemyActor2DAuthority authority;
        private Action terminalAction;
        private bool emitted;

        public void Configure(
            IEnemyActor2DAuthority authority,
            Action terminalAction)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.terminalAction = terminalAction
                ?? throw new ArgumentNullException(nameof(terminalAction));
        }

        private void LateUpdate()
        {
            if (authority == null || terminalAction == null) return;
            EnemyActorState state;
            if (!authority.TryReadState(out state) || state == null) return;
            if (!state.IsDestroyed)
            {
                emitted = false;
                return;
            }
            if (emitted) return;
            emitted = true;
            terminalAction();
        }
    }

    internal sealed class Stage1PendingAdmissionPickupBridgeV1 :
        IPendingTerminalDropAdmissionConsumerV1
    {
        private sealed class SourceBinding
        {
            public SourceBinding(StableId roomStableId, Transform sourceTransform)
            {
                RoomStableId = roomStableId;
                SourceTransform = sourceTransform;
            }

            public StableId RoomStableId { get; }
            public Transform SourceTransform { get; }
        }

        private readonly RunPickupSourcePositionRegistry2D sourcePositions;
        private readonly PendingTerminalDropPickupConsumerV1 pickupConsumer;
        private readonly RunPickupPresenter2D presenter;
        private readonly Dictionary<string, SourceBinding> sources =
            new Dictionary<string, SourceBinding>(StringComparer.Ordinal);

        public Stage1PendingAdmissionPickupBridgeV1(
            RunPickupSourcePositionRegistry2D sourcePositions,
            PendingTerminalDropPickupConsumerV1 pickupConsumer,
            RunPickupPresenter2D presenter)
        {
            this.sourcePositions = sourcePositions
                ?? throw new ArgumentNullException(nameof(sourcePositions));
            this.pickupConsumer = pickupConsumer
                ?? throw new ArgumentNullException(nameof(pickupConsumer));
            this.presenter = presenter
                ?? throw new ArgumentNullException(nameof(presenter));
        }

        public string LastDiagnostic { get; private set; }

        public void RegisterSource(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            StableId roomStableId,
            Transform sourceTransform)
        {
            if (runStableId == null
                || lifecycleGeneration <= 0L
                || sourceEntityStableId == null
                || roomStableId == null
                || sourceTransform == null)
            {
                throw new ArgumentException(
                    "A complete terminal source binding is required.");
            }

            sources[Key(
                runStableId,
                lifecycleGeneration,
                sourceEntityStableId,
                sourcePlacementStableId)] =
                    new SourceBinding(roomStableId, sourceTransform);
        }

        public void Consume(PendingTerminalDropAdmissionResultV1 admission)
        {
            LastDiagnostic = string.Empty;
            if (admission == null
                || !admission.IsAccepted
                || admission.PendingResult == null
                || admission.PendingResult.SourceFact == null)
            {
                LastDiagnostic = "stage1-pickup-admission-not-accepted";
                return;
            }

            TerminalDropSourceFactV1 source = admission.PendingResult.SourceFact;
            SourceBinding binding;
            if (!sources.TryGetValue(
                    Key(
                        source.RunStableId,
                        source.RunLifecycleGeneration,
                        source.SourceEntityStableId,
                        source.SourcePlacementStableId),
                    out binding)
                || binding == null
                || binding.SourceTransform == null)
            {
                LastDiagnostic =
                    "stage1-pickup-terminal-source-transform-missing";
                return;
            }

            Vector2 position = binding.SourceTransform.position;
            string positionFingerprint = RunSessionFingerprintV1.Hash(
                source.TerminalEventStableId
                + "|"
                + position.x.ToString("R", CultureInfo.InvariantCulture)
                + "|"
                + position.y.ToString("R", CultureInfo.InvariantCulture));
            string positionDiagnostic;
            if (!sourcePositions.Register(
                    source.RunStableId,
                    source.RunLifecycleGeneration,
                    source.SourceEntityStableId,
                    source.SourcePlacementStableId,
                    binding.RoomStableId,
                    position,
                    positionFingerprint,
                    out positionDiagnostic))
            {
                LastDiagnostic = positionDiagnostic;
                return;
            }

            RunPickupRealizationResultV1 result =
                pickupConsumer.Consume(admission);
            LastDiagnostic = result == null
                ? "stage1-pickup-realization-null"
                : result.Diagnostic;
            presenter.Synchronize(binding.RoomStableId);
        }

        private static string Key(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId)
        {
            return runStableId
                + "|"
                + lifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|"
                + sourceEntityStableId
                + "|"
                + (sourcePlacementStableId == null
                    ? "none"
                    : sourcePlacementStableId.ToString());
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
