using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class BalanceSimulatorWindow : EditorWindow
    {
        private BalanceSimulationModeV1 mode = BalanceSimulationModeV1.SingleOpen;
        private int characterLevel = 10;
        private int strongboxTier = 1;
        private int strongboxLevel = 10;
        private int shopLevel = 10;
        private string seedText = "123456";
        private int simulations = 1000;
        private long startingMoney = 10000L;
        private long startingScrap = 10000L;
        private Vector2 scroll;
        private string reportText = "Run a simulation to inspect runtime balance output.";

        [MenuItem("Tools/Shooter Mover/Balance Simulator")]
        public static void Open()
        {
            GetWindow<BalanceSimulatorWindow>("Balance Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SIM-001 Runtime Balance Simulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Uses the production deterministic generator, strongbox power-budget resolver, shop pricing, crafting unlock policy, and augment-upgrade cost policy. No alternate reward algorithm is implemented here.",
                MessageType.Info);

            mode = (BalanceSimulationModeV1)EditorGUILayout.EnumPopup("Mode", mode);
            characterLevel = Math.Max(0, EditorGUILayout.IntField("Character Level", characterLevel));
            strongboxTier = Math.Max(0, EditorGUILayout.IntField("Strongbox Tier", strongboxTier));
            strongboxLevel = Math.Max(0, EditorGUILayout.IntField("Strongbox Level", strongboxLevel));
            shopLevel = Math.Max(0, EditorGUILayout.IntField("Shop Level", shopLevel));
            seedText = EditorGUILayout.TextField("Deterministic Seed", seedText);
            using (new EditorGUI.DisabledScope(mode == BalanceSimulationModeV1.SingleOpen))
            {
                simulations = Math.Max(1, EditorGUILayout.IntField("Number of Simulations", simulations));
            }
            startingMoney = Math.Max(0L, EditorGUILayout.LongField("Starting Money", startingMoney));
            startingScrap = Math.Max(0L, EditorGUILayout.LongField("Starting Scrap", startingScrap));

            if (GUILayout.Button(mode == BalanceSimulationModeV1.SingleOpen ? "Run Single Open" : "Run Batch Simulation"))
            {
                RunSimulation();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(reportText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunSimulation()
        {
            ulong seed;
            if (!ulong.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            {
                reportText = "Invalid deterministic seed. Enter an unsigned 64-bit integer.";
                return;
            }

            try
            {
                BalanceSimulationRequestV1 request = new BalanceSimulationRequestV1(
                    mode,
                    characterLevel,
                    strongboxTier,
                    strongboxLevel,
                    shopLevel,
                    seed,
                    simulations,
                    startingMoney,
                    startingScrap);
                BalanceSimulationReportV1 report = new BalanceSimulationServiceV1(
                    new RuntimeBalanceScenarioV1()).Run(request);
                reportText = Format(report);
            }
            catch (Exception exception)
            {
                reportText = exception.ToString();
            }
        }

        private static string Format(BalanceSimulationReportV1 report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Fingerprint: " + report.Fingerprint)
                .AppendLine("Simulations: " + report.Request.NumberOfSimulations)
                .AppendLine("Equipment instances: " + report.EquipmentInstanceCount)
                .AppendLine("Unique equipment instance IDs: " + report.UniqueEquipmentInstanceCount)
                .AppendLine("Duplicate definition count: " + report.DuplicateDefinitionCount)
                .AppendLine("Duplicate definition frequency: " + report.DuplicateDefinitionFrequency.ToString("F2", CultureInfo.InvariantCulture) + "%")
                .AppendLine("Money delta: " + report.MoneyDelta)
                .AppendLine("Scrap delta: " + report.ScrapDelta)
                .AppendLine("Shop money required: " + report.ShopMoneyRequired)
                .AppendLine("Crafting scrap required: " + report.CraftingScrapRequired)
                .AppendLine("Upgrade money required: " + report.UpgradeMoneyRequired)
                .AppendLine("Soft-eligible candidates: " + report.SoftEligibleCandidateCount)
                .AppendLine("Crafting unlock range: " + report.MinimumCraftingUnlockLevel + ".." + report.MaximumCraftingUnlockLevel);
            Append(builder, "Reward type distribution", report.RewardTypes);
            Append(builder, "Weapon/armor definitions", report.EquipmentDefinitions);
            Append(builder, "Equipment categories", report.EquipmentCategories);
            Append(builder, "Item levels", report.ItemLevels);
            Append(builder, "Quality", report.Qualities);
            Append(builder, "Augment count", report.AugmentCounts);
            Append(builder, "Augment tiers", report.AugmentTiers);
            Append(builder, "Augment levels", report.AugmentLevels);
            Append(builder, "Rejected/impossible rolls", report.Rejections);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string heading, System.Collections.Generic.IReadOnlyList<BalanceCountV1> values)
        {
            builder.AppendLine().AppendLine(heading);
            if (values.Count == 0)
            {
                builder.AppendLine("  none");
                return;
            }
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append("  ").Append(values[index].Key)
                    .Append(": ").Append(values[index].Count)
                    .Append(" (").Append(values[index].Percentage.ToString("F2", CultureInfo.InvariantCulture)).AppendLine("%)");
            }
        }
    }
}
