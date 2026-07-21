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
    public sealed class RunSessionAuthorityV1Tests
    {
        [Test]
        public void StartReplayConflictAndDistinctOperationIdentityAreDeterministic()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionAuthorityV1(source);
            StartRunSessionCommandV1 firstCommand = source.Command("start-a", 41L);

            RunSessionStartResultV1 first = authority.Start(firstCommand);
            RunSessionStartResultV1 replay = authority.Start(firstCommand);
            RunSessionStartResultV1 conflict = authority.Start(
                source.CommandWithOperation("start-a", 99L));
            RunSessionStartResultV1 second = authority.Start(
                source.Command("start-b", 41L));

            Assert.That(first.Status, Is.EqualTo(RunSessionStartStatusV1.Started));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(conflict.Status,
                Is.EqualTo(RunSessionStartStatusV1.ConflictingDuplicate));
            Assert.That(second.Status, Is.EqualTo(RunSessionStartStatusV1.Started));
            Assert.That(second.RunStableId, Is.Not.EqualTo(first.RunStableId));
            Assert.That(authority.RunCount, Is.EqualTo(2));
        }

        [Test]
        public void FrozenStatsIgnoreLaterHubChangesAndSubsequentRunSeesThem()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 firstStart = authority.Start(
                source.Command("freeze-a", 7L));
            RunSessionAggregateV1 first;
            Assert.That(authority.TryGetRun(firstStart.RunStableId, out first), Is.True);
            string firstFingerprint = first.FrozenInputs.CombatProfile.Fingerprint;
            Assert.That(first.FrozenInputs.CombatProfile.MaximumHealth,
                Is.EqualTo(100m));

            source.HubMaximumHealth = 175m;
            source.HubSkillVersion = 3L;
            RunSessionStartResultV1 secondStart = authority.Start(
                source.Command("freeze-b", 7L));
            RunSessionAggregateV1 second;
            Assert.That(authority.TryGetRun(secondStart.RunStableId, out second), Is.True);

            Assert.That(first.FrozenInputs.CombatProfile.MaximumHealth,
                Is.EqualTo(100m));
            Assert.That(first.FrozenInputs.CombatProfile.Fingerprint,
                Is.EqualTo(firstFingerprint));
            Assert.That(first.FrozenInputs.SkillSnapshot.Version, Is.EqualTo(0L));
            Assert.That(second.FrozenInputs.CombatProfile.MaximumHealth,
                Is.EqualTo(175m));
            Assert.That(second.FrozenInputs.SkillSnapshot.Version, Is.EqualTo(3L));
            Assert.That(second.FrozenInputs.Fingerprint,
                Is.Not.EqualTo(first.FrozenInputs.Fingerprint));
        }

        [Test]
        public void ExactEquipmentInstancesStayDistinctWhenDefinitionsMatch()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 started = authority.Start(
                source.Command("equipment", 5L));
            RunSessionAggregateV1 run;
            Assert.That(authority.TryGetRun(started.RunStableId, out run), Is.True);

            Assert.That(run.FrozenInputs.Equipment.Count, Is.EqualTo(2));
            Assert.That(
                run.FrozenInputs.Equipment[0].EquipmentDefinitionStableId,
                Is.EqualTo(
                    run.FrozenInputs.Equipment[1].EquipmentDefinitionStableId));
            Assert.That(
                run.FrozenInputs.Equipment[0].EquipmentInstanceStableId,
                Is.Not.EqualTo(
                    run.FrozenInputs.Equipment[1].EquipmentInstanceStableId));
            Assert.That(
                run.RuntimePorts.Weapons.FrozenEquipmentInstanceStableIds,
                Is.EquivalentTo(run.FrozenInputs.Equipment.Select(
                    item => item.EquipmentInstanceStableId)));
        }

        [Test]
        public void HealthCooldownEffectsPositionAndPickupsAreRunLocal()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 firstStart = authority.Start(
                source.Command("local-a", 1L));
            RunSessionStartResultV1 secondStart = authority.Start(
                source.Command("local-b", 2L));
            FakeRuntimeBundle first = source.Bundle(firstStart.RunStableId);
            FakeRuntimeBundle second = source.Bundle(secondStart.RunStableId);
            RunSessionAggregateV1 firstRun;
            RunSessionAggregateV1 secondRun;
            authority.TryGetRun(firstStart.RunStableId, out firstRun);
            authority.TryGetRun(secondStart.RunStableId, out secondRun);
            string permanentFingerprint = source.Character.Fingerprint;

            first.Player.Damage(35d);
            first.Player.MoveTo(8d, -3d);
            first.Weapons.CooldownCount = 2;
            first.Weapons.ProjectileCount = 4;
            first.StatusEffects.SetActiveEffectCount(3);
            Assert.That(firstRun.ApplyLocalMutation(new RunLocalMutationCommandV1(
                Id("operation.pickup-a"),
                firstRun.RunStableId,
                firstRun.LifecycleGeneration,
                RunLocalMutationKindV1.AddTemporaryPickup,
                "pickup.medkit",
                1L,
                "pickup-fact-a")).Accepted, Is.True);

            Assert.That(firstRun.ExportHudSnapshot().CurrentHealth, Is.EqualTo(65d));
            Assert.That(first.Player.ExportSnapshot().PositionX, Is.EqualTo(8d));
            Assert.That(first.Weapons.CooldownCount, Is.EqualTo(2));
            Assert.That(first.StatusEffects.ActiveEffectCount, Is.EqualTo(3));
            Assert.That(firstRun.ExportLocalState().TemporaryPickups["pickup.medkit"],
                Is.EqualTo(1L));

            Assert.That(secondRun.ExportHudSnapshot().CurrentHealth, Is.EqualTo(100d));
            Assert.That(second.Player.ExportSnapshot().PositionX, Is.EqualTo(0d));
            Assert.That(second.Weapons.CooldownCount, Is.EqualTo(0));
            Assert.That(second.StatusEffects.ActiveEffectCount, Is.EqualTo(0));
            Assert.That(secondRun.ExportLocalState().TemporaryPickups, Is.Empty);
            Assert.That(source.Character.Fingerprint, Is.EqualTo(permanentFingerprint));
            Assert.That(source.Character.Revision, Is.EqualTo(4L));
        }

        [Test]
        public void RestartPreservesRunIdentityAdvancesGenerationAndClearsTransientState()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 started = authority.Start(
                source.Command("restart-start", 17L));
            RunSessionAggregateV1 run;
            authority.TryGetRun(started.RunStableId, out run);
            FakeRuntimeBundle bundle = source.Bundle(started.RunStableId);
            StableId originalRunId = run.RunStableId;

            bundle.Player.Damage(80d);
            bundle.Player.MoveTo(12d, 9d);
            bundle.Weapons.CooldownCount = 2;
            bundle.Weapons.ProjectileCount = 8;
            bundle.Weapons.AttackIntentCount = 2;
            bundle.Weapons.ContactOperationCount = 1;
            bundle.StatusEffects.SetActiveEffectCount(4);
            bundle.ConditionalFacts.TransientCount = 3;
            bundle.Rooms.TransientCount = 2;
            run.ApplyLocalMutation(new RunLocalMutationCommandV1(
                Id("operation.restart-pickup"),
                run.RunStableId,
                1L,
                RunLocalMutationKindV1.AddTemporaryPickup,
                "pickup.temp",
                2L,
                "pickup-before-restart"));
            run.ApplyLocalMutation(new RunLocalMutationCommandV1(
                Id("operation.restart-cash"),
                run.RunStableId,
                1L,
                RunLocalMutationKindV1.AddRunCash,
                "cash",
                25L,
                "cash-before-restart"));

            var restart = new RestartRunSessionCommandV1(
                Id("operation.restart-run"),
                run.RunStableId,
                1L,
                2L,
                50L,
                RunRestartPolicyV1.FullTransientReset());
            RunSessionRestartResultV1 applied = run.Restart(restart);
            RunSessionRestartResultV1 replay = run.Restart(restart);

            Assert.That(applied.Status,
                Is.EqualTo(RunSessionRestartStatusV1.Applied));
            Assert.That(replay, Is.SameAs(applied));
            Assert.That(run.RunStableId, Is.EqualTo(originalRunId));
            Assert.That(run.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(bundle.Player.ExportSnapshot().CurrentHealth, Is.EqualTo(100d));
            Assert.That(bundle.Player.ExportSnapshot().PositionX, Is.EqualTo(0d));
            Assert.That(bundle.Weapons.CooldownCount, Is.EqualTo(0));
            Assert.That(bundle.Weapons.ProjectileCount, Is.EqualTo(0));
            Assert.That(bundle.Weapons.AttackIntentCount, Is.EqualTo(0));
            Assert.That(bundle.Weapons.ContactOperationCount, Is.EqualTo(0));
            Assert.That(bundle.StatusEffects.ActiveEffectCount, Is.EqualTo(0));
            Assert.That(bundle.ConditionalFacts.TransientCount, Is.EqualTo(0));
            Assert.That(bundle.Rooms.TransientCount, Is.EqualTo(0));
            Assert.That(run.ExportLocalState().TemporaryPickups, Is.Empty);
            Assert.That(run.ExportLocalState().RunCash, Is.EqualTo(0L));

            RunSessionFactAdmissionResultV1 stale = run.AdmitFact(
                new RunSessionFactEnvelopeV1(
                    Id("operation.old-projectile"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKindV1.Projectile,
                    "old-projectile-fingerprint"));
            RunSessionFactAdmissionResultV1 staleEffect = run.AdmitFact(
                new RunSessionFactEnvelopeV1(
                    Id("operation.old-effect"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKindV1.StatusEffect,
                    "old-effect-fingerprint"));
            RunSessionFactAdmissionResultV1 staleDamage = run.AdmitFact(
                new RunSessionFactEnvelopeV1(
                    Id("operation.old-damage"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKindV1.Damage,
                    "old-damage-fingerprint"));
            RunSessionFactAdmissionResultV1 staleCast = run.AdmitFact(
                new RunSessionFactEnvelopeV1(
                    Id("operation.old-cast"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKindV1.AbilityCast,
                    "old-cast-fingerprint"));
            RunSessionFactAdmissionResultV1 staleContact = run.AdmitFact(
                new RunSessionFactEnvelopeV1(
                    Id("operation.old-contact"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKindV1.Contact,
                    "old-contact-fingerprint"));
            Assert.That(stale.Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.StaleLifecycle));
            Assert.That(staleEffect.Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.StaleLifecycle));
            Assert.That(staleDamage.Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.StaleLifecycle));
            Assert.That(staleCast.Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.StaleLifecycle));
            Assert.That(staleContact.Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.StaleLifecycle));
        }

        [Test]
        public void EndReplayConflictPreservesStrongboxIdentityAndAppliesNoReward()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 started = authority.Start(
                source.Command("end-start", 21L));
            RunSessionAggregateV1 run;
            authority.TryGetRun(started.RunStableId, out run);
            FakeRuntimeBundle bundle = source.Bundle(started.RunStableId);
            StableId definition = Id("strongbox-definition.emerald");
            StableId instance = Id("strongbox-instance.exact-a");
            StableId grant = Id("grant.box-a");
            StableId sourceId = Id("source.enemy-a");

            MissionRunAuthorityResultV1 collected =
                run.RecordCollectedStrongbox(
                    new RunStrongboxCollectionRequestV1(
                        Id("operation.collect-box-a"),
                        run.RunStableId,
                        run.LifecycleGeneration,
                        definition,
                        instance,
                        grant,
                        sourceId));
            Assert.That(collected.Succeeded, Is.True, collected.RejectionCode);

            var command = new EndRunSessionCommandV1(
                Id("operation.end-run"),
                run.RunStableId,
                run.LifecycleGeneration,
                MissionRunCompletionStateV1.Completed,
                100L);
            RunSessionEndResultV1 first = run.End(command);
            RunSessionEndResultV1 replay = run.End(command);
            RunSessionEndResultV1 conflict = run.End(
                new EndRunSessionCommandV1(
                    Id("operation.end-run"),
                    run.RunStableId,
                    run.LifecycleGeneration,
                    MissionRunCompletionStateV1.Failed,
                    100L));

            Assert.That(first.Status, Is.EqualTo(RunSessionEndStatusV1.Ended));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(conflict.Status,
                Is.EqualTo(RunSessionEndStatusV1.ConflictingDuplicate));
            Assert.That(first.Receipt.RunStableId, Is.EqualTo(run.RunStableId));
            Assert.That(first.Receipt.SelectedCharacterStableId,
                Is.EqualTo(source.Character.CharacterInstanceStableId));
            Assert.That(first.Receipt.MissionResult.Strongboxes.Count,
                Is.EqualTo(1));
            MissionRunStrongboxResultV1 box =
                first.Receipt.MissionResult.Strongboxes[0];
            Assert.That(box.DefinitionStableId, Is.EqualTo(definition));
            Assert.That(box.InstanceStableId, Is.EqualTo(instance));
            Assert.That(box.Collection.GrantStableId, Is.EqualTo(grant));
            Assert.That(box.Collection.SourceStableId, Is.EqualTo(sourceId));
            Assert.That(bundle.MissionResults.PermanentRewardApplyCount,
                Is.EqualTo(0));
        }

        [Test]
        public void SnapshotsAreDeterministicAndCheckpointCannotBecomePermanentTruth()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 started = authority.Start(
                source.Command("snapshots", 33L));
            RunSessionAggregateV1 run;
            authority.TryGetRun(started.RunStableId, out run);

            RunHudSnapshotV1 hudA = run.ExportHudSnapshot();
            RunHudSnapshotV1 hudB = run.ExportHudSnapshot();
            RunDebugSnapshotV1 debugA = run.ExportDebugSnapshot();
            RunDebugSnapshotV1 debugB = run.ExportDebugSnapshot();
            RunRecoveryDiagnosticSnapshotV1 recovery =
                run.ExportRecoveryDiagnostics();
            RunCheckpointV1 checkpoint = run.ExportCheckpoint();

            Assert.That(hudB.Fingerprint, Is.EqualTo(hudA.Fingerprint));
            Assert.That(debugB.Fingerprint, Is.EqualTo(debugA.Fingerprint));
            Assert.That(recovery.IsPermanentCharacterTruth, Is.False);
            Assert.That(checkpoint.Recovery.IsPermanentCharacterTruth, Is.False);
            Assert.That(checkpoint.Fingerprint, Is.Not.Empty);
            Assert.That(run.FrozenInputs.Character.Fingerprint,
                Is.EqualTo(source.Character.Fingerprint));
        }

        [Test]
        public void TwoParticipantsAndRunsDoNotLeakState()
        {
            var sourceA = new FakeStartSource("alpha");
            var sourceB = new FakeStartSource("bravo");
            var authorityA = new RunSessionAuthorityV1(sourceA);
            var authorityB = new RunSessionAuthorityV1(sourceB);
            RunSessionStartResultV1 startA = authorityA.Start(
                sourceA.Command("participant-a", 8L));
            RunSessionStartResultV1 startB = authorityB.Start(
                sourceB.Command("participant-b", 8L));
            RunSessionAggregateV1 runA;
            RunSessionAggregateV1 runB;
            authorityA.TryGetRun(startA.RunStableId, out runA);
            authorityB.TryGetRun(startB.RunStableId, out runB);

            sourceA.Bundle(startA.RunStableId).Player.Damage(50d);
            runA.ApplyLocalMutation(new RunLocalMutationCommandV1(
                Id("operation.alpha-counter"),
                runA.RunStableId,
                runA.LifecycleGeneration,
                RunLocalMutationKindV1.IncrementCounter,
                "kills",
                9L,
                "alpha-counter-fact"));

            Assert.That(runA.ExportHudSnapshot().CurrentHealth, Is.EqualTo(50d));
            Assert.That(runB.ExportHudSnapshot().CurrentHealth, Is.EqualTo(100d));
            Assert.That(runA.ExportLocalState().Counters["kills"], Is.EqualTo(9L));
            Assert.That(runB.ExportLocalState().Counters, Is.Empty);
            Assert.That(runA.ExportHudSnapshot().ParticipantStableId,
                Is.Not.EqualTo(runB.ExportHudSnapshot().ParticipantStableId));
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private sealed class FakeStartSource : IRunSessionStartSourceV1
        {
            private readonly string suffix;
            private readonly Dictionary<StableId, FakeRuntimeBundle> bundles =
                new Dictionary<StableId, FakeRuntimeBundle>();

            public FakeStartSource(string suffix = "fixture")
            {
                this.suffix = suffix;
                Character = new CharacterInstanceSnapshotV1(
                    Id("character-instance." + suffix),
                    Id("loadout-profile.striker"),
                    0,
                    "Pilot " + suffix,
                    4L,
                    null);
            }

            public CharacterInstanceSnapshotV1 Character { get; }
            public decimal HubMaximumHealth { get; set; } = 100m;
            public long HubSkillVersion { get; set; }

            public StartRunSessionCommandV1 Command(
                string operationSuffix,
                long seed)
            {
                return CommandWithOperation(operationSuffix, seed);
            }

            public StartRunSessionCommandV1 CommandWithOperation(
                string operationSuffix,
                long seed)
            {
                return new StartRunSessionCommandV1(
                    Id("operation." + operationSuffix),
                    null,
                    "fixture-run-material-" + operationSuffix,
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
                StartRunSessionCommandV1 command,      }

        StableId resolvedRunStableId)
            {
                StableId definitionId = Id("equipment-definition.test-rifle");
                StableId qualityId = Id("quality.common");
                EquipmentDefinition definition = EquipmentDefinition.Create(
                    definitionId,
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.test-rifle"),
                    "Test Rifle",
                    Id("weapon.test-rifle"),
                    InclusiveIntRange.Create(1, 100),
                    2,
                    new[]
                    {
                        EquipmentQualityTier.Create(qualityId, "Common", 1),
                    },
                    null);
                EquipmentInstance first = EquipmentInstance.Create(
                    Id("equipment-instance." + suffix + "-a"),
                    definitionId,
                    10,
                    qualityId,
                    null);
                EquipmentInstance second = EquipmentInstance.Create(
                    Id("equipment-instance." + suffix + "-b"),
                    definitionId,
                    11,
                    qualityId,
                    null);
                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[]
                        {
                            first.InstanceId,
                            second.InstanceId,
                            null,
                            null,
                        });
                var policy = DerivedStatPolicyV1.CreateDefault();
                var baseProfile = new CharacterBaseStatProfileV1(
                    "base-profile." + suffix,
                    Character.ClassDefinitionStableId.ToString(),
                    10,
                    "base-profile-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        { DerivedStatTargetIdsV1.MaximumHealth, HubMaximumHealth },
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
                    "skill-profile." + suffix,
                    Character.ClassDefinitionStableId.ToString(),
                    HubSkillVersion,
                    "1",
                    "fixture",
                    null);
                var frozen = new FrozenCharacterRunInputsV1(
                    Character,
                    route,
                    0L,
                    "loadout-fingerprint-" + suffix,
                    0L,
                    "holdings-fingerprint-" + suffix,
                    skill,
                    characterStats,
                    profile,
                    new[]
                    {
                        new FrozenRunEquipmentV1(
                            Id("weapon-slot.slot-1"),
                            first,
                            definition),
                        new FrozenRunEquipmentV1(
                            Id("weapon-slot.slot-2"),
                            second,
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
            public FakeRuntimeBundle(      }

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

            public virtual RunRuntimePortRestartResultV1 Restart(      }

        StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                        string r = ValidateRestart(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
                if (!string.IsNullOrEmpty( string r))
                {
                    return new RunRuntimePortRestartResultV1(
                        false,
                        retring r,
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

            public FakePlayerPort(      }

        StableId actorId,      }

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

            public void MoveTo(double nextX, double nextY)
            {
                x = nextX;
                y = nextY;
            }

            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
                    actorId,      }

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

            public override RunRuntimePortRestartResultV1 Restart(      }

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

            public int CooldownCount { get; set; }
            public int ProjectileCount { get; set; }
            public int AttackIntentCount { get; set; }
            public int ContactOperationCount { get; set; }
            public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
            {
                get { return equipmentIds; }
            }

            public override string SnapshotFingerprint
            {
                get
                {
                    return base.SnapshotFingerprint
                        + "|"
                        + CooldownCount
                        + "|"
                        + ProjectileCount
                        + "|"
                        + AttackIntentCount
                        + "|"
                        + ContactOperationCount;
                }
            }

            public override RunRuntimePortRestartResultV1 Restart(      }

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
                    CooldownCount = 0;
                    ProjectileCount = 0;
                    AttackIntentCount = 0;
                    ContactOperationCount = 0;
                }
                return new RunRuntimePortRestartResultV1(
                    result.Succeeded,
                    result.RejectionCode,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakeStatusEffectPort : FakeLifecyclePort,
            IRunStatusEffectRuntimePortV1
        {
            public FakeStatusEffectPort(long generation)
                : base("status-effect-runtime", generation)
            {
            }

            public int ActiveEffectCount { get { return TransientCount; } }
            public void SetActiveEffectCount(int count) { TransientCount = count; }
        }

        private sealed class FakeConditionalPort : FakeLifecyclePort,
            IRunConditionalFactRuntimePortV1
        {
            public FakeConditionalPort(long generation)
                : base("conditional-runtime", generation) { }
        }

        private sealed class FakeAbilityPort : FakeLifecyclePort,
            IRunActiveAbilityRuntimePortV1
        {
            public FakeAbilityPort(long generation)
                : base("ability-runtime-placeholder", generation) { }
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
            private readonly List<MissionRunStrongboxCollectionV1> collections =
                new List<MissionRunStrongboxCollectionV1>();
            private MissionRunPayloadV1 runPayload;

            public FakeMissionResultPort(StableId runStableId)
            {
                this.runStableId = runStableId;
            }

            public long Sequence { get; private set; }
            public int PermanentRewardApplyCount { get; private set; }

            public bool TryGetRun(      }

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
                long previous = Sequence;
                       holdingsFingerprint =
                    MissionRunCanonicalV1.Fingerprint("fixture-holdings");
                var collection = new MissionRunStrongboxCollectionV1(
                    request.DefinitionStableId,
                    request.InstanceStableId,
                    request.GrantStableId,
                    request.SourceStableId,
                    request.OperationStableId,
                    0L,
                    holdingsFingerprint);
                collections.Add(collection);
                Sequence++;
                runPayload = MissionRunPayloadV1.Create(
                    runStableId,
                    routePayload,
                    collections,
                    Sequence);
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.StrongboxCollected,
                    previous,
                    Sequence,
                    request.OperationStableId,
                    request.Fingerprint,
                    runPayload,
                    collection,
                    null,
                    string.Empty);
            }

            public MissionRunAuthorityResultV1 EndRun(
                EndRunSessionCommandV1 command,      }

        PlayerRouteProfilePayloadV1 routePayload)
            {
                long previous = Sequence;
                Sequence++;
                var boxes = collections.Select(collection =>
                    new MissionRunStrongboxResultV1(
                        collection,
                        MissionRunStrongboxStateV1.Unopened,
                        null,
                        null)).ToList();
                MissionResultPayloadV1 result = MissionResultPayloadV1.Create(
                    runStableId,
                    routePayload,
                    command.CompletionState,
                    boxes,
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
