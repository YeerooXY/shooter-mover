using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.RunSessions
{
    public sealed class RunSessionDurableEndV1Tests
    {
        [Test]
        public void AcceptedDurabilityEndsTheRunOnceAndExactCommandReplays()
        {
            Fixture fixture = Fixture.Create();
            int durableCalls = 0;

            RunSessionEndResultV1 first = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.That(candidate.Receipt, Is.Not.Null);
                    return RunSessionDurableAcceptanceResultV1.Accepted();
                });
            RunSessionEndResultV1 replay = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    Assert.Fail("Durability callback must not run for a successful exact replay.");
                    return RunSessionDurableAcceptanceResultV1.Accepted();
                });

            Assert.That(first.Status, Is.EqualTo(RunSessionEndStatusV1.Ended));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(fixture.Run.LifecycleState,
                Is.EqualTo(RunSessionLifecycleStateV1.Ended));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndStateV1.None));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.Null);
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(1));
        }

        [Test]
        public void RetryableRejectionRetainsSameCandidateAndRetriesOnlyDurability()
        {
            Fixture fixture = Fixture.Create();
            RunSessionEndResultV1 callbackCandidate = null;
            int durableCalls = 0;

            RunSessionEndResultV1 rejected = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    callbackCandidate = candidate;
                    durableCalls++;
                    return RunSessionDurableAcceptanceResultV1.Retryable(
                        "fixture-transient");
                });
            RunSessionEndResultV1 retained = fixture.Run.PendingDurableEndCandidate;
            RunSessionEndResultV1 accepted = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.That(candidate, Is.SameAs(retained));
                    return RunSessionDurableAcceptanceResultV1.Accepted();
                });

            Assert.That(rejected.Status, Is.EqualTo(RunSessionEndStatusV1.Rejected));
            Assert.That(callbackCandidate, Is.SameAs(retained));
            Assert.That(retained, Is.Not.Null);
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndStateV1.None));
            Assert.That(accepted.Status, Is.EqualTo(RunSessionEndStatusV1.Ended));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(2));
        }

        [Test]
        public void RetryPreservesReceiptLocalSnapshotAndMissionResultExactly()
        {
            Fixture fixture = Fixture.Create();
            fixture.Player.Damage(25d);
            fixture.Run.ApplyLocalMutation(
                new RunLocalMutationCommandV1(
                    Id("operation.retry-counter"),
                    fixture.Run.RunStableId,
                    fixture.Run.LifecycleGeneration,
                    RunLocalMutationKindV1.IncrementCounter,
                    "kills",
                    3L,
                    "fixture-counter"));

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => RunSessionDurableAcceptanceResultV1.Retryable(
                    "fixture-transient"));

            RunSessionEndResultV1 firstCandidate =
                fixture.Run.PendingDurableEndCandidate;
            RunSessionEndReceiptV1 firstReceipt = firstCandidate.Receipt;
            string localFingerprint = firstReceipt.LocalState.Fingerprint;
            string missionFingerprint = firstReceipt.MissionResult.Fingerprint;
            string receiptFingerprint = firstReceipt.Fingerprint;

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    Assert.That(candidate, Is.SameAs(firstCandidate));
                    Assert.That(candidate.Receipt, Is.SameAs(firstReceipt));
                    Assert.That(candidate.Receipt.Fingerprint,
                        Is.EqualTo(receiptFingerprint));
                    Assert.That(candidate.Receipt.LocalState.Fingerprint,
                        Is.EqualTo(localFingerprint));
                    Assert.That(candidate.Receipt.MissionResult.Fingerprint,
                        Is.EqualTo(missionFingerprint));
                    Assert.That(candidate.Receipt.LocalState.Counters["kills"],
                        Is.EqualTo(3L));
                    return RunSessionDurableAcceptanceResultV1.Accepted();
                });

            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        [Test]
        public void TerminalPreparationFailureIsStickyAndNeverReentersAuthorities()
        {
            Fixture fixture = Fixture.Create();
            int durableCalls = 0;

            RunSessionEndResultV1 first = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    return RunSessionDurableAcceptanceResultV1.Terminal(
                        "fixture-terminal");
                });
            RunSessionEndResultV1 retained =
                fixture.Run.PendingDurableEndCandidate;
            RunSessionEndResultV1 second = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.Fail("Sticky terminal state must not re-enter durability.");
                    return RunSessionDurableAcceptanceResultV1.Accepted();
                });

            Assert.That(first.Status, Is.EqualTo(RunSessionEndStatusV1.Rejected));
            Assert.That(second.Status, Is.EqualTo(RunSessionEndStatusV1.Rejected));
            Assert.That(second.Receipt, Is.SameAs(retained.Receipt));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.SameAs(retained));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(
                    RunSessionDurableEndStateV1.TerminalPreparationFailure));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Is.EqualTo("fixture-terminal"));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(1));
        }

        [Test]
        public void DurableUncertaintyIsStickyAndReportsDistinctState()
        {
            Fixture fixture = Fixture.Create();
            int durableCalls = 0;

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    return RunSessionDurableAcceptanceResultV1.Uncertain(
                        "fixture-uncertain");
                });
            RunSessionEndResultV1 retained =
                fixture.Run.PendingDurableEndCandidate;
            RunSessionEndResultV1 second = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.Fail("Sticky uncertainty must not re-enter durability.");
                    return RunSessionDurableAcceptanceResultV1.Accepted();
                });

            Assert.That(second.Status, Is.EqualTo(RunSessionEndStatusV1.Rejected));
            Assert.That(second.Receipt, Is.SameAs(retained.Receipt));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndStateV1.DurableStateUncertain));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Is.EqualTo("fixture-uncertain"));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingEndOperationRejectsWithoutReplacingCandidate()
        {
            Fixture fixture = Fixture.Create();

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => RunSessionDurableAcceptanceResultV1.Retryable(
                    "fixture-transient"));
            RunSessionEndResultV1 retained =
                fixture.Run.PendingDurableEndCandidate;
            var conflict = new EndRunSessionCommandV1(
                fixture.EndCommand.OperationStableId,
                fixture.Run.RunStableId,
                fixture.Run.LifecycleGeneration,
                MissionRunCompletionStateV1.Failed,
                fixture.EndCommand.AuthoritativeTick);

            RunSessionEndResultV1 result = fixture.Run.EndWithDurableAcceptance(
                conflict,
                candidate =>
                {
                    Assert.Fail("Conflicting command must not invoke durability.");
                    return RunSessionDurableAcceptanceResultV1.Accepted();
                });

            Assert.That(result.Status,
                Is.EqualTo(RunSessionEndStatusV1.ConflictingDuplicate));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.SameAs(retained));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        [Test]
        public void CallbackThrowBecomesStickyDurableUncertainty()
        {
            Fixture fixture = Fixture.Create();

            RunSessionEndResultV1 result = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => throw new InvalidOperationException("fixture"));

            Assert.That(result.Status, Is.EqualTo(RunSessionEndStatusV1.Rejected));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndStateV1.DurableStateUncertain));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Does.StartWith("run-end-durable-acceptance-threw:"));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.Not.Null);
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        [Test]
        public void NullCallbackResultBecomesStickyDurableUncertainty()
        {
            Fixture fixture = Fixture.Create();

            RunSessionEndResultV1 result = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => null);

            Assert.That(result.Status, Is.EqualTo(RunSessionEndStatusV1.Rejected));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndStateV1.DurableStateUncertain));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Is.EqualTo("run-end-durable-acceptance-result-null"));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.Not.Null);
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private sealed class Fixture
        {
            private Fixture(
                RunSessionAggregateV1 run,
                FakePlayerPort player,
                FakeMissionResultPort missionResults,
                EndRunSessionCommandV1 endCommand)
            {
                Run = run;
                Player = player;
                MissionResults = missionResults;
                EndCommand = endCommand;
            }

            public RunSessionAggregateV1 Run { get; }
            public FakePlayerPort Player { get; }
            public FakeMissionResultPort MissionResults { get; }
            public EndRunSessionCommandV1 EndCommand { get; }

            public static Fixture Create()
            {
                var source = new FakeStartSource();
                var authority = new RunSessionAuthorityV1(source);
                StartRunSessionCommandV1 start = source.Command(
                    "durable-end-start",
                    55L);
                RunSessionStartResultV1 started = authority.Start(start);
                Assert.That(started.Status,
                    Is.EqualTo(RunSessionStartStatusV1.Started));
                RunSessionAggregateV1 run;
                Assert.That(authority.TryGetRun(started.RunStableId, out run),
                    Is.True);
                FakeRuntimeBundle bundle = source.Bundle(started.RunStableId);
                var end = new EndRunSessionCommandV1(
                    Id("operation.durable-end"),
                    run.RunStableId,
                    run.LifecycleGeneration,
                    MissionRunCompletionStateV1.Completed,
                    100L);
                return new Fixture(run, bundle.Player, bundle.MissionResults, end);
            }
        }

        private sealed class FakeStartSource : IRunSessionStartSourceV1
        {
            private readonly Dictionary<StableId, FakeRuntimeBundle> bundles =
                new Dictionary<StableId, FakeRuntimeBundle>();

            public FakeStartSource()
            {
                Character = new CharacterInstanceSnapshotV1(
                    Id("character-instance.durable-end"),
                    Id("loadout-profile.striker"),
                    0,
                    "Durable End Pilot",
                    4L,
                    null);
            }

            public CharacterInstanceSnapshotV1 Character { get; }

            public StartRunSessionCommandV1 Command(
                string operationSuffix,
                long seed)
            {
                return new StartRunSessionCommandV1(
                    Id("operation." + operationSuffix),
                    null,
                    "durable-end-run-material",
                    Character.CharacterInstanceStableId,
                    Character.Revision,
                    Character.Fingerprint,
                    Id("mission-layout.level-1"),
                    Id("difficulty.normal"),
                    seed,
                    0L,
                    "event-context.none");
            }

            public FakeRuntimeBundle Bundle(StableId runStableId)
            {
                return bundles[runStableId];
            }

            public RunSessionStartMaterialV1 Resolve(
                StartRunSessionCommandV1 command,
                StableId resolvedRunStableId)
            {
                StableId definitionId =
                    Id("equipment-definition.durable-end-rifle");
                StableId qualityId = Id("quality.common");
                EquipmentDefinition definition = EquipmentDefinition.Create(
                    definitionId,
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.durable-end-rifle"),
                    "Durable End Rifle",
                    Id("weapon.durable-end-rifle"),
                    InclusiveIntRange.Create(1, 100),
                    2,
                    new[]
                    {
                        EquipmentQualityTier.Create(qualityId, "Common", 1),
                    },
                    null);
                EquipmentInstance equipment = EquipmentInstance.Create(
                    Id("equipment-instance.durable-end-rifle"),
                    definitionId,
                    10,
                    qualityId,
                    null);
                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[]
                        {
                            equipment.InstanceId,
                            null,
                            null,
                            null,
                        });
                DerivedStatPolicyV1 policy =
                    DerivedStatPolicyV1.CreateDefault();
                var baseProfile = new CharacterBaseStatProfileV1(
                    "base-profile.durable-end",
                    Character.ClassDefinitionStableId.ToString(),
                    10,
                    "base-profile-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                        { DerivedStatTargetIdsV1.MovementSpeed, 5m },
                        { DerivedStatTargetIdsV1.WeaponCapacity, 4m },
                        { DerivedStatTargetIdsV1.AbilityCapacity, 0m },
                    });
                var characterInput = new DerivedCharacterStatInputV1(
                    Character.CharacterInstanceStableId.ToString(),
                    baseProfile,
                    null,
                    policy);
                var composer = new DefaultDerivedCharacterStatComposerV1();
                DerivedCharacterStatsSnapshotV1 characterStats =
                    composer.DeriveCharacter(characterInput);
                RunCombatProfileV1 profile = composer.BuildRunProfile(
                    new RunCombatProfileInputV1(
                        resolvedRunStableId.ToString(),
                        command.Fingerprint,
                        characterStats,
                        null,
                        null,
                        policy));
                var skill = new RankedSkillAllocationSnapshotV2(
                    "skill-profile.durable-end",
                    Character.ClassDefinitionStableId.ToString(),
                    0L,
                    "1",
                    "fixture",
                    null);
                var frozen = new FrozenCharacterRunInputsV1(
                    Character,
                    route,
                    0L,
                    "loadout-fingerprint-durable-end",
                    0L,
                    "holdings-fingerprint-durable-end",
                    skill,
                    characterStats,
                    profile,
                    new[]
                    {
                        new FrozenRunEquipmentV1(
                            Id("weapon-slot.slot-1"),
                            equipment,
                            definition),
                    },
                    command.EventModifierContextFingerprint);
                var bundle = new FakeRuntimeBundle(
                    resolvedRunStableId,
                    Character,
                    frozen);
                bundles.Add(resolvedRunStableId, bundle);
                return RunSessionStartMaterialV1.Accept(
                    frozen,
                    bundle.Ports);
            }
        }

        private sealed class FakeRuntimeBundle
        {
            public FakeRuntimeBundle(
                StableId runStableId,
                CharacterInstanceSnapshotV1 character,
                FrozenCharacterRunInputsV1 frozen)
            {
                Player = new FakePlayerPort(
                    Id("actor.player-" + character.CharacterInstanceStableId.Value),
                    Id("participant." + character.CharacterInstanceStableId.Value),
                    1L,
                    Decimal.ToDouble(frozen.CombatProfile.MaximumHealth));
                Weapons = new FakeWeaponPort(
                    1L,
                    frozen.Equipment.Select(
                        item => item.EquipmentInstanceStableId));
                StatusEffects = new FakeStatusEffectPort(1L);
                ConditionalFacts = new FakeConditionalPort(1L);
                ActiveAbilities = new FakeAbilityPort(1L);
                Rooms = new FakeRoomPort(1L);
                MissionResults = new FakeMissionResultPort(runStableId);
                Ports = new RunSessionRuntimePortsV1(
                    Player,
                    Weapons,
                    StatusEffects,
                    ConditionalFacts,
                    ActiveAbilities,
                    Rooms,
                    MissionResults);
            }

            public FakePlayerPort Player { get; }
            public FakeWeaponPort Weapons { get; }
            public FakeStatusEffectPort StatusEffects { get; }
            public FakeConditionalPort ConditionalFacts { get; }
            public FakeAbilityPort ActiveAbilities { get; }
            public FakeRoomPort Rooms { get; }
            public FakeMissionResultPort MissionResults { get; }
            public RunSessionRuntimePortsV1 Ports { get; }
        }

        private abstract class FakeLifecyclePort : IRunLifecycleRuntimePortV1
        {
            protected FakeLifecyclePort(string portId, long generation)
            {
                PortId = portId;
                Generation = generation;
            }

            protected long Generation { get; set; }
            public int TransientCount { get; set; }
            public string PortId { get; }
            public long LifecycleGeneration { get { return Generation; } }
            public virtual string SnapshotFingerprint
            {
                get { return PortId + "|" + Generation + "|" + TransientCount; }
            }

            public virtual string ValidateRestart(
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                if (retiringLifecycleGeneration != Generation)
                {
                    return "generation-mismatch";
                }
                return replacementLifecycleGeneration == Generation + 1L
                    ? string.Empty
                    : "replacement-invalid";
            }

            public virtual RunRuntimePortRestartResultV1 Restart(
                StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                string rejection = ValidateRestart(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
                if (!string.IsNullOrEmpty(rejection))
                {
                    return new RunRuntimePortRestartResultV1(
                        false,
                        rejection,
                        Generation,
                        SnapshotFingerprint);
                }
                Generation = replacementLifecycleGeneration;
                TransientCount = 0;
                return new RunRuntimePortRestartResultV1(
                    true,
                    string.Empty,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakePlayerPort : FakeLifecyclePort,
            IRunPlayerRuntimePortV1
        {
            private readonly StableId actorId;
            private readonly StableId participantId;
            private readonly double maximumHealth;
            private double currentHealth;
            private double x;
            private double y;
            private long acceptedSequence;

            public FakePlayerPort(
                StableId actorId,
                StableId participantId,
                long generation,
                double maximumHealth)
                : base("player-runtime", generation)
            {
                this.actorId = actorId;
                this.participantId = participantId;
                this.maximumHealth = maximumHealth;
                currentHealth = maximumHealth;
            }

            public void Damage(double amount)
            {
                currentHealth = Math.Max(0d, currentHealth - amount);
                acceptedSequence++;
            }

            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
                    actorId,
                    participantId,
                    Generation,
                    currentHealth,
                    maximumHealth,
                    x,
                    y,
                    acceptedSequence);
            }

            public override string SnapshotFingerprint
            {
                get { return ExportSnapshot().Fingerprint; }
            }

            public override RunRuntimePortRestartResultV1 Restart(
                StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                RunRuntimePortRestartResultV1 result = base.Restart(
                    operationStableId,
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
                if (result.Succeeded)
                {
                    currentHealth = maximumHealth;
                    x = 0d;
                    y = 0d;
                    acceptedSequence++;
                }
                return new RunRuntimePortRestartResultV1(
                    result.Succeeded,
                    result.RejectionCode,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakeWeaponPort : FakeLifecyclePort,
            IRunWeaponRuntimePortV1
        {
            private readonly IReadOnlyList<StableId> equipmentIds;

            public FakeWeaponPort(
                long generation,
                IEnumerable<StableId> equipmentIds)
                : base("weapon-runtime", generation)
            {
                this.equipmentIds = equipmentIds.ToList().AsReadOnly();
            }

            public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
            {
                get { return equipmentIds; }
            }
        }

        private sealed class FakeStatusEffectPort : FakeLifecyclePort,
            IRunStatusEffectRuntimePortV1
        {
            public FakeStatusEffectPort(long generation)
                : base("status-effect-runtime", generation)
            {
            }
        }

        private sealed class FakeConditionalPort : FakeLifecyclePort,
            IRunConditionalFactRuntimePortV1
        {
            public FakeConditionalPort(long generation)
                : base("conditional-runtime", generation)
            {
            }
        }

        private sealed class FakeAbilityPort : FakeLifecyclePort,
            IRunActiveAbilityRuntimePortV1
        {
            public FakeAbilityPort(long generation)
                : base("ability-runtime-placeholder", generation)
            {
            }
        }

        private sealed class FakeRoomPort : FakeLifecyclePort,
            IRunRoomRuntimePortV1
        {
            public FakeRoomPort(long generation)
                : base("room-runtime", generation)
            {
                CurrentRoomStableId = Id("room.start");
            }

            public StableId CurrentRoomStableId { get; private set; }
        }

        private sealed class FakeMissionResultPort : IRunMissionResultPortV1
        {
            private readonly StableId runStableId;
            private MissionRunPayloadV1 runPayload;

            public FakeMissionResultPort(StableId runStableId)
            {
                this.runStableId = runStableId;
            }

            public long Sequence { get; private set; }
            public int EndRunCallCount { get; private set; }

            public bool TryGetRun(
                StableId requestedRunStableId,
                out MissionRunPayloadV1 payload)
            {
                payload = requestedRunStableId == runStableId
                    ? runPayload
                    : null;
                return payload != null;
            }

            public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
                RunStrongboxCollectionRequestV1 request,
                PlayerRouteProfilePayloadV1 routePayload)
            {
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.Rejected,
                    Sequence,
                    Sequence,
                    request.OperationStableId,
                    request.Fingerprint,
                    runPayload,
                    null,
                    null,
                    "fixture-no-strongbox");
            }

            public MissionRunAuthorityResultV1 EndRun(
                EndRunSessionCommandV1 command,
                PlayerRouteProfilePayloadV1 routePayload)
            {
                EndRunCallCount++;
                long previous = Sequence;
                Sequence++;
                MissionResultPayloadV1 result = MissionResultPayloadV1.Create(
                    runStableId,
                    routePayload,
                    command.CompletionState,
                    Array.Empty<MissionRunStrongboxResultV1>(),
                    Sequence,
                    0L,
                    MissionRunCanonicalV1.Fingerprint("fixture-holdings"),
                    0L,
                    MissionRunCanonicalV1.Fingerprint("fixture-openings"));
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.RunEnded,
                    previous,
                    Sequence,
                    command.OperationStableId,
                    command.Fingerprint,
                    runPayload,
                    null,
                    result,
                    string.Empty);
            }
        }
    }
}
