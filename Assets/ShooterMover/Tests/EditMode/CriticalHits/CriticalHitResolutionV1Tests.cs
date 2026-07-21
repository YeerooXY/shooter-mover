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
        public void IdenticalImmutableFacts_ProduceIdenticalResolution()
        {
            RunCombatProfileV1 profile = Profile(
                "run.fixture-a",
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
                hit,
                CriticalHitPolicyIdsV1.Normal,
                "weapon-blaster",
                "blaster-instance-a");
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
                    CombatEffectGeometryKindV1.Projectile),
                CriticalHitPolicyIdsV1.Normal,
                "weapon-blaster",
                "blaster-instance-a");

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
        public void RunEquipmentAndEffectDefinition_AreExplicitRollDomainFacts()
        {
            CombatActorSnapshotV1 source =
                Actor("source-a", "players", "player-a", 1L);
            CombatActorSnapshotV1 target =
                Actor("target-a", "enemies", "enemy-a", 1L);
            CombatHitPolicyResultV1 hit = AcceptedHit(
                source,
                target,
                "shared-effect-instance",
                CombatEffectGeometryKindV1.Projectile);
            RunCombatProfileV1 profileA = Profile(
                "run.fixture-a",
                "player-a",
                0.5m,
                2m,
                1m);
            CriticalHitResolutionResultV1 baseline = ResolveNew(
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profileA,
                    hit,
                    CriticalHitPolicyIdsV1.Normal,
                    "weapon-blaster",
                    "equipment-a"));

            var variants = new[]
            {
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    Profile(
                        "run.fixture-b",
                        "player-a",
                        0.5m,
                        2m,
                        1m),
                    hit,
                    CriticalHitPolicyIdsV1.Normal,
                    "weapon-blaster",
                    "equipment-a"),
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profileA,
                    hit,
                    CriticalHitPolicyIdsV1.Normal,
                    "weapon-blaster",
                    "equipment-b"),
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profileA,
                    hit,
                    CriticalHitPolicyIdsV1.Normal,
                    "weapon-shotgun",
                    "equipment-a"),
            };

            foreach (CriticalHitResolutionCommandV1 variantCommand in variants)
            {
                CriticalHitResolutionResultV1 variant = ResolveNew(variantCommand);
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
                "run.fixture-a",
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
                hit,
                CriticalHitPolicyIdsV1.Normal,
                "weapon-blaster",
                "equipment-a");
            var authority = new CriticalHitResolutionAuthorityV1();

            CriticalHitResolutionResultV1 applied = authority.Resolve(command);
            CriticalHitResolutionResultV1 duplicate = authority.Resolve(
                Command(
                    "operation-replay",
                    "seed-a",
                    4L,
                    25m,
                    profile,
                    hit,
                    CriticalHitPolicyIdsV1.Normal,
                    "weapon-blaster",
                    "equipment-a"));
            CriticalHitResolutionResultV1 conflict = authority.Resolve(
                Command(
                    "operation-replay",
                    "seed-a",
                    4L,
                    25m,
                    profile,
                    hit,
                    CriticalHitPolicyIdsV1.CannotCrit,
                    "weapon-blaster",
                    "equipment-a"));

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
        public void NormalPolicy_AppliesOutgoingDamageBeforeCriticalMultiplier()
        {
            CriticalHitResolutionResultV1 result = ResolveNew(
                Command(
                    "operation-normal",
                    "edge-seed",
                    0L,
                    40m,
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        1m,
                        2.5m,
                        1.25m),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "normal-effect",
                        CombatEffectGeometryKindV1.Projectile),
                    CriticalHitPolicyIdsV1.Normal,
                    "weapon-blaster",
                    "equipment-a"));

            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.OrdinaryDamage, Is.EqualTo(50m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(125m));
        }

        [Test]
        public void CannotCritPolicy_IgnoresOneHundredPercentCharacterCritModifiers()
        {
            CriticalHitResolutionResultV1 result = ResolveNew(
                Command(
                    "operation-field-cannot-crit",
                    "field-seed",
                    1L,
                    40m,
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        1m,
                        9m,
                        1.25m),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "persistent-field-a",
                        CombatEffectGeometryKindV1.PersistentField),
                    CriticalHitPolicyIdsV1.CannotCrit,
                    "acid-persistent-field",
                    "equipment-acid-a"));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(result.ResolvedDamage.IsCritical, Is.False);
            Assert.That(
                result.ResolvedDamage.PolicyApplication.CanCrit,
                Is.False);
            Assert.That(result.ResolvedDamage.CriticalChance, Is.EqualTo(0m));
            Assert.That(result.ResolvedDamage.CriticalMultiplier, Is.EqualTo(1m));
            Assert.That(result.ResolvedDamage.OrdinaryDamage, Is.EqualTo(50m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(50m));
        }

        [Test]
        public void GuaranteedPolicy_CritsWithZeroCharacterCriticalChance()
        {
            CriticalHitResolutionResultV1 result = ResolveNew(
                Command(
                    "operation-guaranteed",
                    "guaranteed-seed",
                    2L,
                    20m,
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        0m,
                        2m,
                        1m),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "guaranteed-effect",
                        CombatEffectGeometryKindV1.ContactAttack),
                    CriticalHitPolicyIdsV1.Guaranteed,
                    "contact-finisher",
                    null));

            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.CriticalChance, Is.EqualTo(1m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(40m));
        }

        [Test]
        public void ModifiedChancePolicy_OverridesProfileChance()
        {
            var registry = new CriticalHitPolicyRegistryV1(
                new[]
                {
                    new CriticalHitPolicyDefinitionV1(
                        CriticalHitPolicyIdsV1.ModifiedChance,
                        true,
                        criticalChanceOverride: 0m),
                });
            var authority = new CriticalHitResolutionAuthorityV1(registry);
            CriticalHitResolutionResultV1 result = authority.Resolve(
                Command(
                    "operation-modified-chance",
                    "modified-seed",
                    3L,
                    20m,
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        1m,
                        5m,
                        1m),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "modified-chance-effect",
                        CombatEffectGeometryKindV1.Projectile),
                    CriticalHitPolicyIdsV1.ModifiedChance,
                    "weapon-modified-chance",
                    "equipment-a"));

            Assert.That(result.ResolvedDamage.IsCritical, Is.False);
            Assert.That(result.ResolvedDamage.CriticalChance, Is.EqualTo(0m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(20m));
        }

        [Test]
        public void ModifiedMultiplierPolicy_ChangesOnlyCriticalMultiplier()
        {
            CriticalHitResolutionResultV1 result = ResolveNew(
                Command(
                    "operation-modified-multiplier",
                    "modified-multiplier-seed",
                    4L,
                    10m,
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        1m,
                        2m,
                        1m),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "modified-multiplier-effect",
                        CombatEffectGeometryKindV1.Explosion),
                    CriticalHitPolicyIdsV1.ModifiedMultiplier,
                    "weapon-modified-multiplier",
                    "equipment-a"));

            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.CriticalChance, Is.EqualTo(1m));
            Assert.That(result.ResolvedDamage.CriticalMultiplier, Is.EqualTo(3m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(30m));
        }

        [Test]
        public void SameGeometry_CanSelectDifferentCriticalPolicies()
        {
            RunCombatProfileV1 profile = Profile(
                "run.fixture-a",
                "player-a",
                1m,
                2m,
                1m);
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L),
                "same-projectile-effect",
                CombatEffectGeometryKindV1.Projectile);

            CriticalHitResolutionResultV1 cannotCrit = ResolveNew(
                Command(
                    "operation-projectile-no-crit",
                    "same-geometry-seed",
                    0L,
                    10m,
                    profile,
                    hit,
                    CriticalHitPolicyIdsV1.CannotCrit,
                    "projectile-no-crit",
                    "equipment-a"));
            CriticalHitResolutionResultV1 guaranteed = ResolveNew(
                Command(
                    "operation-projectile-guaranteed",
                    "same-geometry-seed",
                    0L,
                    10m,
                    profile,
                    hit,
                    CriticalHitPolicyIdsV1.Guaranteed,
                    "projectile-guaranteed",
                    "equipment-a"));

            Assert.That(cannotCrit.ResolvedDamage.IsCritical, Is.False);
            Assert.That(cannotCrit.ResolvedDamage.FinalDamage, Is.EqualTo(10m));
            Assert.That(guaranteed.ResolvedDamage.IsCritical, Is.True);
            Assert.That(guaranteed.ResolvedDamage.FinalDamage, Is.EqualTo(20m));
        }

        [TestCase(CombatEffectGeometryKindV1.Projectile)]
        [TestCase(CombatEffectGeometryKindV1.Explosion)]
        [TestCase(CombatEffectGeometryKindV1.MeleeSwing)]
        [TestCase(CombatEffectGeometryKindV1.ContactAttack)]
        [TestCase(CombatEffectGeometryKindV1.PersistentField)]
        [TestCase(CombatEffectGeometryKindV1.Chain)]
        public void EveryGeometry_RespectsExplicitCannotCritPolicy(
            CombatEffectGeometryKindV1 geometry)
        {
            CriticalHitResolutionResultV1 result = ResolveNew(
                Command(
                    "operation-geometry-" + ((int)geometry),
                    "geometry-seed",
                    (long)geometry,
                    10m,
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        1m,
                        4m,
                        1m),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "effect-geometry-" + ((int)geometry),
                        geometry),
                    CriticalHitPolicyIdsV1.CannotCrit,
                    "definition-geometry-" + ((int)geometry),
                    null));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(result.ResolvedDamage.IsCritical, Is.False);
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(10m));
        }

        [Test]
        public void PermanentEventAndStatusModifiers_FlowIntoNormalPolicy()
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
                "run.fixture-a",
                "player-a",
                0.2m,
                1.5m,
                1m,
                new[] { permanent },
                new[] { eventSource, statusSource },
                new[] { "condition.focus-active" });

            CriticalHitResolutionResultV1 result = ResolveNew(
                Command(
                    "operation-modifiers",
                    "modifier-seed",
                    3L,
                    40m,
                    profile,
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "effect-modifiers",
                        CombatEffectGeometryKindV1.PersistentField),
                    CriticalHitPolicyIdsV1.Normal,
                    "effect-modifiers",
                    null));

            Assert.That(profile.CriticalChance, Is.EqualTo(1m));
            Assert.That(profile.CriticalMultiplier, Is.EqualTo(2.5m));
            Assert.That(profile.OutgoingDamageMultiplier, Is.EqualTo(1.25m));
            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.OrdinaryDamage, Is.EqualTo(50m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(125m));
        }

        [Test]
        public void DamageAdapter_PreservesAttributionAndReplayIdentity()
        {
            RunCombatProfileV1 profile = Profile(
                "run.fixture-network",
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
            CriticalHitResolutionCommandV1 command = Command(
                "operation-networked",
                "network-seed",
                19L,
                15m,
                profile,
                hit,
                CriticalHitPolicyIdsV1.Normal,
                "weapon-chain",
                "equipment-chain-a");
            CriticalHitResolutionResultV1 applied = authority.Resolve(command);
            CriticalHitResolutionResultV1 duplicate = authority.Resolve(command);

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
                firstCommand.SourceRunParticipantId,
                Is.EqualTo(Id("participant", "player-two")));
            Assert.That(
                firstCommand.TargetActorId,
                Is.EqualTo(Id("actor", "target-nine")));
            Assert.That(firstCommand.LifecycleGeneration, Is.EqualTo(12L));
            Assert.That(firstCommand.Amount, Is.EqualTo(30d));
        }

        [Test]
        public void UnknownCriticalPolicy_FailsClosedWithoutConsumingOperation()
        {
            var authority = new CriticalHitResolutionAuthorityV1();
            CriticalHitResolutionResultV1 result = authority.Resolve(
                Command(
                    "operation-unknown-policy",
                    "seed-a",
                    0L,
                    10m,
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        1m,
                        2m,
                        1m),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L),
                        "effect-unknown-policy",
                        CombatEffectGeometryKindV1.Projectile),
                    Id("critical-hit-policy", "missing-v1"),
                    "weapon-unknown",
                    "equipment-a"));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCodeV1.UnknownCriticalPolicy));
            Assert.That(result.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(0));
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
                    Profile(
                        "run.fixture-a",
                        "player-a",
                        1m,
                        2m,
                        1m),
                    denied,
                    CriticalHitPolicyIdsV1.Normal,
                    "weapon-friendly",
                    "equipment-a"));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCodeV1.HitNotDamageEligible));
            Assert.That(result.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(0));
        }

        private static CriticalHitResolutionResultV1 ResolveNew(
            CriticalHitResolutionCommandV1 command)
        {
            return new CriticalHitResolutionAuthorityV1().Resolve(command);
        }

        private static CriticalHitResolutionCommandV1 Command(
            string operation,
            string seed,
            long hitSequence,
            decimal baseDamage,
            RunCombatProfileV1 profile,
            CombatHitPolicyResultV1 hit,
            StableId criticalPolicyId,
            string effectDefinition,
            string equipmentInstance)
        {
            return new CriticalHitResolutionCommandV1(
                Id("critical-operation", operation),
                seed,
                hitSequence,
                baseDamage,
                CombatChannel.Kinetic,
                profile,
                new CriticalHitEffectFactsV1(
                    Id("effect-definition", effectDefinition),
                    criticalPolicyId,
                    equipmentInstance == null
                        ? null
                        : Id("equipment-instance", equipmentInstance)),
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
            string runId,
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
                    runId,
                    "run-context-" + runId,
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
