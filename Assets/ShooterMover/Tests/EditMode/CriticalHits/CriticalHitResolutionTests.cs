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
    public sealed class CriticalHitResolutionTests
    {
        private readonly CombatHitRules hitPolicy =
            new CombatHitRules(CombatHitPolicyRegistry.CreateDefault());

        [Test]
        public void IdenticalImmutableFacts_ProduceIdenticalResolution()
        {
            RunCombatProfile profile = Profile(
                "run.fixture-a",
                "player-a",
                0.42m,
                2.25m,
                1.10m);
            CombatHitPolicyResult hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 3L),
                Actor("target-a", "enemies", "enemy-a", 8L),
                "effect-a",
                CombatEffectGeometryKind.Projectile);
            CriticalHitResolutionCommand firstCommand = Command(
                "operation-a",
                "run-seed-991",
                7L,
                40m,
                profile,
                hit,
                CriticalHitPolicyIds.Normal,
                "weapon-blaster",
                "blaster-instance-a");
            CriticalHitResolutionCommand secondCommand = Command(
                "operation-a",
                "run-seed-991",
                7L,
                40m,
                profile,
                AcceptedHit(
                    Actor("source-a", "players", "player-a", 3L),
                    Actor("target-a", "enemies", "enemy-a", 8L),
                    "effect-a",
                    CombatEffectGeometryKind.Projectile),
                CriticalHitPolicyIds.Normal,
                "weapon-blaster",
                "blaster-instance-a");

            CriticalHitResolutionResult first =
                new CriticalHitResolutionState().Resolve(firstCommand);
            CriticalHitResolutionResult second =
                new CriticalHitResolutionState().Resolve(secondCommand);

            Assert.That(first.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Applied));
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
            CombatActorSnapshot source =
                Actor("source-a", "players", "player-a", 1L);
            CombatActorSnapshot target =
                Actor("target-a", "enemies", "enemy-a", 1L);
            CombatHitPolicyResult hit = AcceptedHit(
                source,
                target,
                "shared-effect-instance",
                CombatEffectGeometryKind.Projectile);
            RunCombatProfile profileA = Profile(
                "run.fixture-a",
                "player-a",
                0.5m,
                2m,
                1m);
            CriticalHitResolutionResult baseline = ResolveNew(
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profileA,
                    hit,
                    CriticalHitPolicyIds.Normal,
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
                    CriticalHitPolicyIds.Normal,
                    "weapon-blaster",
                    "equipment-a"),
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profileA,
                    hit,
                    CriticalHitPolicyIds.Normal,
                    "weapon-blaster",
                    "equipment-b"),
                Command(
                    "operation-domain",
                    "seed-a",
                    10L,
                    20m,
                    profileA,
                    hit,
                    CriticalHitPolicyIds.Normal,
                    "weapon-shotgun",
                    "equipment-a"),
            };

            foreach (CriticalHitResolutionCommand variantCommand in variants)
            {
                CriticalHitResolutionResult variant = ResolveNew(variantCommand);
                Assert.That(
                    variant.ResolvedDamage.RollDomainFingerprint,
                    Is.Not.EqualTo(
                        baseline.ResolvedDamage.RollDomainFingerprint));
            }
        }

        [Test]
        public void DuplicateAndConflictingOperations_DoNotResolveTwice()
        {
            RunCombatProfile profile = Profile(
                "run.fixture-a",
                "player-a",
                0.5m,
                2m,
                1m);
            CombatHitPolicyResult hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 2L),
                Actor("target-a", "enemies", "enemy-a", 5L),
                "effect-a",
                CombatEffectGeometryKind.Projectile);
            CriticalHitResolutionCommand command = Command(
                "operation-replay",
                "seed-a",
                4L,
                25m,
                profile,
                hit,
                CriticalHitPolicyIds.Normal,
                "weapon-blaster",
                "equipment-a");
            var authority = new CriticalHitResolutionState();

            CriticalHitResolutionResult applied = authority.Resolve(command);
            CriticalHitResolutionResult duplicate = authority.Resolve(
                Command(
                    "operation-replay",
                    "seed-a",
                    4L,
                    25m,
                    profile,
                    hit,
                    CriticalHitPolicyIds.Normal,
                    "weapon-blaster",
                    "equipment-a"));
            CriticalHitResolutionResult conflict = authority.Resolve(
                Command(
                    "operation-replay",
                    "seed-a",
                    4L,
                    25m,
                    profile,
                    hit,
                    CriticalHitPolicyIds.CannotCrit,
                    "weapon-blaster",
                    "equipment-a"));

            Assert.That(applied.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Duplicate));
            Assert.That(
                duplicate.ResolvedDamage,
                Is.SameAs(applied.ResolvedDamage));
            Assert.That(conflict.Status, Is.EqualTo(
                CriticalHitResolutionStatus.ConflictingDuplicate));
            Assert.That(conflict.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCode.ConflictingDuplicate));
            Assert.That(conflict.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void NormalPolicy_AppliesOutgoingDamageBeforeCriticalMultiplier()
        {
            CriticalHitResolutionResult result = ResolveNew(
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
                        CombatEffectGeometryKind.Projectile),
                    CriticalHitPolicyIds.Normal,
                    "weapon-blaster",
                    "equipment-a"));

            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.OrdinaryDamage, Is.EqualTo(50m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(125m));
        }

        [Test]
        public void CannotCritPolicy_IgnoresOneHundredPercentCharacterCritModifiers()
        {
            CriticalHitResolutionResult result = ResolveNew(
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
                        CombatEffectGeometryKind.PersistentField),
                    CriticalHitPolicyIds.CannotCrit,
                    "acid-persistent-field",
                    "equipment-acid-a"));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Applied));
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
            CriticalHitResolutionResult result = ResolveNew(
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
                        CombatEffectGeometryKind.ContactAttack),
                    CriticalHitPolicyIds.Guaranteed,
                    "contact-finisher",
                    null));

            Assert.That(result.ResolvedDamage.IsCritical, Is.True);
            Assert.That(result.ResolvedDamage.CriticalChance, Is.EqualTo(1m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(40m));
        }

        [Test]
        public void ModifiedChancePolicy_OverridesProfileChance()
        {
            var registry = new CriticalHitPolicyRegistry(
                new[]
                {
                    new CriticalHitPolicyDefinition(
                        CriticalHitPolicyIds.ModifiedChance,
                        true,
                        criticalChanceOverride: 0m),
                });
            var authority = new CriticalHitResolutionState(registry);
            CriticalHitResolutionResult result = authority.Resolve(
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
                        CombatEffectGeometryKind.Projectile),
                    CriticalHitPolicyIds.ModifiedChance,
                    "weapon-modified-chance",
                    "equipment-a"));

            Assert.That(result.ResolvedDamage.IsCritical, Is.False);
            Assert.That(result.ResolvedDamage.CriticalChance, Is.EqualTo(0m));
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(20m));
        }

        [Test]
        public void ModifiedMultiplierPolicy_ChangesOnlyCriticalMultiplier()
        {
            CriticalHitResolutionResult result = ResolveNew(
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
                        CombatEffectGeometryKind.Explosion),
                    CriticalHitPolicyIds.ModifiedMultiplier,
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
            RunCombatProfile profile = Profile(
                "run.fixture-a",
                "player-a",
                1m,
                2m,
                1m);
            CombatHitPolicyResult hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L),
                "same-projectile-effect",
                CombatEffectGeometryKind.Projectile);

            CriticalHitResolutionResult cannotCrit = ResolveNew(
                Command(
                    "operation-projectile-no-crit",
                    "same-geometry-seed",
                    0L,
                    10m,
                    profile,
                    hit,
                    CriticalHitPolicyIds.CannotCrit,
                    "projectile-no-crit",
                    "equipment-a"));
            CriticalHitResolutionResult guaranteed = ResolveNew(
                Command(
                    "operation-projectile-guaranteed",
                    "same-geometry-seed",
                    0L,
                    10m,
                    profile,
                    hit,
                    CriticalHitPolicyIds.Guaranteed,
                    "projectile-guaranteed",
                    "equipment-a"));

            Assert.That(cannotCrit.ResolvedDamage.IsCritical, Is.False);
            Assert.That(cannotCrit.ResolvedDamage.FinalDamage, Is.EqualTo(10m));
            Assert.That(guaranteed.ResolvedDamage.IsCritical, Is.True);
            Assert.That(guaranteed.ResolvedDamage.FinalDamage, Is.EqualTo(20m));
        }

        [TestCase(CombatEffectGeometryKind.Projectile)]
        [TestCase(CombatEffectGeometryKind.Explosion)]
        [TestCase(CombatEffectGeometryKind.MeleeSwing)]
        [TestCase(CombatEffectGeometryKind.ContactAttack)]
        [TestCase(CombatEffectGeometryKind.PersistentField)]
        [TestCase(CombatEffectGeometryKind.Chain)]
        public void EveryGeometry_RespectsExplicitCannotCritPolicy(
            CombatEffectGeometryKind geometry)
        {
            CriticalHitResolutionResult result = ResolveNew(
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
                    CriticalHitPolicyIds.CannotCrit,
                    "definition-geometry-" + ((int)geometry),
                    null));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Applied));
            Assert.That(result.ResolvedDamage.IsCritical, Is.False);
            Assert.That(result.ResolvedDamage.FinalDamage, Is.EqualTo(10m));
        }

        [Test]
        public void PermanentEventAndStatusModifiers_FlowIntoNormalPolicy()
        {
            DerivedStatModifierSource permanent =
                new DerivedStatModifierSource(
                    "skill.critical-training",
                    DerivedStatSourcePriorities.Skills,
                    "skill-snapshot-a",
                    new LiveModifierSnapshot(
                        new[]
                        {
                            new LiveModifierDefinition(
                                "skill.critical-training",
                                DerivedStatTargetIds.CriticalChance,
                                LiveModifierOperation.Flat,
                                0.2m),
                        }));
            DerivedStatModifierSource eventSource =
                new DerivedStatModifierSource(
                    "event.damage-week",
                    DerivedStatSourcePriorities.Events,
                    "event-snapshot-a",
                    new LiveModifierSnapshot(
                        new[]
                        {
                            new LiveModifierDefinition(
                                "event.damage-week",
                                DerivedStatTargetIds.CriticalChance,
                                LiveModifierOperation.Flat,
                                0.2m),
                            new LiveModifierDefinition(
                                "event.damage-week",
                                DerivedStatTargetIds
                                    .OutgoingDamageMultiplier,
                                LiveModifierOperation.Percentage,
                                0.25m),
                        }));
            DerivedStatModifierSource statusSource =
                new DerivedStatModifierSource(
                    "status.focus",
                    DerivedStatSourcePriorities.RunConditions,
                    "status-snapshot-a",
                    new LiveModifierSnapshot(
                        new[]
                        {
                            new LiveModifierDefinition(
                                "status.focus",
                                DerivedStatTargetIds.CriticalChance,
                                LiveModifierOperation.Flat,
                                0.4m,
                                "condition.focus-active"),
                            new LiveModifierDefinition(
                                "status.focus",
                                DerivedStatTargetIds.CriticalMultiplier,
                                LiveModifierOperation.Flat,
                                1m,
                                "condition.focus-active"),
                        }));
            RunCombatProfile profile = Profile(
                "run.fixture-a",
                "player-a",
                0.2m,
                1.5m,
                1m,
                new[] { permanent },
                new[] { eventSource, statusSource },
                new[] { "condition.focus-active" });

            CriticalHitResolutionResult result = ResolveNew(
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
                        CombatEffectGeometryKind.PersistentField),
                    CriticalHitPolicyIds.Normal,
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
            RunCombatProfile profile = Profile(
                "run.fixture-network",
                "player-two",
                1m,
                2m,
                1m);
            CombatHitPolicyResult hit = AcceptedHit(
                Actor("source-two", "players", "player-two", 6L),
                Actor("target-nine", "enemies", "enemy-nine", 12L),
                "effect-networked",
                CombatEffectGeometryKind.Chain);
            var authority = new CriticalHitResolutionState();
            CriticalHitResolutionCommand command = Command(
                "operation-networked",
                "network-seed",
                19L,
                15m,
                profile,
                hit,
                CriticalHitPolicyIds.Normal,
                "weapon-chain",
                "equipment-chain-a");
            CriticalHitResolutionResult applied = authority.Resolve(command);
            CriticalHitResolutionResult duplicate = authority.Resolve(command);

            DamageReceiverCommand firstCommand;
            DamageReceiverCommand replayCommand;
            bool firstCreated = CriticalHitDamageCommandBridge.TryCreate(
                applied,
                out firstCommand);
            bool replayCreated = CriticalHitDamageCommandBridge.TryCreate(
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
            var authority = new CriticalHitResolutionState();
            CriticalHitResolutionResult result = authority.Resolve(
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
                        CombatEffectGeometryKind.Projectile),
                    Id("critical-hit-policy", "missing-v1"),
                    "weapon-unknown",
                    "equipment-a"));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCode.UnknownCriticalPolicy));
            Assert.That(result.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(0));
        }

        [Test]
        public void NonEligibleHit_IsRejectedWithoutConsumingOperation()
        {
            CombatActorSnapshot source =
                Actor("source-a", "players", "player-a", 1L);
            CombatActorSnapshot ally =
                Actor("ally-a", "players", "player-b", 1L);
            CombatHitPolicyResult denied = Evaluate(
                source,
                ally,
                "effect-friendly",
                CombatEffectGeometryKind.Projectile);
            var authority = new CriticalHitResolutionState();

            CriticalHitResolutionResult result = authority.Resolve(
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
                    CriticalHitPolicyIds.Normal,
                    "weapon-friendly",
                    "equipment-a"));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCode.HitNotDamageEligible));
            Assert.That(result.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(0));
        }

        private static CriticalHitResolutionResult ResolveNew(
            CriticalHitResolutionCommand command)
        {
            return new CriticalHitResolutionState().Resolve(command);
        }

        private static CriticalHitResolutionCommand Command(
            string operation,
            string seed,
            long hitSequence,
            decimal baseDamage,
            RunCombatProfile profile,
            CombatHitPolicyResult hit,
            StableId criticalPolicyId,
            string effectDefinition,
            string equipmentInstance)
        {
            return new CriticalHitResolutionCommand(
                Id("critical-operation", operation),
                seed,
                hitSequence,
                baseDamage,
                CombatChannel.Kinetic,
                profile,
                new CriticalHitEffectFacts(
                    Id("effect-definition", effectDefinition),
                    criticalPolicyId,
                    equipmentInstance == null
                        ? null
                        : Id("equipment-instance", equipmentInstance)),
                hit);
        }

        private CombatHitPolicyResult AcceptedHit(
            CombatActorSnapshot source,
            CombatActorSnapshot target,
            string effect,
            CombatEffectGeometryKind geometry)
        {
            CombatHitPolicyResult result = Evaluate(
                source,
                target,
                effect,
                geometry);
            Assert.That(result.DamageEligible, Is.True);
            return result;
        }

        private CombatHitPolicyResult Evaluate(
            CombatActorSnapshot source,
            CombatActorSnapshot target,
            string effect,
            CombatEffectGeometryKind geometry)
        {
            CombatEffectSnapshot snapshot = new CombatEffectSnapshot(
                Id("effect", effect),
                CombatHitPolicyIds.PlayerNormal,
                source.ActorId,
                source.LifecycleGeneration,
                geometry,
                CombatWorldBlockerBehavior.Terminate,
                false,
                false,
                0,
                1);
            return hitPolicy.Evaluate(
                new CombatHitPolicyInput(
                    source,
                    snapshot,
                    CombatHitContact.Actor(
                        target,
                        target.LifecycleGeneration,
                        1d),
                    CombatHitHistorySnapshot.Empty(snapshot.EffectId)));
        }

        private static CombatActorSnapshot Actor(
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
            return CombatActorSnapshotFactory.CreateDamageReceiver(
                identity,
                generation,
                true);
        }

        private static RunCombatProfile Profile(
            string runId,
            string character,
            decimal criticalChance,
            decimal criticalMultiplier,
            decimal outgoingDamageMultiplier,
            IEnumerable<DerivedStatModifierSource> permanentSources = null,
            IEnumerable<DerivedStatModifierSource> runSources = null,
            IEnumerable<string> activeConditionIds = null)
        {
            DerivedStatPolicy policy = DerivedStatPolicy.CreateDefault();
            var composer = new DefaultDerivedCharacterStatComposer();
            CharacterBaseStatProfile baseProfile =
                new CharacterBaseStatProfile(
                    "fixture." + character,
                    "class.fixture",
                    10,
                    "fixture-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        {
                            DerivedStatTargetIds.MaximumHealth,
                            100m
                        },
                        {
                            DerivedStatTargetIds.MovementSpeed,
                            5m
                        },
                        {
                            DerivedStatTargetIds.CriticalChance,
                            criticalChance
                        },
                        {
                            DerivedStatTargetIds.CriticalMultiplier,
                            criticalMultiplier
                        },
                        {
                            DerivedStatTargetIds.OutgoingDamageMultiplier,
                            outgoingDamageMultiplier
                        },
                    });
            DerivedCharacterStatsSnapshot characterStats =
                composer.DeriveCharacter(
                    new DerivedCharacterStatInput(
                        Id("character", character).ToString(),
                        baseProfile,
                        permanentSources
                            ?? Array.Empty<DerivedStatModifierSource>(),
                        policy));
            return composer.BuildRunProfile(
                new RunCombatProfileInput(
                    runId,
                    "run-context-" + runId,
                    characterStats,
                    runSources
                        ?? Array.Empty<DerivedStatModifierSource>(),
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
