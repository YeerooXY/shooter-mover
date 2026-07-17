using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.Domain.Weapons.Catalog
{
    internal static class WeaponCatalogFingerprint
    {
        public static string Calculate(WeaponCatalog catalog)
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, "version", catalog.Version);
            Append(builder, "status", catalog.Status);
            AppendRules(builder, catalog.Rules);
            AppendInputs(builder, catalog.Inputs);

            foreach (KeyValuePair<string, WeaponArchetypeDefinition> pair in catalog.Archetypes)
            {
                WeaponArchetypeDefinition value = pair.Value;
                Append(builder, "archetype.id", value.ArchetypeId);
                Append(builder, "archetype.description", value.Description);
                Append(builder, "archetype.dps_factor", value.DpsFactor);
                Append(builder, "archetype.fire_rate", value.FireRate);
                Append(builder, "archetype.projectiles", value.Projectiles);
                Append(builder, "archetype.burst", value.Burst);
                Append(builder, "archetype.spread", value.Spread);
                Append(builder, "archetype.speed", value.Speed);
                Append(builder, "archetype.range", value.Range);
                Append(builder, "archetype.direct_share", value.DirectShare);
                Append(builder, "archetype.area_share", value.AreaShare);
                Append(builder, "archetype.dot_share", value.DotShare);
                Append(builder, "archetype.radius", value.Radius);
                Append(builder, "archetype.dot_duration", value.DotDuration);
                Append(builder, "archetype.pool_radius", value.PoolRadius);
                Append(builder, "archetype.pool_duration", value.PoolDuration);
                Append(builder, "archetype.pierce", value.Pierce);
                Append(builder, "archetype.chain_targets", value.ChainTargets);
                Append(builder, "archetype.chain_range", value.ChainRange);
                Append(builder, "archetype.knockback", value.Knockback);
                Append(builder, "archetype.power_cost", value.PowerCost);
            }

            for (int index = 0; index < catalog.Families.Count; index++)
            {
                WeaponFamilyDefinition value = catalog.Families[index];
                Append(builder, "family.id", value.FamilyId);
                Append(builder, "family.display_name", value.DisplayName);
                Append(builder, "family.archetype", value.Archetype);
                Append(builder, "family.damage_type", value.DamageType);
                Append(builder, "family.build_affinity", value.BuildAffinity);
                Append(builder, "family.mk1_peak", value.Mk1Peak);
                Append(builder, "family.gap_mk1_to_2", value.GapMk1To2);
                Append(builder, "family.gap_mk2_to_3", value.GapMk2To3);
                Append(builder, "family.max_planned_mark", value.MaxPlannedMark);
                Append(builder, "family.mk1_rarity", value.Mk1Rarity);
                Append(builder, "family.mk2_rarity", value.Mk2Rarity);
                Append(builder, "family.mk3_rarity", value.Mk3Rarity);
                Append(builder, "family.definition_weight_modifier", value.DefinitionWeightModifier);
                Append(builder, "family.acquisition_class", value.AcquisitionClass);
                Append(builder, "family.primary_effect", value.PrimaryEffect);
                Append(builder, "family.notes", value.Notes);
                Append(builder, "family.availability", value.Availability.ToString());
                AppendList(builder, "family.side_profile_art", value.SideProfileArtReferences);
            }

            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                AppendDefinition(builder, catalog.Definitions[index]);
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                StringBuilder hex = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    hex.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
                }
                return "sha256:" + hex.ToString();
            }
        }

        private static void AppendRules(StringBuilder builder, WeaponCatalogRules value)
        {
            Append(builder, "rules.fixed_stats_per_definition", value.FixedStatsPerDefinition);
            Append(builder, "rules.runtime_level_scaling", value.RuntimeLevelScaling);
            Append(builder, "rules.ordinary_mark_gap", value.OrdinaryMarkGap);
            for (int index = 0; index < value.ApexPowerAnchors.Count; index++)
            {
                Append(builder, "rules.apex_power_anchor", value.ApexPowerAnchors[index]);
            }
            AppendList(builder, "rules.damage_type", value.DamageTypes);
            Append(builder, "rules.max_augments", value.MaxAugments);
            Append(builder, "rules.no_recoil", value.NoRecoil);
            Append(builder, "rules.no_spin_up", value.NoSpinUp);
            Append(builder, "rules.no_heat_generation", value.NoHeatGeneration);
        }

        private static void AppendInputs(StringBuilder builder, WeaponCatalogInputs value)
        {
            Append(builder, "inputs.base_dps", value.BaseDps);
            Append(builder, "inputs.growth_1_30", value.Growth1To30);
            Append(builder, "inputs.growth_31_70", value.Growth31To70);
            Append(builder, "inputs.growth_71_plus", value.Growth71Plus);
            List<string> rarityIds = new List<string>(value.Rarities.Keys);
            rarityIds.Sort(StringComparer.Ordinal);
            for (int index = 0; index < rarityIds.Count; index++)
            {
                WeaponRarityInput rarity = value.Rarities[rarityIds[index]];
                Append(builder, "rarity.id", rarity.Rarity);
                Append(builder, "rarity.weight", rarity.Weight);
                Append(builder, "rarity.power_bonus", rarity.PowerBonus);
                Append(builder, "rarity.early_tail", rarity.EarlyTail);
                Append(builder, "rarity.late_tail", rarity.LateTail);
            }
        }

        private static void AppendDefinition(StringBuilder builder, WeaponDefinitionData value)
        {
            Append(builder, "definition.id", value.DefinitionId);
            Append(builder, "definition.display_name", value.DisplayName);
            Append(builder, "definition.family_id", value.FamilyId);
            Append(builder, "definition.mark", value.Mark);
            Append(builder, "definition.damage_type", value.DamageType);
            Append(builder, "definition.archetype", value.Archetype);
            Append(builder, "definition.build_affinity", value.BuildAffinity);
            Append(builder, "definition.first_appearance", value.FirstAppearance);
            Append(builder, "definition.peak_drop_level", value.PeakDropLevel);
            Append(builder, "definition.power_anchor", value.PowerAnchor);
            Append(builder, "definition.rarity", value.Rarity);
            Append(builder, "definition.rarity_weight", value.RarityWeight);
            Append(builder, "definition.definition_weight_modifier", value.DefinitionWeightModifier);
            Append(builder, "definition.final_base_weight", value.FinalBaseWeight);
            Append(builder, "definition.early_tail", value.EarlyTail);
            Append(builder, "definition.late_tail", value.LateTail);
            Append(builder, "definition.acquisition_class", value.AcquisitionClass);
            Append(builder, "definition.top_box_only", value.TopBoxOnly);
            Append(builder, "definition.crafting_route", value.CraftingRoute);
            Append(builder, "definition.archetype_dps_factor", value.ArchetypeDpsFactor);
            Append(builder, "definition.power_index", value.PowerIndex);
            Append(builder, "definition.target_dps", value.TargetDps);
            Append(builder, "definition.direct_share", value.DirectShare);
            Append(builder, "definition.area_share", value.AreaShare);
            Append(builder, "definition.dot_share", value.DotShare);
            Append(builder, "definition.fire_rate", value.FireRate);
            Append(builder, "definition.projectiles_per_trigger", value.ProjectilesPerTrigger);
            Append(builder, "definition.burst_count", value.BurstCount);
            Append(builder, "definition.damage_per_projectile", value.DamagePerProjectile);
            Append(builder, "definition.spread_degrees", value.SpreadDegrees);
            Append(builder, "definition.projectile_speed", value.ProjectileSpeed);
            Append(builder, "definition.range", value.Range);
            Append(builder, "definition.pierce", value.Pierce);
            Append(builder, "definition.explosion_radius", value.ExplosionRadius);
            Append(builder, "definition.area_damage_per_trigger", value.AreaDamagePerTrigger);
            Append(builder, "definition.dot_dps", value.DotDps);
            Append(builder, "definition.dot_duration", value.DotDuration);
            Append(builder, "definition.pool_radius", value.PoolRadius);
            Append(builder, "definition.pool_duration", value.PoolDuration);
            Append(builder, "definition.chain_targets", value.ChainTargets);
            Append(builder, "definition.chain_range", value.ChainRange);
            Append(builder, "definition.knockback", value.Knockback);
            Append(builder, "definition.power_cost", value.PowerCost);
            Append(builder, "definition.healing_per_second", value.HealingPerSecond);
            Append(builder, "definition.primary_effect", value.PrimaryEffect);
            Append(builder, "definition.notes", value.Notes);
            Append(builder, "definition.availability", value.Availability.ToString());
            AppendList(builder, "definition.side_profile_art", value.SideProfileArtReferences);
        }

        private static void AppendList(StringBuilder builder, string name, IReadOnlyList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                Append(builder, name, values[index]);
            }
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            string text = value ?? string.Empty;
            builder.Append(name.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(name)
                .Append('=').Append(text.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(text)
                .Append('\n');
        }

        private static void Append(StringBuilder builder, string name, int value)
        {
            Append(builder, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Append(StringBuilder builder, string name, double value)
        {
            Append(builder, name, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Append(StringBuilder builder, string name, bool value)
        {
            Append(builder, name, value ? "true" : "false");
        }
    }
}
