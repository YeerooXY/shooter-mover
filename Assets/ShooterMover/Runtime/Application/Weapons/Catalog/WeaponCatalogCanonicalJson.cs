using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static class WeaponCatalogCanonicalJson
    {
        public static string Export(WeaponCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            CanonicalWriter writer = new CanonicalWriter();
            writer.BeginObject();
            writer.Property("version", catalog.Version);
            writer.Property("status", catalog.Status);
            writer.Name("rules");
            WriteRules(writer, catalog.Rules);
            writer.Name("inputs");
            WriteInputs(writer, catalog.Inputs);
            writer.Name("archetypes");
            writer.BeginObject();
            foreach (KeyValuePair<string, WeaponArchetypeDefinition> pair in catalog.Archetypes)
            {
                writer.Name(pair.Key);
                WriteArchetype(writer, pair.Value);
            }
            writer.EndObject();
            writer.Name("families");
            writer.BeginArray();
            for (int index = 0; index < catalog.Families.Count; index++)
            {
                WriteFamily(writer, catalog.Families[index]);
            }
            writer.EndArray();
            writer.Name("definitions");
            writer.BeginArray();
            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                WriteDefinition(writer, catalog.Definitions[index]);
            }
            writer.EndArray();
            writer.EndObject();
            return writer.ToString() + "\n";
        }

        private static void WriteRules(CanonicalWriter writer, WeaponCatalogRules value)
        {
            writer.BeginObject();
            writer.Property("fixed_stats_per_definition", value.FixedStatsPerDefinition);
            writer.Property("ordinary_mark_gap", value.OrdinaryMarkGap);
            writer.Name("apex_power_anchors");
            writer.BeginArray();
            for (int index = 0; index < value.ApexPowerAnchors.Count; index++)
            {
                writer.Value(value.ApexPowerAnchors[index]);
            }
            writer.EndArray();
            writer.Name("damage_types");
            writer.BeginArray();
            for (int index = 0; index < value.DamageTypes.Count; index++)
            {
                writer.Value(value.DamageTypes[index]);
            }
            writer.EndArray();
            writer.Property("max_augments", value.MaxAugments);
            writer.Property("no_recoil", value.NoRecoil);
            writer.Property("no_spin_up", value.NoSpinUp);
            writer.Property("no_heat_generation", value.NoHeatGeneration);
            writer.EndObject();
        }

        private static void WriteInputs(CanonicalWriter writer, WeaponCatalogInputs value)
        {
            writer.BeginObject();
            writer.Property("base_dps", value.BaseDps);
            writer.Property("growth_1_30", value.Growth1To30);
            writer.Property("growth_31_70", value.Growth31To70);
            writer.Property("growth_71_plus", value.Growth71Plus);
            writer.Name("rarities");
            writer.BeginObject();
            List<string> rarityIds = new List<string>(value.Rarities.Keys);
            rarityIds.Sort(StringComparer.Ordinal);
            for (int index = 0; index < rarityIds.Count; index++)
            {
                WeaponRarityInput rarity = value.Rarities[rarityIds[index]];
                writer.Name(rarity.Rarity);
                writer.BeginObject();
                writer.Property("weight", rarity.Weight);
                writer.Property("power_bonus", rarity.PowerBonus);
                writer.Property("early_tail", rarity.EarlyTail);
                writer.Property("late_tail", rarity.LateTail);
                writer.EndObject();
            }
            writer.EndObject();
            writer.EndObject();
        }

        private static void WriteArchetype(CanonicalWriter writer, WeaponArchetypeDefinition value)
        {
            writer.BeginObject();
            writer.Property("description", value.Description);
            writer.Property("dps_factor", value.DpsFactor);
            writer.Property("fire_rate", value.FireRate);
            writer.Property("projectiles", value.Projectiles);
            writer.Property("burst", value.Burst);
            writer.Property("spread", value.Spread);
            writer.Property("speed", value.Speed);
            writer.Property("range", value.Range);
            writer.Property("direct_share", value.DirectShare);
            writer.Property("area_share", value.AreaShare);
            writer.Property("dot_share", value.DotShare);
            writer.Property("radius", value.Radius);
            writer.Property("dot_duration", value.DotDuration);
            writer.Property("pool_radius", value.PoolRadius);
            writer.Property("pool_duration", value.PoolDuration);
            writer.Property("pierce", value.Pierce);
            writer.Property("chain_targets", value.ChainTargets);
            writer.Property("chain_range", value.ChainRange);
            writer.Property("knockback", value.Knockback);
            writer.Property("power_cost", value.PowerCost);
            writer.EndObject();
        }

        private static void WriteFamily(CanonicalWriter writer, WeaponFamilyDefinition value)
        {
            writer.BeginObject();
            writer.Property("FamilyId", value.FamilyId);
            writer.Property("DisplayName", value.DisplayName);
            writer.Property("Archetype", value.Archetype);
            writer.Property("DamageType", value.DamageType);
            writer.Property("BuildAffinity", value.BuildAffinity);
            writer.Property("MK1Peak", value.Mk1Peak);
            writer.Property("GapMK1To2", value.GapMk1To2);
            writer.Property("GapMK2To3", value.GapMk2To3);
            writer.Property("MaxPlannedMark", value.MaxPlannedMark);
            writer.Property("MK1Rarity", value.Mk1Rarity);
            writer.Property("MK2Rarity", value.Mk2Rarity);
            writer.Property("MK3Rarity", value.Mk3Rarity);
            writer.Property("DefinitionWeightModifier", value.DefinitionWeightModifier);
            writer.Property("AcquisitionClass", value.AcquisitionClass);
            writer.Property("PrimaryEffect", value.PrimaryEffect);
            writer.Property("Notes", value.Notes);
            writer.Property("Availability", value.Availability.ToString());
            WriteArtReferences(writer, value.SideProfileArtReferences);
            writer.EndObject();
        }

        private static void WriteDefinition(CanonicalWriter writer, WeaponDefinitionData value)
        {
            writer.BeginObject();
            writer.Property("DefinitionId", value.DefinitionId);
            writer.Property("DisplayName", value.DisplayName);
            writer.Property("FamilyId", value.FamilyId);
            writer.Property("Mark", value.Mark);
            writer.Property("DamageType", value.DamageType);
            writer.Property("Archetype", value.Archetype);
            writer.Property("BuildAffinity", value.BuildAffinity);
            writer.Property("FirstAppearance", value.FirstAppearance);
            writer.Property("PeakDropLevel", value.PeakDropLevel);
            writer.Property("PowerAnchor", value.PowerAnchor);
            writer.Property("Rarity", value.Rarity);
            writer.Property("RarityWeight", value.RarityWeight);
            writer.Property("DefinitionWeightModifier", value.DefinitionWeightModifier);
            writer.Property("FinalBaseWeight", value.FinalBaseWeight);
            writer.Property("EarlyTail", value.EarlyTail);
            writer.Property("LateTail", value.LateTail);
            writer.Property("AcquisitionClass", value.AcquisitionClass);
            writer.Property("TopBoxOnly", value.TopBoxOnly ? "Yes" : "No");
            writer.Property("CraftingRoute", value.CraftingRoute);
            writer.Property("ArchetypeDPSFactor", value.ArchetypeDpsFactor);
            writer.Property("PowerIndex", value.PowerIndex);
            writer.Property("TargetDPS", value.TargetDps);
            writer.Property("DirectShare", value.DirectShare);
            writer.Property("AreaShare", value.AreaShare);
            writer.Property("DoTShare", value.DotShare);
            writer.Property("FireRate", value.FireRate);
            writer.Property("ProjectilesPerTrigger", value.ProjectilesPerTrigger);
            writer.Property("BurstCount", value.BurstCount);
            writer.Property("DamagePerProjectile", value.DamagePerProjectile);
            writer.Property("SpreadDegrees", value.SpreadDegrees);
            writer.Property("ProjectileSpeed", value.ProjectileSpeed);
            writer.Property("Range", value.Range);
            writer.Property("Pierce", value.Pierce);
            writer.Property("ExplosionRadius", value.ExplosionRadius);
            writer.Property("AreaDamagePerTrigger", value.AreaDamagePerTrigger);
            writer.Property("DoTDPS", value.DotDps);
            writer.Property("DoTDuration", value.DotDuration);
            writer.Property("PoolRadius", value.PoolRadius);
            writer.Property("PoolDuration", value.PoolDuration);
            writer.Property("ChainTargets", value.ChainTargets);
            writer.Property("ChainRange", value.ChainRange);
            writer.Property("Knockback", value.Knockback);
            writer.Property("PowerCost", value.PowerCost);
            writer.Property("HealingPerSecond", value.HealingPerSecond);
            writer.Property("PrimaryEffect", value.PrimaryEffect);
            writer.Property("Notes", value.Notes);
            writer.Property("Availability", value.Availability.ToString());
            WriteArtReferences(writer, value.SideProfileArtReferences);
            writer.EndObject();
        }

        private static void WriteArtReferences(CanonicalWriter writer, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
            {
                return;
            }
            writer.Name("SideProfileArtReferences");
            writer.BeginArray();
            for (int index = 0; index < values.Count; index++)
            {
                writer.Value(values[index]);
            }
            writer.EndArray();
        }

        private sealed class CanonicalWriter
        {
            private readonly StringBuilder _builder = new StringBuilder();
            private readonly Stack<Context> _contexts = new Stack<Context>();
            private bool _afterName;

            public void BeginObject() { BeforeValue(); _builder.Append('{'); _contexts.Push(new Context(true)); }
            public void EndObject() { _builder.Append('}'); _contexts.Pop(); _afterName = false; }
            public void BeginArray() { BeforeValue(); _builder.Append('['); _contexts.Push(new Context(false)); }
            public void EndArray() { _builder.Append(']'); _contexts.Pop(); _afterName = false; }

            public void Name(string name)
            {
                Context context = _contexts.Peek();
                if (!context.IsObject) throw new InvalidOperationException("Names are valid only inside JSON objects.");
                if (!context.IsFirst) _builder.Append(',');
                context.IsFirst = false;
                AppendQuoted(name);
                _builder.Append(':');
                _afterName = true;
            }

            public void Property(string name, string value) { Name(name); Value(value); }
            public void Property(string name, int value) { Name(name); Value(value); }
            public void Property(string name, double value) { Name(name); Value(value); }
            public void Property(string name, bool value) { Name(name); Value(value); }
            public void Value(string value) { BeforeValue(); AppendQuoted(value ?? string.Empty); }
            public void Value(int value) { BeforeValue(); _builder.Append(value.ToString(CultureInfo.InvariantCulture)); }
            public void Value(double value) { BeforeValue(); _builder.Append(value.ToString("R", CultureInfo.InvariantCulture)); }
            public void Value(bool value) { BeforeValue(); _builder.Append(value ? "true" : "false"); }

            public override string ToString()
            {
                if (_contexts.Count != 0) throw new InvalidOperationException("JSON document is incomplete.");
                return _builder.ToString();
            }

            private void BeforeValue()
            {
                if (_afterName) { _afterName = false; return; }
                if (_contexts.Count == 0) return;
                Context context = _contexts.Peek();
                if (context.IsObject) throw new InvalidOperationException("Object values require a property name.");
                if (!context.IsFirst) _builder.Append(',');
                context.IsFirst = false;
            }

            private void AppendQuoted(string value)
            {
                _builder.Append('"');
                for (int index = 0; index < value.Length; index++)
                {
                    char current = value[index];
                    switch (current)
                    {
                        case '"': _builder.Append("\\\""); break;
                        case '\\': _builder.Append("\\\\"); break;
                        case '\b': _builder.Append("\\b"); break;
                        case '\f': _builder.Append("\\f"); break;
                        case '\n': _builder.Append("\\n"); break;
                        case '\r': _builder.Append("\\r"); break;
                        case '\t': _builder.Append("\\t"); break;
                        default:
                            if (current < 0x20)
                            {
                                _builder.Append("\\u");
                                _builder.Append(((int)current).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else _builder.Append(current);
                            break;
                    }
                }
                _builder.Append('"');
            }

            private sealed class Context
            {
                public Context(bool isObject) { IsObject = isObject; IsFirst = true; }
                public bool IsObject { get; private set; }
                public bool IsFirst { get; set; }
            }
        }
    }
}
