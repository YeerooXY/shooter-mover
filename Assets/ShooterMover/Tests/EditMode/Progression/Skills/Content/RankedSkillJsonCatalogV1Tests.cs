using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Progression.Skills.Content;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Progression.Skills.Content
{
    public sealed class RankedSkillJsonCatalogV1Tests
    {
        private const string RelativeCatalogPath = "ShooterMover/Content/Definitions/Progression/Skills/ranked_skills_v01.json";

        [Test]
        public void FullCatalogImportsAndReportsExpectedContent()
        {
            RankedSkillJsonImportResultV1 result = ImportProductionDraft();
            Assert.That(result.Success, Is.True, FormatDiagnostics(result));
            Assert.That(result.Catalog.Skills.Count, Is.EqualTo(20));
            Assert.That(result.Catalog.Synergies.Count, Is.EqualTo(2));
            Assert.That(result.ImportedSynergies.Count, Is.EqualTo(2));
            Assert.That(result.Summary.TotalPurchasableRanks, Is.EqualTo(306));
            Assert.That(result.Summary.MaximumRanksByClass["class.striker"], Is.GreaterThan(100));
            Assert.That(result.NormalizedFingerprint, Has.Length.EqualTo(64));
        }

        [Test]
        public void ClassSpecificCapsAreImported()
        {
            RankedSkillJsonImportResultV1 result = ImportProductionDraft();
            RankedSkillDefinitionV2 armor;
            RankedSkillDefinitionV2 speed;
            Assert.That(result.Catalog.TryGet("generic.combat.armor", out armor), Is.True);
            Assert.That(armor.EffectiveMaximumRank("class.striker"), Is.EqualTo(6));
            Assert.That(armor.EffectiveMaximumRank("class.combat_medic"), Is.EqualTo(6));
            Assert.That(armor.EffectiveMaximumRank("class.juggernaut"), Is.EqualTo(18));
            Assert.That(result.Catalog.TryGet("generic.combat.movement_speed", out speed), Is.True);
            Assert.That(speed.EffectiveMaximumRank("class.striker"), Is.EqualTo(18));
            Assert.That(speed.EffectiveMaximumRank("class.combat_medic"), Is.EqualTo(6));
            Assert.That(speed.EffectiveMaximumRank("class.juggernaut"), Is.EqualTo(9));
        }

        [Test]
        public void TripleReserveRequiresEightRanksInBothSkills()
        {
            RankedSkillJsonImportResultV1 result = ImportProductionDraft();
            SkillSynergyDefinitionV2 synergy = result.Catalog.Synergies.Single(x => x.Id == "synergy.striker.triple_reserve");
            var sevenEight = new RankedSkillAllocationSnapshotV2("profile.demo", "class.striker", 1, result.Catalog.SchemaVersion, result.Catalog.ContentVersion,
                new System.Collections.Generic.Dictionary<string, int> { ["striker.thruster_recovery"] = 8, ["striker.movement_efficiency"] = 7 });
            var eightEight = new RankedSkillAllocationSnapshotV2("profile.demo", "class.striker", 2, result.Catalog.SchemaVersion, result.Catalog.ContentVersion,
                new System.Collections.Generic.Dictionary<string, int> { ["striker.thruster_recovery"] = 8, ["striker.movement_efficiency"] = 8 });
            Assert.That(synergy.IsSatisfied(sevenEight), Is.False);
            Assert.That(synergy.IsSatisfied(eightEight), Is.True);
        }

        [Test]
        public void LegendaryProspectingRequiresIndividualAndCombinedRanksInDomainModel()
        {
            RankedSkillJsonImportResultV1 result = ImportProductionDraft();
            SkillSynergyDefinitionV2 synergy = result.Catalog.Synergies.Single(x => x.Id == "synergy.farming.legendary_prospecting");
            var twelve = Allocation(result, 4, 4, 4);
            var sixteen = Allocation(result, 8, 4, 4);
            Assert.That(synergy.CombinedRankRequirements.Count, Is.EqualTo(1));
            Assert.That(synergy.CombinedRankRequirements[0].MinimumCombinedRank, Is.EqualTo(16));
            Assert.That(synergy.IsSatisfied(twelve), Is.False);
            Assert.That(synergy.IsSatisfied(sixteen), Is.True);
            Assert.That(synergy.Effects.Single().Value, Is.EqualTo(0.20m));
        }

        [Test]
        public void StandardProjectorAddsLegendaryRelativeWeightOnlyWhenActive()
        {
            RankedSkillJsonImportResultV1 result = ImportProductionDraft();
            SkillEffectSnapshotV2 inactive = new SkillEffectProjectorV2().Project(result.Catalog, Allocation(result, 4, 4, 4));
            SkillEffectSnapshotV2 active = new SkillEffectProjectorV2().Project(result.Catalog, Allocation(result, 8, 4, 4));
            Assert.That(inactive.Apply("reward.legendary_definition_weight_multiplier", 1m), Is.EqualTo(1m));
            Assert.That(active.Apply("reward.legendary_definition_weight_multiplier", 1m), Is.EqualTo(1.20m));
        }

        [Test]
        public void FormattingOnlyDifferencesDoNotChangeNormalizedFingerprint()
        {
            string json = ReadProductionDraft();
            RankedSkillJsonImportResultV1 compact = RankedSkillJsonCanonicalV2.Import(json.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("  ", string.Empty));
            RankedSkillJsonImportResultV1 normal = RankedSkillJsonCanonicalV2.Import(json);
            Assert.That(compact.Success, Is.True, FormatDiagnostics(compact));
            Assert.That(compact.NormalizedFingerprint, Is.EqualTo(normal.NormalizedFingerprint));
        }

        [Test]
        public void LegendaryRelativeBonusAboveTwentyPercentIsRejected()
        {
            string json = ReadProductionDraft().Replace("\"statId\": \"reward.legendary_definition_weight_multiplier\", \"kind\": \"Percentage\", \"value\": 0.20",
                "\"statId\": \"reward.legendary_definition_weight_multiplier\", \"kind\": \"Percentage\", \"value\": 0.21");
            RankedSkillJsonImportResultV1 result = RankedSkillJsonCanonicalV2.Import(json);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Any(x => x.ErrorCode == "skill-legendary-relative-bonus-invalid"), Is.True);
        }

        [Test]
        public void UnknownEffectIdIsRejected()
        {
            string json = ReadProductionDraft().Replace("economy.credit_reward_multiplier\", \"kind\": \"Percentage\", \"valueSource\": \"rankValue",
                "economy.unknown_reward_multiplier\", \"kind\": \"Percentage\", \"valueSource\": \"rankValue");
            RankedSkillJsonImportResultV1 result = RankedSkillJsonCanonicalV2.Import(json);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Any(x => x.ErrorCode == "skill-effect-id-unknown"), Is.True);
        }

        [Test]
        public void UnsatisfiableCombinedRankRequirementIsRejectedByDomainCatalog()
        {
            RankedSkillJsonImportResultV1 result = ImportProductionDraft();
            SkillSynergyDefinitionV2 legendary = result.Catalog.Synergies.Single(x => x.Id == "synergy.farming.legendary_prospecting");
            var impossible = new SkillSynergyDefinitionV2("synergy.test.impossible", legendary.Requirements, legendary.Effects,
                new[] { new SkillCombinedRankRequirementV2(legendary.CombinedRankRequirements[0].SkillIds, 999) });
            Assert.Throws<ArgumentException>(() => new RankedSkillCatalogV2(result.Catalog.SchemaVersion, result.Catalog.ContentVersion,
                result.Catalog.Skills, result.Catalog.Synergies.Concat(new[] { impossible })));
        }

        [Test]
        public void ExcludedRejectedSkillNamesAreAbsent()
        {
            string json = ReadProductionDraft();
            string[] rejected = { "Triage", "Medical Reserve", "Lasting Presence", "Overlapping Fire", "Arsenal Rotation", "Fortress Protocol", "Brace", "Forward Pressure", "Overrun", "Marked for Elimination" };
            foreach (string value in rejected) Assert.That(json, Does.Not.Contain(value));
        }

        private static RankedSkillAllocationSnapshotV2 Allocation(RankedSkillJsonImportResultV1 result, int credits, int finder, int quality) =>
            new RankedSkillAllocationSnapshotV2("profile.demo", "class.striker", credits + finder + quality, result.Catalog.SchemaVersion, result.Catalog.ContentVersion,
                new System.Collections.Generic.Dictionary<string, int> { ["generic.farming.credit_gain"] = credits, ["generic.farming.strongbox_finder"] = finder, ["generic.farming.strongbox_quality"] = quality });

        private static RankedSkillJsonImportResultV1 ImportProductionDraft() => RankedSkillJsonCanonicalV2.Import(ReadProductionDraft());
        private static string ReadProductionDraft() => File.ReadAllText(Path.Combine(Application.dataPath, RelativeCatalogPath));
        private static string FormatDiagnostics(RankedSkillJsonImportResultV1 result) => string.Join("\n", result.Diagnostics.Select(x => x.ErrorCode + " " + x.JsonPath + " " + x.Message));
    }
}
