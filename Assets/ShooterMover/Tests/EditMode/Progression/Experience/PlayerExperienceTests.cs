using System;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;

namespace ShooterMover.Tests.EditMode.Progression.Experience
{
    public sealed class PlayerExperienceTests
    {
        [Test]
        public void FreshAuthority_StartsAtLevelOneWithOneSkillPoint()
        {
            var authority = CreateAuthority(CreateConstantCurve(), 77);

            Assert.That(authority.CurrentState.Level, Is.EqualTo(1));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.Zero);
            Assert.That(authority.CurrentState.ExperienceToNextLevel, Is.EqualTo(100L));
            Assert.That(authority.CurrentState.TotalSkillPointsAwarded, Is.EqualTo(1));
            Assert.That(authority.CurrentContext.CharacterLevel, Is.EqualTo(1));
            Assert.That(authority.CurrentContext.RegionLevel, Is.EqualTo(77));
            Assert.That(authority.CurrentSnapshot.Sequence, Is.Zero);
        }

        [Test]
        public void ConfiguredCurve_IsDeterministicAndUsesSharedSoftCurveShape()
        {
            var shape = new SoftActivationCurveParameters(0.1, 10L, 10L);
            var first = new PlayerExperienceCurve(100L, 1000L, 50, shape);
            var second = new PlayerExperienceCurve(
                100L,
                1000L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(
                first.GetExperienceToAdvance(1),
                Is.LessThan(first.GetExperienceToAdvance(50)));
            Assert.That(
                first.GetExperienceToAdvance(50),
                Is.LessThanOrEqualTo(first.GetExperienceToAdvance(99)));
            Assert.That(
                first.GetCumulativeExperienceForLevel(100),
                Is.EqualTo(first.MaximumProgressionExperience));
        }

        [Test]
        public void ProductionCurve_HasAuthoredLinearCostsAndExactTotal()
        {
            PlayerExperienceCurve curve = PlayerExperienceCurve.CreateProduction();

            Assert.That(curve.GetExperienceToAdvance(1), Is.EqualTo(100L));
            Assert.That(curve.GetExperienceToAdvance(2), Is.EqualTo(147L));
            Assert.That(curve.GetExperienceToAdvance(3), Is.EqualTo(195L));
            Assert.That(curve.GetExperienceToAdvance(99), Is.EqualTo(4748L));
            Assert.That(curve.MaximumProgressionExperience, Is.EqualTo(240000L));
        }

        [Test]
        public void LegacyPlaceholderMigration_RetainsCumulativeXpAndReceipts()
        {
            PlayerExperienceCurve legacy =
                PlayerExperienceCurve.CreateLegacyPlaceholder();
            var original = CreateAuthority(legacy);
            StableId source = StableId.Parse("xp-source.legacy-migration");
            original.Grant(new PlayerExperienceGrantRequest(source, 350L));

            var migrated = CreateAuthority(
                PlayerExperienceCurve.CreateProduction());
            PlayerExperienceImportResult result =
                migrated.TryMigrateLegacyPlaceholder(
                    original.ExportSnapshot());
            PlayerExperienceGrantFact replay = migrated.Grant(
                new PlayerExperienceGrantRequest(source, 350L));

            Assert.That(result.Status, Is.EqualTo(PlayerExperienceImportStatus.Imported));
            Assert.That(migrated.CurrentState.CumulativeExperience, Is.EqualTo(350L));
            Assert.That(migrated.CurrentState.Level, Is.EqualTo(3));
            Assert.That(migrated.CurrentSnapshot.CurveFingerprint,
                Is.EqualTo(PlayerExperienceCurve.CreateProduction().Fingerprint));
            Assert.That(replay.Status,
                Is.EqualTo(PlayerExperienceGrantStatus.DuplicateNoChange));
        }

        [Test]
        public void DuplicateGrant_ProducesNoAdditionalExperience()
        {
            var authority = CreateAuthority(CreateConstantCurve());
            StableId source = StableId.Parse("xp-source.enemy-one");
            var request = new PlayerExperienceGrantRequest(source, 100L);

            PlayerExperienceGrantFact applied = authority.Grant(request);
            PlayerExperienceGrantFact duplicate = authority.Grant(request);
            PlayerExperienceGrantFact conflict = authority.Grant(
                new PlayerExperienceGrantRequest(source, 101L));

            Assert.That(applied.Status, Is.EqualTo(PlayerExperienceGrantStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.DuplicateNoChange));
            Assert.That(conflict.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.ConflictingDuplicate));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(100L));
            Assert.That(authority.CurrentSnapshot.Sequence, Is.EqualTo(1L));
            Assert.That(duplicate.LevelUpFacts, Is.Empty);
            Assert.That(conflict.LevelUpFacts, Is.Empty);
        }

        [Test]
        public void LevelThresholds_UseCumulativeExperienceExactly()
        {
            var authority = CreateAuthority(CreateConstantCurve());

            authority.Grant(new PlayerExperienceGrantRequest(
                StableId.Parse("xp-source.first"),
                99L));

            Assert.That(authority.CurrentState.Level, Is.EqualTo(1));
            Assert.That(authority.CurrentState.ExperienceIntoCurrentLevel, Is.EqualTo(99L));
            Assert.That(authority.CurrentState.ExperienceToNextLevel, Is.EqualTo(1L));

            PlayerExperienceGrantFact threshold = authority.Grant(
                new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.second"),
                    1L));

            Assert.That(authority.CurrentState.Level, Is.EqualTo(2));
            Assert.That(authority.CurrentState.ExperienceIntoCurrentLevel, Is.Zero);
            Assert.That(authority.CurrentState.ExperienceToNextLevel, Is.EqualTo(100L));
            Assert.That(threshold.LevelUpFacts.Count, Is.EqualTo(1));
            Assert.That(threshold.LevelUpFacts[0].CumulativeThreshold, Is.EqualTo(100L));
            Assert.That(threshold.LevelUpFacts[0].SkillPointsGranted, Is.EqualTo(1));
            Assert.That(threshold.LevelUpFacts[0].TotalSkillPointsAfter, Is.EqualTo(2));
        }

        [Test]
        public void MultiLevelGrant_ReturnsOrderedLevelUpFacts()
        {
            var authority = CreateAuthority(CreateConstantCurve());

            PlayerExperienceGrantFact result = authority.Grant(
                new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.mission"),
                    350L));

            Assert.That(result.Status, Is.EqualTo(PlayerExperienceGrantStatus.Applied));
            Assert.That(result.LevelUpFacts.Count, Is.EqualTo(3));
            Assert.That(result.LevelUpFacts[0].CurrentLevel, Is.EqualTo(2));
            Assert.That(result.LevelUpFacts[1].CurrentLevel, Is.EqualTo(3));
            Assert.That(result.LevelUpFacts[2].CurrentLevel, Is.EqualTo(4));
            Assert.That(authority.CurrentState.Level, Is.EqualTo(4));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(350L));
            Assert.That(authority.CurrentState.ExperienceIntoCurrentLevel, Is.EqualTo(50L));
            Assert.That(authority.CurrentState.ExperienceToNextLevel, Is.EqualTo(50L));
            Assert.That(authority.CurrentState.TotalSkillPointsAwarded, Is.EqualTo(4));
            Assert.That(authority.CurrentContext.CharacterLevel, Is.EqualTo(4));
        }

        [Test]
        public void LevelOneHundred_CapsLevelAndTracksOverflowExplicitly()
        {
            PlayerExperienceCurve curve = CreateConstantCurve();
            var authority = CreateAuthority(curve);
            long amount = curve.MaximumProgressionExperience + 123L;

            PlayerExperienceGrantFact result = authority.Grant(
                new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.cap-grant"),
                    amount));

            Assert.That(result.LevelUpFacts.Count, Is.EqualTo(99));
            Assert.That(authority.CurrentState.Level, Is.EqualTo(100));
            Assert.That(authority.CurrentState.IsAtLevelCap, Is.True);
            Assert.That(
                authority.CurrentState.ProgressionExperience,
                Is.EqualTo(curve.MaximumProgressionExperience));
            Assert.That(authority.CurrentState.OverflowExperience, Is.EqualTo(123L));
            Assert.That(authority.CurrentState.ExperienceToNextLevel, Is.Zero);
            Assert.That(authority.CurrentState.TotalSkillPointsAwarded, Is.EqualTo(100));
            Assert.That(authority.CurrentContext.CharacterLevel, Is.EqualTo(100));
        }

        [Test]
        public void SnapshotRoundTrip_PreservesStateContextAndReplayProtection()
        {
            PlayerExperienceCurve curve = CreateConstantCurve();
            var original = CreateAuthority(curve, 5);
            StableId firstSource = StableId.Parse("xp-source.first-roundtrip");
            original.Grant(new PlayerExperienceGrantRequest(firstSource, 100L));
            original.Grant(new PlayerExperienceGrantRequest(
                StableId.Parse("xp-source.second-roundtrip"),
                250L));
            PlayerExperienceSnapshot exported = original.ExportSnapshot();

            var restored = CreateAuthority(curve, 999);
            PlayerExperienceImportResult import = restored.TryImport(exported);
            PlayerExperienceGrantFact replay = restored.Grant(
                new PlayerExperienceGrantRequest(firstSource, 100L));

            Assert.That(import.Status, Is.EqualTo(PlayerExperienceImportStatus.Imported));
            Assert.That(restored.CurrentState, Is.EqualTo(original.CurrentState));
            Assert.That(restored.CurrentContext, Is.EqualTo(original.CurrentContext));
            Assert.That(
                restored.CurrentSnapshot.Fingerprint,
                Is.EqualTo(exported.Fingerprint));
            Assert.That(replay.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.DuplicateNoChange));
            Assert.That(restored.CurrentState.CumulativeExperience, Is.EqualTo(350L));
            Assert.That(restored.CurrentSnapshot.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void CorruptSnapshot_RejectsAtomically()
        {
            PlayerExperienceCurve curve = CreateConstantCurve();
            var authority = CreateAuthority(curve);
            authority.Grant(new PlayerExperienceGrantRequest(
                StableId.Parse("xp-source.snapshot"),
                100L));
            PlayerExperienceSnapshot before = authority.CurrentSnapshot;
            var corrupt = new PlayerExperienceSnapshot(
                before.SchemaVersion,
                before.AuthorityStableId,
                before.Sequence,
                before.CurveFingerprint,
                before.CumulativeExperience + 1L,
                before.ProgressionContext,
                before.Grants,
                before.Fingerprint);

            PlayerExperienceImportResult result = authority.TryImport(corrupt);

            Assert.That(
                result.Status,
                Is.EqualTo(PlayerExperienceImportStatus.FingerprintMismatch));
            Assert.That(authority.CurrentSnapshot, Is.SameAs(before));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(100L));
        }

        [Test]
        public void SemanticallyInvalidCanonicalSnapshot_RejectsWithoutMutation()
        {
            PlayerExperienceCurve curve = CreateConstantCurve();
            var authority = CreateAuthority(curve);
            PlayerExperienceSnapshot before = authority.CurrentSnapshot;
            StableId source = StableId.Parse("xp-source.semantic");
            var grant = PlayerExperienceGrantSnapshot.Create(source, 100L, 1L);
            PlayerExperienceSnapshot invalid =
                PlayerExperienceSnapshot.CreateCanonical(
                    1L,
                    curve.Fingerprint,
                    101L,
                    ProgressionContext.Create(
                        2,
                        1,
                        StableId.Parse("difficulty.normal"),
                        0),
                    new[] { grant });

            PlayerExperienceImportResult result = authority.TryImport(invalid);

            Assert.That(
                result.Status,
                Is.EqualTo(PlayerExperienceImportStatus.ValidationRejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                "xp-snapshot-cumulative-mismatch"));
            Assert.That(authority.CurrentSnapshot, Is.SameAs(before));
        }

        [Test]
        public void SnapshotFromDifferentCurve_RejectsExplicitly()
        {
            var sourceAuthority = CreateAuthority(CreateConstantCurve());
            sourceAuthority.Grant(new PlayerExperienceGrantRequest(
                StableId.Parse("xp-source.curve"),
                100L));
            PlayerExperienceSnapshot snapshot =
                sourceAuthority.ExportSnapshot();
            var differentCurve = new PlayerExperienceCurve(
                100L,
                200L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
            var target = CreateAuthority(differentCurve);

            PlayerExperienceImportResult result =
                target.TryImport(snapshot);

            Assert.That(
                result.Status,
                Is.EqualTo(PlayerExperienceImportStatus.CurveMismatch));
            Assert.That(target.CurrentState.CumulativeExperience, Is.Zero);
        }

        [Test]
        public void InvalidGrantInputs_AreRejectedWithoutMutation()
        {
            var authority = CreateAuthority(CreateConstantCurve());

            PlayerExperienceGrantFact missingRequest = authority.Grant(null);
            PlayerExperienceGrantFact missingSource = authority.Grant(
                new PlayerExperienceGrantRequest(null, 1L));
            PlayerExperienceGrantFact zero = authority.Grant(
                new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.zero"),
                    0L));
            PlayerExperienceGrantFact negative = authority.Grant(
                new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.negative"),
                    -1L));

            Assert.That(missingRequest.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.InvalidRequest));
            Assert.That(missingSource.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.InvalidRequest));
            Assert.That(zero.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.InvalidAmount));
            Assert.That(negative.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.InvalidAmount));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.Zero);
            Assert.That(authority.CurrentSnapshot.Sequence, Is.Zero);
        }

        [Test]
        public void CumulativeOverflow_RejectsWithoutRecordingSecondGrant()
        {
            var authority = CreateAuthority(CreateConstantCurve());
            authority.Grant(new PlayerExperienceGrantRequest(
                StableId.Parse("xp-source.maximum"),
                long.MaxValue));

            PlayerExperienceGrantFact overflow = authority.Grant(
                new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.overflow"),
                    1L));

            Assert.That(overflow.Status, Is.EqualTo(
                PlayerExperienceGrantStatus.ArithmeticOverflow));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(long.MaxValue));
            Assert.That(authority.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExperienceAssemblies_HaveNoUnityEngineDependency()
        {
            AssertNoUnityReference(typeof(PlayerExperienceCurve).Assembly);
            AssertNoUnityReference(typeof(IPlayerExperience).Assembly);
            AssertNoUnityReference(typeof(PlayerExperience).Assembly);
        }

        [TestCase(0L, 100L, 50)]
        [TestCase(100L, 99L, 50)]
        [TestCase(100L, 100L, 0)]
        [TestCase(100L, 100L, 100)]
        public void InvalidCurveInputs_Throw(
            long minimum,
            long maximum,
            int nominalFullCostLevel)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayerExperienceCurve(
                    minimum,
                    maximum,
                    nominalFullCostLevel,
                    new SoftActivationCurveParameters(0.1, 10L, 10L)));
        }

        private static PlayerExperienceCurve CreateConstantCurve()
        {
            return new PlayerExperienceCurve(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
        }

        private static PlayerExperience CreateAuthority(
            PlayerExperienceCurve curve,
            int regionLevel = 1)
        {
            return new PlayerExperience(
                curve,
                ProgressionContext.Create(
                    42,
                    regionLevel,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }));
        }

        private static void AssertNoUnityReference(Assembly assembly)
        {
            AssemblyName[] references = assembly.GetReferencedAssemblies();
            for (int index = 0; index < references.Length; index++)
            {
                Assert.That(
                    references[index].Name.StartsWith(
                        "UnityEngine",
                        StringComparison.Ordinal),
                    Is.False);
            }
        }
    }
}
