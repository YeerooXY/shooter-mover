using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class LootboxOpenerSimulatorWindow : EditorWindow
    {
        private enum Page
        {
            LootQueue,
            Opening,
            Odds,
        }

        private readonly List<int> queue = new List<int>();
        private LootboxSimulatorRuntimeV1 runtime;
        private LootboxGeneratedItemV1 current;
        private LootboxOddsReportV1 odds;
        private Page page;
        private int playerLevel = 30;
        private string seedText = "123456";
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
            EditorGUILayout.LabelField("Real-content Lootbox Opener Simulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Boxes open in the exact order they are added. Rolls delegate to BOX/GEN. Keep writes the immutable item through PlayerHoldingsService; Sell adds the temporary 1,000 cash value.",
                MessageType.Info);

            DrawCatalogControls();
            using (new EditorGUI.DisabledScope(runtime == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(page == Page.LootQueue, "Loot Queue", "Button")) page = Page.LootQueue;
                if (GUILayout.Toggle(page == Page.Opening, "Opening", "Button")) page = Page.Opening;
                if (GUILayout.Toggle(page == Page.Odds, "Odds", "Button")) page = Page.Odds;
                EditorGUILayout.EndHorizontal();

                scroll = EditorGUILayout.BeginScrollView(scroll);
                if (page == Page.LootQueue) DrawQueue();
                else if (page == Page.Opening) DrawOpening();
                else DrawOdds();
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
            playerLevel = Math.Max(0, EditorGUILayout.IntField("Player Level", playerLevel));
            seedText = EditorGUILayout.TextField("Seed", seedText);
            EditorGUILayout.EndHorizontal();

            if (runtime != null)
            {
                EditorGUILayout.LabelField(
                    "Live weapons: " + runtime.WeaponCatalog.GetDefinitions(
                        ShooterMover.Domain.Weapons.Catalog.WeaponCatalogContentFilter.LiveOnly).Count
                    + " | Inventory: " + runtime.AcceptedInventory.Count
                    + " | Cash: " + runtime.Cash.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void DrawQueue()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add boxes to Currently Looted", EditorStyles.boldLabel);
            for (int row = 0; row < 4; row++)
            {
                EditorGUILayout.BeginHorizontal();
                int start = row * 3;
                int end = Math.Min(start + 3, ProductionStrongboxCatalogV1.Tiers.Count);
                for (int index = start; index < end; index++)
                {
                    ProductionStrongboxTierV1 tier = ProductionStrongboxCatalogV1.Tiers[index];
                    if (GUILayout.Button("+ " + tier.TierNumber + " " + tier.DisplayName))
                    {
                        queue.Add(tier.TierNumber);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Currently Looted — " + queue.Count.ToString(CultureInfo.InvariantCulture),
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
                    (index + 1).ToString("D3") + ". Tier "
                    + tier.TierNumber + " — " + tier.DisplayName);
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

            DrawInventory();
        }

        private void DrawOpening()
        {
            EditorGUILayout.Space();
            if (queue.Count == 0)
            {
                EditorGUILayout.HelpBox("Add boxes on the Loot Queue page first.", MessageType.Warning);
                return;
            }
            if (currentQueueIndex >= queue.Count)
            {
                EditorGUILayout.LabelField("Opening complete", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Kept items: " + runtime.AcceptedInventory.Count);
                EditorGUILayout.LabelField("Cash: " + runtime.Cash);
                if (GUILayout.Button("Return To Loot Queue"))
                {
                    page = Page.LootQueue;
                }
                DrawInventory();
                return;
            }
            if (current == null)
            {
                RevealCurrent();
                if (current == null) return;
            }

            EditorGUILayout.LabelField(
                "Box " + (currentQueueIndex + 1) + " / " + queue.Count
                + " — " + current.Tier.DisplayName,
                EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(current.DefinitionDisplayName, EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Family", current.FamilyId);
            EditorGUILayout.LabelField("Mark", current.Mark.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Item level", current.Equipment.ItemLevel.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Quality", current.Equipment.QualityId.ToString());
            EditorGUILayout.LabelField("Augment slots", current.Equipment.Augments.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < current.Equipment.Augments.Count; index++)
            {
                AugmentInstance augment = current.Equipment.Augments[index];
                EditorGUILayout.LabelField(
                    "  Slot " + (index + 1)
                    + ": " + augment.DefinitionId
                    + " | Tier " + augment.Tier
                    + " | Level " + augment.Level);
            }
            EditorGUILayout.LabelField("Instance ID", current.Equipment.InstanceId.ToString());
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep / Accept"))
            {
                PlayerHoldingsMutationStatusV1 status =
                    runtime.Keep(current, currentQueueIndex);
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
                    diagnostic = "Sold for 1,000. TODO: actual item value calculation.";
                    Advance();
                }
                else
                {
                    diagnostic = "This exact generated item was already decided.";
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOdds()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Deterministic Odds Inspector", EditorStyles.boldLabel);
            oddsTier = EditorGUILayout.IntSlider(
                "Strongbox Tier",
                oddsTier,
                1,
                ProductionStrongboxCatalogV1.Tiers.Count);
            oddsSamples = Math.Max(1, EditorGUILayout.IntField("Samples", oddsSamples));
            if (GUILayout.Button("Calculate Odds"))
            {
                ulong seed;
                if (!TrySeed(out seed)) return;
                try
                {
                    odds = runtime.CalculateOdds(
                        oddsTier,
                        playerLevel,
                        seed,
                        oddsSamples);
                    diagnostic = "Odds calculated from "
                        + oddsSamples.ToString(CultureInfo.InvariantCulture)
                        + " deterministic BOX/GEN opens.";
                }
                catch (Exception exception)
                {
                    diagnostic = exception.ToString();
                }
            }

            if (odds == null) return;
            EditorGUILayout.LabelField("Rejected/impossible rolls: " + odds.RejectedRolls);
            DrawOddsGroup("Item definition odds", odds.ItemOdds);
            DrawOddsGroup("Augment-slot odds (1 / 2 / 3)", odds.SlotOdds);
            DrawOddsGroup("Augment-level odds (1..10)", odds.AugmentLevelOdds);
            DrawOddsGroup("Item-level delta versus player", odds.ItemLevelDeltaOdds);
        }

        private void DrawInventory()
        {
            if (runtime == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Simulator Inventory — " + runtime.AcceptedInventory.Count,
                EditorStyles.boldLabel);
            for (int index = 0; index < runtime.AcceptedInventory.Count; index++)
            {
                EquipmentInstance item = runtime.AcceptedInventory[index];
                EditorGUILayout.LabelField(
                    (index + 1).ToString("D3")
                    + ". " + item.DefinitionId
                    + " | L" + item.ItemLevel
                    + " | " + item.QualityId
                    + " | slots " + item.Augments.Count
                    + " | " + item.InstanceId);
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
                    + " (" + entry.Percentage.ToString("F3", CultureInfo.InvariantCulture) + "%)");
            }
        }

        private void LoadCatalog(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                LootboxSimulatorRuntimeV1 created;
                string error;
                if (!LootboxSimulatorRuntimeV1.TryCreate(json, out created, out error))
                {
                    runtime = null;
                    diagnostic = error;
                    return;
                }
                runtime = created;
                queue.Clear();
                current = null;
                currentQueueIndex = 0;
                odds = null;
                page = Page.LootQueue;
                diagnostic = "Loaded " + Path.GetFileName(path)
                    + " with fingerprint " + runtime.WeaponCatalog.Fingerprint + ".";
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
            if (!TrySeed(out seed)) return;
            currentQueueIndex = 0;
            current = null;
            page = Page.Opening;
            RevealCurrent();
        }

        private void RevealCurrent()
        {
            ulong seed;
            if (!TrySeed(out seed)) return;
            try
            {
                current = runtime.Generate(
                    queue[currentQueueIndex],
                    playerLevel,
                    seed,
                    currentQueueIndex);
                diagnostic = "Generated and frozen box "
                    + (currentQueueIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + " of " + queue.Count.ToString(CultureInfo.InvariantCulture) + ".";
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
            if (currentQueueIndex < queue.Count)
            {
                RevealCurrent();
            }
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
