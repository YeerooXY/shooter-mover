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
    public sealed class CriticalHitOrdinalTests
    {
        private readonly CombatHitPolicy hitPolicy =
            new CombatHitPolicy(CombatHitPolicyRegistry.CreateDefault());

        [Test]
        public void SameShotAndTarget_DifferentHitOrdinal_ChangesRollDomain()
        {
            RunCombatProfile profile = Profile();
            CombatActorSnapshot source =
                Actor("source-a", "players", "player-a", 1L);
            CombatActorSnapshot target =
                Actor("target-a", "enemies", "enemy-a", 1L);
            CombatHitPolicyResult hit = AcceptedHit(source, target);

            CriticalHitResolutionResult ordinalZero = Resolve(
                Command("operation-pellet", 42L, 0, profile, hit));
            CriticalHitResolutionResult ordinalOne = Resolve(
                Command("operation-pellet", 42L, 1, profile, hit));

            Assert.That(ordinalZero.ResolvedDamage.ShotSequence, Is.EqualTo(42L));
            Assert.That(ordinalZero.ResolvedDamage.HitOrdinal, Is.EqualTo(0));
            Assert.That(ordinalOne.ResolvedDamage.HitOrdinal, Is.EqualTo(1));
            Assert.That(
                ordinalOne.ResolvedDamage.RollDomainFingerprint,
                Is.Not.EqualTo(
                    ordinalZero.ResolvedDamage.RollDomainFingerprint));
        }

        [Test]
        public void SameOrdinalAndTarget_DifferentShotSequence_ChangesRollDomain()
        {
            RunCombatProfile profile = Profile();
            CombatHitPolicyResult hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L));

            CriticalHitResolutionResult shotTen = Resolve(
                Command("operation-shot", 10L, 3, profile, hit));
            CriticalHitResolutionResult shotEleven = Resolve(
                Command("operation-shot", 11L, 3, profile, hit));

            Assert.That(shotTen.ResolvedDamage.ShotSequence, Is.EqualTo(10L));
            Assert.That(shotTen.ResolvedDamage.HitOrdinal, Is.EqualTo(3));
            Assert.That(shotEleven.ResolvedDamage.ShotSequence, Is.EqualTo(11L));
            Assert.That(
                shotEleven.ResolvedDamage.RollDomainFingerprint,
                Is.Not.EqualTo(shotTen.ResolvedDamage.RollDomainFingerprint));
        }

        [Test]
        public void SameShotAndOrdinal_DifferentTarget_ChangesRollDomain()
        {
            RunCombatProfile profile = Profile();
            CombatActorSnapshot source =
                Actor("source-a", "players", "player-a", 1L);
            CombatHitPolicyResult targetAHit = AcceptedHit(
                source,
                Actor("target-a", "enemies", "enemy-a", 1L));
            CombatHitPolicyResult targetBHit = AcceptedHit(
                source,
                Actor("target-b", "enemies", "enemy-b", 1L));

            CriticalHitResolutionResult targetA = Resolve(
                Command("operation-target", 15L, 2, profile, targetAHit));
            CriticalHitResolutionResult targetB = Resolve(
                Command("operation-target", 15L, 2, profile, targetBHit));

            Assert.That(
                targetB.ResolvedDamage.RollDomainFingerprint,
                Is.Not.EqualTo(targetA.ResolvedDamage.RollDomainFingerprint));
        }

        [Test]
        public void ReusedOperation_WithChangedHitOrdinal_IsConflictingDuplicate()
        {
            RunCombatProfile profile = Profile();
            CombatHitPolicyResult hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L));
            var authority = new CriticalHitResolutionState();

            CriticalHitResolutionResult applied = authority.Resolve(
                Command("operation-replay-ordinal", 42L, 0, profile, hit));
            CriticalHitResolutionResult duplicate = authority.Resolve(
                Command("operation-replay-ordinal", 42L, 0, profile, hit));
            CriticalHitResolutionResult conflict = authority.Resolve(
                Command("operation-replay-ordinal", 42L, 1, profile, hit));

            Assert.That(applied.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Duplicate));
            Assert.That(duplicate.ResolvedDamage, Is.SameAs(applied.ResolvedDamage));
            Assert.That(conflict.Status, Is.EqualTo(
                CriticalHitResolutionStatus.ConflictingDuplicate));
            Assert.That(conflict.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void ReusedOperation_WithChangedShotSequence_IsConflictingDuplicate()
        {
            RunCombatProfile profile = Profile();
            CombatHitPolicyResult hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L));
            var authority = new CriticalHitResolutionState();

            CriticalHitResolutionResult applied = authority.Resolve(
                Command("operation-replay-shot", 10L, 2, profile, hit));
            CriticalHitResolutionResult conflict = authority.Resolve(
                Command("operation-replay-shot", 11L, 2, profile, hit));

            Assert.That(applied.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Applied));
            Assert.That(conflict.Status, Is.EqualTo(
                CriticalHitResolutionStatus.ConflictingDuplicate));
            Assert.That(conflict.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCode.ConflictingDuplicate));
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void NegativeHitOrdinal_IsRejectedWithoutConsumingOperation()
        {
            var authority = new CriticalHitResolutionState();
            CriticalHitResolutionResult result = authority.Resolve(
                Command(
                    "operation-invalid-ordinal",
                    0L,
                    -1,
                    Profile(),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L))));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatus.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCode.InvalidHitOrdinal));
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(0));
        }

        private static CriticalHitResolutionResult Resolve(
            CriticalHitResolutionCommand command)
        {
            return new CriticalHitResolutionState().Resolve(command);
        }

        private static CriticalHitResolutionCommand Command(
            string operation,
            long shotSequence,
            int hitOrdinal,
            RunCombatProfile profile,
            CombatHitPolicyResult hit)
        {
            return new CriticalHitResolutionCommand(
                Id("critical-operation", operation),
                "ordinal-domain-seed",
                shotSequence,
                hitOrdinal,
                10m,
                CombatChannel.Kinetic,
                profile,
                new CriticalHitEffectFacts(
                    Id("effect-definition", "weapon-shotgun"),
                    CriticalHitPolicyIds.Normal,
                    Id("equipment-instance", "shotgun-a")),
                hit);
        }

        private CombatHitPolicyResult AcceptedHit(
            CombatActorSnapshot source,
            CombatActorSnapshot target)
        {
            CombatEffectSnapshot effect = new CombatEffectSnapshot(
                Id("effect", "shared-projectile"),
                CombatHitPolicyIds.PlayerNormal,
                source.ActorId,
                source.LifecycleGeneration,
                CombatEffectGeometryKind.Projectile,
                CombatWorldBlockerBehavior.Terminate,
                false,
                false,
                0,
                8);
            CombatHitPolicyResult result = hitPolicy.Evaluate(
                new CombatHitPolicyInput(
                    source,
                    effect,
                    CombatHitContact.Actor(
                        target,
                        target.LifecycleGeneration,
                        1d),
                    CombatHitHistorySnapshot.Empty(effect.EffectId)));
            Assert.That(result.DamageEligible, Is.True);
            return result;
        }

        private static CombatActorSnapshot Actor(
            string actor,
            string faction,
            string participant,
            long generation)
        {
            GameplayEntityOwnership ownership = GameplayEntityOwnership.Create(
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

        private static RunCombatProfile Profile()
        {
            DerivedStatPolicy policy = DerivedStatPolicy.CreateDefault();
            CharacterBaseStatProfile baseProfile =
                new CharacterBaseStatProfile(
                    "fixture.player-a",
                    "class.fixture",
                    10,
                    "fixture-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        { DerivedStatTargetIds.MaximumHealth, 100m },
                        { DerivedStatTargetIds.MovementSpeed, 5m },
                        { DerivedStatTargetIds.CriticalChance, 0.5m },
                        { DerivedStatTargetIds.CriticalMultiplier, 2m },
                        { DerivedStatTargetIds.OutgoingDamageMultiplier, 1m },
                    });
            var composer = new DefaultDerivedCharacterStatComposer();
            DerivedCharacterStatsSnapshot characterStats =
                composer.DeriveCharacter(
                    new DerivedCharacterStatInput(
                        Id("character", "player-a").ToString(),
                        baseProfile,
                        new DerivedStatModifierSource[0],
                        policy));
            return composer.BuildRunProfile(
                new RunCombatProfileInput(
                    "run.fixture-ordinal",
                    "run-context-fixture-ordinal-v1",
                    characterStats,
                    new DerivedStatModifierSource[0],
                    new string[0],
                    policy));
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value.ToLowerInvariant());
        }
    }
}
