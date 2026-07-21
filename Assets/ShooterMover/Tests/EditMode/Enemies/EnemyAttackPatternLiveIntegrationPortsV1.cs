using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternLiveIntegrationV1Tests
    {
        private sealed class RecordingLegacyAttackPort : IEnemyAttackEffectPortV1
        {
            public int ExecutionCount { get; private set; }

            public void Emit(EnemyAttackExecutionRequestV1 request)
            {
                ExecutionCount++;
            }
        }

        private sealed class RecordingPatternPorts :
            IEnemyAttackEffectPortV1,
            IEnemyAttackPatternEffectPortV1,
            IEnemyPlayerDamagePortV1,
            IEnemyRoomTerminalPortV1,
            IEnemyExperienceFactConsumerV1,
            IEnemyDropFactConsumerV1,
            IEnemyKillStatFactConsumerV1,
            IEnemyTerminalCollisionAdapterV1
        {
            private readonly Dictionary<StableId, string> sequenceFingerprints =
                new Dictionary<StableId, string>();
            private readonly Dictionary<StableId, string> cancellationFingerprints =
                new Dictionary<StableId, string>();
            private readonly List<EnemyAttackSequenceDispatchV1> dispatchedSequences =
                new List<EnemyAttackSequenceDispatchV1>();
            private readonly List<EnemyAttackEffectEmissionV1> emissions =
                new List<EnemyAttackEffectEmissionV1>();
            private readonly List<EnemyAttackEffectEmissionV1> executedEmissions =
                new List<EnemyAttackEffectEmissionV1>();
            private readonly HashSet<StableId> cancelledEmissionIds =
                new HashSet<StableId>();
            private readonly HashSet<StableId> executedEmissionIds =
                new HashSet<StableId>();

            public RecordingPatternPorts()
            {
                Bundle = WithAttackEffects(this);
                ThrowOnDispatchEmissionIndex = -1;
            }

            public EnemyRuntimeDownstreamPortsV1 Bundle { get; }
            public IReadOnlyList<EnemyAttackSequenceDispatchV1> DispatchedSequences
            {
                get { return dispatchedSequences; }
            }
            public IReadOnlyList<EnemyAttackEffectEmissionV1> Emissions
            {
                get { return emissions; }
            }
            public IReadOnlyList<EnemyAttackEffectEmissionV1> ExecutedEmissions
            {
                get { return executedEmissions; }
            }
            public int LegacyExecutionCount { get; private set; }
            public int DispatchAttempts { get; private set; }
            public EnemyAttackSequenceDispatchV1 LastAttemptedSequence
            {
                get;
                private set;
            }
            public int CancellationAttempts { get; private set; }
            public int AcceptedCancellationCount { get; private set; }
            public int ThrowOnDispatchEmissionIndex { get; set; }
            public bool RejectNextCancellation { get; set; }
            public EnemyAttackSequenceCancellationFactV1 LastCancellation
            {
                get;
                private set;
            }
            public int RoomCount { get; private set; }
            public int TerminalCollisionCount { get; private set; }

            public EnemyRuntimeDownstreamPortsV1 WithAttackEffects(
                IEnemyAttackEffectPortV1 attackEffects)
            {
                return new EnemyRuntimeDownstreamPortsV1(
                    attackEffects,
                    this,
                    this,
                    this,
                    this,
                    this,
                    this);
            }

            public void Emit(EnemyAttackExecutionRequestV1 request)
            {
                LegacyExecutionCount++;
            }

            public EnemyAttackPatternDispatchResultV1 Dispatch(
                EnemyAttackSequenceDispatchV1 sequence)
            {
                DispatchAttempts++;
                LastAttemptedSequence = sequence;
                string existing;
                if (sequenceFingerprints.TryGetValue(
                    sequence.DispatchStableId,
                    out existing))
                {
                    return string.Equals(existing, sequence.Fingerprint, StringComparison.Ordinal)
                        ? EnemyAttackPatternDispatchResultV1.ExactReplay(
                            sequence.DispatchStableId,
                            sequence.Fingerprint)
                        : EnemyAttackPatternDispatchResultV1.Rejected(
                            sequence.DispatchStableId,
                            sequence.Fingerprint,
                            EnemyAttackPatternDispatchRejectionCodeV1
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
                return EnemyAttackPatternDispatchResultV1.Applied(
                    sequence.DispatchStableId,
                    sequence.Fingerprint);
            }

            public EnemyAttackPatternDispatchResultV1 Cancel(
                EnemyAttackSequenceCancellationFactV1 fact)
            {
                CancellationAttempts++;
                string existing;
                if (cancellationFingerprints.TryGetValue(
                    fact.CancellationStableId,
                    out existing))
                {
                    return string.Equals(existing, fact.Fingerprint, StringComparison.Ordinal)
                        ? EnemyAttackPatternDispatchResultV1.ExactReplay(
                            fact.CancellationStableId,
                            fact.Fingerprint)
                        : EnemyAttackPatternDispatchResultV1.Rejected(
                            fact.CancellationStableId,
                            fact.Fingerprint,
                            EnemyAttackPatternDispatchRejectionCodeV1
                                .ConflictingDuplicate);
                }
                if (RejectNextCancellation)
                {
                    RejectNextCancellation = false;
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        fact.CancellationStableId,
                        fact.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
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
                return EnemyAttackPatternDispatchResultV1.Applied(
                    fact.CancellationStableId,
                    fact.Fingerprint);
            }

            public void ProcessScheduledEffects(double throughSeconds)
            {
                for (int index = 0; index < emissions.Count; index++)
                {
                    EnemyAttackEffectEmissionV1 emission = emissions[index];
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

            public EnemyPlayerDamagePortResultV1 Route(
                EnemyPlayerDamageRequestV1 request)
            {
                return new EnemyPlayerDamagePortResultV1(
                    EnemyRuntimeOperationStatusV1.NoEffect,
                    EnemyRuntimeRejectionCodeV1.None);
            }

            public void Report(
                ReportRoomOccupantTerminalCommandV1 command,
                EnemyDeathFactV1 deathFact)
            {
                RoomCount++;
            }

            void IEnemyExperienceFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
            }

            void IEnemyDropFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
            }

            void IEnemyKillStatFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
            }

            public void SetTerminal(EnemyTerminalCollisionFactV1 fact)
            {
                TerminalCollisionCount++;
            }
        }
    }
}
