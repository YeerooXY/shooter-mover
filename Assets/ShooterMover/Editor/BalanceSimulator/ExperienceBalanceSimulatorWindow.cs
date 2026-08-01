using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Progression.Experience;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class ExperienceBalanceSimulatorWindow : EditorWindow
    {
        private int startingLevel = 1;
        private float missionDurationMinutes = 10f;
        private int completedRooms = 3;
        private float modeMultiplier = 1f;
        private int simulatedMissions = 100;
        private readonly int[] lightCounts = new int[4];
        private readonly int[] standardCounts = { 3, 0, 0, 0 };
        private readonly int[] turretCounts = new int[4];
        private ExperienceBalanceReport report;
        private Vector2 scroll;

        [MenuItem("Shooter Mover/Balance/Player XP Simulator")]
        private static void Open()
        {
            GetWindow<ExperienceBalanceSimulatorWindow>(
                "Player XP Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mission inputs", EditorStyles.boldLabel);
            startingLevel = EditorGUILayout.IntSlider(
                "Starting level", startingLevel, 1, 100);
            missionDurationMinutes = EditorGUILayout.FloatField(
                "Mission duration (minutes)", missionDurationMinutes);
            completedRooms = EditorGUILayout.IntField(
                "Completed rooms", completedRooms);
            modeMultiplier = EditorGUILayout.FloatField(
                "Mode multiplier", modeMultiplier);
            simulatedMissions = EditorGUILayout.IntField(
                "Simulated missions", simulatedMissions);

            EditorGUILayout.Space();
            DrawProfile("Light (7 XP)", lightCounts);
            DrawProfile("Standard (10 XP)", standardCounts);
            DrawProfile("Turret (12 XP)", turretCounts);
            EditorGUILayout.Space();

            if (GUILayout.Button("SIMULATE", GUILayout.Height(32f)))
            {
                Simulate();
            }
            if (report == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mission report", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "XP per mission",
                report.ExperiencePerMission.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "XP per hour",
                report.ExperiencePerHour.ToString("0.##", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Level after simulation",
                report.SimulatedFinalLevel.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Hours to level 100",
                report.TotalHoursToLevel100.ToString("0.##", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Casual: 100 XP / 10 min",
                report.CasualHoursToLevel100.ToString("0.##", CultureInfo.InvariantCulture)
                    + " hours");
            EditorGUILayout.LabelField(
                "Efficient: 80 XP / 5 min",
                report.EfficientHoursToLevel100.ToString("0.##", CultureInfo.InvariantCulture)
                    + " hours");
            EditorGUILayout.LabelField(
                "Same 100 XP / 5 min",
                report.IdenticalRewardFiveMinuteHoursToLevel100.ToString(
                    "0.##", CultureInfo.InvariantCulture) + " hours");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Level-by-level", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < report.Levels.Count; index++)
            {
                ExperienceLevelProjection level = report.Levels[index];
                EditorGUILayout.LabelField(
                    "Level " + level.Level.ToString(CultureInfo.InvariantCulture),
                    "cost " + level.CostToNextLevel.ToString(CultureInfo.InvariantCulture)
                        + " | cumulative "
                        + level.CumulativeExperience.ToString(CultureInfo.InvariantCulture)
                        + " | missions "
                        + level.MissionsToReach.ToString(CultureInfo.InvariantCulture)
                        + " | hours "
                        + level.HoursToReach.ToString("0.##", CultureInfo.InvariantCulture));
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawProfile(string label, int[] counts)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int tier = 1; tier <= 4; tier++)
            {
                counts[tier - 1] = EditorGUILayout.IntField(
                    "T" + tier.ToString(CultureInfo.InvariantCulture),
                    counts[tier - 1]);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void Simulate()
        {
            var enemies = new List<ExperienceEnemyCount>();
            AddCounts(enemies, MissionExperienceProfileIds.Light, lightCounts);
            AddCounts(enemies, MissionExperienceProfileIds.Standard, standardCounts);
            AddCounts(enemies, MissionExperienceProfileIds.Turret, turretCounts);
            try
            {
                report = ExperienceBalanceSimulator.Simulate(
                    startingLevel,
                    missionDurationMinutes,
                    completedRooms,
                    enemies,
                    checked((decimal)modeMultiplier),
                    simulatedMissions);
            }
            catch (Exception exception)
            {
                report = null;
                Debug.LogException(exception);
            }
        }

        private static void AddCounts(
            ICollection<ExperienceEnemyCount> output,
            ShooterMover.Domain.Common.StableId profile,
            int[] counts)
        {
            for (int tier = 1; tier <= 4; tier++)
            {
                output.Add(new ExperienceEnemyCount(
                    profile,
                    tier,
                    Mathf.Max(0, counts[tier - 1])));
            }
        }
    }
}
