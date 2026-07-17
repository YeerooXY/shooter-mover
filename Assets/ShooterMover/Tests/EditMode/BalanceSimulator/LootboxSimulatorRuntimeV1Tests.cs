using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using ShooterMover.Contracts.Holdings;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed partial class LootboxSimulatorRuntimeV1Tests
    {
        [Test]
        public void SameInputProducesSameFrozenEquipmentAndDifferentOrdinalsStayUnique()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();

            LootboxGeneratedItemV1 first = runtime.Generate(5, 30, 123456UL, 0);
            LootboxGeneratedItemV1 replay = runtime.Generate(5, 30, 123456UL, 0);
            LootboxGeneratedItemV1 second = runtime.Generate(5, 30, 123456UL, 1);

            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(replay.Equipment.Fingerprint, Is.EqualTo(first.Equipment.Fingerprint));
            Assert.That(replay.Equipment.InstanceId, Is.EqualTo(first.Equipment.InstanceId));
            Assert.That(second.Equipment.InstanceId, Is.Not.EqualTo(first.Equipment.InstanceId));
            Assert.That(first.Equipment.Augments.Count, Is.InRange(1, 3));
            Assert.That(first.SourceDefinitionId, Does.StartWith("family_000.mk"));
        }

        [Test]
        public void KeepAndSellAreExactlyOnceForTheConcreteEquipmentInstance()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            LootboxGeneratedItemV1 kept = runtime.Generate(1, 30, 8765UL, 0);
            LootboxGeneratedItemV1 sold = runtime.Generate(1, 30, 8765UL, 1);

            Assert.That(
                runtime.Keep(kept),
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(
                runtime.Keep(kept),
                Is.EqualTo(PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange));
            Assert.That(runtime.Sell(kept), Is.False);
            Assert.That(runtime.AcceptedInventory.Count, Is.EqualTo(1));
            Assert.That(runtime.AcceptedInventory[0].InstanceId, Is.EqualTo(kept.Equipment.InstanceId));

            Assert.That(runtime.Sell(sold), Is.True);
            Assert.That(runtime.Sell(sold), Is.False);
            Assert.That(runtime.Cash, Is.EqualTo(1000L));
            Assert.That(runtime.AcceptedInventory.Count, Is.EqualTo(1));
        }

        [Test]
        public void AntimatterOddsExposeThreeSlotsAndOneToTenAugmentLevelsDeterministically()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();

            LootboxOddsReportV1 first = runtime.CalculateOdds(11, 30, 99UL, 64);
            LootboxOddsReportV1 replay = runtime.CalculateOdds(11, 30, 99UL, 64);

            Assert.That(first.RejectedRolls, Is.Zero);
            Assert.That(first.SuccessfulOpenCount, Is.EqualTo(64));
            Assert.That(Sum(first.ItemOdds), Is.EqualTo(64L));
            Assert.That(Sum(first.QualityOdds), Is.EqualTo(64L));
            Assert.That(Sum(first.SlotOdds), Is.EqualTo(64L));
            Assert.That(first.SlotOdds.Count, Is.EqualTo(1));
            Assert.That(first.SlotOdds[0].Key, Is.EqualTo("3"));
            Assert.That(first.SlotOdds[0].Count, Is.EqualTo(64L));
            Assert.That(Sum(first.AugmentTierOdds), Is.EqualTo(192L));
            Assert.That(Sum(first.AugmentLevelOdds), Is.EqualTo(192L));
            Assert.That(Sum(first.ItemLevelDeltaOdds), Is.EqualTo(64L));
            for (int index = 0; index < first.AugmentLevelOdds.Count; index++)
            {
                int value = int.Parse(
                    first.AugmentLevelOdds[index].Key,
                    CultureInfo.InvariantCulture);
                Assert.That(value, Is.InRange(1, 10));
            }
            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(replay.ToCanonicalString(), Is.EqualTo(first.ToCanonicalString()));
        }

        [Test]
        public void InvalidCatalogReturnsDiagnosticWithoutCreatingRuntime()
        {
            LootboxSimulatorRuntimeV1 runtime;
            string diagnostic;

            Assert.That(
                LootboxSimulatorRuntimeV1.TryCreate("{}", out runtime, out diagnostic),
                Is.False);
            Assert.That(runtime, Is.Null);
            Assert.That(diagnostic, Is.Not.Empty);
        }

        private static long Sum(IReadOnlyList<LootboxOddsEntryV1> values)
        {
            long result = 0L;
            for (int index = 0; index < values.Count; index++)
            {
                result += values[index].Count;
            }
            return result;
        }

        private static LootboxSimulatorRuntimeV1 CreateRuntime()
        {
            LootboxSimulatorRuntimeV1 runtime;
            string diagnostic;
            bool created = LootboxSimulatorRuntimeV1.TryCreate(
                BuildCatalogJson(),
                out runtime,
                out diagnostic);
            Assert.That(created, Is.True, diagnostic);
            Assert.That(runtime, Is.Not.Null);
            return runtime;
        }

        private static string BuildCatalogJson()
        {
            var json = new StringBuilder();
            json.Append('{');
            json.Append("\"version\":\"0.1\",");
            json.Append("\"status\":\"simulator fixture\",");
            json.Append("\"rules\":{");
            json.Append("\"fixed_stats_per_definition\":true,");
            json.Append("\"runtime_level_scaling\":false,");
            json.Append("\"ordinary_mark_gap\":\"1\",");
            json.Append("\"apex_power_anchors\":[75,105,135],");
            json.Append("\"damage_types\":[\"Kinetic\"],");
            json.Append("\"max_augments\":10,");
            json.Append("\"no_recoil\":true,");
            json.Append("\"no_spin_up\":true,");
            json.Append("\"no_heat_generation\":true},");
            json.Append("\"inputs\":{");
            json.Append("\"base_dps\":12,");
            json.Append("\"growth_1_30\":0.05,");
            json.Append("\"growth_31_70\":0.055,");
            json.Append("\"growth_71_plus\":0.06,");
            json.Append("\"rarities\":{\"Common\":{");
            json.Append("\"weight\":1000,");
            json.Append("\"power_bonus\":0,");
            json.Append("\"early_tail\":4,");
            json.Append("\"late_tail\":13}}},");
            json.Append("\"archetypes\":{\"Hybrid\":{");
            json.Append("\"description\":\"Hybrid test archetype\",");
            json.Append("\"dps_factor\":1,");
            json.Append("\"fire_rate\":2,");
            json.Append("\"projectiles\":1,");
            json.Append("\"burst\":1,");
            json.Append("\"spread\":1.25,");
            json.Append("\"speed\":40,");
            json.Append("\"range\":30,");
            json.Append("\"direct_share\":0.2,");
            json.Append("\"area_share\":0.3,");
            json.Append("\"dot_share\":0.5,");
            json.Append("\"radius\":2,");
            json.Append("\"dot_duration\":3,");
            json.Append("\"pool_radius\":4,");
            json.Append("\"pool_duration\":5,");
            json.Append("\"pierce\":1,");
            json.Append("\"chain_targets\":2,");
            json.Append("\"chain_range\":6,");
            json.Append("\"knockback\":0.7,");
            json.Append("\"power_cost\":1.5}},");
            json.Append("\"families\":[{");
            AddString(json, "FamilyId", "family_000", true);
            AddString(json, "DisplayName", "Family 000", true);
            AddString(json, "Archetype", "Hybrid", true);
            AddString(json, "DamageType", "Kinetic", true);
            AddString(json, "BuildAffinity", "Universal", true);
            AddNumber(json, "MK1Peak", 1, true);
            AddNumber(json, "GapMK1To2", 1, true);
            AddNumber(json, "GapMK2To3", 1, true);
            AddNumber(json, "MaxPlannedMark", 3, true);
            AddString(json, "MK1Rarity", "Common", true);
            AddString(json, "MK2Rarity", "Common", true);
            AddString(json, "MK3Rarity", "Common", true);
            AddNumber(json, "DefinitionWeightModifier", 1.0, true);
            AddString(json, "AcquisitionClass", "Standard", true);
            AddString(json, "PrimaryEffect", "Primary effect", true);
            AddString(json, "Notes", "Family notes", true);
            AddString(json, "Availability", "Live", true);
            json.Append("\"SideProfileArtReference\":\"art/family_000.png\"");
            json.Append("}],\"definitions\":[");
            for (int mark = 1; mark <= 3; mark++)
            {
                if (mark > 1) json.Append(',');
                WriteDefinition(json, mark);
            }
            json.Append("]}");
            return json.ToString();
        }

        private static void WriteDefinition(StringBuilder json, int mark)
        {
            double powerIndex = PowerIndex(mark);
            double targetDps = 12.0 * powerIndex / 100.0;
            const double fireRate = 2.0;
            json.Append('{');
            AddString(json, "DefinitionId", "family_000.mk" + mark, true);
            AddString(json, "DisplayName", "Family 000 MK" + mark, true);
            AddString(json, "FamilyId", "family_000", true);
            AddNumber(json, "Mark", mark, true);
            AddString(json, "DamageType", "Kinetic", true);
            AddString(json, "Archetype", "Hybrid", true);
            AddString(json, "BuildAffinity", "Universal", true);
            AddNumber(json, "FirstAppearance", mark, true);
            AddNumber(json, "PeakDropLevel", mark, true);
            AddNumber(json, "PowerAnchor", mark, true);
            AddString(json, "Rarity", "Common", true);
            AddNumber(json, "RarityWeight", 1000.0, true);
            AddNumber(json, "DefinitionWeightModifier", 1.0, true);
            AddNumber(json, "FinalBaseWeight", 1000.0, true);
            AddNumber(json, "EarlyTail", 4.0, true);
            AddNumber(json, "LateTail", 13.0, true);
            AddString(json, "AcquisitionClass", "Standard", true);
            AddString(json, "TopBoxOnly", "No", true);
            AddString(json, "CraftingRoute", "Standard equipment generation", true);
            AddNumber(json, "ArchetypeDPSFactor", 1.0, true);
            AddNumber(json, "PowerIndex", powerIndex, true);
            AddNumber(json, "TargetDPS", targetDps, true);
            AddNumber(json, "DirectShare", 0.2, true);
            AddNumber(json, "AreaShare", 0.3, true);
            AddNumber(json, "DoTShare", 0.5, true);
            AddNumber(json, "FireRate", fireRate, true);
            AddNumber(json, "ProjectilesPerTrigger", 1, true);
            AddNumber(json, "BurstCount", 1, true);
            AddNumber(json, "DamagePerProjectile", targetDps * 0.2 / fireRate, true);
            AddNumber(json, "SpreadDegrees", 1.25, true);
            AddNumber(json, "ProjectileSpeed", 40.0, true);
            AddNumber(json, "Range", 30.0, true);
            AddNumber(json, "Pierce", 1, true);
            AddNumber(json, "ExplosionRadius", 2.0, true);
            AddNumber(json, "AreaDamagePerTrigger", targetDps * 0.3 / fireRate, true);
            AddNumber(json, "DoTDPS", targetDps * 0.5, true);
            AddNumber(json, "DoTDuration", 3.0, true);
            AddNumber(json, "PoolRadius", 4.0, true);
            AddNumber(json, "PoolDuration", 5.0, true);
            AddNumber(json, "ChainTargets", 2, true);
            AddNumber(json, "ChainRange", 6.0, true);
            AddNumber(json, "Knockback", 0.7, true);
            AddNumber(json, "PowerCost", 1.5, true);
            AddNumber(json, "HealingPerSecond", 2.5, true);
            AddString(json, "PrimaryEffect", "Primary effect", true);
            AddString(json, "Notes", "Definition notes", true);
            AddString(json, "Availability", "Live", true);
            json.Append("\"SideProfileArtReferences\":[\"art/family_000-mk")
                .Append(mark)
                .Append(".png\"]");
            json.Append('}');
        }

        private static double PowerIndex(int level)
        {
            double value = 100.0;
            for (int current = 2; current <= level; current++)
            {
                value *= 1.05;
            }
            return value;
        }

        private static void AddString(
            StringBuilder json,
            string name,
            string value,
            bool comma)
        {
            json.Append('"').Append(name).Append("\":\"").Append(value).Append('"');
            if (comma) json.Append(',');
        }

        private static void AddNumber(
            StringBuilder json,
            string name,
            double value,
            bool comma)
        {
            json.Append('"').Append(name).Append("\":")
                .Append(value.ToString("R", CultureInfo.InvariantCulture));
            if (comma) json.Append(',');
        }
    }
}
