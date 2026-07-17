using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class LootboxOpenerSimulatorWindow : EditorWindow
    {
        private enum Page
        {
            LootQueue,
            ResultsOpening,
            Odds,
        }

        private readonly List<int> queue = new List<int>();
        private readonly List<int> openingQueue = new List<int>();
        private LootboxSimulatorRuntimeV1 runtime;
        private LootboxGeneratedItemV1 current;
        private LootboxOddsReportV1 odds;
        private Page page;
        private int playerLevel = 30;
        private string seedText = "123456";
        private int openingPlayerLevel;
        private ulong openingSeed;
        private string openingFingerprint = string.Empty;
        private int currentQueueIndex;
        private int oddsTier = 1;
        private int oddsSamples = 10000;
        private Vector2 scroll;
        private string diagnostic =
            "Load weapon_baseline_v01.json to enable real-content box generation.";

        [MenuItem("Tools/Shooter Mover/Lootbox Opener Simulator")]
        public static void Open()
        {
            GetWindow<LootboxOpenerSimulatorWindow>("Lootbox Opener");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Real-content Lootbox Opener Simulator",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Add boxes in any order, then open the frozen queue on the Results / Opening page. Rolls delegate to BOX/GEN. Keep writes the immutable item through PlayerHoldingsService; Sell adds the temporary 1,000 cash value.",
                MessageType.Info);

            DrawCatalogControls();
            using (new EditorGUI.DisabledScope(runtime == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(page == Page.LootQueue, "Loot Queue", "Button"))
                {
                    page = Page.LootQueue;
                }
                if (GUILayout.Toggle(page == Page.ResultsOpening, "Results / Opening", "Button"))
                {
                    page = Page.ResultsOpening;
                }
                if (GUILayout.Toggle(page == Page.Odds, "Odds", "Button"))
                {
                    page = Page.Odds;
                }
                EditorGUILayout.EndHorizontal();

                scroll = EditorGUILayout.BeginScrollView(scroll);
                if (page == Page.LootQueue)
                {
                    DrawQueue();
                }
                else if (page == Page.ResultsOpening)
                {
                    DrawOpening();
                }
                else
                {
                    DrawOdds();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(diagnostic, MessageType.None);
        }

        private void DrawCatalogControls()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load weapon_baseline_v01.json"))
            {
                string path = EditorUtility.OpenFilePanel(
                    "Select weapon catalog JSON",
                    Application.dataPath,
                    "json");
                if (!string.IsNullOrEmpty(path))
                {
                    LoadCatalog(path);
                }
            }
            playerLevel = Math.Max(
                0,
                EditorGUILayout.IntField("Player Level", playerLevel));
            seedText = EditorGUILayout.TextField("Seed", seedText);
            EditorGUILayout.EndHorizontal();

            if (runtime != null)
            {
                EditorGUILayout.LabelField(
                    "Live weapons: "
                    + runtime.WeaponCatalog.GetDefinitions(
                        ShooterMover.Domain.Weapons.Catalog.WeaponCatalogContentFilter.LiveOnly).Count
                    + " | Inventory: "
                    + runtime.AcceptedInventory.Count
                    + " | Cash: "
                    + runtime.Cash.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void DrawQueue()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Add boxes to Currently Looted",
                EditorStyles.boldLabel);
            for (int row = 0; row < 4; row++)
            {
                EditorGUILayout.BeginHorizontal();
                int start = row * 3;
                int end = Math.Min(
                    start + 3,
                    ProductionStrongboxCatalogV1.Tiers.Count);
                for (int index = start; index < end; index++)
                {
                    ProductionStrongboxTierV1 tier =
                        ProductionStrongboxCatalogV1.Tiers[index];
                    if (GUILayout.Button(
                        "+ " + tier.TierNumber + " " + tier.DisplayName))
                    {
                        queue.Add(tier.TierNumber);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Currently Looted — "
                + queue.Count.ToString(CultureInfo.InvariantCulture),
                EditorStyles.boldLabel);
            if (queue.Count == 0)
            {
                EditorGUILayout.LabelField("No boxes selected.");
            }
            for (int index = 0; index < queue.Count; index++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.GetByNumber(queue[index]);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    (index + 1).ToString("D3", CultureInfo.InvariantCulture)
                    + ". Tier "
                    + tier.TierNumber
                    + " — "
                    + tier.DisplayName);
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    queue.RemoveAt(index);
                    index--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(queue.Count == 0))
            {
                if (GUILayout.Button("Open Selected Boxes In Order"))
                {
                    BeginOpening();
                }
            }
            if (GUILayout.Button("Clear Queue"))
            {
                queue.Clear();
            }
            EditorGUILayout.EndHorizontal();

            if (openingQueue.Count > 0 && currentQueueIndex < openingQueue.Count)
            {
                EditorGUILayout.HelpBox(
                    "An opening session is already frozen. New selections stay in Currently Looted and will not alter the active opening order.",
                    MessageType.Info);
            }

            DrawInventory();
        }

        private void DrawOpening()
        {
            EditorGUILayout.Space();
            if (openingQueue.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Add boxes on the Loot Queue page and press Open Selected Boxes In Order.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                "Frozen session: player L"
                + openingPlayerLevel.ToString(CultureInfo.InvariantCulture)
                + " | seed "
                + openingSeed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Session fingerprint", openingFingerprint);
            if (GUILayout.Button("Copy Frozen Queue"))
            {
                EditorGUIUtility.systemCopyBuffer = BuildOpeningQueueExport();
                diagnostic = "Copied the deterministic frozen queue description.";
            }

            if (currentQueueIndex >= openingQueue.Count)
            {
                EditorGUILayout.LabelField("Opening complete", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Kept items: "
                    + runtime.AcceptedInventory.Count.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField(
                    "Cash: " + runtime.Cash.ToString(CultureInfo.InvariantCulture));
                if (GUILayout.Button("Finish Session And Return To Loot Queue"))
                {
                    openingQueue.Clear();
                    currentQueueIndex = 0;
                    current = null;
                    openingFingerprint = string.Empty;
                    page = Page.LootQueue;
                }
                DrawInventory();
                return;
            }
            if (current == null)
            {
                RevealCurrent();
                if (current == null)
                {
                    return;
                }
            }

            EditorGUILayout.LabelField(
                "Box "
                + (currentQueueIndex + 1).ToString(CultureInfo.InvariantCulture)
                + " / "
                + openingQueue.Count.ToString(CultureInfo.InvariantCulture)
                + " — "
                + current.Tier.DisplayName,
                EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                current.DefinitionDisplayName,
                EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Definition", current.SourceDefinitionId);
            EditorGUILayout.LabelField("Family", current.FamilyId);
            EditorGUILayout.LabelField(
                "Mark",
                current.Mark.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Item level",
                current.Equipment.ItemLevel.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Quality", current.Equipment.QualityId.ToString());
            EditorGUILayout.LabelField(
                "Augment slots",
                current.Equipment.Augments.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < current.Equipment.Augments.Count; index++)
            {
                AugmentInstance augment = current.Equipment.Augments[index];
                EditorGUILayout.LabelField(
                    "  Slot "
                    + (index + 1).ToString(CultureInfo.InvariantCulture)
                    + ": "
                    + augment.DefinitionId
                    + " | Tier "
                    + augment.Tier.ToString(CultureInfo.InvariantCulture)
                    + " | Level "
                    + augment.Level.ToString(CultureInfo.InvariantCulture));
            }
            EditorGUILayout.LabelField(
                "Instance ID",
                current.Equipment.InstanceId.ToString());
            EditorGUILayout.LabelField("Item fingerprint", current.Fingerprint);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Current Item"))
            {
                EditorGUIUtility.systemCopyBuffer = current.ToCanonicalString();
                diagnostic = "Copied the current item's deterministic canonical text.";
            }
            if (GUILayout.Button(
                "Calculate This Tier's Odds ("
                + oddsSamples.ToString(CultureInfo.InvariantCulture)
                + ")"))
            {
                CalculateOddsFor(
                    current.Tier.TierNumber,
                    openingPlayerLevel,
                    openingSeed);
            }
            EditorGUILayout.EndHorizontal();

            if (OddsMatchCurrent())
            {
                DrawCurrentItemOdds();
                DrawOddsGroup("Slot odds", odds.SlotOdds);
                DrawOddsGroup("Augment-tier odds", odds.AugmentTierOdds);
                DrawOddsGroup("Augment-level odds", odds.AugmentLevelOdds);
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep / Accept"))
            {
                PlayerHoldingsMutationStatusV1 status = runtime.Keep(current);
                diagnostic = "Keep: " + status;
                if (status == PlayerHoldingsMutationStatusV1.Applied
                    || status == PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange)
                {
                    Advance();
                }
            }
            if (GUILayout.Button("Sell (+1,000 cash)"))
            {
                if (runtime.Sell(current))
                {
                    diagnostic =
                        "Sold for 1,000. TODO: actual item value calculation.";
                    Advance();
                }
                else
                {
                    diagnostic =
                        "This exact generated item was already decided.";
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOdds()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Deterministic Odds Inspector",
                EditorStyles.boldLabel);
            oddsTier = EditorGUILayout.IntSlider(
                "Strongbox Tier",
                oddsTier,
                1,
                ProductionStrongboxCatalogV1.Tiers.Count);
            oddsSamples = Math.Max(
                1,
                EditorGUILayout.IntField("Samples", oddsSamples));
            if (GUILayout.Button("Calculate Odds"))
            {
                ulong seed;
                if (!TrySeed(out seed))
                {
                    return;
                }
                CalculateOddsFor(oddsTier, playerLevel, seed);
            }

            if (odds == null)
            {
                return;
            }
            EditorGUILayout.LabelField(
                "Successful opens: "
                + odds.SuccessfulOpenCount.ToString(CultureInfo.InvariantCulture)
                + " / "
                + odds.SampleCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Rejected/impossible rolls: "
                + odds.RejectedRolls.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Report fingerprint", odds.Fingerprint);
            if (GUILayout.Button("Copy Deterministic Odds Report"))
            {
                EditorGUIUtility.systemCopyBuffer = odds.ToCanonicalString();
                diagnostic = "Copied byte-stable canonical odds report text.";
            }
            DrawOddsGroup("Item definition odds", odds.ItemOdds);
            DrawOddsGroup("Quality odds", odds.QualityOdds);
            DrawOddsGroup("Augment-slot odds (1 / 2 / 3)", odds.SlotOdds);
            DrawOddsGroup("Augment-tier odds (1..3)", odds.AugmentTierOdds);
            DrawOddsGroup("Augment-level odds (1..10)", odds.AugmentLevelOdds);
            DrawOddsGroup(
                "Item-level delta versus player",
                odds.ItemLevelDeltaOdds);
        }

        private void DrawCurrentItemOdds()
        {
            LootboxOddsEntryV1 itemOdds = odds.FindItemOdds(current.OddsKey);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Observed odds for this exact definition",
                EditorStyles.boldLabel);
            if (itemOdds == null)
            {
                EditorGUILayout.LabelField(
                    "This definition did not occur in the selected deterministic sample.");
                return;
            }
            EditorGUILayout.LabelField(
                itemOdds.Key,
                itemOdds.Count.ToString(CultureInfo.InvariantCulture)
                + " / "
                + itemOdds.Total.ToString(CultureInfo.InvariantCulture)
                + " ("
                + itemOdds.Percentage.ToString("F3", CultureInfo.InvariantCulture)
                + "%)");
        }

        private void DrawInventory()
        {
            if (runtime == null)
            {
                return;
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Simulator Inventory — "
                + runtime.AcceptedInventory.Count.ToString(CultureInfo.InvariantCulture),
                EditorStyles.boldLabel);
            for (int index = 0; index < runtime.AcceptedInventory.Count; index++)
            {
                EquipmentInstance item = runtime.AcceptedInventory[index];
                EditorGUILayout.LabelField(
                    (index + 1).ToString("D3", CultureInfo.InvariantCulture)
                    + ". "
                    + item.DefinitionId
                    + " | L"
                    + item.ItemLevel.ToString(CultureInfo.InvariantCulture)
                    + " | "
                    + item.QualityId
                    + " | slots "
                    + item.Augments.Count.ToString(CultureInfo.InvariantCulture)
                    + " | "
                    + item.InstanceId);
            }
        }

        private static void DrawOddsGroup(
            string heading,
            IReadOnlyList<LootboxOddsEntryV1> entries)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("No successful observations.");
                return;
            }
            for (int index = 0; index < entries.Count; index++)
            {
                LootboxOddsEntryV1 entry = entries[index];
                EditorGUILayout.LabelField(
                    entry.Key,
                    entry.Count.ToString(CultureInfo.InvariantCulture)
                    + " / "
                    + entry.Total.ToString(CultureInfo.InvariantCulture)
                    + " ("
                    + entry.Percentage.ToString("F3", CultureInfo.InvariantCulture)
                    + "%)");
            }
        }

        private void LoadCatalog(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                LootboxSimulatorRuntimeV1 created;
                string error;
                if (!LootboxSimulatorRuntimeV1.TryCreate(
                    json,
                    out created,
                    out error))
                {
                    runtime = null;
                    diagnostic = error;
                    return;
                }
                runtime = created;
                queue.Clear();
                openingQueue.Clear();
                current = null;
                currentQueueIndex = 0;
                openingFingerprint = string.Empty;
                odds = null;
                page = Page.LootQueue;
                diagnostic = "Loaded "
                    + Path.GetFileName(path)
                    + " with fingerprint "
                    + runtime.WeaponCatalog.Fingerprint
                    + ".";
            }
            catch (Exception exception)
            {
                runtime = null;
                diagnostic = exception.ToString();
            }
        }

        private void BeginOpening()
        {
            ulong seed;
            if (!TrySeed(out seed))
            {
                return;
            }
            openingQueue.Clear();
            openingQueue.AddRange(queue);
            queue.Clear();
            openingPlayerLevel = playerLevel;
            openingSeed = seed;
            openingFingerprint = BuildOpeningFingerprint();
            currentQueueIndex = 0;
            current = null;
            page = Page.ResultsOpening;
            RevealCurrent();
        }

        private void RevealCurrent()
        {
            if (runtime == null
                || currentQueueIndex < 0
                || currentQueueIndex >= openingQueue.Count)
            {
                return;
            }
            try
            {
                current = runtime.Generate(
                    openingQueue[currentQueueIndex],
                    openingPlayerLevel,
                    openingSeed,
                    currentQueueIndex);
                diagnostic = "Generated and frozen box "
                    + (currentQueueIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + " of "
                    + openingQueue.Count.ToString(CultureInfo.InvariantCulture)
                    + ".";
            }
            catch (Exception exception)
            {
                current = null;
                diagnostic = exception.ToString();
            }
        }

        private void Advance()
        {
            currentQueueIndex++;
            current = null;
            if (currentQueueIndex < openingQueue.Count)
            {
                RevealCurrent();
            }
        }

        private void CalculateOddsFor(
            int tierNumber,
            int level,
            ulong seed)
        {
            try
            {
                odds = runtime.CalculateOdds(
                    tierNumber,
                    level,
                    seed,
                    oddsSamples);
                oddsTier = tierNumber;
                diagnostic = "Odds calculated from "
                    + oddsSamples.ToString(CultureInfo.InvariantCulture)
                    + " deterministic BOX/GEN opens. Fingerprint: "
                    + odds.Fingerprint;
            }
            catch (Exception exception)
            {
                odds = null;
                diagnostic = exception.ToString();
            }
        }

        private bool OddsMatchCurrent()
        {
            return odds != null
                && current != null
                && odds.TierNumber == current.Tier.TierNumber
                && odds.PlayerLevel == openingPlayerLevel
                && odds.RootSeed == openingSeed;
        }

        private string BuildOpeningFingerprint()
        {
            return StrongboxCanonicalV1.Fingerprint(BuildOpeningQueueExport());
        }

        private string BuildOpeningQueueExport()
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(
                builder,
                "schema",
                "lootbox-opening-queue-v1");
            StrongboxCanonicalV1.AppendToken(
                builder,
                "player_level",
                openingPlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "root_seed",
                openingSeed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "box_count",
                openingQueue.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < openingQueue.Count; index++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.GetByNumber(openingQueue[index]);
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    "box_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    tier.TierStableId.ToString());
            }
            return builder.ToString();
        }

        private bool TrySeed(out ulong seed)
        {
            if (!ulong.TryParse(
                seedText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out seed))
            {
                diagnostic = "Seed must be an unsigned 64-bit integer.";
                return false;
            }
            return true;
        }
    }
}
