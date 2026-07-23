using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    /// <summary>
    /// Unity editor front end for the production-backed drop source simulator. All
    /// probabilities, overrides, pacing and tier selection remain owned by production
    /// catalogs/services; this window only captures inputs and renders the report.
    /// </summary>
    public sealed class DropSourceSimulationWindow : EditorWindow
    {
        private static readonly StableId[] SourceProfileIds =
        {
            ProductionRewardSourceCatalogV1.SmallEnemyId,
            ProductionRewardSourceCatalogV1.NormalEnemyId,
            ProductionRewardSourceCatalogV1.LargeEnemyId,
            ProductionRewardSourceCatalogV1.BossEnemyId,
            ProductionRewardSourceCatalogV1.ExtraBossEnemyId,
            ProductionRewardSourceCatalogV1.NormalPropId,
            ProductionRewardSourceCatalogV1.RarePropId,
            ProductionRewardSourceCatalogV1.ExtraRarePropId,
            ProductionRewardSourceCatalogV1.NormalHiddenTreasureId,
            ProductionRewardSourceCatalogV1.LargeHiddenLootId,
            ProductionRewardSourceCatalogV1.LargeTreasureLootId,
            ProductionRewardSourceCatalogV1.ExplicitNoDropId,
        };

        private static readonly string[] SourceProfileLabels =
            BuildLabels(SourceProfileIds);
        private static readonly string[] GameModeLabels =
        {
            "Campaign",
            "Survival",
        };
        private static readonly string[] MissionLabels =
        {
            "Campaign Stage 1",
            "Boss Rush",
        };
        private static readonly string[] DifficultyLabels =
        {
            "Normal",
            "Hard",
            "Nightmare",
        };

        private readonly int[] playerLevels = { 30, 30, 30, 30 };
        private int sourceProfileIndex = 1;
        private int participantCount = 1;
        private int gameModeIndex;
        private int missionIndex;
        private int difficultyIndex;
        private int missionLevel = 30;
        private int sourcesPerRoom = 10;
        private int roomCount = 5;
        private int sampleCount = 1000;
        private int moneyMultiplierPermille = 1000;
        private int scrapMultiplierPermille = 1000;
        private string seedText = "123456";
        private bool doubleRewardsEvent;
        private bool boxFrenzyEvent;
        private bool lockedVaultPlacement;
        private Vector2 scroll;
        private RewardSimulationReportV1 report;
        private string diagnostic =
            "Configure one source profile and run 1-4 independent personal reward streams through production services.";

        [MenuItem("Tools/Shooter Mover/Drop Source Simulator")]
        public static void Open()
        {
            GetWindow<DropSourceSimulationWindow>("Drop Source Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Production Drop Source Simulator",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Uses ProductionRewardSourceCatalogV1, RewardProfileResolverV1, ProductionRewardOverrideCatalogV1, ParticipantDropPacingAuthorityV1 and ProductionStrongboxTierSelectionCatalogV1. No simulator-owned probability or pity formula is used.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSourceInputs();
            DrawRunInputs();
            DrawParticipantInputs();
            DrawContextInputs();
            EditorGUILayout.Space();
            if (GUILayout.Button("Run Production Simulation", GUILayout.Height(34f)))
            {
                RunSimulation();
            }
            DrawReport();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(diagnostic, MessageType.None);
        }

        private void DrawSourceInputs()
        {
            EditorGUILayout.LabelField("Reward source", EditorStyles.boldLabel);
            sourceProfileIndex = EditorGUILayout.Popup(
                "Profile",
                sourceProfileIndex,
                SourceProfileLabels);
            sourcesPerRoom = Math.Max(
                1,
                EditorGUILayout.IntField(
                    "Sources per room",
                    sourcesPerRoom));
            roomCount = Math.Max(
                1,
                EditorGUILayout.IntField("Rooms per run", roomCount));
        }

        private void DrawRunInputs()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
            sampleCount = Math.Max(
                1,
                EditorGUILayout.IntField("Run samples", sampleCount));
            seedText = EditorGUILayout.TextField("Root seed", seedText);
            missionLevel = Math.Max(
                0,
                EditorGUILayout.IntField("Mission level", missionLevel));
            participantCount = EditorGUILayout.IntSlider(
                "Participants",
                participantCount,
                1,
                4);
        }

        private void DrawParticipantInputs()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Personal participant levels",
                EditorStyles.boldLabel);
            for (int index = 0; index < participantCount; index++)
            {
                playerLevels[index] = Math.Max(
                    0,
                    EditorGUILayout.IntField(
                        "Player " + (index + 1) + " level",
                        playerLevels[index]));
            }
        }

        private void DrawContextInputs()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Live override context",
                EditorStyles.boldLabel);
            gameModeIndex = EditorGUILayout.Popup(
                "Game mode",
                gameModeIndex,
                GameModeLabels);
            missionIndex = EditorGUILayout.Popup(
                "Mission",
                missionIndex,
                MissionLabels);
            difficultyIndex = EditorGUILayout.Popup(
                "Difficulty",
                difficultyIndex,
                DifficultyLabels);
            doubleRewardsEvent = EditorGUILayout.Toggle(
                "Double rewards event",
                doubleRewardsEvent);
            boxFrenzyEvent = EditorGUILayout.Toggle(
                "Box frenzy event",
                boxFrenzyEvent);
            lockedVaultPlacement = EditorGUILayout.Toggle(
                "Locked-vault placement",
                lockedVaultPlacement);
            moneyMultiplierPermille = Math.Max(
                0,
                EditorGUILayout.IntField(
                    "Money multiplier permille",
                    moneyMultiplierPermille));
            scrapMultiplierPermille = Math.Max(
                0,
                EditorGUILayout.IntField(
                    "Scrap multiplier permille",
                    scrapMultiplierPermille));
        }

        private void DrawReport()
        {
            if (report == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Fingerprint", report.Fingerprint);
            EditorGUILayout.LabelField(
                "Rejected generations",
                report.RejectedGenerationCount.ToString(
                    CultureInfo.InvariantCulture));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy canonical report"))
            {
                EditorGUIUtility.systemCopyBuffer = report.ToCanonicalString();
                diagnostic = "Canonical report copied to the clipboard.";
            }
            if (GUILayout.Button("Save canonical report"))
            {
                SaveReport();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.TextArea(
                report.ToCanonicalString(),
                GUILayout.MinHeight(320f));
        }

        private void RunSimulation()
        {
            ulong seed;
            if (!ulong.TryParse(
                    seedText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out seed))
            {
                diagnostic = "Root seed must be an unsigned integer.";
                return;
            }

            try
            {
                StableId sourceProfileId =
                    SourceProfileIds[sourceProfileIndex];
                StableId gameModeId = gameModeIndex == 1
                    ? ProductionRewardOverrideCatalogV1.SurvivalModeId
                    : StableId.Parse("game-mode.campaign");
                StableId missionId = missionIndex == 1
                    ? ProductionRewardOverrideCatalogV1.BossRushMissionId
                    : StableId.Parse("mission-layout.campaign-stage-1");
                StableId difficultyId = ResolveDifficulty();
                var eventIds = new List<StableId>();
                if (doubleRewardsEvent)
                {
                    eventIds.Add(
                        ProductionRewardOverrideCatalogV1
                            .DoubleRewardsEventId);
                }
                if (boxFrenzyEvent)
                {
                    eventIds.Add(
                        ProductionRewardOverrideCatalogV1.BoxFrenzyEventId);
                }
                StableId placementId = lockedVaultPlacement
                    ? ProductionRewardOverrideCatalogV1.LockedVaultPlacementId
                    : StableId.Parse("placement.simulator-default");
                RewardContextOverrideResolutionV1 overrides =
                    ProductionRewardOverrideCatalogV1.Resolve(
                        sourceProfileId,
                        gameModeId,
                        missionId,
                        difficultyId,
                        eventIds,
                        placementId);
                var participants =
                    new List<RewardSimulationParticipantInputV1>();
                for (int index = 0; index < participantCount; index++)
                {
                    participants.Add(new RewardSimulationParticipantInputV1(
                        StableId.Create(
                            "participant",
                            "drop-simulator-" + (index + 1)),
                        playerLevels[index]));
                }

                var request = new DropSourceSimulationRequestV1(
                    sourceProfileId,
                    participants,
                    missionLevel,
                    difficultyId,
                    gameModeId,
                    eventIds,
                    sourcesPerRoom,
                    roomCount,
                    seed,
                    sampleCount,
                    moneyMultiplierPermille,
                    scrapMultiplierPermille,
                    ProductionRunDropPacingCatalogV1.Resolve(
                        gameModeId,
                        null),
                    overrides.GameModeOverride,
                    overrides.MissionOverride,
                    overrides.DifficultyOverride,
                    overrides.EventOverrides,
                    overrides.PlacementOverride);
                report = new DropSourceSimulationRuntimeV1().Run(request);
                diagnostic = "Simulation complete: "
                    + report.Fingerprint;
            }
            catch (Exception exception)
            {
                report = null;
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private StableId ResolveDifficulty()
        {
            if (difficultyIndex == 1)
            {
                return ProductionRewardOverrideCatalogV1.HardDifficultyId;
            }
            return difficultyIndex == 2
                ? ProductionRewardOverrideCatalogV1.NightmareDifficultyId
                : StableId.Parse("difficulty.normal");
        }

        private void SaveReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save reward simulation report",
                string.Empty,
                "reward-simulation-" + report.Fingerprint + ".txt",
                "txt");
            if (string.IsNullOrWhiteSpace(path)) return;
            File.WriteAllText(path, report.ToCanonicalString());
            diagnostic = "Saved " + path;
        }

        private static string[] BuildLabels(IReadOnlyList<StableId> ids)
        {
            var labels = new string[ids.Count];
            for (int index = 0; index < ids.Count; index++)
            {
                labels[index] = ids[index].ToString();
            }
            return labels;
        }
    }
}
