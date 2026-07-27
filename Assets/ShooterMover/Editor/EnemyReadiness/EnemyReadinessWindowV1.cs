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
        private const string RoomContentPath =
            "Assets/ShooterMover/Resources/ProductionLevels/Level1RoomContent.asset";

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
            EditorGUILayout.HelpBox(
                row.Reason,
                row.ProductionReady ? MessageType.Info : MessageType.Warning);
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
            JsonRoomContentDefinition2D roomContent =
                AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(RoomContentPath);
            if (enemyJson == null || presentationCatalog == null || roomContent == null)
            {
                loadFailure = "Required production assets are missing. Enemy catalogue: "
                    + Presence(enemyJson)
                    + "; presentation catalogue: "
                    + Presence(presentationCatalog)
                    + "; room content: "
                    + Presence(roomContent)
                    + ".";
                return;
            }

            EnemyCatalogImportResultV1 enemyImport = EnemyCatalogJsonImporterV1.Import(
                enemyJson.text,
                BuiltInEnemyCatalogRegistryV1.Create());
            if (!enemyImport.IsValid)
            {
                loadFailure = "Enemy catalogue is invalid: " + JoinEnemyIssues(enemyImport.Issues);
                return;
            }

            RoomContentImportResultV1 roomImport = roomContent.Import();
            if (roomImport == null || !roomImport.IsValid)
            {
                loadFailure = "Production room content is invalid: " + JoinRoomIssues(
                    roomImport == null ? null : roomImport.Issues);
                return;
            }

            string roomFailure;
            Dictionary<StableId, StableId> roomMappings = ReadRoomMappings(
                roomImport.Bundle,
                out roomFailure);
            for (int index = 0; index < enemyImport.Catalog.Definitions.Count; index++)
            {
                EnemyDefinitionV1 definition = enemyImport.Catalog.Definitions[index];
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

            string adapterFailure;
            bool presentationRegistered = ReadPresentationEvidence(
                prefab,
                definition.DefinitionId,
                out adapterFailure);

            StableId mappedPresentation;
            bool roomMappingAvailable = string.IsNullOrEmpty(roomFailure)
                && roomMappings.TryGetValue(definition.DefinitionId, out mappedPresentation)
                && mappedPresentation == definition.PresentationId;

            string mechanicsReason;
            bool mechanics = ReadMechanicsEvidence(prefab, out mechanicsReason);
            string damageReason;
            bool playerDamage = ReadPlayerDamageEvidence(prefab, out damageReason);
            bool deathDownstream = roomMappingAvailable;

            var missing = new List<string>();
            if (!string.IsNullOrEmpty(presentationFailure)) missing.Add(presentationFailure);
            if (!presentationRegistered) missing.Add(adapterFailure);
            if (!roomMappingAvailable)
            {
                missing.Add(string.IsNullOrEmpty(roomFailure)
                    ? "No canonical authored room mapping exists."
                    : roomFailure);
            }
            if (!mechanics) missing.Add(mechanicsReason);
            if (!playerDamage) missing.Add(damageReason);
            if (!deathDownstream)
            {
                missing.Add("No authored room mapping reaches the generic room terminal route.");
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

        private static bool ReadPresentationEvidence(
            GameObject prefab,
            StableId definitionId,
            out string reason)
        {
            if (prefab == null)
            {
                reason = "No presentation prefab is registered.";
                return false;
            }

            EnemyPresentationAdapter2D[] adapters =
                prefab.GetComponentsInChildren<EnemyPresentationAdapter2D>(true);
            if (adapters.Length == 0)
            {
                reason = "No matching presentation adapter is registered.";
                return false;
            }
            if (adapters.Length > 1)
            {
                reason = "Multiple presentation adapters are registered; ownership is ambiguous.";
                return false;
            }

            try
            {
                return adapters[0].TryValidateFor(definitionId, out reason);
            }
            catch (Exception exception)
            {
                reason = "Presentation evidence failed: " + exception.Message;
                return false;
            }
        }

        private static bool ReadMechanicsEvidence(GameObject prefab, out string reason)
        {
            IEnemyRuntimeMechanicsReadiness2D evidence = null;
            int count = 0;
            if (prefab != null)
            {
                MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < components.Length; index++)
                {
                    IEnemyRuntimeMechanicsReadiness2D candidate =
                        components[index] as IEnemyRuntimeMechanicsReadiness2D;
                    if (candidate == null) continue;
                    evidence = candidate;
                    count++;
                }
            }

            if (count == 0)
            {
                reason = "No typed production mechanics-readiness evidence is registered.";
                return false;
            }
            if (count > 1)
            {
                reason = "Multiple runtime mechanics-readiness providers are registered; ownership is ambiguous.";
                return false;
            }

            try
            {
                bool ready = evidence.RuntimeMechanicsReady;
                string detail = evidence.RuntimeMechanicsReadinessReason;
                reason = string.IsNullOrWhiteSpace(detail)
                    ? (ready ? string.Empty : "Runtime mechanics evidence reports unsupported.")
                    : detail;
                return ready;
            }
            catch (Exception exception)
            {
                reason = "Runtime mechanics readiness evidence failed: " + exception.Message;
                return false;
            }
        }

        private static bool ReadPlayerDamageEvidence(GameObject prefab, out string reason)
        {
            IEnemyPlayerDamageRouteReadiness2D evidence = null;
            int count = 0;
            if (prefab != null)
            {
                MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < components.Length; index++)
                {
                    IEnemyPlayerDamageRouteReadiness2D candidate =
                        components[index] as IEnemyPlayerDamageRouteReadiness2D;
                    if (candidate == null) continue;
                    evidence = candidate;
                    count++;
                }
            }

            if (count == 0)
            {
                reason = "No typed canonical player-damage-route evidence is registered.";
                return false;
            }
            if (count > 1)
            {
                reason = "Multiple player-damage-route readiness providers are registered; ownership is ambiguous.";
                return false;
            }

            try
            {
                bool ready = evidence.PlayerDamageRouteReady;
                string detail = evidence.PlayerDamageRouteReadinessReason;
                reason = string.IsNullOrWhiteSpace(detail)
                    ? (ready ? string.Empty : "Player-damage route evidence reports unsupported.")
                    : detail;
                return ready;
            }
            catch (Exception exception)
            {
                reason = "Player-damage-route readiness evidence failed: " + exception.Message;
                return false;
            }
        }

        private static Dictionary<StableId, StableId> ReadRoomMappings(
            RoomContentBundleV1 bundle,
            out string failure)
        {
            failure = string.Empty;
            var result = new Dictionary<StableId, StableId>();
            if (bundle == null)
            {
                failure = "The canonical room importer returned no bundle.";
                return result;
            }

            RoomContentObjectCatalogV1 objectCatalog =
                BuiltInRoomContentObjectCatalogV1.Create();
            for (int index = 0; index < bundle.Enemies.Count; index++)
            {
                RoomEnemyPlacementContentV1 placement = bundle.Enemies[index];
                if (placement == null || placement.ObjectStableId == null)
                {
                    failure = "Canonical room content contains an enemy without stable object identity.";
                    return new Dictionary<StableId, StableId>();
                }

                RoomContentObjectDefinitionV1 mapping;
                if (!objectCatalog.TryResolve(
                    placement.ObjectStableId,
                    RoomContentObjectKindV1.Enemy,
                    out mapping))
                {
                    failure = "Canonical room content references an unknown enemy object: "
                        + placement.ObjectStableId
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

            return result;
        }

        private static string JoinEnemyIssues(IReadOnlyList<EnemyCatalogIssueV1> issues)
        {
            if (issues == null || issues.Count == 0) return "no structured issue was returned.";
            var values = new List<string>();
            for (int index = 0; index < issues.Count; index++)
            {
                EnemyCatalogIssueV1 issue = issues[index];
                values.Add(issue == null ? "<null issue>" : issue.ToString());
            }
            return string.Join(" | ", values);
        }

        private static string JoinRoomIssues(IReadOnlyList<RoomContentImportIssueV1> issues)
        {
            if (issues == null || issues.Count == 0) return "no structured issue was returned.";
            var values = new List<string>();
            for (int index = 0; index < issues.Count; index++)
            {
                RoomContentImportIssueV1 issue = issues[index];
                values.Add(issue == null
                    ? "<null issue>"
                    : "[" + issue.Code + "] " + issue.Path + ": " + issue.Message);
            }
            return string.Join(" | ", values);
        }

        private static string Presence(UnityEngine.Object value)
        {
            return value == null ? "missing" : "found";
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
