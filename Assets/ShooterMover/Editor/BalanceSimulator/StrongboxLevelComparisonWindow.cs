using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// One immutable comparison input. The level is captured when the box is clicked,
    /// so changing the editor field later cannot mutate an earlier queue entry.
    /// </summary>
    public sealed class StrongboxLevelQueueEntryV1
    {
        private readonly string canonicalText;

        public StrongboxLevelQueueEntryV1(int tierNumber, int playerLevel)
        {
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }

            Tier = ProductionStrongboxCatalogV1.GetByNumber(tierNumber);
            PlayerLevel = playerLevel;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(
                builder,
                "schema",
                "strongbox-level-queue-entry-v1");
            StrongboxCanonicalV1.AppendToken(
                builder,
                "tier",
                Tier.TierStableId.ToString());
            StrongboxCanonicalV1.AppendToken(
                builder,
                "player_level",
                PlayerLevel.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public ProductionStrongboxTierV1 Tier { get; }
        public int PlayerLevel { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class StrongboxLevelComparisonResultV1
    {
        private readonly ReadOnlyCollection<EquipmentInstance> equipment;

        public StrongboxLevelComparisonResultV1(
            StrongboxLevelQueueEntryV1 input,
            AuthoritativeStrongboxPreparedOpenV1 prepared,
            StrongboxOpeningResultRuntimeV1 openingResult,
            IEnumerable<EquipmentInstance> equipment,
            long moneyBalance,
            long scrapBalance,
            long holdingsSequence,
            long openingSequence)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
            OpeningResult = openingResult
                ?? throw new ArgumentNullException(nameof(openingResult));
            this.equipment = new ReadOnlyCollection<EquipmentInstance>(
                new List<EquipmentInstance>(equipment
                    ?? throw new ArgumentNullException(nameof(equipment))));
            MoneyBalance = moneyBalance;
            ScrapBalance = scrapBalance;
            HoldingsSequence = holdingsSequence;
            OpeningSequence = openingSequence;
        }

        public StrongboxLevelQueueEntryV1 Input { get; }
        public AuthoritativeStrongboxPreparedOpenV1 Prepared { get; }
        public StrongboxOpeningResultRuntimeV1 OpeningResult { get; }
        public IReadOnlyList<EquipmentInstance> Equipment { get { return equipment; } }
        public long MoneyBalance { get; }
        public long ScrapBalance { get; }
        public long HoldingsSequence { get; }
        public long OpeningSequence { get; }
    }

    /// <summary>
    /// Visual comparison harness for testing one tier at multiple player levels. Every
    /// queued row receives a fresh authority sandbox but reuses the same deterministic
    /// root seed, keeping level as the meaningful changed input.
    /// </summary>
    public sealed class StrongboxLevelComparisonWindow : EditorWindow
    {
        private readonly List<StrongboxLevelQueueEntryV1> queue =
            new List<StrongboxLevelQueueEntryV1>();
        private readonly List<StrongboxLevelComparisonResultV1> results =
            new List<StrongboxLevelComparisonResultV1>();

        private string loadedCatalogJson;
        private string loadedCatalogName;
        private int playerLevel = 1;
        private string seedText = "123456";
        private Vector2 scroll;
        private string diagnostic =
            "Load weapon_baseline_v01.json, select a level, then click boxes into the comparison queue.";

        [MenuItem("Tools/Shooter Mover/Strongbox Level Comparison")]
        public static void Open()
        {
            GetWindow<StrongboxLevelComparisonWindow>("Box Level Compare");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Strongbox Level Comparison",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The current Player Level is captured when a box button is clicked. Change the level and click the same box again to compare it at another level. Every row uses the same seed in an isolated BOX/RAP/INV/SCR authority sandbox.",
                MessageType.Info);

            DrawControls();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawTierButtons();
            DrawQueue();
            DrawResults();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(diagnostic, MessageType.None);
        }

        private void DrawControls()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(
                "Load weapon_baseline_v01.json",
                GUILayout.Width(245f)))
            {
                string path = EditorUtility.OpenFilePanel(
                    "Select weapon catalog JSON",
                    UnityEngine.Application.dataPath,
                    "json");
                if (!string.IsNullOrEmpty(path))
                {
                    LoadCatalog(path);
                }
            }

            playerLevel = Math.Max(
                0,
                EditorGUILayout.IntField("Player Level", playerLevel));
            seedText = EditorGUILayout.TextField("Comparison Seed", seedText);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(loadedCatalogName)
                    ? "Catalog: not loaded"
                    : "Catalog: " + loadedCatalogName);
        }

        private void DrawTierButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Click to capture at Player Level "
                + playerLevel.ToString(CultureInfo.InvariantCulture),
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
                        "[BOX] " + tier.TierNumber
                        + " " + tier.DisplayName
                        + "\nPlayer L" + playerLevel,
                        GUILayout.MinHeight(52f)))
                    {
                        AddEntry(tier.TierNumber);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawQueue()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Level Comparison Queue — "
                + queue.Count.ToString(CultureInfo.InvariantCulture),
                EditorStyles.boldLabel);

            if (queue.Count == 0)
            {
                EditorGUILayout.LabelField("No comparison inputs selected.");
            }

            for (int index = 0; index < queue.Count; index++)
            {
                StrongboxLevelQueueEntryV1 entry = queue[index];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    (index + 1).ToString("D3", CultureInfo.InvariantCulture)
                    + ". Tier " + entry.Tier.TierNumber
                    + " — " + entry.Tier.DisplayName
                    + " @ Player L" + entry.PlayerLevel);
                if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                {
                    queue.RemoveAt(index);
                    results.Clear();
                    index--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(
                queue.Count == 0 || string.IsNullOrEmpty(loadedCatalogJson)))
            {
                if (GUILayout.Button("Run Authoritative Level Comparison"))
                {
                    RunComparison();
                }
            }
            if (GUILayout.Button("Clear", GUILayout.Width(100f)))
            {
                queue.Clear();
                results.Clear();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResults()
        {
            if (results.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Authoritative Results",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each row is independent and receives the same comparison seed. This intentionally isolates player level as the changed input.",
                MessageType.None);

            if (GUILayout.Button("Copy Deterministic Comparison Report"))
            {
                EditorGUIUtility.systemCopyBuffer = BuildReport();
                diagnostic = "Copied deterministic level-comparison report.";
            }

            for (int index = 0; index < results.Count; index++)
            {
                StrongboxLevelComparisonResultV1 result = results[index];
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(
                    (index + 1).ToString("D3", CultureInfo.InvariantCulture)
                    + ". " + result.Input.Tier.DisplayName
                    + " @ Player L" + result.Input.PlayerLevel,
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "BOX status",
                    result.OpeningResult.Status.ToString());
                EditorGUILayout.LabelField(
                    "Committed weapon",
                    result.Prepared.CommittedSourceDefinitionId);
                EditorGUILayout.LabelField(
                    "Box instance",
                    result.Prepared.Context.InstanceStableId.ToString());
                EditorGUILayout.LabelField(
                    "Input fingerprint",
                    result.Input.Fingerprint);
                EditorGUILayout.LabelField(
                    "Prepared fingerprint",
                    result.Prepared.Fingerprint);
                EditorGUILayout.LabelField(
                    "Scrap",
                    result.ScrapBalance.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField(
                    "BOX sequence",
                    result.OpeningSequence.ToString(CultureInfo.InvariantCulture));

                if (!string.IsNullOrEmpty(result.OpeningResult.RejectionCode))
                {
                    EditorGUILayout.LabelField(
                        "Rejection / pending code",
                        result.OpeningResult.RejectionCode);
                }

                for (int itemIndex = 0;
                     itemIndex < result.Equipment.Count;
                     itemIndex++)
                {
                    DrawEquipment(result.Equipment[itemIndex]);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawEquipment(EquipmentInstance item)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Equipment " + item.InstanceId,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Definition", item.DefinitionId.ToString());
            EditorGUILayout.LabelField(
                "Item level",
                item.ItemLevel.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Quality", item.QualityId.ToString());
            EditorGUILayout.LabelField(
                "Augment slots",
                item.Augments.Count.ToString(CultureInfo.InvariantCulture));
            for (int augmentIndex = 0;
                 augmentIndex < item.Augments.Count;
                 augmentIndex++)
            {
                AugmentInstance augment = item.Augments[augmentIndex];
                EditorGUILayout.LabelField(
                    "  Slot " + (augmentIndex + 1)
                    + ": " + augment.DefinitionId
                    + " | Tier " + augment.Tier
                    + " | Level " + augment.Level);
            }
        }

        private void AddEntry(int tierNumber)
        {
            StrongboxLevelQueueEntryV1 entry =
                new StrongboxLevelQueueEntryV1(tierNumber, playerLevel);
            queue.Add(entry);
            results.Clear();
            diagnostic = entry.Tier.DisplayName
                + " captured at Player L"
                + entry.PlayerLevel.ToString(CultureInfo.InvariantCulture)
                + " in queue position "
                + queue.Count.ToString(CultureInfo.InvariantCulture)
                + ".";
        }

        private void LoadCatalog(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                AuthoritativeStrongboxSimulatorRuntimeV1 runtime;
                string error;
                if (!AuthoritativeStrongboxSimulatorRuntimeV1.TryCreate(
                    json,
                    out runtime,
                    out error))
                {
                    loadedCatalogJson = null;
                    loadedCatalogName = null;
                    diagnostic = error;
                    return;
                }

                loadedCatalogJson = json;
                loadedCatalogName = Path.GetFileName(path);
                results.Clear();
                diagnostic = "Loaded " + loadedCatalogName
                    + " with weapon-catalog fingerprint "
                    + runtime.WeaponCatalog.Fingerprint + ".";
            }
            catch (Exception exception)
            {
                loadedCatalogJson = null;
                loadedCatalogName = null;
                diagnostic = exception.ToString();
            }
        }

        private void RunComparison()
        {
            ulong seed;
            if (!TrySeed(out seed))
            {
                return;
            }

            results.Clear();
            try
            {
                for (int index = 0; index < queue.Count; index++)
                {
                    StrongboxLevelQueueEntryV1 input = queue[index];
                    AuthoritativeStrongboxSimulatorRuntimeV1 runtime;
                    string error;
                    if (!AuthoritativeStrongboxSimulatorRuntimeV1.TryCreate(
                        loadedCatalogJson,
                        out runtime,
                        out error))
                    {
                        throw new InvalidOperationException(error);
                    }

                    IReadOnlyList<AuthoritativeStrongboxPreparedOpenV1> prepared =
                        runtime.PrepareBatch(
                            new[] { input.Tier.TierNumber },
                            input.PlayerLevel,
                            seed);
                    StrongboxOpeningResultRuntimeV1 openingResult =
                        runtime.OpenOrRetry(prepared[0]);
                    results.Add(new StrongboxLevelComparisonResultV1(
                        input,
                        prepared[0],
                        openingResult,
                        runtime.EquipmentFrom(openingResult),
                        runtime.MoneyBalance,
                        runtime.ScrapBalance,
                        runtime.HoldingsSequence,
                        runtime.OpeningSequence));
                }

                diagnostic = "Completed "
                    + results.Count.ToString(CultureInfo.InvariantCulture)
                    + " isolated authoritative comparisons with seed "
                    + seed.ToString(CultureInfo.InvariantCulture)
                    + ".";
            }
            catch (Exception exception)
            {
                results.Clear();
                diagnostic = exception.ToString();
            }
        }

        private string BuildReport()
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(
                builder,
                "schema",
                "strongbox-level-comparison-report-v1");
            StrongboxCanonicalV1.AppendToken(
                builder,
                "catalog",
                loadedCatalogName ?? "none");
            StrongboxCanonicalV1.AppendToken(
                builder,
                "comparison_seed",
                seedText);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "result_count",
                results.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < results.Count; index++)
            {
                StrongboxLevelComparisonResultV1 result = results[index];
                string prefix = "result_"
                    + index.ToString("D4", CultureInfo.InvariantCulture)
                    + "_";
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "input",
                    result.Input.ToCanonicalString());
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "prepared",
                    result.Prepared.ToCanonicalString());
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "status",
                    result.OpeningResult.Status.ToString());
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "generated_outcome",
                    result.OpeningResult.GeneratedOutcome == null
                        ? "none"
                        : result.OpeningResult.GeneratedOutcome.Fingerprint);
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "scrap_balance",
                    result.ScrapBalance.ToString(CultureInfo.InvariantCulture));
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "equipment_count",
                    result.Equipment.Count.ToString(CultureInfo.InvariantCulture));
                for (int itemIndex = 0;
                     itemIndex < result.Equipment.Count;
                     itemIndex++)
                {
                    StrongboxCanonicalV1.AppendToken(
                        builder,
                        prefix + "equipment_"
                            + itemIndex.ToString("D4", CultureInfo.InvariantCulture),
                        result.Equipment[itemIndex].ToCanonicalString());
                }
            }

            string body = builder.ToString();
            return body
                + "report_fingerprint="
                + StrongboxCanonicalV1.Fingerprint(body)
                + "\n";
        }

        private bool TrySeed(out ulong seed)
        {
            if (!ulong.TryParse(
                seedText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out seed))
            {
                diagnostic = "Comparison seed must be an unsigned 64-bit integer.";
                return false;
            }
            return true;
        }
    }
}
