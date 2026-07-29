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
    public sealed class RunSessionStateTests
    {
        [Test]
        public void StartReplayConflictAndDistinctOperationIdentityAreDeterministic()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionState(source);
            StartRunSessionCommand firstCommand = source.Command("start-a", 41L);

            RunSessionStartResult first = authority.Start(firstCommand);
            RunSessionStartResult replay = authority.Start(firstCommand);
            RunSessionStartResult conflict = authority.Start(
                source.CommandWithOperation("start-a", 99L));
            RunSessionStartResult second = authority.Start(
                source.Command("start-b", 41L));

            Assert.That(first.Status, Is.EqualTo(RunSessionStartStatus.Started));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(conflict.Status,
                Is.EqualTo(RunSessionStartStatus.ConflictingDuplicate));
            Assert.That(second.Status, Is.EqualTo(RunSessionStartStatus.Started));
            Assert.That(second.RunStableId, Is.Not.EqualTo(first.RunStableId));
            Assert.That(authority.RunCount, Is.EqualTo(2));
        }

        [Test]
        public void FrozenStatsIgnoreLaterHubChangesAndSubsequentRunSeesThem()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionState(source);
            RunSessionStartResult firstStart = authority.Start(
                source.Command("freeze-a", 7L));
            RunSessionAggregate first;
            Assert.That(authority.TryGetRun(firstStart.RunStableId, out first), Is.True);
            string firstFingerprint = first.FrozenInputs.CombatProfile.Fingerprint;
            Assert.That(first.FrozenInputs.CombatProfile.MaximumHealth,
                Is.EqualTo(100m));

            source.HubMaximumHealth = 175m;
            source.HubSkillVersion = 3L;
            RunSessionStartResult secondStart = authority.Start(
                source.Command("freeze-b", 7L));
            RunSessionAggregate second;
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
            var authority = new RunSessionState(source);
            RunSessionStartResult started = authority.Start(
                source.Command("equipment", 5L));
            RunSessionAggregate run;
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
            var authority = new RunSessionState(source);
            RunSessionStartResult firstStart = authority.Start(
                source.Command("local-a", 1L));
            RunSessionStartResult secondStart = authority.Start(
                source.Command("local-b", 2L));
            FakeLiveBundle first = source.Bundle(firstStart.RunStableId);
            FakeLiveBundle second = source.Bundle(secondStart.RunStableId);
            RunSessionAggregate firstRun;
            RunSessionAggregate secondRun;
            authority.TryGetRun(firstStart.RunStableId, out firstRun);
            authority.TryGetRun(secondStart.RunStableId, out secondRun);
            string permanentFingerprint = source.Character.Fingerprint;

            first.Player.Damage(35d);
            first.Player.MoveTo(8d, -3d);
            first.Weapons.CooldownCount = 2;
            first.Weapons.ProjectileCount = 4;
            first.StatusEffects.SetActiveEffectCount(3);
            Assert.That(firstRun.ApplyLocalMutation(new RunLocalMutationCommand(
                Id("operation.pickup-a"),
                firstRun.RunStableId,
                firstRun.LifecycleGeneration,
                RunLocalMutationKind.AddTemporaryPickup,
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
            var authority = new RunSessionState(source);
            RunSessionStartResult started = authority.Start(
                source.Command("restart-start", 17L));
            RunSessionAggregate run;
            authority.TryGetRun(started.RunStableId, out run);
            FakeLiveBundle bundle = source.Bundle(started.RunStableId);
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
            run.ApplyLocalMutation(new RunLocalMutationCommand(
                Id("operation.restart-pickup"),
                run.RunStableId,
                1L,
                RunLocalMutationKind.AddTemporaryPickup,
                "pickup.temp",
                2L,
                "pickup-before-restart"));
            run.ApplyLocalMutation(new RunLocalMutationCommand(
                Id("operation.restart-cash"),
                run.RunStableId,
                1L,
                RunLocalMutationKind.AddRunCash,
                "cash",
                25L,
                "cash-before-restart"));

            var restart = new RestartRunSessionCommand(
                Id("operation.restart-run"),
                run.RunStableId,
                1L,
                2L,
                50L,
                RunRestartPolicy.FullTransientReset());
            RunSessionRestartResult applied = run.Restart(restart);
            RunSessionRestartResult replay = run.Restart(restart);

            Assert.That(applied.Status,
                Is.EqualTo(RunSessionRestartStatus.Applied));
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

            RunSessionFactAdmissionResult stale = run.AdmitFact(
                new RunSessionFactEnvelope(
                    Id("operation.old-projectile"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKind.Projectile,
                    "old-projectile-fingerprint"));
            RunSessionFactAdmissionResult staleEffect = run.AdmitFact(
                new RunSessionFactEnvelope(
                    Id("operation.old-effect"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKind.StatusEffect,
                    "old-effect-fingerprint"));
            RunSessionFactAdmissionResult staleDamage = run.AdmitFact(
                new RunSessionFactEnvelope(
                    Id("operation.old-damage"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKind.Damage,
                    "old-damage-fingerprint"));
            RunSessionFactAdmissionResult staleCast = run.AdmitFact(
                new RunSessionFactEnvelope(
                    Id("operation.old-cast"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKind.AbilityCast,
                    "old-cast-fingerprint"));
            RunSessionFactAdmissionResult staleContact = run.AdmitFact(
                new RunSessionFactEnvelope(
                    Id("operation.old-contact"),
                    run.RunStableId,
                    1L,
                    RunSessionFactKind.Contact,
                    "old-contact-fingerprint"));
            Assert.That(stale.Status,
                Is.EqualTo(RunSessionFactAdmissionStatus.StaleLifecycle));
            Assert.That(staleEffect.Status,
                Is.EqualTo(RunSessionFactAdmissionStatus.StaleLifecycle));
            Assert.That(staleDamage.Status,
                Is.EqualTo(RunSessionFactAdmissionStatus.StaleLifecycle));
            Assert.That(staleCast.Status,
                Is.EqualTo(RunSessionFactAdmissionStatus.StaleLifecycle));
            Assert.That(staleContact.Status,
                Is.EqualTo(RunSessionFactAdmissionStatus.StaleLifecycle));
        }

        [Test]
        public void EndReplayConflictPreservesStrongboxIdentityAndAppliesNoReward()
        {
            var source = new FakeStartSource();
            var authority = new RunSessionState(source);
            RunSessionStartResult started = authority.Start(
                source.Command("end-start", 21L));
            RunSessionAggregate run;
            authority.TryGetRun(started.RunStableId, out run);
            FakeLiveBundle bundle = source.Bundle(started.RunStableId);
            StableId definition = Id("strongbox-definition.emerald");
            StableId instance = Id("strongbox-instance.exact-a");
            StableId grant = Id("grant.box-a");
            StableId sourceId = Id("source.enemy-a");

            MissionRunStateResult collected =
                run.RecordCollectedStrongbox(
                    new RunStrongboxCollectionRequest(
                        Id("operation.collect-box-a"),
                        run.RunStableId,
                        run.LifecycleGeneration,
                        definition,
                        instance,
                        grant,
                        sourceId));
            Assert.That(collected.Succeeded, Is.True, collected.RejectionCode);

            var command = new EndRunSessionCommand(
                Id("operation.end-run"),
                run.RunStableId,
                run.LifecycleGeneration,
                MissionRunCompletionState.Completed,
                100L);
            RunSessionEndResult first = run.End(command);
            RunSessionEndResult replay = run.End(command);
            RunSessionEndResult conflict = run.End(
                new EndRunSessionCommand(
                    Id("operation.end-run"),
                    run.RunStableId,
                    run.LifecycleGeneration,
                    MissionRunCompletionState.Failed,
                    100L));

            Assert.That(first.Status, Is.EqualTo(RunSessionEndStatus.Ended));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(conflict.Status,
                Is.EqualTo(RunSessionEndStatus.ConflictingDuplicate));
            Assert.That(first.Receipt.RunStableId, Is.EqualTo(run.RunStableId));
            Assert.That(first.Receipt.SelectedCharacterStableId,
                Is.EqualTo(source.Character.CharacterInstanceStableId));
            Assert.That(first.Receipt.MissionResult.Strongboxes.Count,
                Is.EqualTo(1));
            MissionRunStrongboxResult box =
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
            var authority = new RunSessionState(source);
            RunSessionStartResult started = authority.Start(
                source.Command("snapshots", 33L));
            RunSessionAggregate run;
            authority.TryGetRun(started.RunStableId, out run);

            RunHudSnapshot hudA = run.ExportHudSnapshot();
            RunHudSnapshot hudB = run.ExportHudSnapshot();
            RunDebugSnapshot debugA = run.ExportDebugSnapshot();
            RunDebugSnapshot debugB = run.ExportDebugSnapshot();
            RunRecoveryDiagnosticSnapshot recovery =
                run.ExportRecoveryDiagnostics();
            RunCheckpoint checkpoint = run.ExportCheckpoint();

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
            var authorityA = new RunSessionState(sourceA);
            var authorityB = new RunSessionState(sourceB);
            RunSessionStartResult startA = authorityA.Start(
                sourceA.Command("participant-a", 8L));
            RunSessionStartResult startB = authorityB.Start(
                sourceB.Command("participant-b", 8L));
            RunSessionAggregate runA;
            RunSessionAggregate runB;
            authorityA.TryGetRun(startA.RunStableId, out runA);
            authorityB.TryGetRun(startB.RunStableId, out runB);

            sourceA.Bundle(startA.RunStableId).Player.Damage(50d);
            runA.ApplyLocalMutation(new RunLocalMutationCommand(
                Id("operation.alpha-counter"),
                runA.RunStableId,
                runA.LifecycleGeneration,
                RunLocalMutationKind.IncrementCounter,
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

        private sealed class FakeStartSource : IRunSessionStartSource
        {
            private readonly string suffix;
            private readonly Dictionary<StableId, FakeLiveBundle> bundles =
                new Dictionary<StableId, FakeLiveBundle>();

            public FakeStartSource(string suffix = "fixture")
            {
                this.suffix = suffix;
                Character = new CharacterInstanceSnapshot(
                    Id("character-instance." + suffix),
                    Id("loadout-profile.striker"),
                    0,
                    "Pilot " + suffix,
                    4L,
                    null);
            }

            public CharacterInstanceSnapshot Character { get; }
            public decimal HubMaximumHealth { get; set; } = 100m;
            public long HubSkillVersion { get; set; }

            public StartRunSessionCommand Command(
                string operationSuffix,
                long seed)
            {
                return CommandWithOperation(operationSuffix, seed);
            }

            public StartRunSessionCommand CommandWithOperation(
                string operationSuffix,
                long seed)
            {
                return new StartRunSessionCommand(
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

            public FakeLiveBundle Bundle(StableId runStableId)
            {
                return bundles[runStableId];
            }

            public RunSessionStartMaterial Resolve(
                StartRunSessionCommand command,
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
                PlayerRouteProfilePayload route =
                    PlayerRouteProfilePayload.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[]
                        {
                            first.InstanceId,
                            second.InstanceId,
                            null,
                            null,
                        });
                var policy = DerivedStatPolicy.CreateDefault();
                var baseProfile = new CharacterBaseStatProfile(
                    "base-profile." + suffix,
                    Character.ClassDefinitionStableId.ToString(),
                    10,
                    "base-profile-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        { DerivedStatTargetIds.MaximumHealth, HubMaximumHealth },
                        { DerivedStatTargetIds.MovementSpeed, 5m },
                        { DerivedStatTargetIds.WeaponCapacity, 4m },
                        { DerivedStatTargetIds.AbilityCapacity, 0m },
                    });
                var characterInput = new DerivedCharacterStatInput(
                    Character.CharacterInstanceStableId.ToString(),
                    baseProfile,
                    null,
                    policy);
                var composer = new DefaultDerivedCharacterStatComposer();
                DerivedCharacterStatsSnapshot characterStats =
                    composer.DeriveCharacter(characterInput);
                RunCombatProfile profile = composer.BuildRunProfile(
                    new RunCombatProfileInput(
                        resolvedRunStableId.ToString(),
                        command.Fingerprint,
                        characterStats,
                        null,
                        null,
                        policy));
                var skill = new RankedSkillAllocationSnapshot(
                    "skill-profile." + suffix,
                    Character.ClassDefinitionStableId.ToString(),
                    HubSkillVersion,
                    "1",
                    "fixture",
                    null);
                var frozen = new FrozenCharacterRunInputs(
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
                        new FrozenRunEquipment(
                            Id("weapon-slot.slot-1"),
                            first,
                            definition),
                        new FrozenRunEquipment(
                            Id("weapon-slot.slot-2"),
                            second,
                            definition),
                    },
                    command.EventModifierContextFingerprint);
                var bundle = new FakeLiveBundle(
                    resolvedRunStableId,
                    Character,
                    frozen);
                bundles.Add(resolvedRunStableId, bundle);
                return RunSessionStartMaterial.Accept(
                    frozen,
                    bundle.Ports);
            }
        }

        private sealed class FakeLiveBundle
        {
            public FakeLiveBundle(
                StableId runStableId,
                CharacterInstanceSnapshot character,
                FrozenCharacterRunInputs frozen)
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
                Ports = new RunSessionLivePorts(
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
            public RunSessionLivePorts Ports { get; }
        }

        private abstract class FakeLifecyclePort : IRunLifecycleLivePort
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

            public virtual RunLivePortRestartResult Restart(
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
                    return new RunLivePortRestartResult(
                        false,
                        rejection,
                        Generation,
                        SnapshotFingerprint);
                }
                Generation = replacementLifecycleGeneration;
                TransientCount = 0;
                return new RunLivePortRestartResult(
                    true,
                    string.Empty,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakePlayerPort : FakeLifecyclePort,
            IRunPlayerLivePort
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

            public void MoveTo(double nextX, double nextY)
            {
                x = nextX;
                y = nextY;
            }

            public RunPlayerLiveSnapshot ExportSnapshot()
            {
                return new RunPlayerLiveSnapshot(
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

            public override RunLivePortRestartResult Restart(
                StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                RunLivePortRestartResult result = base.Restart(
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
                return new RunLivePortRestartResult(
                    result.Succeeded,
                    result.RejectionCode,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakeWeaponPort : FakeLifecyclePort,
            IRunWeaponLivePort
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

            public override RunLivePortRestartResult Restart(
                StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                RunLivePortRestartResult result = base.Restart(
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
                return new RunLivePortRestartResult(
                    result.Succeeded,
                    result.RejectionCode,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakeStatusEffectPort : FakeLifecyclePort,
            IRunStatusEffectLivePort
        {
            public FakeStatusEffectPort(long generation)
                : base("status-effect-runtime", generation)
            {
            }

            public int ActiveEffectCount { get { return TransientCount; } }
            public void SetActiveEffectCount(int count) { TransientCount = count; }
        }

        private sealed class FakeConditionalPort : FakeLifecyclePort,
            IRunConditionalFactLivePort
        {
            public FakeConditionalPort(long generation)
                : base("conditional-runtime", generation) { }
        }

        private sealed class FakeAbilityPort : FakeLifecyclePort,
            IRunActiveAbilityLivePort
        {
            public FakeAbilityPort(long generation)
                : base("ability-runtime-placeholder", generation) { }
        }

        private sealed class FakeRoomPort : FakeLifecyclePort,
            IRunRoomLivePort
        {
            public FakeRoomPort(long generation)
                : base("room-runtime", generation)
            {
                CurrentRoomStableId = Id("room.start");
            }

            public StableId CurrentRoomStableId { get; private set; }
        }

        private sealed class FakeMissionResultPort : IRunMissionResultPort
        {
            private readonly StableId runStableId;
            private readonly List<MissionRunStrongboxCollection> collections =
                new List<MissionRunStrongboxCollection>();
            private MissionRunPayload runPayload;

            public FakeMissionResultPort(StableId runStableId)
            {
                this.runStableId = runStableId;
            }

            public long Sequence { get; private set; }
            public int PermanentRewardApplyCount { get; private set; }

            public bool TryGetRun(
                StableId requestedRunStableId,
                out MissionRunPayload payload)
            {
                payload = requestedRunStableId == runStableId
                    ? runPayload
                    : null;
                return payload != null;
            }

            public MissionRunStateResult RecordCollectedStrongbox(
                RunStrongboxCollectionRequest request,
                PlayerRouteProfilePayload routePayload)
            {
                long previous = Sequence;
                string holdingsFingerprint =
                    MissionRun.Fingerprint("fixture-holdings");
                var collection = new MissionRunStrongboxCollection(
                    request.DefinitionStableId,
                    request.InstanceStableId,
                    request.GrantStableId,
                    request.SourceStableId,
                    request.OperationStableId,
                    0L,
                    holdingsFingerprint);
                collections.Add(collection);
                Sequence++;
                runPayload = MissionRunPayload.Create(
                    runStableId,
                    routePayload,
                    collections,
                    Sequence);
                return new MissionRunStateResult(
                    MissionRunStateStatus.StrongboxCollected,
                    previous,
                    Sequence,
                    request.OperationStableId,
                    request.Fingerprint,
                    runPayload,
                    collection,
                    null,
                    string.Empty);
            }

            public MissionRunStateResult EndRun(
                EndRunSessionCommand command,
                PlayerRouteProfilePayload routePayload)
            {
                long previous = Sequence;
                Sequence++;
                var boxes = collections.Select(collection =>
                    new MissionRunStrongboxResult(
                        collection,
                        MissionRunStrongboxState.Unopened,
                        null,
                        null)).ToList();
                MissionResultPayload result = MissionResultPayload.Create(
                    runStableId,
                    routePayload,
                    command.CompletionState,
                    boxes,
                    Sequence,
                    0L,
                    MissionRun.Fingerprint("fixture-holdings"),
                    0L,
                    MissionRun.Fingerprint("fixture-openings"));
                return new MissionRunStateResult(
                    MissionRunStateStatus.RunEnded,
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
