using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Equipment;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class AuthoritativeStrongboxSimulatorWindow :
        EditorWindow
    {
        private enum Page
        {
            Queue,
            Opening,
            Complete,
        }

        private readonly List<int> queue = new List<int>();
        private readonly List<int> frozenQueue = new List<int>();
        private readonly List<AuthoritativeStrongboxPreparedOpenV1>
            prepared =
                new List<AuthoritativeStrongboxPreparedOpenV1>();

        private AuthoritativeStrongboxSimulatorRuntimeV1 runtime;
        private StrongboxOpeningResultRuntimeV1 currentResult;
        private Page page;
        private int currentIndex;
        private int playerLevel = 30;
        private string seedText = "123456";
        private string loadedCatalogJson;
        private Vector2 scroll;
        private bool authorityDiagnosticsExpanded;
        private bool cardDiagnosticsExpanded;
        private string diagnostic =
            "Load weapon_baseline_v01.json, click boxes into the queue, then open them through the real authorities.";

        [MenuItem("Tools/Shooter Mover/Authoritative Strongbox Wiring")]
        public static void Open()
        {
            GetWindow<AuthoritativeStrongboxSimulatorWindow>(
                "Strongbox Wiring");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Authoritative Strongbox Wiring",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Exact owned box -> BOX -> GEN -> RAP -> MON/SCR/INV -> exact box consumption. Fresh equipment is empty; the compact card resolves real weapon statistics through the equipment definition.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Keep/Sell remains only in the separate decision-preview opener until an exactly-once equipment disposition authority exists.",
                MessageType.Warning);

            DrawCatalogControls();
            using (new EditorGUI.DisabledScope(runtime == null))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                if (page == Page.Queue)
                {
                    DrawQueue();
                }
                else if (page == Page.Opening)
                {
                    DrawOpening();
                }
                else
                {
                    DrawComplete();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(diagnostic, MessageType.None);
        }

        private void DrawCatalogControls()
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
                EditorGUILayout.IntField(
                    "Player Level",
                    playerLevel));
            seedText = EditorGUILayout.TextField("Seed", seedText);
            EditorGUILayout.EndHorizontal();

            if (runtime != null)
            {
                EditorGUILayout.LabelField(
                    "Live weapons: "
                    + runtime.WeaponCatalog.GetDefinitions(
                        ShooterMover.Domain.Weapons.Catalog
                            .WeaponCatalogContentFilter.LiveOnly).Count
                    + " | Money: "
                    + runtime.MoneyBalance.ToString(
                        CultureInfo.InvariantCulture)
                    + " | Scrap: "
                    + runtime.ScrapBalance.ToString(
                        CultureInfo.InvariantCulture)
                    + " | BOX sequence: "
                    + runtime.OpeningSequence.ToString(
                        CultureInfo.InvariantCulture));
            }
        }

        private void DrawQueue()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Click a box to add one exact entry",
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
                            "[BOX] "
                            + tier.TierNumber
                            + "\n"
                            + tier.DisplayName,
                            GUILayout.MinHeight(52f)))
                    {
                        queue.Add(tier.TierNumber);
                        diagnostic = tier.DisplayName
                            + " added at queue position "
                            + queue.Count
                            + ".";
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
                    ProductionStrongboxCatalogV1.GetByNumber(
                        queue[index]);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    (index + 1).ToString(
                        "D3",
                        CultureInfo.InvariantCulture)
                    + ". Tier "
                    + tier.TierNumber
                    + " — "
                    + tier.DisplayName);
                if (GUILayout.Button(
                        "Remove",
                        GUILayout.Width(80f)))
                {
                    queue.RemoveAt(index);
                    index--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(queue.Count == 0))
            {
                if (GUILayout.Button(
                        "Prepare Exact Boxes And Start Opening"))
                {
                    BeginOpening();
                }
            }
            if (GUILayout.Button(
                    "Clear Queue",
                    GUILayout.Width(120f)))
            {
                queue.Clear();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOpening()
        {
            if (prepared.Count == 0
                || currentIndex >= prepared.Count)
            {
                page = Page.Complete;
                return;
            }

            AuthoritativeStrongboxPreparedOpenV1 box =
                prepared[currentIndex];
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Box "
                + (currentIndex + 1).ToString(
                    CultureInfo.InvariantCulture)
                + " / "
                + prepared.Count.ToString(
                    CultureInfo.InvariantCulture)
                + " — "
                + box.Tier.DisplayName,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Owned before/after open",
                runtime.IsBoxOwned(box)
                    ? "YES"
                    : "NO — exact instance consumed");

            authorityDiagnosticsExpanded =
                EditorGUILayout.Foldout(
                    authorityDiagnosticsExpanded,
                    "Authority diagnostics / identity",
                    true);
            if (authorityDiagnosticsExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(
                    "Exact instance ID",
                    box.Context.InstanceStableId.ToString());
                EditorGUILayout.LabelField(
                    "Production tier ID",
                    box.Tier.TierStableId.ToString());
                EditorGUILayout.LabelField(
                    "Authority binding tier ID",
                    box.Context.TierStableId.ToString());
                EditorGUILayout.LabelField(
                    "Committed weapon definition",
                    box.CommittedSourceDefinitionId);
                EditorGUILayout.LabelField(
                    "Root seed",
                    box.Context.RootSeed.ToString(
                        CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField(
                    "Context fingerprint",
                    box.Context.Fingerprint);
                EditorGUILayout.LabelField(
                    "Opening ID",
                    box.Command.OpeningStableId.ToString());
                EditorGUILayout.LabelField(
                    "Prepared fingerprint",
                    box.Fingerprint);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            bool pending = IsPending(currentResult);
            if (currentResult == null)
            {
                if (GUILayout.Button(
                        "OPEN THROUGH REAL BOX AUTHORITIES",
                        GUILayout.MinHeight(46f)))
                {
                    OpenCurrent(box);
                }
            }
            else if (pending)
            {
                EditorGUILayout.HelpBox(
                    "The transaction is pending. Retry submits the exact same immutable opening command.",
                    MessageType.Warning);
                if (GUILayout.Button(
                        "RETRY SAME OPENING COMMAND",
                        GUILayout.MinHeight(42f)))
                {
                    OpenCurrent(box);
                }
            }

            if (currentResult != null)
            {
                DrawResult(currentResult);
                if (!pending
                    && GUILayout.Button(
                        "Continue To Next Exact Box"))
                {
                    currentIndex++;
                    currentResult = null;
                    authorityDiagnosticsExpanded = false;
                    cardDiagnosticsExpanded = false;
                    if (currentIndex >= prepared.Count)
                    {
                        page = Page.Complete;
                    }
                }
            }
        }

        private void DrawResult(
            StrongboxOpeningResultRuntimeV1 result)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Authority result",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Status",
                result.Status.ToString());
            EditorGUILayout.LabelField(
                "Opening sequence",
                result.PreviousSequence.ToString(
                    CultureInfo.InvariantCulture)
                + " -> "
                + result.CurrentSequence.ToString(
                    CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(result.RejectionCode))
            {
                EditorGUILayout.LabelField(
                    "Rejection / pending code",
                    result.RejectionCode);
            }
            EditorGUILayout.LabelField(
                "Money balance",
                runtime.MoneyBalance.ToString(
                    CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Scrap balance",
                runtime.ScrapBalance.ToString(
                    CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Holdings sequence",
                runtime.HoldingsSequence.ToString(
                    CultureInfo.InvariantCulture));

            if (result.GeneratedOutcome == null)
            {
                EditorGUILayout.LabelField(
                    "No frozen generated outcome.");
                return;
            }

            IReadOnlyList<EquipmentInstance> equipment =
                runtime.EquipmentFrom(result);
            if (equipment.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "No equipment result.");
            }
            for (int index = 0; index < equipment.Count; index++)
            {
                EquipmentInstance item = equipment[index];
                if (item.Augments.Count != 0)
                {
                    EditorGUILayout.HelpBox(
                        "Authoritative result rejected for display: fresh equipment contains installed augments.",
                        MessageType.Error);
                    continue;
                }
                WeaponLootCardEditorDrawerV1.Draw(
                    item,
                    runtime.EquipmentCatalog,
                    runtime.WeaponCatalog,
                    ref cardDiagnosticsExpanded);
            }

            authorityDiagnosticsExpanded =
                EditorGUILayout.Foldout(
                    authorityDiagnosticsExpanded,
                    "Generated payload diagnostics",
                    true);
            if (!authorityDiagnosticsExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                "Generated outcome fingerprint",
                result.GeneratedOutcome.Fingerprint);
            for (int payloadIndex = 0;
                payloadIndex
                    < result.GeneratedOutcome.Payloads.Count;
                payloadIndex++)
            {
                RewardGrantApplicationPayloadV1 payload =
                    result.GeneratedOutcome.Payloads[payloadIndex];
                EditorGUILayout.LabelField(
                    payload.Grant.Kind
                    + " — "
                    + payload.Grant.ContentStableId,
                    payload.Grant.Quantity.ToString(
                        CultureInfo.InvariantCulture));
                for (int instanceIndex = 0;
                    instanceIndex
                        < payload.InstanceStableIds.Count;
                    instanceIndex++)
                {
                    EditorGUILayout.LabelField(
                        "Instance "
                        + (instanceIndex + 1).ToString(
                            CultureInfo.InvariantCulture),
                        payload.InstanceStableIds[
                            instanceIndex].ToString());
                }
            }
            EditorGUI.indentLevel--;
        }

        private void DrawComplete()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Authoritative opening complete",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Boxes opened",
                prepared.Count.ToString(
                    CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Money balance",
                runtime.MoneyBalance.ToString(
                    CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Scrap balance",
                runtime.ScrapBalance.ToString(
                    CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "BOX sequence",
                runtime.OpeningSequence.ToString(
                    CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Holdings sequence",
                runtime.HoldingsSequence.ToString(
                    CultureInfo.InvariantCulture));
            if (GUILayout.Button("Return To Queue"))
            {
                frozenQueue.Clear();
                prepared.Clear();
                currentIndex = 0;
                currentResult = null;
                authorityDiagnosticsExpanded = false;
                cardDiagnosticsExpanded = false;
                page = Page.Queue;
            }
        }

        private void LoadCatalog(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                AuthoritativeStrongboxSimulatorRuntimeV1 created;
                string error;
                if (!AuthoritativeStrongboxSimulatorRuntimeV1
                    .TryCreate(
                        json,
                        out created,
                        out error))
                {
                    runtime = null;
                    loadedCatalogJson = null;
                    diagnostic = error;
                    return;
                }

                loadedCatalogJson = json;
                runtime = created;
                queue.Clear();
                frozenQueue.Clear();
                prepared.Clear();
                currentIndex = 0;
                currentResult = null;
                authorityDiagnosticsExpanded = false;
                cardDiagnosticsExpanded = false;
                page = Page.Queue;
                diagnostic = "Loaded "
                    + Path.GetFileName(path)
                    + " with weapon-catalog fingerprint "
                    + runtime.WeaponCatalog.Fingerprint
                    + ".";
            }
            catch (Exception exception)
            {
                runtime = null;
                loadedCatalogJson = null;
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
            if (string.IsNullOrEmpty(loadedCatalogJson))
            {
                diagnostic =
                    "Reload weapon_baseline_v01.json before preparing the batch.";
                return;
            }

            try
            {
                AuthoritativeStrongboxSimulatorRuntimeV1 fresh;
                string error;
                if (!AuthoritativeStrongboxSimulatorRuntimeV1
                    .TryCreate(
                        loadedCatalogJson,
                        out fresh,
                        out error))
                {
                    diagnostic = error;
                    return;
                }
                runtime = fresh;
                frozenQueue.Clear();
                frozenQueue.AddRange(queue);
                prepared.Clear();
                prepared.AddRange(runtime.PrepareBatch(
                    frozenQueue,
                    playerLevel,
                    seed));

                currentIndex = 0;
                currentResult = null;
                authorityDiagnosticsExpanded = false;
                cardDiagnosticsExpanded = false;
                page = Page.Opening;
                diagnostic = prepared.Count.ToString(
                    CultureInfo.InvariantCulture)
                    + " exact owned boxes committed, prepared, and registered with one BOX authority in insertion order.";
            }
            catch (Exception exception)
            {
                prepared.Clear();
                diagnostic = exception.ToString();
            }
        }

        private void OpenCurrent(
            AuthoritativeStrongboxPreparedOpenV1 box)
        {
            try
            {
                currentResult = runtime.OpenOrRetry(box);
                IReadOnlyList<EquipmentInstance> equipment =
                    runtime.EquipmentFrom(currentResult);
                for (int index = 0; index < equipment.Count; index++)
                {
                    if (equipment[index].Augments.Count != 0)
                    {
                        diagnostic =
                            "BOX returned equipment with installed augments; the result is blocked from player-facing projection.";
                        return;
                    }
                }
                diagnostic = "BOX returned "
                    + currentResult.Status
                    + " for exact instance "
                    + box.Context.InstanceStableId
                    + ".";
            }
            catch (Exception exception)
            {
                diagnostic = exception.ToString();
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
                diagnostic =
                    "Seed must be an unsigned 64-bit integer.";
                return false;
            }
            return true;
        }

        private static bool IsPending(
            StrongboxOpeningResultRuntimeV1 result)
        {
            return result != null
                && (result.Status
                        == StrongboxOpeningRuntimeStatusV1
                            .ClaimedPendingApplication
                    || result.Status
                        == StrongboxOpeningRuntimeStatusV1
                            .ConsumePending);
        }
    }
}
