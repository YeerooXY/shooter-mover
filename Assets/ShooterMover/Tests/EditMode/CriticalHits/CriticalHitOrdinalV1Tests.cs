using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Combat.CriticalHits;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Tests.EditMode.CriticalHits
{
    public sealed class CriticalHitOrdinalV1Tests
    {
        private readonly CombatHitPolicyV1 hitPolicy =
            new CombatHitPolicyV1(CombatHitPolicyRegistryV1.CreateDefault());

        [Test]
        public void SameShotAndTarget_DifferentHitOrdinal_ChangesRollDomain()
        {
            RunCombatProfileV1 profile = Profile();
            CombatActorSnapshotV1 source =
                Actor("source-a", "players", "player-a", 1L);
            CombatActorSnapshotV1 target =
                Actor("target-a", "enemies", "enemy-a", 1L);
            CombatHitPolicyResultV1 hit = AcceptedHit(source, target);

            CriticalHitResolutionResultV1 ordinalZero = Resolve(
                Command("operation-pellet", 42L, 0, profile, hit));
            CriticalHitResolutionResultV1 ordinalOne = Resolve(
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
            RunCombatProfileV1 profile = Profile();
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L));

            CriticalHitResolutionResultV1 shotTen = Resolve(
                Command("operation-shot", 10L, 3, profile, hit));
            CriticalHitResolutionResultV1 shotEleven = Resolve(
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
            RunCombatProfileV1 profile = Profile();
            CombatActorSnapshotV1 source =
                Actor("source-a", "players", "player-a", 1L);
            CombatHitPolicyResultV1 targetAHit = AcceptedHit(
                source,
                Actor("target-a", "enemies", "enemy-a", 1L));
            CombatHitPolicyResultV1 targetBHit = AcceptedHit(
                source,
                Actor("target-b", "enemies", "enemy-b", 1L));

            CriticalHitResolutionResultV1 targetA = Resolve(
                Command("operation-target", 15L, 2, profile, targetAHit));
            CriticalHitResolutionResultV1 targetB = Resolve(
                Command("operation-target", 15L, 2, profile, targetBHit));

            Assert.That(
                targetB.ResolvedDamage.RollDomainFingerprint,
                Is.Not.EqualTo(targetA.ResolvedDamage.RollDomainFingerprint));
        }

        [Test]
        public void ReusedOperation_WithChangedHitOrdinal_IsConflictingDuplicate()
        {
            RunCombatProfileV1 profile = Profile();
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L));
            var authority = new CriticalHitResolutionAuthorityV1();

            CriticalHitResolutionResultV1 applied = authority.Resolve(
                Command("operation-replay-ordinal", 42L, 0, profile, hit));
            CriticalHitResolutionResultV1 duplicate = authority.Resolve(
                Command("operation-replay-ordinal", 42L, 0, profile, hit));
            CriticalHitResolutionResultV1 conflict = authority.Resolve(
                Command("operation-replay-ordinal", 42L, 1, profile, hit));

            Assert.That(applied.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Duplicate));
            Assert.That(duplicate.ResolvedDamage, Is.SameAs(applied.ResolvedDamage));
            Assert.That(conflict.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.ConflictingDuplicate));
            Assert.That(conflict.ResolvedDamage, Is.Null);
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void ReusedOperation_WithChangedShotSequence_IsConflictingDuplicate()
        {
            RunCombatProfileV1 profile = Profile();
            CombatHitPolicyResultV1 hit = AcceptedHit(
                Actor("source-a", "players", "player-a", 1L),
                Actor("target-a", "enemies", "enemy-a", 1L));
            var authority = new CriticalHitResolutionAuthorityV1();

            CriticalHitResolutionResultV1 applied = authority.Resolve(
                Command("operation-replay-shot", 10L, 2, profile, hit));
            CriticalHitResolutionResultV1 conflict = authority.Resolve(
                Command("operation-replay-shot", 11L, 2, profile, hit));

            Assert.That(applied.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Applied));
            Assert.That(conflict.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.ConflictingDuplicate));
            Assert.That(conflict.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCodeV1.ConflictingDuplicate));
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void NegativeHitOrdinal_IsRejectedWithoutConsumingOperation()
        {
            var authority = new CriticalHitResolutionAuthorityV1();
            CriticalHitResolutionResultV1 result = authority.Resolve(
                Command(
                    "operation-invalid-ordinal",
                    0L,
                    -1,
                    Profile(),
                    AcceptedHit(
                        Actor("source-a", "players", "player-a", 1L),
                        Actor("target-a", "enemies", "enemy-a", 1L))));

            Assert.That(result.Status, Is.EqualTo(
                CriticalHitResolutionStatusV1.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                CriticalHitRejectionCodeV1.InvalidHitOrdinal));
            Assert.That(authority.AppliedResolutionCount, Is.EqualTo(0));
        }

        private static CriticalHitResolutionResultV1 Resolve(
            CriticalHitResolutionCommandV1 command)
        {
            return new CriticalHitResolutionAuthorityV1().Resolve(command);
        }

        private static CriticalHitResolutionCommandV1 Command(
            string operation,
            long shotSequence,
            int hitOrdinal,
            RunCombatProfileV1 profile,
            CombatHitPolicyResultV1 hit)
        {
            return new CriticalHitResolutionCommandV1(
                Id("critical-operation", operation),
                "ordinal-domain-seed",
                shotSequence,
                hitOrdinal,
                10m,
                CombatChannel.Kinetic,
                profile,
                new CriticalHitEffectFactsV1(
                    Id("effect-definition", "weapon-shotgun"),
                    CriticalHitPolicyIdsV1.Normal,
                    Id("equipment-instance", "shotgun-a")),
                hit);
        }

        private CombatHitPolicyResultV1 AcceptedHit(
            CombatActorSnapshotV1 source,
            CombatActorSnapshotV1 target)
        {
            CombatEffectSnapshotV1 effect = new CombatEffectSnapshotV1(
                Id("effect", "shared-projectile"),
                CombatHitPolicyIdsV1.PlayerNormal,
                source.ActorId,
                source.LifecycleGeneration,
                CombatEffectGeometryKindV1.Projectile,
                CombatWorldBlockerBehaviorV1.Terminate,
                false,
                false,
                0,
                8);
            CombatHitPolicyResultV1 result = hitPolicy.Evaluate(
                new CombatHitPolicyInputV1(
                    source,
                    effect,
                    CombatHitContactV1.Actor(
                        target,
                        target.LifecycleGeneration,
                        1d),
                    CombatHitHistorySnapshotV1.Empty(effect.EffectId)));
            Assert.That(result.DamageEligible, Is.True);
            return result;
        }

        private static CombatActorSnapshotV1 Actor(
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
            return CombatActorSnapshotFactoryV1.CreateDamageReceiver(
                identity,
                generation,
                true);
        }

        private static RunCombatProfileV1 Profile()
        {
            DerivedStatPolicyV1 policy = DerivedStatPolicyV1.CreateDefault();
            CharacterBaseStatProfileV1 baseProfile =
                new CharacterBaseStatProfileV1(
                    "fixture.player-a",
                    "class.fixture",
                    10,
                    "fixture-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                        { DerivedStatTargetIdsV1.MovementSpeed, 5m },
                        { DerivedStatTargetIdsV1.CriticalChance, 0.5m },
                        { DerivedStatTargetIdsV1.CriticalMultiplier, 2m },
                        { DerivedStatTargetIdsV1.OutgoingDamageMultiplier, 1m },
                    });
            var composer = new DefaultDerivedCharacterStatComposerV1();
            DerivedCharacterStatsSnapshotV1 characterStats =
                composer.DeriveCharacter(
                    new DerivedCharacterStatInputV1(
                        Id("character", "player-a").ToString(),
                        baseProfile,
                        new DerivedStatModifierSourceV1[0],
                        policy));
            return composer.BuildRunProfile(
                new RunCombatProfileInputV1(
                    "run.fixture-ordinal",
                    "run-context-fixture-ordinal-v1",
                    characterStats,
                    new DerivedStatModifierSourceV1[0],
                    new string[0],
                    policy));
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value.ToLowerInvariant());
        }
    }
}
