using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Combat.CriticalHits;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Tests.EditMode.CriticalHits
{
    public sealed class CriticalHitResolutionV1Tests
    {
        private readonly CombatHitPolicyV1 hitPolicy =
            new CombatHitPolicyV1(CombatHitPolicyRegistryV1.CreateDefault());

        [Test]
        public void IdenticalImmutableHitFacts_ProduceIdenticalResolvedFingerprint()
        {
            RunCombatProfileV1 profile = Profile(
                "player-a",
                0.42m,
                2.25m,
                1.10m);
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 3L),
                Actor("target-a", "enemies", "enemy-a", 8L),
                "effect-a",
                CombatEffectGeometryKindV1.Projectile);
            CriticalHitResolutionCommandV1 firstCommand = Command(
                "operation-a",
                "run-seed-991",
                7L,
                40m,
                profile,
                hit);
            CriticalHitResolutionCommandV1 secondCommand = Command(
                "operation-a",
                "run-seed-991",
                7L,
                40m,
                profile,
                AcceptedHit(
                    Actor("source-a", "players", "player-a", 3L),
                    Actor("target-a", "enemies", "enemy-a", 8L),
                    "effect-a",
                    CombatEffectGeometryKindV1.Projectile));

            CriticalHitResolutionResultV1 first =
                new CriticalHitResolutionAuthorityV1().Resolve(firstCommand);
            CriticalHitResolutionResultV1 second =
                new CriticalHitResolutionAuthorityV1().Resolve(secondCommand);

            Assert.That(first.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(second.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(
                second.ResolvedDamage.IsCritical,
                Is.EqualTo(first.ResolvedDamage.IsCritical));
            Assert.That(
                second.ResolvedDamage.RollSample,
                Is.EqualTo(first.ResolvedDamage.RollSample));
            Assert.That(
                second.ResolvedDamage.FinalDamage,
                Is.EqualTo(first.ResolvedDamage.FinalDamage));
            Assert.That(
                second.ResolvedDamage.Fingerprint,
                Is.EqualTo(first.ResolvedDamage.Fingerprint));
        }

        [Test]
        public void SourceTargetEffectSequenceAndSeed_ChangeRollDomain()
        {
            RunCombatProfileV1 profile = Profile(
                "player-a",
                0.5m,
                2m,
                1m);
            CombatActorSnapshotV1 source =
                Actor("source-a", "players", "player-a", 1L);
            CombatActorSnapshotV1 target =
                Actor("target-a", "enemies", "enemy-a", 1L);
            CriticalHitResolutionCommandV1 baselineCommand = Command(
                "operation-domain",
                "seed-a",
                10L,
                20m,
                profile,
                AcceptedHit(
                    source,
                    target,
                    "effect-a",
                    CombatEffectGeometryKindV1.Projectile));
            CriticalHitResolutionResultV1 baseline =
                new CriticalHitResolutionAuthorityV1().Resolve(baselineCommand);

            var variants = new[]
            {
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profile,
                    AcceptedHit(
                        Actor("source-b", "players", "player-a", 1L),
                        target,
                        "effect-a",
                        CombatEffectGeometryKindV1.Projectile)),
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profile,
                    AcceptedHit(
                        source,
                        Actor("target-b", "enemies", "enemy-b", 1L),
                        "effect-a",
                        CombatEffectGeometryKindV1.Projectile)),
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profile,
                    AcceptedHit(
                        source,
                        target,
                        "effect-b",
                        CombatEffectGeometryKindV1.Projectile)),
                Command(
                    "operation-domain",
                    "seed-a",
                    11L,
                    20m,
                    profile,
                    AcceptedHit(
                        source,
                        target,
                        "effect-a",
                        CombatEffectGeometryKindV1.Projectile)),
                Command(
                    "operation-domain",
                    "seed-b",
                    10L,
                    20m,
                    profile,
                    AcceptedHit(
                        source,
                        target,
                        "effect-a",
                        CombatEffectGeometryKindV1.Projectile)),
            };

            Assert.That(baseline.ResolvedDamage, Is.Not.Null);
            foreach (CriticalHitResolutionCommandV1 variantCommand in variants)
            {
                CriticalHitResolutionResultV1 variant =
                    new CriticalHitResolutionAuthorityV1().Resolve(
                        variantCommand);
                Assert.That(variant.ResolvedDamage, Is.Not.Null);
                Assert.That(
                    variant.ResolvedDamage.RollDomainFingerprint,
                    Is.Not.EqualTo(
                        baseline.ResolvedDamage.RollDomainFingerprint));
            }
        }

        [Test]
        public void DuplicateAndConflictingOperations_DoNotResolveTwice()
        {
            RunCombatProfileV1 profile = Profile(
                "player-a",
                0.5m,
                2m,
                1m);
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 2L),
                Actor("target-a", "enemies", "enemy-a", 5L),
                "effect-a",
                CombatEffectGeometryKindV1.Projectile);
            CriticalHitResolutionCommandV1 command = Command(
                "operation-replay",
                "seed-a",
                4L,
                25m,
                profile,
                hit);
            var authority = new CriticalHitResolutionAuthorityV1();

            CriticalHitResolutionResultV1 applied = authority.Resolve(command);
            CriticalHitResolutionResultV1 duplicate = authority.Resolve(
                Command(
                    "operation-replay",
                    "seed-a",
                    4L,
                    25m,
                    profile,
                    hit));
            CriticalHitResolutionResultV1 conflict = authority.Resolve(
                Command(
                    "operation-replay",
                    "seed-a",
                    5L,
                    25m,
                    profile,
                    hit));

            Assert.That(applied.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Duplicate));
            Assert.That(
                duplicate.ResolvedDamage,
                Is.SameAs(applied.ResolvedDamage));
            Assert.That(conflict.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.ConflictingDuplicate));
            Assert.That(conflict.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCodeV1.ConflictingDuplicate));
            Assert.That(conflict.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void GuaranteedNonCriticalAndCriticalEdges_ResolveExactDamage()
        {
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L),
                "edge-effect",
                CombatEffectGeometryKindV1.Projectile);

            CriticalHitResolutionResultV1 ordinary =
                new CriticalHitResolutionAuthorityV1().Resolve(
                    Command(
                        "operation-never-crit",
                        "edge-seed",
                        0L,
                        40m,
                        Profile("player-a", 0m, 2.5m, 1.25m),
                        hit));
            CriticalHitResolutionResultV1 critical =
                new CriticalHitResolutionAuthorityV1().Resolve(
                    Command(
                        "operation-always-crit",
                        "edge-seed",
                        0L,
                        40m,
                        Profile("player-a", 1m, 2.5m, 1.25m),
                        hit));

            Assert.That(ordinary.ResolvedDamage.IsCritical, Is.False);
            Assert.That(ordinary.ResolvedDamage.OrdinaryDamage, Is.EqualTo(50m));
            Assert.That(ordinary.ResolvedDamage.FinalDamage, Is.EqualTo(50m));
            Assert.That(critical.ResolvedDamage.IsCritical, Is.True);
            Assert.That(critical.ResolvedDamage.OrdinaryDamage, Is.EqualTo(50m));
            Assert.That(critical.ResolvedDamage.FinalDamage, Is.EqualTo(125m));
        }

        [TestCase(CombatEffectGeometryKindV1.Projectile)]
        [TestCase(CombatEffectGeometryKindV1.Explosion)]
        [TestCase(CombatEffectGeometryKindV1.MeleeSwing)]
        [TestCase(CombatEffectGeometryKindV1.ContactAttack)]
        [TestCase(CombatEffectGeometryKindV1.PersistentField)]
        [TestCase(CombatEffectGeometryKindV1.Chain)]
        public void EverySupportedGeometry_UsesSameCriticalBoundary(
            CombatEffectGeometryKindV1 geometry)
        {
            RunCombatProfileV1 profile = Profile(
                "player-a",
                1m,
                2m,
                1m);
            CriticalHitResolutionResultV1 result =
                new CriticalHitResolutionAuthorityV1().Resolve(
                    Command(
                        "operation-geometry-" + ((int)geometry),
                        "geometry-seed",
                        (long)geometry,
                        10m,
                        profile,
                        AcceptedHit(
                            Actor(
                                "source-a",
                                "players",
                                "player-a",
                                1L),
                            Actor(
                                "target-a",
                                "enemies",
                                "enemy-a",
                                1L),
                            "effect-geometry-" + ((int)geometry),
                            geometry)));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(20m));
        }

        [Test]
        public void PermanentEventAndStatusModifiers_FlowThroughSharedProfile()
        {
            DerivedStatModifierSourceV1 permanent =
                new DerivedStatModifierSourceV1(
                    "skill.critical-training",
                    DerivedStatSourcePrioritiesV1.Skills,
                    "skill-snapshot-a",
                    new RuntimeModifierSnapshotV1(
                        new[]
                        {
                            new RuntimeModifierDefinitionV1(
                                "skill.critical-training",
                                DerivedStatTargetIdsV1.CriticalChance,
                                RuntimeModifierOperationV1.Flat,
                                0.2m),
                        }));
            DerivedStatModifierSourceV1 eventSource =
                new DerivedStatModifierSourceV1(
                    "event.damage-week",
                    DerivedStatSourcePrioritiesV1.Events,
                    "event-snapshot-a",
                    new RuntimeModifierSnapshotV1(
                        new[]
                        {
                            new RuntimeModifierDefinitionV1(
                                "event.damage-week",
                                DerivedStatTargetIdsV1.CriticalChance,
                                RuntimeModifierOperationV1.Flat,
                                0.2m),
                            new RuntimeModifierDefinitionV1(
                                "event.damage-week",
                                DerivedStatTargetIdsV1
                                    .OutgoingDamageMultiplier,
                                RuntimeModifierOperationV1.Percentage,
                                0.25m),
                        }));
            DerivedStatModifierSourceV1 statusSource =
                new DerivedStatModifierSourceV1(
                    "status.focus",
                    DerivedStatSourcePrioritiesV1.RunConditions,
                    "status-snapshot-a",
                    new RuntimeModifierSnapshotV1(
                        new[]
                        {
                            new RuntimeModifierDefinitionV1(
                                "status.focus",
                                DerivedStatTargetIdsV1.CriticalChance,
                                RuntimeModifierOperationV1.Flat,
                                0.4m,
                                "condition.focus-active"),
                            new RuntimeModifierDefinitionV1(
                                "status.focus",
                                DerivedStatTargetIdsV1.CriticalMultiplier,
                                RuntimeModifierOperationV1.Flat,
                                1m,
                                "condition.focus-active"),
                        }));
            RunCombatProfileV1 profile = Profile(
                "player-a",
                0.2m,
                1.5m,
                1m,
                new[] { permanent },
                new[] { eventSource, statusSource },
                new[] { "condition.focus-active" });

            CriticalHitResolutionResultV1 result =
                new CriticalHitResolutionAuthorityV1().Resolve(
                    Command(
                        "operation-modifiers",
                        "modifier-seed",
                        3L,
                        40m,
                        profile,
                        AcceptedHit(
                            Actor(
                                "source-a",
                                "players",
                                "player-a",
                                1L),
                            Actor(
                                "target-a",
                                "enemies",
                                "enemy-a",
                                1L),
                            "effect-modifiers",
                            CombatEffectGeometryKindV1.PersistentField)));

            Assert.That(profile.CriticalChance, Is.EqualTo(1m));
            Assert.That(profile.CriticalMultiplier, Is.EqualTo(2.5m));
            Assert.That(profile.OutgoingDamageMultiplier, Is.EqualTo(1.25m));
            Assert.That(
                result.ResolvedDamage.RunCombatProfileFingerprint,
                Is.EqualTo(profile.Fingerprint));
            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.OrdinaryDamage, Is.EqualTo(50m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(125m));
        }

        [Test]
        public void DamageAdapter_PreservesMultiplayerAttributionAndReplayIdentity()
        {
            RunCombatProfileV1 profile = Profile(
                "player-two",
                1m,
                2m,
                1m);
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-two", "players", "player-two", 6L),
                Actor("target-nine", "enemies", "enemy-nine", 12L),
                "effect-networked",
                CombatEffectGeometryKindV1.Chain);
            var authority = new CriticalHitResolutionAuthorityV1();
            CriticalHitResolutionResultV1 applied = authority.Resolve(
                Command(
                    "operation-networked",
                    "network-seed",
                    19L,
                    15m,
                    profile,
                    hit));
            CriticalHitResolutionResultV1 duplicate = authority.Resolve(
                Command(
                    "operation-networked",
                    "network-seed",
                    19L,
                    15m,
                    profile,
                    hit));

            DamageReceiverCommand firstCommand;
            DamageReceiverCommand replayCommand;
            bool firstCreated = CriticalHitDamageCommandAdapterV1.TryCreate(
                applied,
                out firstCommand);
            bool replayCreated = CriticalHitDamageCommandAdapterV1.TryCreate(
                duplicate,
                out replayCommand);

            Assert.That(firstCreated, Is.True);
            Assert.That(replayCreated, Is.True);
            Assert.That(firstCommand, Is.EqualTo(replayCommand));
            Assert.That(
                firstCommand.EventId,
                Is.EqualTo(Id("critical-operation", "operation-networked")));
            Assert.That(
                firstCommand.SourceActorId,
                Is.EqualTo(Id("actor", "source-two")));
            Assert.That(
                firstCommand.SourceRunParticipantId,
                Is.EqualTo(Id("participant", "player-two")));
            Assert.That(
                firstCommand.TargetActorId,
                Is.EqualTo(Id("actor", "target-nine")));
            Assert.That(firstCommand.LifecycleGeneration, Is.EqualTo(12L));
            Assert.That(firstCommand.Amount, Is.EqualTo(30d));
        }

        [Test]
        public void NonEligibleHit_IsRejectedWithoutConsumingOperation()
        {
            CombatActorSnapshotV1 source =
                Actor("source-a", "players", "player-a", 1L);
            CombatActorSnapshotV1 ally =
                Actor("ally-a", "players", "player-b", 1L);
            CombatHitPolicyResultV1 denied = Evaluate(
                source,
                ally,
                "effect-friendly",
                CombatEffectGeometryKindV1.Projectile);
            var authority = new CriticalHitResolutionAuthorityV1();

            CriticalHitResolutionResultV1 result = authority.Resolve(
                Command(
                    "operation-denied",
                    "seed-a",
                    0L,
                    10m,
                    Profile("player-a", 1m, 2m, 1m),
                    denied));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCodeV1.HitNotDamageEligible));
            Assert.That(result.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(0));
        }

        private CriticalHitResolutionCommandV1 Command(
            string operation,
            string seed,
            long hitSequence,
            decimal baseDamage,
            RunCombatProfileV1 profile,
            CombatHitPolicyResultV1 hit)
        {
            return new CriticalHitResolutionCommandV1(
                Id("critical-operation", operation),
                seed,
                hitSequence,
                baseDamage,
                CombatChannel.Kinetic,
                profile,
                hit);
        }

        private CombatHitPolicyResultV1 AcceptedHit(
            CombatActorSnapshotV1 source,
            CombatActorSnapshotV1 target,
            string effect,
            CombatEffectGeometryKindV1 geometry)
        {
            CombatHitPolicyResultV1 result = Evaluate(
                source,
                target,
                effect,
                geometry);
            Assert.That(result.DamageEligible, Is.True);
            return result;
        }

        private CombatHitPolicyResultV1 Evaluate(
            CombatActorSnapshotV1 source,
            CombatActorSnapshotV1 target,
            string effect,
            CombatEffectGeometryKindV1 geometry)
        {
            CombatEffectSnapshotV1 snapshot = new CombatEffectSnapshotV1(
                Id("effect", effect),
                CombatHitPolicyIdsV1.PlayerNormal,
                source.ActorId,
                source.LifecycleGeneration,
                geometry,
                CombatWorldBlockerBehaviorV1.Terminate,
                false,
                false,
                0,
                1);
            return hitPolicy.Evaluate(
                new CombatHitPolicyInputV1(
                    source,
                    snapshot,
                    CombatHitContactV1.Actor(
                        target,
                        target.LifecycleGeneration,
                        1d),
                    CombatHitHistorySnapshotV1.Empty(snapshot.EffectId)));
        }

        private static CombatActorSnapshotV1 Actor(
            string actor,
            string faction,
            string participant,
            long generation)
        {
            GameplayEntityOwnership ownership = participant == null
                ? GameplayEntityOwnership.None()
                : GameplayEntityOwnership.Create(
                    Id("participant", participant),
                    Id("character", participant));
            GameplayEntityIdentity identity = new GameplayEntityIdentity(
                Id("actor", actor),
                ownership,
                Id("faction", faction));
            return CombatActorSnapshotFactoryV1.CreateDamageReceiver(
                identity,
                generation,
                true);
        }

        private static RunCombatProfileV1 Profile(
            string character,
            decimal criticalChance,
            decimal criticalMultiplier,
            decimal outgoingDamageMultiplier,
            IEnumerable<DerivedStatModifierSourceV1> permanentSources = null,
            IEnumerable<DerivedStatModifierSourceV1> runSources = null,
            IEnumerable<string> activeConditionIds = null)
        {
            DerivedStatPolicyV1 policy = DerivedStatPolicyV1.CreateDefault();
            var composer = new DefaultDerivedCharacterStatComposerV1();
            CharacterBaseStatProfileV1 baseProfile =
                new CharacterBaseStatProfileV1(
                    "fixture." + character,
                    "class.fixture",
                    10,
                    "fixture-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        {
                            DerivedStatTargetIdsV1.MaximumHealth,
                            100m
                        },
                        {
                            DerivedStatTargetIdsV1.MovementSpeed,
                            5m
                        },
                        {
                            DerivedStatTargetIdsV1.CriticalChance,
                            criticalChance
                        },
                        {
                            DerivedStatTargetIdsV1.CriticalMultiplier,
                            criticalMultiplier
                        },
                        {
                            DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                            outgoingDamageMultiplier
                        },
                    });
            DerivedCharacterStatsSnapshotV1 characterStats =
                composer.DeriveCharacter(
                    new DerivedCharacterStatInputV1(
                        Id("character", character).ToString(),
                        baseProfile,
                        permanentSources
                            ?? Array.Empty<DerivedStatModifierSourceV1>(),
                        policy));
            return composer.BuildRunProfile(
                new RunCombatProfileInputV1(
                    "run.fixture",
                    "run-context-fixture-v1",
                    characterStats,
                    runSources
                        ?? Array.Empty<DerivedStatModifierSourceV1>(),
                    activeConditionIds ?? Array.Empty<string>(),
                    policy));
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(
                namespaceName,
                value.ToLowerInvariant());
        }
    }
}
