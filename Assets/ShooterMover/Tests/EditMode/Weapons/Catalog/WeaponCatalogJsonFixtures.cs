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
        private static string BuildCatalogJson(
            int familyCount,
            int marksPerFamily,
            bool reverse,
            int previewFamilyIndex,
            int previewDefinitionIndex,
            bool duplicateDefinition,
            bool duplicateFamilyMark)
        {
            List<int> familyOrder = new List<int>();
            for (int family = 0; family < familyCount; family++) familyOrder.Add(family);
            if (reverse) familyOrder.Reverse();

            List<DefinitionSpec> definitions = new List<DefinitionSpec>();
            for (int family = 0; family < familyCount; family++)
                for (int mark = 1; mark <= marksPerFamily; mark++)
                    definitions.Add(new DefinitionSpec(family, mark, false));
            if (reverse) definitions.Reverse();
            if (duplicateDefinition && definitions.Count > 0) definitions.Add(definitions[0]);
            if (duplicateFamilyMark && definitions.Count > 0) definitions.Add(new DefinitionSpec(0, 1, true));

            StringBuilder json = new StringBuilder();
            json.Append('{');
            json.Append("\"version\":\"0.1\",");
            json.Append("\"status\":\"planning baseline\",");
            json.Append("\"rules\":{");
            json.Append("\"fixed_stats_per_definition\":true,");
            json.Append("\"runtime_level_scaling\":false,");
            json.Append("\"ordinary_mark_gap\":\"20-25 peak-drop levels\",");
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
            json.Append("\"weight\":1000,\"power_bonus\":0,\"early_tail\":4,\"late_tail\":13}}},");
            json.Append("\"archetypes\":{\"Hybrid\":{");
            json.Append("\"description\":\"Hybrid test archetype\",");
            json.Append("\"dps_factor\":1,\"fire_rate\":2,\"projectiles\":1,\"burst\":1,");
            json.Append("\"spread\":1.25,\"speed\":40,\"range\":30,");
            json.Append("\"direct_share\":0.2,\"area_share\":0.3,\"dot_share\":0.5,");
            json.Append("\"radius\":2,\"dot_duration\":3,\"pool_radius\":4,\"pool_duration\":5,");
            json.Append("\"pierce\":1,\"chain_targets\":2,\"chain_range\":6,");
            json.Append("\"knockback\":0.7,\"power_cost\":1.5}},");
            json.Append("\"families\":[");
            for (int orderIndex = 0; orderIndex < familyOrder.Count; orderIndex++)
            {
                if (orderIndex > 0) json.Append(',');
                int family = familyOrder[orderIndex];
                string id = FamilyId(family);
                json.Append('{');
                AddString(json, "FamilyId", id, true);
                AddString(json, "DisplayName", "Family " + family.ToString("000", CultureInfo.InvariantCulture), true);
                AddString(json, "Archetype", "Hybrid", true);
                AddString(json, "DamageType", "Kinetic", true);
                AddString(json, "BuildAffinity", "Universal", true);
                AddNumber(json, "MK1Peak", 1, true);
                AddNumber(json, "GapMK1To2", 1, true);
                AddNumber(json, "GapMK2To3", 1, true);
                AddNumber(json, "MaxPlannedMark", marksPerFamily, true);
                AddString(json, "MK1Rarity", "Common", true);
                AddString(json, "MK2Rarity", marksPerFamily >= 2 ? "Common" : string.Empty, true);
                AddString(json, "MK3Rarity", marksPerFamily >= 3 ? "Common" : string.Empty, true);
                AddNumber(json, "DefinitionWeightModifier", 1.0, true);
                AddString(json, "AcquisitionClass", "Standard", true);
                AddString(json, "PrimaryEffect", "Primary effect", true);
                AddString(json, "Notes", "Family notes", true);
                AddString(json, "Availability", family == previewFamilyIndex ? "PreviewOnly" : "Live", true);
                json.Append("\"SideProfileArtReference\":\"art/").Append(id).Append(".png\"");
                json.Append('}');
            }
            json.Append("],\"definitions\":[");
            for (int index = 0; index < definitions.Count; index++)
            {
                if (index > 0) json.Append(',');
                WriteDefinition(json, definitions[index], index == previewDefinitionIndex);
            }
            json.Append("]}");
            return json.ToString();
        }

        private static void WriteDefinition(StringBuilder json, DefinitionSpec spec, bool previewOnly)
        {
            string familyId = FamilyId(spec.Family);
            string definitionId = spec.ConflictingId ? "family_conflict.mk1" : familyId + ".mk" + spec.Mark.ToString(CultureInfo.InvariantCulture);
            int powerAnchor = spec.Mark;
            double powerIndex = PowerIndex(powerAnchor);
            double targetDps = 12.0 * powerIndex / 100.0;
            double fireRate = 2.0;
            json.Append('{');
            AddString(json, "DefinitionId", definitionId, true);
            AddString(json, "DisplayName", "Family " + spec.Family.ToString("000", CultureInfo.InvariantCulture) + " MK" + spec.Mark, true);
            AddString(json, "FamilyId", familyId, true);
            AddNumber(json, "Mark", spec.Mark, true);
            AddString(json, "DamageType", "Kinetic", true);
            AddString(json, "Archetype", "Hybrid", true);
            AddString(json, "BuildAffinity", "Universal", true);
            AddNumber(json, "FirstAppearance", powerAnchor, true);
            AddNumber(json, "PeakDropLevel", powerAnchor, true);
            AddNumber(json, "PowerAnchor", powerAnchor, true);
            AddString(json, "Rarity", "Common", true);
            AddNumber(json, "RarityWeight", 1000.0, true);
            AddNumber(json, "DefinitionWeightModifier", 1.0, true);
            AddNumber(json, "FinalBaseWeight", 1000.0, true);
            AddNumber(json, "EarlyTail", 4.0, true);
            AddNumber(json, "LateTail", 13.0, true);
            AddString(json, "AcquisitionClass", "Standard", true);
            AddString(json, "TopBoxOnly", "Yes", true);
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
            AddString(json, "Availability", previewOnly ? "PreviewOnly" : "Live", true);
            json.Append("\"SideProfileArtReferences\":[\"art/").Append(familyId).Append("-mk").Append(spec.Mark).Append(".png\"]");
            json.Append('}');
        }

        private static string FamilyId(int family) { return "family_" + family.ToString("000", CultureInfo.InvariantCulture); }
        private static double PowerIndex(int level) { double value = 100.0; for (int current = 2; current <= level; current++) value *= 1.05; return value; }
        private static void AddString(StringBuilder json, string name, string value, bool comma) { json.Append('"').Append(name).Append("\":\"").Append(value).Append('"'); if (comma) json.Append(','); }
        private static void AddNumber(StringBuilder json, string name, double value, bool comma) { json.Append('"').Append(name).Append("\":").Append(value.ToString("R", CultureInfo.InvariantCulture)); if (comma) json.Append(','); }

        private sealed class DefinitionSpec
        {
            public DefinitionSpec(int family, int mark, bool conflictingId) { Family = family; Mark = mark; ConflictingId = conflictingId; }
            public int Family { get; private set; }
            public int Mark { get; private set; }
            public bool ConflictingId { get; private set; }
        }
    }
}
