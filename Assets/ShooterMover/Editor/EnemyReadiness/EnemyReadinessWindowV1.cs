using System;
using System.Collections.Generic;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Enemies;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.Enemies
{
    public sealed class EnemyReadinessWindowV1 : EditorWindow
    {
        private const string EnemyCatalogPath =
            "Assets/ShooterMover/Resources/EnemyCatalog/enemy_catalog_v2.json";
        private const string PresentationCatalogPath =
            "Assets/ShooterMover/Resources/ProductionLevels/Level1PresentationCatalog.asset";
        private const string RoomContentFolder =
            "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json";

        private readonly List<ReadinessRowV1> rows = new List<ReadinessRowV1>();
        private Vector2 scroll;
        private string loadFailure = string.Empty;

        [MenuItem("Shooter Mover/Diagnostics/Enemy Readiness")]
        public static void Open()
        {
            GetWindow<EnemyReadinessWindowV1>("Enemy Readiness");
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Enemy Production Readiness", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This is a read-only projection. Missing, stale, unsupported, or ambiguous "
                + "evidence is reported as not ready; the tool never enables catalogue entries.",
                MessageType.Info);

            if (GUILayout.Button("Refresh from authoritative assets"))
            {
                Refresh();
            }

            if (!string.IsNullOrEmpty(loadFailure))
            {
                EditorGUILayout.HelpBox(loadFailure, MessageType.Error);
                return;
            }

            int ready = 0;
            for (int index = 0; index < rows.Count; index++)
            {
                if (rows[index].ProductionReady) ready++;
            }

            EditorGUILayout.LabelField(
                "Ready: " + ready + " / " + rows.Count,
                ready == rows.Count && rows.Count > 0
                    ? EditorStyles.boldLabel
                    : EditorStyles.label);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < rows.Count; index++)
            {
                Draw(rows[index]);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void Draw(ReadinessRowV1 row)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                row.DefinitionId,
                row.ProductionReady ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.LabelField("presentation", row.PresentationId);
            Flag("catalogue valid", row.CatalogueValid);
            Flag("runtime mechanics supported", row.RuntimeMechanicsSupported);
            Flag("presentation registered", row.PresentationRegistered);
            Flag("room mapping available", row.RoomMappingAvailable);
            Flag("player-damage route supported", row.PlayerDamageRouteSupported);
            Flag("death downstream integration", row.DeathDownstreamIntegrationAvailable);
            EditorGUILayout.LabelField(
                row.ProductionReady ? "PRODUCTION READY" : "NOT READY",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(row.Reason, row.ProductionReady
                ? MessageType.Info
                : MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        private static void Flag(string label, bool value)
        {
            EditorGUILayout.LabelField(label, value ? "yes" : "no");
        }

        private void Refresh()
        {
            rows.Clear();
            loadFailure = string.Empty;
            try
            {
                RefreshCore();
            }
            catch (Exception exception)
            {
                rows.Clear();
                loadFailure = "Readiness projection failed closed: " + exception.Message;
            }
        }

        private void RefreshCore()
        {
            TextAsset enemyJson = AssetDatabase.LoadAssetAtPath<TextAsset>(EnemyCatalogPath);
            RoomPresentationCatalog2D presentationCatalog =
                AssetDatabase.LoadAssetAtPath<RoomPresentationCatalog2D>(PresentationCatalogPath);
            if (enemyJson == null || presentationCatalog == null)
            {
                loadFailure = "Required production assets are missing. Enemy catalogue: "
                    + (enemyJson == null ? "missing" : "found")
                    + "; presentation catalogue: "
                    + (presentationCatalog == null ? "missing" : "found")
                    + ".";
                return;
            }

            EnemyCatalogImportResultV1 import = EnemyCatalogJsonImporterV1.Import(
                enemyJson.text,
                BuiltInEnemyCatalogRegistryV1.Create());
            if (!import.IsValid)
            {
                loadFailure = "Enemy catalogue is invalid: " + JoinIssues(import.Issues);
                return;
            }

            string roomFailure;
            Dictionary<StableId, StableId> roomMappings = ReadRoomMappings(out roomFailure);
            for (int index = 0; index < import.Catalog.Definitions.Count; index++)
            {
                EnemyDefinitionV1 definition = import.Catalog.Definitions[index];
                rows.Add(BuildRow(
                    definition,
                    presentationCatalog,
                    roomMappings,
                    roomFailure));
            }
        }

        private static ReadinessRowV1 BuildRow(
            EnemyDefinitionV1 definition,
            RoomPresentationCatalog2D presentationCatalog,
            IDictionary<StableId, StableId> roomMappings,
            string roomFailure)
        {
            GameObject prefab = null;
            string presentationFailure = string.Empty;
            try
            {
                presentationCatalog.TryResolve(definition.PresentationId, out prefab);
            }
            catch (Exception exception)
            {
                presentationFailure = "Presentation catalogue invalid: " + exception.Message;
            }

            EnemyPresentationAdapter2D adapter = prefab == null
                ? null
                : prefab.GetComponentInChildren<EnemyPresentationAdapter2D>(true);
            string adapterFailure = string.Empty;
            bool presentationRegistered = adapter != null
                && adapter.TryValidateFor(definition.DefinitionId, out adapterFailure);

            StableId mappedPresentation;
            bool roomMappingAvailable = string.IsNullOrEmpty(roomFailure)
                && roomMappings.TryGetValue(definition.DefinitionId, out mappedPresentation)
                && mappedPresentation == definition.PresentationId;

            string mechanicsReason;
            bool mechanics = ReadMechanicsEvidence(prefab, out mechanicsReason);
            string damageReason;
            bool playerDamage = ReadPlayerDamageEvidence(prefab, out damageReason);
            bool deathDownstream = roomMappingAvailable && prefab != null;

            var missing = new List<string>();
            if (!string.IsNullOrEmpty(presentationFailure)) missing.Add(presentationFailure);
            if (!presentationRegistered)
            {
                missing.Add(string.IsNullOrEmpty(adapterFailure)
                    ? "No matching presentation adapter is registered."
                    : adapterFailure);
            }
            if (!roomMappingAvailable)
            {
                missing.Add(string.IsNullOrEmpty(roomFailure)
                    ? "No unambiguous authored room mapping exists."
                    : roomFailure);
            }
            if (!mechanics) missing.Add(mechanicsReason);
            if (!playerDamage) missing.Add(damageReason);
            if (!deathDownstream)
            {
                missing.Add("Canonical room terminal/death route is unavailable for this mapping.");
            }

            bool ready = presentationRegistered
                && roomMappingAvailable
                && mechanics
                && playerDamage
                && deathDownstream;
            return new ReadinessRowV1(
                definition.DefinitionId.ToString(),
                definition.PresentationId.ToString(),
                true,
                mechanics,
                presentationRegistered,
                roomMappingAvailable,
                playerDamage,
                deathDownstream,
                ready,
                ready ? "All required production evidence is present." : string.Join(" ", missing));
        }

        private static bool ReadMechanicsEvidence(GameObject prefab, out string reason)
        {
            if (prefab != null)
            {
                MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < components.Length; index++)
                {
                    IEnemyRuntimeMechanicsReadiness2D evidence =
                        components[index] as IEnemyRuntimeMechanicsReadiness2D;
                    if (evidence == null) continue;
                    reason = string.IsNullOrWhiteSpace(evidence.RuntimeMechanicsReadinessReason)
                        ? "Runtime mechanics evidence reports unsupported."
                        : evidence.RuntimeMechanicsReadinessReason;
                    return evidence.RuntimeMechanicsReady;
                }
            }

            reason = "No typed production mechanics-readiness evidence is registered.";
            return false;
        }

        private static bool ReadPlayerDamageEvidence(GameObject prefab, out string reason)
        {
            if (prefab != null)
            {
                MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < components.Length; index++)
                {
                    IEnemyPlayerDamageRouteReadiness2D evidence =
                        components[index] as IEnemyPlayerDamageRouteReadiness2D;
                    if (evidence == null) continue;
                    reason = string.IsNullOrWhiteSpace(evidence.PlayerDamageRouteReadinessReason)
                        ? "Player-damage route evidence reports unsupported."
                        : evidence.PlayerDamageRouteReadinessReason;
                    return evidence.PlayerDamageRouteReady;
                }
            }

            reason = "No typed canonical player-damage-route evidence is registered.";
            return false;
        }

        private static Dictionary<StableId, StableId> ReadRoomMappings(out string failure)
        {
            failure = string.Empty;
            var result = new Dictionary<StableId, StableId>();
            RoomContentObjectCatalogV1 objectCatalog =
                BuiltInRoomContentObjectCatalogV1.Create();
            string[] guids = AssetDatabase.FindAssets(
                "t:TextAsset",
                new[] { RoomContentFolder });

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null || asset.text.IndexOf("\"enemies\"", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                EnemyPlacementDocumentV1 document;
                try
                {
                    document = JsonUtility.FromJson<EnemyPlacementDocumentV1>(asset.text);
                }
                catch (Exception exception)
                {
                    failure = "Malformed room enemy document " + path + ": " + exception.Message;
                    return new Dictionary<StableId, StableId>();
                }

                EnemyPlacementV1[] placements = document == null
                    ? null
                    : document.enemies;
                if (placements == null)
                {
                    failure = "Room enemy document has no readable enemies array: " + path;
                    return new Dictionary<StableId, StableId>();
                }

                for (int placementIndex = 0; placementIndex < placements.Length; placementIndex++)
                {
                    EnemyPlacementV1 placement = placements[placementIndex];
                    if (placement == null || string.IsNullOrWhiteSpace(placement.objectId))
                    {
                        failure = "Room enemy placement has no stable object identity: " + path;
                        return new Dictionary<StableId, StableId>();
                    }

                    StableId objectStableId;
                    try
                    {
                        objectStableId = StableId.Parse(placement.objectId);
                    }
                    catch (Exception exception)
                    {
                        failure = "Invalid room enemy object identity "
                            + placement.objectId
                            + " in "
                            + path
                            + ": "
                            + exception.Message;
                        return new Dictionary<StableId, StableId>();
                    }

                    RoomContentObjectDefinitionV1 mapping;
                    if (!objectCatalog.TryResolve(
                        objectStableId,
                        RoomContentObjectKindV1.Enemy,
                        out mapping))
                    {
                        failure = "Unknown room enemy object identity "
                            + placement.objectId
                            + " in "
                            + path
                            + ".";
                        return new Dictionary<StableId, StableId>();
                    }

                    StableId existing;
                    if (result.TryGetValue(mapping.RuntimeDefinitionStableId, out existing)
                        && existing != mapping.PresentationStableId)
                    {
                        failure = "Conflicting room presentation mappings for "
                            + mapping.RuntimeDefinitionStableId
                            + ".";
                        return new Dictionary<StableId, StableId>();
                    }

                    result[mapping.RuntimeDefinitionStableId] = mapping.PresentationStableId;
                }
            }

            return result;
        }

        private static string JoinIssues(IReadOnlyList<EnemyCatalogIssueV1> issues)
        {
            var values = new List<string>();
            for (int index = 0; index < issues.Count; index++)
            {
                values.Add(issues[index].ToString());
            }
            return string.Join(" | ", values);
        }

        [Serializable]
        private sealed class EnemyPlacementDocumentV1
        {
            public EnemyPlacementV1[] enemies;
        }

        [Serializable]
        private sealed class EnemyPlacementV1
        {
            public string @object;

            public string objectId { get { return @object; } }
        }

        private sealed class ReadinessRowV1
        {
            public ReadinessRowV1(
                string definitionId,
                string presentationId,
                bool catalogueValid,
                bool runtimeMechanicsSupported,
                bool presentationRegistered,
                bool roomMappingAvailable,
                bool playerDamageRouteSupported,
                bool deathDownstreamIntegrationAvailable,
                bool productionReady,
                string reason)
            {
                DefinitionId = definitionId;
                PresentationId = presentationId;
                CatalogueValid = catalogueValid;
                RuntimeMechanicsSupported = runtimeMechanicsSupported;
                PresentationRegistered = presentationRegistered;
                RoomMappingAvailable = roomMappingAvailable;
                PlayerDamageRouteSupported = playerDamageRouteSupported;
                DeathDownstreamIntegrationAvailable = deathDownstreamIntegrationAvailable;
                ProductionReady = productionReady;
                Reason = reason;
            }

            public string DefinitionId { get; private set; }
            public string PresentationId { get; private set; }
            public bool CatalogueValid { get; private set; }
            public bool RuntimeMechanicsSupported { get; private set; }
            public bool PresentationRegistered { get; private set; }
            public bool RoomMappingAvailable { get; private set; }
            public bool PlayerDamageRouteSupported { get; private set; }
            public bool DeathDownstreamIntegrationAvailable { get; private set; }
            public bool ProductionReady { get; private set; }
            public string Reason { get; private set; }
        }
    }
}
