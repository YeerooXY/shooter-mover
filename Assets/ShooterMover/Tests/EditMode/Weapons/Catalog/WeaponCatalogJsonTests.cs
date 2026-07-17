using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Tests.EditMode.Weapons.Catalog
{
    public sealed partial class WeaponCatalogJsonTests
    {
        [Test]
        public void Import_PreservesCompleteDefinitionShape()
        {
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(
                BuildCatalogJson(1, 1, false, -1, -1, false, false));
            Assert.That(result.IsSuccess, Is.True, JoinIssues(result));
            Assert.That(result.Catalog.Version, Is.EqualTo("0.1"));
            Assert.That(result.Catalog.Status, Is.EqualTo("planning baseline"));
            WeaponDefinitionData value = result.Catalog.Definitions[0];
            Assert.That(value.DefinitionId, Is.EqualTo("family_000.mk1"));
            Assert.That(value.DisplayName, Is.EqualTo("Family 000 MK1"));
            Assert.That(value.FamilyId, Is.EqualTo("family_000"));
            Assert.That(value.Mark, Is.EqualTo(1));
            Assert.That(value.DamageType, Is.EqualTo("Kinetic"));
            Assert.That(value.Archetype, Is.EqualTo("Hybrid"));
            Assert.That(value.BuildAffinity, Is.EqualTo("Universal"));
            Assert.That(value.FirstAppearance, Is.EqualTo(1));
            Assert.That(value.PeakDropLevel, Is.EqualTo(1));
            Assert.That(value.PowerAnchor, Is.EqualTo(1));
            Assert.That(value.Rarity, Is.EqualTo("Common"));
            Assert.That(value.RarityWeight, Is.EqualTo(1000.0));
            Assert.That(value.DefinitionWeightModifier, Is.EqualTo(1.0));
            Assert.That(value.FinalBaseWeight, Is.EqualTo(1000.0));
            Assert.That(value.EarlyTail, Is.EqualTo(4.0));
            Assert.That(value.LateTail, Is.EqualTo(13.0));
            Assert.That(value.AcquisitionClass, Is.EqualTo("Standard"));
            Assert.That(value.TopBoxOnly, Is.True);
            Assert.That(value.CraftingRoute, Is.EqualTo("Standard equipment generation"));
            Assert.That(value.ArchetypeDpsFactor, Is.EqualTo(1.0));
            Assert.That(value.PowerIndex, Is.EqualTo(100.0));
            Assert.That(value.TargetDps, Is.EqualTo(12.0));
            Assert.That(value.DirectShare, Is.EqualTo(0.2));
            Assert.That(value.AreaShare, Is.EqualTo(0.3));
            Assert.That(value.DotShare, Is.EqualTo(0.5));
            Assert.That(value.FireRate, Is.EqualTo(2.0));
            Assert.That(value.ProjectilesPerTrigger, Is.EqualTo(1));
            Assert.That(value.BurstCount, Is.EqualTo(1));
            Assert.That(value.DamagePerProjectile, Is.EqualTo(1.2).Within(0.000001));
            Assert.That(value.SpreadDegrees, Is.EqualTo(1.25));
            Assert.That(value.ProjectileSpeed, Is.EqualTo(40.0));
            Assert.That(value.Range, Is.EqualTo(30.0));
            Assert.That(value.Pierce, Is.EqualTo(1));
            Assert.That(value.ExplosionRadius, Is.EqualTo(2.0));
            Assert.That(value.AreaDamagePerTrigger, Is.EqualTo(1.8).Within(0.000001));
            Assert.That(value.DotDps, Is.EqualTo(6.0).Within(0.000001));
            Assert.That(value.DotDuration, Is.EqualTo(3.0));
            Assert.That(value.PoolRadius, Is.EqualTo(4.0));
            Assert.That(value.PoolDuration, Is.EqualTo(5.0));
            Assert.That(value.ChainTargets, Is.EqualTo(2));
            Assert.That(value.ChainRange, Is.EqualTo(6.0));
            Assert.That(value.Knockback, Is.EqualTo(0.7));
            Assert.That(value.PowerCost, Is.EqualTo(1.5));
            Assert.That(value.HealingPerSecond, Is.EqualTo(2.5));
            Assert.That(value.PrimaryEffect, Is.EqualTo("Primary effect"));
            Assert.That(value.Notes, Is.EqualTo("Definition notes"));
            Assert.That(value.SideProfileArtReferences, Is.EqualTo(new[] { "art/family_000-mk1.png" }));
        }

        [Test]
        public void Import_DeterministicallyOrdersAndFingerprints_UnorderedContent()
        {
            WeaponCatalogImportResult forward = WeaponCatalogJsonImporter.Import(BuildCatalogJson(5, 3, false, -1, -1, false, false));
            WeaponCatalogImportResult reverse = WeaponCatalogJsonImporter.Import(BuildCatalogJson(5, 3, true, -1, -1, false, false));
            Assert.That(forward.IsSuccess, Is.True, JoinIssues(forward));
            Assert.That(reverse.IsSuccess, Is.True, JoinIssues(reverse));
            Assert.That(reverse.Catalog.Fingerprint, Is.EqualTo(forward.Catalog.Fingerprint));
            Assert.That(reverse.Catalog.Definitions[0].DefinitionId, Is.EqualTo("family_000.mk1"));
            Assert.That(reverse.Catalog.Definitions[14].DefinitionId, Is.EqualTo("family_004.mk3"));
            Assert.That(WeaponCatalogCanonicalJson.Export(reverse.Catalog), Is.EqualTo(WeaponCatalogCanonicalJson.Export(forward.Catalog)));
        }

        [Test]
        public void CanonicalExport_RoundTripsWithIdenticalFingerprintAndBytes()
        {
            WeaponCatalogImportResult original = WeaponCatalogJsonImporter.Import(BuildCatalogJson(3, 3, true, 1, 8, false, false));
            Assert.That(original.IsSuccess, Is.True, JoinIssues(original));
            string firstExport = WeaponCatalogCanonicalJson.Export(original.Catalog);
            WeaponCatalogImportResult roundTrip = WeaponCatalogJsonImporter.Import(firstExport);
            Assert.That(roundTrip.IsSuccess, Is.True, JoinIssues(roundTrip));
            Assert.That(roundTrip.Catalog.Fingerprint, Is.EqualTo(original.Catalog.Fingerprint));
            Assert.That(WeaponCatalogCanonicalJson.Export(roundTrip.Catalog), Is.EqualTo(firstExport));
        }

        [Test]
        public void AvailabilityFiltering_ExcludesPreviewFamilyAndPreviewDefinitionFromLive()
        {
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(BuildCatalogJson(3, 1, false, 1, 2, false, false));
            Assert.That(result.IsSuccess, Is.True, JoinIssues(result));
            Assert.That(result.Catalog.GetFamilies(WeaponCatalogContentFilter.LiveOnly).Count, Is.EqualTo(2));
            Assert.That(result.Catalog.GetFamilies(WeaponCatalogContentFilter.PreviewOnly).Count, Is.EqualTo(1));
            Assert.That(result.Catalog.GetDefinitions(WeaponCatalogContentFilter.LiveOnly).Count, Is.EqualTo(1));
            Assert.That(result.Catalog.GetDefinitions(WeaponCatalogContentFilter.PreviewOnly).Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateDefinitionId_IsRejectedDeterministically()
        {
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(BuildCatalogJson(1, 1, false, -1, -1, true, false));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(HasIssue(result, WeaponCatalogIssueCode.DuplicateId), Is.True, JoinIssues(result));
        }

        [Test]
        public void DuplicateFamilyMarkPair_IsRejectedDeterministically()
        {
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(BuildCatalogJson(1, 1, false, -1, -1, false, true));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(HasIssue(result, WeaponCatalogIssueCode.DuplicateFamilyMark), Is.True, JoinIssues(result));
        }

        [Test]
        public void UnsupportedArchetype_IsRejected()
        {
            string json = BuildCatalogJson(1, 1, false, -1, -1, false, false).Replace("\"Archetype\":\"Hybrid\"", "\"Archetype\":\"Unknown\"");
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(json);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(HasIssue(result, WeaponCatalogIssueCode.UnsupportedArchetype), Is.True, JoinIssues(result));
        }

        [Test]
        public void InvalidRangesAndShareTotals_AreRejected()
        {
            string json = BuildCatalogJson(1, 1, false, -1, -1, false, false).Replace("\"Range\":30", "\"Range\":-1").Replace("\"DirectShare\":0.2", "\"DirectShare\":0.4");
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(json);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(HasIssue(result, WeaponCatalogIssueCode.RangeViolation), Is.True, JoinIssues(result));
            Assert.That(HasIssue(result, WeaponCatalogIssueCode.ShareTotalMismatch), Is.True, JoinIssues(result));
        }

        [Test]
        public void DerivedPowerWeightAndDpsDrift_IsRejected()
        {
            string json = BuildCatalogJson(1, 1, false, -1, -1, false, false).Replace("\"PowerIndex\":100", "\"PowerIndex\":101").Replace("\"FinalBaseWeight\":1000", "\"FinalBaseWeight\":999").Replace("\"DoTDPS\":6", "\"DoTDPS\":7");
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(json);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(HasIssue(result, WeaponCatalogIssueCode.DerivedValueMismatch), Is.True, JoinIssues(result));
        }

        [Test]
        public void LargeEvolvingCatalog_IsNotLockedToHistoricalCounts()
        {
            WeaponCatalogImportResult result = WeaponCatalogJsonImporter.Import(BuildCatalogJson(45, 3, true, 44, -1, false, false));
            Assert.That(result.IsSuccess, Is.True, JoinIssues(result));
            Assert.That(result.Catalog.Families.Count, Is.EqualTo(45));
            Assert.That(result.Catalog.Definitions.Count, Is.EqualTo(135));
            Assert.That(result.Catalog.GetDefinitions(WeaponCatalogContentFilter.LiveOnly).Count, Is.EqualTo(132));
            Assert.That(result.Catalog.GetDefinitions(WeaponCatalogContentFilter.PreviewOnly).Count, Is.EqualTo(3));
        }

        private static bool HasIssue(WeaponCatalogImportResult result, WeaponCatalogIssueCode code)
        {
            for (int index = 0; index < result.Issues.Count; index++) if (result.Issues[index].Code == code) return true;
            return false;
        }

        private static string JoinIssues(WeaponCatalogImportResult result)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < result.Issues.Count; index++)
            {
                if (index > 0) builder.AppendLine();
                builder.Append(result.Issues[index].ToString());
            }
            return builder.ToString();
        }
    }
}
