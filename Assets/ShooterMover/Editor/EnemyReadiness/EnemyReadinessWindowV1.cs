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
                "This window validates static authoritative assets and exact presentation "
                + "retirement configuration. It does not accept prefab booleans as proof of "
                + "live mechanics or player damage. Production readiness remains blocked until "
                + "the production scene passes PlayMode/manual runtime acceptance.",
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
                "Production ready: " + ready + " / " + rows.Count,
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
            Flag("presentation recipe valid", row.PresentationRegistered);
            Flag("canonical room mapping", row.RoomMappingAvailable);
            Flag("terminal retirement configured", row.TerminalRetirementConfigured);
            Flag("live mechanics acceptance verified", row.RuntimeMechanicsVerified);
            Flag("live player-damage acceptance verified", row.PlayerDamageVerified);
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
                rows.Add(BuildRow(
                    enemyImport.Catalog.Definitions[index],
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

            string retirementFailure;
            bool retirementConfigured = ReadRetirementEvidence(
                prefab,
                out retirementFailure);

            // Static asset inspection cannot prove that the current scene successfully bound the
            // factory runtime, attack publisher, current player lifecycle and damage receiver.
            // These facts deliberately remain false until executable acceptance evidence exists.
            const bool mechanicsVerified = false;
            const bool playerDamageVerified = false;
            const bool runtimeAcceptanceVerified = false;

            var missing = new List<string>();
            if (!string.IsNullOrEmpty(presentationFailure)) missing.Add(presentationFailure);
            if (!presentationRegistered) missing.Add(adapterFailure);
            if (!roomMappingAvailable)
            {
                missing.Add(string.IsNullOrEmpty(roomFailure)
                    ? "No canonical authored room mapping exists."
                    : roomFailure);
            }
            if (!retirementConfigured) missing.Add(retirementFailure);
            missing.Add(
                "Live mechanics are not proven by static prefab declarations; run the production "
                + "PlayMode/manual movement, aim, wind-up and emission acceptance route.");
            missing.Add(
                "Canonical player damage is not proven by static prefab declarations; run the "
                + "production hit, replay, stale-lifecycle and defeat acceptance route.");

            bool ready = presentationRegistered
                && roomMappingAvailable
                && retirementConfigured
                && mechanicsVerified
                && playerDamageVerified
                && runtimeAcceptanceVerified;
            return new ReadinessRowV1(
                definition.DefinitionId.ToString(),
                definition.PresentationId.ToString(),
                true,
                presentationRegistered,
                roomMappingAvailable,
                retirementConfigured,
                mechanicsVerified,
                playerDamageVerified,
                ready,
                ready
                    ? "All required static and executable production evidence is present."
                    : string.Join(" ", missing));
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

        private static bool ReadRetirementEvidence(GameObject prefab, out string reason)
        {
            if (prefab == null)
            {
                reason = "No prefab exists for terminal-presentation validation.";
                return false;
            }

            EnemyDefeatedPresentationRetirement2D[] providers =
                prefab.GetComponentsInChildren<EnemyDefeatedPresentationRetirement2D>(true);
            if (providers.Length == 0)
            {
                reason = "No defeated-presentation retirement component is configured.";
                return false;
            }
            if (providers.Length > 1)
            {
                reason = "Multiple defeated-presentation retirement components are ambiguous.";
                return false;
            }

            try
            {
                return providers[0].TryValidate(out reason);
            }
            catch (Exception exception)
            {
                reason = "Terminal retirement evidence failed: " + exception.Message;
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
                bool presentationRegistered,
                bool roomMappingAvailable,
                bool terminalRetirementConfigured,
                bool runtimeMechanicsVerified,
                bool playerDamageVerified,
                bool productionReady,
                string reason)
            {
                DefinitionId = definitionId;
                PresentationId = presentationId;
                CatalogueValid = catalogueValid;
                PresentationRegistered = presentationRegistered;
                RoomMappingAvailable = roomMappingAvailable;
                TerminalRetirementConfigured = terminalRetirementConfigured;
                RuntimeMechanicsVerified = runtimeMechanicsVerified;
                PlayerDamageVerified = playerDamageVerified;
                ProductionReady = productionReady;
                Reason = reason;
            }

            public string DefinitionId { get; private set; }
            public string PresentationId { get; private set; }
            public bool CatalogueValid { get; private set; }
            public bool PresentationRegistered { get; private set; }
            public bool RoomMappingAvailable { get; private set; }
            public bool TerminalRetirementConfigured { get; private set; }
            public bool RuntimeMechanicsVerified { get; private set; }
            public bool PlayerDamageVerified { get; private set; }
            public bool ProductionReady { get; private set; }
            public string Reason { get; private set; }
        }
    }
}
