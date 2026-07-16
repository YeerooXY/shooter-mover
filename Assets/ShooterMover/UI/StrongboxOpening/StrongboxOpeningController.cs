using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.StrongboxOpening
{
    public enum StrongboxRevealStageV1
    {
        BoxClosed = 1,
        OpeningAnimation = 2,
        RewardReveal = 3,
        ContinueOrBack = 4,
    }

    public enum StrongboxRewardPresentationKindV1
    {
        Equipment = 1,
        Armor = 2,
        Money = 3,
        Scrap = 4,
        Miscellaneous = 5,
    }

    public sealed class StrongboxOpeningPreviewConfigurationV1 : IEquatable<StrongboxOpeningPreviewConfigurationV1>
    {
        private readonly string canonicalText;

        public StrongboxOpeningPreviewConfigurationV1(
            string tierStableId,
            string tierLabel,
            ulong deterministicSeed,
            float openingDurationSeconds,
            float revealIntervalSeconds,
            float revealCompleteHoldSeconds)
        {
            if (string.IsNullOrWhiteSpace(tierStableId))
            {
                throw new ArgumentException("Tier identity is required.", nameof(tierStableId));
            }
            if (string.IsNullOrWhiteSpace(tierLabel))
            {
                throw new ArgumentException("Tier label is required.", nameof(tierLabel));
            }
            if (openingDurationSeconds <= 0f || float.IsNaN(openingDurationSeconds) || float.IsInfinity(openingDurationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(openingDurationSeconds));
            }
            if (revealIntervalSeconds <= 0f || float.IsNaN(revealIntervalSeconds) || float.IsInfinity(revealIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(revealIntervalSeconds));
            }
            if (revealCompleteHoldSeconds < 0f || float.IsNaN(revealCompleteHoldSeconds) || float.IsInfinity(revealCompleteHoldSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(revealCompleteHoldSeconds));
            }

            TierStableId = tierStableId.Trim();
            TierLabel = tierLabel.Trim();
            DeterministicSeed = deterministicSeed;
            OpeningDurationSeconds = openingDurationSeconds;
            RevealIntervalSeconds = revealIntervalSeconds;
            RevealCompleteHoldSeconds = revealCompleteHoldSeconds;
            canonicalText = "tier=" + TierStableId
                + "\nlabel=" + TierLabel
                + "\nseed=" + DeterministicSeed.ToString(CultureInfo.InvariantCulture)
                + "\nopening_ms=" + Mathf.RoundToInt(OpeningDurationSeconds * 1000f).ToString(CultureInfo.InvariantCulture)
                + "\nreveal_ms=" + Mathf.RoundToInt(RevealIntervalSeconds * 1000f).ToString(CultureInfo.InvariantCulture)
                + "\nhold_ms=" + Mathf.RoundToInt(RevealCompleteHoldSeconds * 1000f).ToString(CultureInfo.InvariantCulture);
        }

        public string TierStableId { get; }
        public string TierLabel { get; }
        public ulong DeterministicSeed { get; }
        public float OpeningDurationSeconds { get; }
        public float RevealIntervalSeconds { get; }
        public float RevealCompleteHoldSeconds { get; }
        public string ToCanonicalString() { return canonicalText; }

        public bool Equals(StrongboxOpeningPreviewConfigurationV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as StrongboxOpeningPreviewConfigurationV1); }
        public override int GetHashCode() { return StringComparer.Ordinal.GetHashCode(canonicalText); }
    }

    public sealed class StrongboxRewardRevealItemV1
    {
        public StrongboxRewardRevealItemV1(
            StrongboxRewardPresentationKindV1 kind,
            string title,
            string contentStableId,
            string instanceStableId,
            long quantity,
            string detail)
        {
            if (!Enum.IsDefined(typeof(StrongboxRewardPresentationKindV1), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Reward title is required.", nameof(title));
            }
            if (quantity < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            Kind = kind;
            Title = title.Trim();
            ContentStableId = contentStableId ?? string.Empty;
            InstanceStableId = instanceStableId ?? string.Empty;
            Quantity = quantity;
            Detail = detail ?? string.Empty;
        }

        public StrongboxRewardPresentationKindV1 Kind { get; }
        public string Title { get; }
        public string ContentStableId { get; }
        public string InstanceStableId { get; }
        public long Quantity { get; }
        public string Detail { get; }
        public bool IsUniqueInstance { get { return InstanceStableId.Length > 0; } }
    }

    public sealed class StrongboxOpeningPresentationResultV1
    {
        private readonly ReadOnlyCollection<StrongboxRewardRevealItemV1> items;

        private StrongboxOpeningPresentationResultV1(
            bool succeeded,
            bool pending,
            bool replay,
            bool previewOnly,
            string statusText,
            string rejectionCode,
            IEnumerable<StrongboxRewardRevealItemV1> items)
        {
            Succeeded = succeeded;
            Pending = pending;
            WasExactReplay = replay;
            PreviewOnly = previewOnly;
            StatusText = statusText ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
            this.items = new ReadOnlyCollection<StrongboxRewardRevealItemV1>(
                new List<StrongboxRewardRevealItemV1>(items ?? Array.Empty<StrongboxRewardRevealItemV1>()));
        }

        public bool Succeeded { get; }
        public bool Pending { get; }
        public bool WasExactReplay { get; }
        public bool PreviewOnly { get; }
        public string StatusText { get; }
        public string RejectionCode { get; }
        public IReadOnlyList<StrongboxRewardRevealItemV1> Items { get { return items; } }

        public static StrongboxOpeningPresentationResultV1 Success(
            IEnumerable<StrongboxRewardRevealItemV1> items,
            bool replay,
            bool previewOnly,
            string statusText)
        {
            return new StrongboxOpeningPresentationResultV1(
                true, false, replay, previewOnly, statusText, null, items);
        }

        public static StrongboxOpeningPresentationResultV1 PendingResult(string statusText, string rejectionCode)
        {
            return new StrongboxOpeningPresentationResultV1(
                false, true, false, false, statusText, rejectionCode, Array.Empty<StrongboxRewardRevealItemV1>());
        }

        public static StrongboxOpeningPresentationResultV1 Rejected(string statusText, string rejectionCode)
        {
            return new StrongboxOpeningPresentationResultV1(
                false, false, false, false, statusText, rejectionCode, Array.Empty<StrongboxRewardRevealItemV1>());
        }
    }

    /// <summary>
    /// Presentation-side adapter for one immutable BOX open command. Pending BOX/RAP
    /// work may be retried with the same command; terminal outcomes are cached so UI
    /// callbacks cannot submit a second opening or mint another reward.
    /// </summary>
    public sealed class StrongboxOpeningRuntimePortV1
    {
        private readonly Func<StrongboxOpeningResultRuntimeV1> execute;
        private StrongboxOpeningResultRuntimeV1 lastResult;
        private bool terminal;

        public StrongboxOpeningRuntimePortV1(
            StrongboxOpeningServiceV1 service,
            StrongboxOpenCommandV1 command)
            : this(delegate
            {
                if (service == null) { throw new ArgumentNullException(nameof(service)); }
                if (command == null) { throw new ArgumentNullException(nameof(command)); }
                return service.Open(command);
            })
        {
        }

        public StrongboxOpeningRuntimePortV1(Func<StrongboxOpeningResultRuntimeV1> execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public int AuthorityInvocationCount { get; private set; }
        public StrongboxOpeningResultRuntimeV1 LastResult { get { return lastResult; } }
        public bool IsTerminal { get { return terminal; } }

        public StrongboxOpeningResultRuntimeV1 OpenOrContinue()
        {
            if (terminal)
            {
                return lastResult;
            }

            AuthorityInvocationCount++;
            lastResult = execute();
            terminal = lastResult == null || !IsPendingStatus(lastResult.Status);
            return lastResult;
        }

        private static bool IsPendingStatus(StrongboxOpeningRuntimeStatusV1 status)
        {
            return status == StrongboxOpeningRuntimeStatusV1.ClaimedPendingApplication
                || status == StrongboxOpeningRuntimeStatusV1.ConsumePending;
        }
    }

    public static class StrongboxRewardRevealProjectorV1
    {
        public static StrongboxOpeningPresentationResultV1 Project(
            StrongboxOpeningResultRuntimeV1 result,
            EquipmentCatalog equipmentCatalog)
        {
            if (result == null)
            {
                return StrongboxOpeningPresentationResultV1.Rejected(
                    "OPENING RESULT UNAVAILABLE",
                    "opening-result-null");
            }

            if (result.Status == StrongboxOpeningRuntimeStatusV1.ClaimedPendingApplication
                || result.Status == StrongboxOpeningRuntimeStatusV1.ConsumePending)
            {
                return StrongboxOpeningPresentationResultV1.PendingResult(
                    "OPENING PENDING — RETRY SAME TRANSACTION",
                    result.RejectionCode);
            }

            bool success = result.Status == StrongboxOpeningRuntimeStatusV1.Opened
                || result.Status == StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange;
            if (!success || result.GeneratedOutcome == null)
            {
                return StrongboxOpeningPresentationResultV1.Rejected(
                    "OPENING REJECTED: " + result.Status,
                    result.RejectionCode);
            }

            IReadOnlyList<StrongboxRewardRevealItemV1> items = ProjectPayloads(
                result.GeneratedOutcome.Payloads,
                equipmentCatalog);
            return StrongboxOpeningPresentationResultV1.Success(
                items,
                result.Status == StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange,
                false,
                result.Status == StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange
                    ? "ORIGINAL OPENING REPLAYED — NO ADDITIONAL VALUE"
                    : "STRONGBOX OPENED");
        }

        public static IReadOnlyList<StrongboxRewardRevealItemV1> ProjectPayloads(
            IEnumerable<RewardGrantApplicationPayloadV1> payloads,
            EquipmentCatalog equipmentCatalog)
        {
            if (payloads == null)
            {
                throw new ArgumentNullException(nameof(payloads));
            }

            List<StrongboxRewardRevealItemV1> items = new List<StrongboxRewardRevealItemV1>();
            foreach (RewardGrantApplicationPayloadV1 payload in payloads)
            {
                if (payload == null)
                {
                    throw new ArgumentException("Payloads must not contain null entries.", nameof(payloads));
                }

                switch (payload.Grant.Kind)
                {
                    case RewardGrantKindV1.Money:
                        items.Add(ValueItem(StrongboxRewardPresentationKindV1.Money, "MONEY", payload));
                        break;
                    case RewardGrantKindV1.Scrap:
                        items.Add(ValueItem(StrongboxRewardPresentationKindV1.Scrap, "SCRAP", payload));
                        break;
                    case RewardGrantKindV1.EquipmentReference:
                        AddEquipment(items, payload, equipmentCatalog);
                        break;
                    case RewardGrantKindV1.PremiumAmmo:
                        items.Add(ValueItem(StrongboxRewardPresentationKindV1.Miscellaneous, "PREMIUM AMMUNITION", payload));
                        break;
                    case RewardGrantKindV1.Miscellaneous:
                        items.Add(ValueItem(StrongboxRewardPresentationKindV1.Miscellaneous, "MISCELLANEOUS", payload));
                        break;
                    case RewardGrantKindV1.Strongbox:
                        for (int index = 0; index < payload.InstanceStableIds.Count; index++)
                        {
                            items.Add(new StrongboxRewardRevealItemV1(
                                StrongboxRewardPresentationKindV1.Miscellaneous,
                                "STRONGBOX",
                                payload.Grant.ContentStableId.ToString(),
                                payload.InstanceStableIds[index].ToString(),
                                1L,
                                "Separate owned strongbox instance"));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(payload.Grant.Kind), payload.Grant.Kind, "Unsupported reward kind.");
                }
            }

            return new ReadOnlyCollection<StrongboxRewardRevealItemV1>(items);
        }

        private static StrongboxRewardRevealItemV1 ValueItem(
            StrongboxRewardPresentationKindV1 kind,
            string title,
            RewardGrantApplicationPayloadV1 payload)
        {
            return new StrongboxRewardRevealItemV1(
                kind,
                title,
                payload.Grant.ContentStableId.ToString(),
                null,
                payload.Grant.Quantity,
                payload.Grant.ContentStableId.ToString());
        }

        private static void AddEquipment(
            ICollection<StrongboxRewardRevealItemV1> items,
            RewardGrantApplicationPayloadV1 payload,
            EquipmentCatalog equipmentCatalog)
        {
            for (int index = 0; index < payload.EquipmentInstances.Count; index++)
            {
                EquipmentInstance instance = payload.EquipmentInstances[index];
                EquipmentDefinition definition = equipmentCatalog == null
                    ? null
                    : equipmentCatalog.FindEquipmentDefinition(instance.DefinitionId);
                bool armor = definition != null && definition.CategoryId == EquipmentCategoryIds.Armor;
                string title = definition == null ? instance.DefinitionId.ToString() : definition.DisplayName;
                string detail = "Item level " + instance.ItemLevel.ToString(CultureInfo.InvariantCulture)
                    + "  |  Quality " + instance.QualityId
                    + "  |  Augments " + instance.Augments.Count.ToString(CultureInfo.InvariantCulture);
                items.Add(new StrongboxRewardRevealItemV1(
                    armor ? StrongboxRewardPresentationKindV1.Armor : StrongboxRewardPresentationKindV1.Equipment,
                    title,
                    instance.DefinitionId.ToString(),
                    instance.InstanceId.ToString(),
                    1L,
                    detail));
            }
        }
    }

    public sealed class StrongboxOpeningSceneSessionV1
    {
        private readonly Func<StrongboxOpeningPresentationResultV1> openOrContinue;
        private float stageElapsed;
        private bool openRequested;

        public StrongboxOpeningSceneSessionV1(
            StrongboxOpeningPreviewConfigurationV1 configuration,
            Func<StrongboxOpeningPresentationResultV1> openOrContinue)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.openOrContinue = openOrContinue ?? throw new ArgumentNullException(nameof(openOrContinue));
            Stage = StrongboxRevealStageV1.BoxClosed;
        }

        public StrongboxOpeningPreviewConfigurationV1 Configuration { get; }
        public StrongboxRevealStageV1 Stage { get; private set; }
        public StrongboxOpeningPresentationResultV1 Result { get; private set; }
        public int VisibleRewardCount { get; private set; }
        public bool ContinueRequested { get; private set; }
        public bool OpenRequested { get { return openRequested; } }
        public float OpeningProgress
        {
            get { return Stage == StrongboxRevealStageV1.OpeningAnimation
                ? Mathf.Clamp01(stageElapsed / Configuration.OpeningDurationSeconds)
                : Stage > StrongboxRevealStageV1.OpeningAnimation ? 1f : 0f; }
        }

        public bool RequestOpen()
        {
            if (openRequested || Stage != StrongboxRevealStageV1.BoxClosed)
            {
                return false;
            }

            openRequested = true;
            Stage = StrongboxRevealStageV1.OpeningAnimation;
            stageElapsed = 0f;
            Result = openOrContinue();
            return true;
        }

        public bool RetryPending()
        {
            if (!openRequested || Result == null || !Result.Pending)
            {
                return false;
            }

            Result = openOrContinue();
            stageElapsed = 0f;
            return true;
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime < 0f || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }
            if (Stage == StrongboxRevealStageV1.BoxClosed
                || Stage == StrongboxRevealStageV1.ContinueOrBack
                || Result == null
                || Result.Pending)
            {
                return;
            }

            stageElapsed += unscaledDeltaTime;
            if (Stage == StrongboxRevealStageV1.OpeningAnimation)
            {
                if (stageElapsed < Configuration.OpeningDurationSeconds)
                {
                    return;
                }

                stageElapsed = 0f;
                if (!Result.Succeeded || Result.Items.Count == 0)
                {
                    Stage = StrongboxRevealStageV1.ContinueOrBack;
                    return;
                }

                Stage = StrongboxRevealStageV1.RewardReveal;
                VisibleRewardCount = 1;
                return;
            }

            if (Stage == StrongboxRevealStageV1.RewardReveal)
            {
                int desired = 1 + Mathf.FloorToInt(stageElapsed / Configuration.RevealIntervalSeconds);
                VisibleRewardCount = Mathf.Min(Result.Items.Count, desired);
                float completeAt = Mathf.Max(0, Result.Items.Count - 1) * Configuration.RevealIntervalSeconds
                    + Configuration.RevealCompleteHoldSeconds;
                if (stageElapsed >= completeAt)
                {
                    Stage = StrongboxRevealStageV1.ContinueOrBack;
                    VisibleRewardCount = Result.Items.Count;
                }
            }
        }

        public bool RequestContinue()
        {
            if (Stage != StrongboxRevealStageV1.ContinueOrBack || ContinueRequested)
            {
                return false;
            }

            ContinueRequested = true;
            return true;
        }
    }

    /// <summary>
    /// Standalone IMGUI presentation for one strongbox opening. The component never
    /// grants currency, adds inventory, consumes a box, or applies rewards directly;
    /// a bound StrongboxOpeningServiceV1 remains the BOX/RAP/INV/SCR authority path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StrongboxOpeningController : MonoBehaviour
    {
        private const float PanelWidth = 940f;
        private const float PanelHeight = 720f;

        [Header("Deterministic preview")]
        [SerializeField] private string tierStableId = "strongbox-tier.preview";
        [SerializeField] private string tierLabel = "REFERENCE STRONGBOX";
        [SerializeField] private long deterministicTestSeed = 424242L;
        [SerializeField] private bool usePreviewWhenUnbound = true;
        [SerializeField] private bool autoOpen;

        [Header("Reveal timing")]
        [SerializeField] private float openingDurationSeconds = 1.25f;
        [SerializeField] private float revealIntervalSeconds = 0.35f;
        [SerializeField] private float revealCompleteHoldSeconds = 0.7f;

        [Header("Navigation")]
        [SerializeField] private string backScenePath = string.Empty;

        private StrongboxOpeningSceneSessionV1 session;
        private StrongboxOpeningRuntimePortV1 runtimePort;
        private EquipmentCatalog equipmentCatalog;
        private bool previewOnly;
        private Vector2 scroll;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle rewardStyle;
        private GUIStyle warningStyle;

        public event Action ContinueOrBackRequested;

        public StrongboxOpeningSceneSessionV1 Session
        {
            get
            {
                EnsureInitialized();
                return session;
            }
        }

        public StrongboxOpeningRuntimePortV1 RuntimePort { get { return runtimePort; } }
        public bool IsPreviewOnly { get { return previewOnly; } }

        private void Awake()
        {
            EnsureInitialized();
            if (autoOpen)
            {
                RequestOpen();
            }
        }

        private void Update()
        {
            EnsureInitialized();
            session.Advance(Time.unscaledDeltaTime);

            bool confirm = (Keyboard.current != null
                    && (Keyboard.current.enterKey.wasPressedThisFrame
                        || Keyboard.current.spaceKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            bool back = (Keyboard.current != null
                    && (Keyboard.current.escapeKey.wasPressedThisFrame
                        || Keyboard.current.backspaceKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (confirm)
            {
                if (session.Stage == StrongboxRevealStageV1.BoxClosed)
                {
                    RequestOpen();
                }
                else if (session.Result != null && session.Result.Pending)
                {
                    RetryPendingOpening();
                }
                else if (session.Stage == StrongboxRevealStageV1.ContinueOrBack)
                {
                    RequestContinueOrBack();
                }
            }
            else if (back && session.Stage == StrongboxRevealStageV1.ContinueOrBack)
            {
                RequestContinueOrBack();
            }
        }

        private void OnGUI()
        {
            EnsureInitialized();
            EnsureStyles();

            float width = Mathf.Min(PanelWidth, Mathf.Max(480f, Screen.width - 32f));
            float height = Mathf.Min(PanelHeight, Mathf.Max(420f, Screen.height - 32f));
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(panel, GUI.skin.window);
            GUILayout.Label("STRONGBOX OPENING", titleStyle);
            GUILayout.Label(session.Configuration.TierLabel, headingStyle);
            GUILayout.Label(
                "Tier: " + session.Configuration.TierStableId
                + "    Deterministic seed: " + session.Configuration.DeterministicSeed.ToString(CultureInfo.InvariantCulture),
                bodyStyle);
            GUILayout.Label("Stage " + ((int)session.Stage).ToString(CultureInfo.InvariantCulture) + "/4 — " + session.Stage, bodyStyle);
            if (previewOnly)
            {
                GUILayout.Label("PREVIEW ONLY — no BOX/RAP/INV/SCR mutation is performed until a runtime is bound.", warningStyle);
            }
            GUILayout.Space(12f);

            DrawBoxPresentation();
            GUILayout.Space(10f);
            DrawResult();
            GUILayout.FlexibleSpace();
            DrawActions();
            GUILayout.EndArea();
        }

        public void BindRuntime(
            StrongboxOpeningServiceV1 service,
            StrongboxOpenCommandV1 command,
            EquipmentCatalog catalog)
        {
            runtimePort = new StrongboxOpeningRuntimePortV1(
                service ?? throw new ArgumentNullException(nameof(service)),
                command ?? throw new ArgumentNullException(nameof(command)));
            equipmentCatalog = catalog;
            previewOnly = false;
            session = new StrongboxOpeningSceneSessionV1(
                BuildConfiguration(),
                delegate { return StrongboxRewardRevealProjectorV1.Project(runtimePort.OpenOrContinue(), equipmentCatalog); });
        }

        public void ConfigureForTests(
            StrongboxOpeningPreviewConfigurationV1 configuration,
            Func<StrongboxOpeningPresentationResultV1> openOrContinue,
            bool isPreviewOnly = false)
        {
            runtimePort = null;
            equipmentCatalog = null;
            previewOnly = isPreviewOnly;
            session = new StrongboxOpeningSceneSessionV1(
                configuration ?? throw new ArgumentNullException(nameof(configuration)),
                openOrContinue ?? throw new ArgumentNullException(nameof(openOrContinue)));
        }

        public bool RequestOpen()
        {
            EnsureInitialized();
            return session.RequestOpen();
        }

        public bool RetryPendingOpening()
        {
            EnsureInitialized();
            return session.RetryPending();
        }

        public bool RequestContinueOrBack()
        {
            EnsureInitialized();
            if (!session.RequestContinue())
            {
                return false;
            }

            Action handler = ContinueOrBackRequested;
            if (handler != null)
            {
                handler();
            }
            if (!string.IsNullOrWhiteSpace(backScenePath))
            {
                SceneManager.LoadScene(backScenePath);
            }
            return true;
        }

        public void AdvanceForTests(float unscaledDeltaTime)
        {
            EnsureInitialized();
            session.Advance(unscaledDeltaTime);
        }

        private void EnsureInitialized()
        {
            if (session != null)
            {
                return;
            }

            previewOnly = true;
            StrongboxOpeningPreviewConfigurationV1 configuration = BuildConfiguration();
            session = new StrongboxOpeningSceneSessionV1(
                configuration,
                usePreviewWhenUnbound
                    ? (Func<StrongboxOpeningPresentationResultV1>)delegate { return BuildDeterministicPreview(configuration); }
                    : delegate
                    {
                        return StrongboxOpeningPresentationResultV1.Rejected(
                            "RUNTIME NOT BOUND",
                            "strongbox-opening-runtime-not-bound");
                    });
        }

        private StrongboxOpeningPreviewConfigurationV1 BuildConfiguration()
        {
            ulong seed = deterministicTestSeed < 0L ? 0UL : (ulong)deterministicTestSeed;
            return new StrongboxOpeningPreviewConfigurationV1(
                tierStableId,
                tierLabel,
                seed,
                Mathf.Max(0.05f, openingDurationSeconds),
                Mathf.Max(0.05f, revealIntervalSeconds),
                Mathf.Max(0f, revealCompleteHoldSeconds));
        }

        private static StrongboxOpeningPresentationResultV1 BuildDeterministicPreview(
            StrongboxOpeningPreviewConfigurationV1 configuration)
        {
            string suffix = configuration.DeterministicSeed.ToString("x8", CultureInfo.InvariantCulture);
            return StrongboxOpeningPresentationResultV1.Success(
                new[]
                {
                    new StrongboxRewardRevealItemV1(StrongboxRewardPresentationKindV1.Equipment, "Blaster Rifle", "equipment.preview-rifle", "equipment-instance." + suffix + "a", 1L, "Duplicate definitions remain separate instances"),
                    new StrongboxRewardRevealItemV1(StrongboxRewardPresentationKindV1.Equipment, "Blaster Rifle", "equipment.preview-rifle", "equipment-instance." + suffix + "b", 1L, "Same definition, different immutable identity"),
                    new StrongboxRewardRevealItemV1(StrongboxRewardPresentationKindV1.Armor, "Field Armor", "equipment.preview-armor", "equipment-instance." + suffix + "c", 1L, "Armor presentation path"),
                    new StrongboxRewardRevealItemV1(StrongboxRewardPresentationKindV1.Money, "MONEY", "currency.money", null, 275L, "Preview quantity"),
                    new StrongboxRewardRevealItemV1(StrongboxRewardPresentationKindV1.Scrap, "SCRAP", "currency.scrap", null, 48L, "Mandatory strongbox scrap presentation"),
                    new StrongboxRewardRevealItemV1(StrongboxRewardPresentationKindV1.Miscellaneous, "MISCELLANEOUS", "item.preview-token", null, 2L, "Miscellaneous reward presentation"),
                },
                false,
                true,
                "DETERMINISTIC PRESENTATION PREVIEW");
        }

        private void DrawBoxPresentation()
        {
            Rect area = GUILayoutUtility.GetRect(300f, 150f, GUILayout.ExpandWidth(true));
            GUI.Box(area, GUIContent.none);
            Rect inner = new Rect(area.x + 24f, area.y + 20f, area.width - 48f, area.height - 40f);
            if (session.Stage == StrongboxRevealStageV1.BoxClosed)
            {
                GUI.Label(inner, "[ STRONGBOX CLOSED ]\nPress OPEN to submit exactly one opening identity.", headingStyle);
                return;
            }

            float progress = session.OpeningProgress;
            GUI.Label(inner, session.Stage == StrongboxRevealStageV1.OpeningAnimation
                ? "OPENING  " + Mathf.RoundToInt(progress * 100f).ToString(CultureInfo.InvariantCulture) + "%"
                : "[ STRONGBOX OPEN ]", headingStyle);
            Rect bar = new Rect(inner.x + 40f, inner.yMax - 30f, inner.width - 80f, 18f);
            GUI.Box(bar, GUIContent.none);
            GUI.Box(new Rect(bar.x + 2f, bar.y + 2f, Mathf.Max(0f, (bar.width - 4f) * progress), bar.height - 4f), GUIContent.none);
        }

        private void DrawResult()
        {
            StrongboxOpeningPresentationResultV1 result = session.Result;
            if (result == null)
            {
                GUILayout.Label("The box is waiting for an opening request.", bodyStyle);
                return;
            }

            GUILayout.Label(result.StatusText, result.Succeeded ? headingStyle : warningStyle);
            if (result.Pending)
            {
                GUILayout.Label("The same BOX opening command can be retried safely; no new identity is created.", bodyStyle);
                if (!string.IsNullOrEmpty(result.RejectionCode))
                {
                    GUILayout.Label("Detail: " + result.RejectionCode, bodyStyle);
                }
                return;
            }
            if (!result.Succeeded)
            {
                GUILayout.Label("Detail: " + result.RejectionCode, bodyStyle);
                return;
            }

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(310f));
            int count = Mathf.Min(session.VisibleRewardCount, result.Items.Count);
            for (int index = 0; index < count; index++)
            {
                StrongboxRewardRevealItemV1 item = result.Items[index];
                string identity = item.IsUniqueInstance ? "\nInstance: " + item.InstanceStableId : string.Empty;
                GUILayout.Label(
                    item.Kind + "  —  " + item.Title
                    + (item.Quantity == 1L ? string.Empty : "  x" + item.Quantity.ToString(CultureInfo.InvariantCulture))
                    + "\nContent: " + item.ContentStableId
                    + identity
                    + (item.Detail.Length == 0 ? string.Empty : "\n" + item.Detail),
                    rewardStyle);
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
        }

        private void DrawActions()
        {
            if (session.Stage == StrongboxRevealStageV1.BoxClosed)
            {
                if (GUILayout.Button("OPEN STRONGBOX", GUILayout.Height(48f)))
                {
                    RequestOpen();
                }
                return;
            }
            if (session.Result != null && session.Result.Pending)
            {
                if (GUILayout.Button("RETRY SAME OPENING", GUILayout.Height(48f)))
                {
                    RetryPendingOpening();
                }
                return;
            }
            if (session.Stage == StrongboxRevealStageV1.ContinueOrBack)
            {
                if (GUILayout.Button("CONTINUE / BACK", GUILayout.Height(48f)))
                {
                    RequestContinueOrBack();
                }
                GUILayout.Label("Enter / controller South: continue    Escape / controller East: back", bodyStyle);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true,
            };
            rewardStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                wordWrap = true,
                padding = new RectOffset(14, 14, 10, 10),
            };
            warningStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }
    }
}
