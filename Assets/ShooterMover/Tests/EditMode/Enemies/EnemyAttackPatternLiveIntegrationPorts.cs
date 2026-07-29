using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternLiveIntegrationTests
    {
        private sealed class RecordingLegacyAttackPort : IEnemyAttackEffectPort
        {
            public int ExecutionCount { get; private set; }

            public void Emit(EnemyAttackExecutionRequest request)
            {
                ExecutionCount++;
            }
        }

        private sealed class RecordingPatternPorts :
            IEnemyAttackEffectPort,
            IEnemyAttackPatternEffectPort,
            IEnemyPlayerDamagePort,
            IEnemyRoomTerminalPort,
            IEnemyExperienceFactConsumer,
            IEnemyDropFactConsumer,
            IEnemyKillStatFactConsumer,
            IEnemyTerminalCollisionBridge
        {
            private readonly Dictionary<StableId, string> sequenceFingerprints =
                new Dictionary<StableId, string>();
            private readonly Dictionary<StableId, string> cancellationFingerprints =
                new Dictionary<StableId, string>();
            private readonly List<EnemyAttackSequenceDispatch> dispatchedSequences =
                new List<EnemyAttackSequenceDispatch>();
            private readonly List<EnemyAttackEffectEmission> emissions =
                new List<EnemyAttackEffectEmission>();
            private readonly List<EnemyAttackEffectEmission> executedEmissions =
                new List<EnemyAttackEffectEmission>();
            private readonly HashSet<StableId> cancelledEmissionIds =
                new HashSet<StableId>();
            private readonly HashSet<StableId> executedEmissionIds =
                new HashSet<StableId>();

            public RecordingPatternPorts()
            {
                Bundle = WithAttackEffects(this);
                ThrowOnDispatchEmissionIndex = -1;
            }

            public EnemyLiveDownstreamPorts Bundle { get; }
            public IReadOnlyList<EnemyAttackSequenceDispatch> DispatchedSequences
            {
                get { return dispatchedSequences; }
            }
            public IReadOnlyList<EnemyAttackEffectEmission> Emissions
            {
                get { return emissions; }
            }
            public IReadOnlyList<EnemyAttackEffectEmission> ExecutedEmissions
            {
                get { return executedEmissions; }
            }
            public int LegacyExecutionCount { get; private set; }
            public int DispatchAttempts { get; private set; }
            public EnemyAttackSequenceDispatch LastAttemptedSequence
            {
                get;
                private set;
            }
            public int CancellationAttempts { get; private set; }
            public int AcceptedCancellationCount { get; private set; }
            public int ThrowOnDispatchEmissionIndex { get; set; }
            public bool RejectNextCancellation { get; set; }
            public EnemyAttackSequenceCancellationFact LastCancellation
            {
                get;
                private set;
            }
            public int RoomCount { get; private set; }
            public int TerminalCollisionCount { get; private set; }

            public EnemyLiveDownstreamPorts WithAttackEffects(
                IEnemyAttackEffectPort attackEffects)
            {
                return new EnemyLiveDownstreamPorts(
                    attackEffects,
                    this,
                    this,
                    this,
                    this,
                    this,
                    this);
            }

            public void Emit(EnemyAttackExecutionRequest request)
            {
                LegacyExecutionCount++;
            }

            public EnemyAttackPatternDispatchResult Dispatch(
                EnemyAttackSequenceDispatch sequence)
            {
                DispatchAttempts++;
                LastAttemptedSequence = sequence;
                string existing;
                if (sequenceFingerprints.TryGetValue(
                    sequence.DispatchStableId,
                    out existing))
                {
                    return string.Equals(existing, sequence.Fingerprint, StringComparison.Ordinal)
                        ? EnemyAttackPatternDispatchResult.ExactReplay(
                            sequence.DispatchStableId,
                            sequence.Fingerprint)
                        : EnemyAttackPatternDispatchResult.Rejected(
                            sequence.DispatchStableId,
                            sequence.Fingerprint,
                            EnemyAttackPatternDispatchRejectionCode
                                .ConflictingDuplicate);
                }

                // Prevalidate the complete immutable batch before mutating the queue.
                for (int index = 0; index < sequence.Emissions.Count; index++)
                {
                    if (index == ThrowOnDispatchEmissionIndex)
                    {
                        ThrowOnDispatchEmissionIndex = -1;
                        throw new InvalidOperationException(
                            "Injected sequence prevalidation failure.");
                    }
                    Assert.That(
                        sequence.Emissions[index].SequenceStableId,
                        Is.EqualTo(sequence.DispatchStableId));
                }

                sequenceFingerprints.Add(
                    sequence.DispatchStableId,
                    sequence.Fingerprint);
                dispatchedSequences.Add(sequence);
                for (int index = 0; index < sequence.Emissions.Count; index++)
                    emissions.Add(sequence.Emissions[index]);
                return EnemyAttackPatternDispatchResult.Applied(
                    sequence.DispatchStableId,
                    sequence.Fingerprint);
            }

            public EnemyAttackPatternDispatchResult Cancel(
                EnemyAttackSequenceCancellationFact fact)
            {
                CancellationAttempts++;
                string existing;
                if (cancellationFingerprints.TryGetValue(
                    fact.CancellationStableId,
                    out existing))
                {
                    return string.Equals(existing, fact.Fingerprint, StringComparison.Ordinal)
                        ? EnemyAttackPatternDispatchResult.ExactReplay(
                            fact.CancellationStableId,
                            fact.Fingerprint)
                        : EnemyAttackPatternDispatchResult.Rejected(
                            fact.CancellationStableId,
                            fact.Fingerprint,
                            EnemyAttackPatternDispatchRejectionCode
                                .ConflictingDuplicate);
                }
                if (RejectNextCancellation)
                {
                    RejectNextCancellation = false;
                    return EnemyAttackPatternDispatchResult.Rejected(
                        fact.CancellationStableId,
                        fact.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCode.DownstreamFailure);
                }

                cancellationFingerprints.Add(
                    fact.CancellationStableId,
                    fact.Fingerprint);
                AcceptedCancellationCount++;
                LastCancellation = fact;
                for (int index = 0;
                    index < fact.CancelledProjectileStableIds.Count;
                    index++)
                {
                    cancelledEmissionIds.Add(
                        fact.CancelledProjectileStableIds[index]);
                }
                for (int index = 0;
                    index < fact.CancelledMeleeStrikeStableIds.Count;
                    index++)
                {
                    cancelledEmissionIds.Add(
                        fact.CancelledMeleeStrikeStableIds[index]);
                }
                return EnemyAttackPatternDispatchResult.Applied(
                    fact.CancellationStableId,
                    fact.Fingerprint);
            }

            public void ProcessScheduledEffects(double throughSeconds)
            {
                for (int index = 0; index < emissions.Count; index++)
                {
                    EnemyAttackEffectEmission emission = emissions[index];
                    if (emission.ScheduledAtSeconds > throughSeconds
                        || cancelledEmissionIds.Contains(emission.EmissionStableId)
                        || !executedEmissionIds.Add(emission.EmissionStableId))
                    {
                        continue;
                    }
                    executedEmissions.Add(emission);
                }
            }

            public bool WasExecuted(StableId emissionStableId)
            {
                return executedEmissionIds.Contains(emissionStableId);
            }

            public EnemyPlayerDamagePortResult Route(
                EnemyPlayerDamageRequest request)
            {
                return new EnemyPlayerDamagePortResult(
                    EnemyLiveOperationStatus.NoEffect,
                    EnemyLiveRejectionCode.None);
            }

            public void Report(
                ReportRoomOccupantTerminalCommand command,
                EnemyDeathFact deathFact)
            {
                RoomCount++;
            }

            void IEnemyExperienceFactConsumer.Consume(EnemyDeathFact fact)
            {
            }

            void IEnemyDropFactConsumer.Consume(EnemyDeathFact fact)
            {
            }

            void IEnemyKillStatFactConsumer.Consume(EnemyDeathFact fact)
            {
            }

            public void SetTerminal(EnemyTerminalCollisionFact fact)
            {
                TerminalCollisionCount++;
            }
        }
    }
}
