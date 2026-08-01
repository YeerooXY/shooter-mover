using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.UI.Common;
using ShooterMover.UnityAdapters.Presentation.Guns;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// uGUI Strongbox presentation. StrongboxMenu remains responsible for the real
    /// opening command, reward application, persistence and route completion. This
    /// component prepares and plays one presentation-only vertical roll around the
    /// already-resolved weapon reward.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class StrongboxScreen : MonoBehaviour
    {
        private enum ScreenState
        {
            Ready = 1,
            Pending = 2,
            Rolling = 3,
            Complete = 4,
            Rejected = 5,
        }

        private static readonly IReadOnlyList<AugmentLine> NoAugments =
            Array.Empty<AugmentLine>();

        [Header("Source")]
        [SerializeField] private StrongboxMenu menu;
        [SerializeField] private StrongboxRoller roller;

        [Header("Header")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text tierText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text previewText;

        [Header("Secondary rewards")]
        [SerializeField] private GameObject secondaryRewardsRoot;
        [SerializeField] private RectTransform secondaryCardsRoot;
        [SerializeField] private ItemCard secondaryCardPrefab;

        [Header("Action")]
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionText;

        [Header("Testing modes")]
        [SerializeField] private Button cinematicButton;
        [SerializeField] private Button fastButton;
        [SerializeField] private Button revealOnlyButton;
        [SerializeField] private Button replayButton;

        private readonly List<ItemCard> secondaryCards = new List<ItemCard>();
        private ScreenState state = ScreenState.Ready;
        private StrongboxOpenMode selectedMode = StrongboxOpenMode.Cinematic;
        private StrongboxRollPlan currentPlan;
        private StrongboxOpeningPresentationResult currentResult;
        private StrongboxRewardRevealItem winningItem;
        private bool wired;

        public StrongboxMenu Menu { get { return menu; } }
        public StrongboxOpenMode SelectedMode { get { return selectedMode; } }

        private void Awake()
        {
            if (!ResolveDependencies())
            {
                return;
            }

            _ = menu.Session;
            menu.enabled = false;
            roller.PhaseChanged += SetStatus;
            RefreshStaticText();
            RefreshControls();
        }

        private void OnEnable()
        {
            WireControls();
            RefreshStaticText();
            RefreshControls();
        }

        private void OnDisable()
        {
            UnwireControls();
        }

        private void OnDestroy()
        {
            if (roller != null)
            {
                roller.PhaseChanged -= SetStatus;
            }
        }

        private void Update()
        {
            if (!ResolveDependencies())
            {
                return;
            }

            bool confirm = Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.spaceKey.wasPressedThisFrame);
            confirm |= Gamepad.current != null
                && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (confirm && state != ScreenState.Rolling)
            {
                SubmitAction();
                return;
            }

            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back
                && (state == ScreenState.Complete
                    || state == ScreenState.Rejected))
            {
                ContinueOrBack();
            }
        }

        public void SelectCinematic()
        {
            SelectMode(StrongboxOpenMode.Cinematic);
        }

        public void SelectFast()
        {
            SelectMode(StrongboxOpenMode.Fast);
        }

        public void SelectRevealOnly()
        {
            SelectMode(StrongboxOpenMode.RevealOnly);
        }

        public void ReplaySameResult()
        {
            if (state == ScreenState.Rolling || currentPlan == null)
            {
                return;
            }

            HideSecondaryRewards();
            state = ScreenState.Rolling;
            RefreshControls();
            roller.Prepare(currentPlan);
            roller.Play(selectedMode, null, null, RollCompleted);
        }

        private bool ResolveDependencies()
        {
            if (menu == null)
            {
                menu = FindFirstObjectByType<StrongboxMenu>();
            }
            if (roller == null)
            {
                roller = GetComponentInChildren<StrongboxRoller>(true);
            }

            if (menu != null && roller != null)
            {
                return true;
            }

            Debug.LogError(
                "StrongboxScreen requires StrongboxMenu and StrongboxRoller.",
                this);
            enabled = false;
            return false;
        }

        private void SubmitAction()
        {
            switch (state)
            {
                case ScreenState.Ready:
                    OpenStrongbox();
                    break;
                case ScreenState.Pending:
                    RetryPending();
                    break;
                case ScreenState.Complete:
                case ScreenState.Rejected:
                    ContinueOrBack();
                    break;
            }
        }

        private void OpenStrongbox()
        {
            if (!menu.RequestOpen())
            {
                return;
            }
            HandleOpeningResult(menu.Session.Result);
        }

        private void RetryPending()
        {
            if (!menu.RetryPendingOpening())
            {
                return;
            }
            HandleOpeningResult(menu.Session.Result);
        }

        private void HandleOpeningResult(
            StrongboxOpeningPresentationResult result)
        {
            currentResult = result;
            if (result == null)
            {
                state = ScreenState.Rejected;
                SetStatus("OPENING RESULT UNAVAILABLE");
                RefreshControls();
                return;
            }
            if (result.Pending)
            {
                state = ScreenState.Pending;
                SetStatus(result.StatusText);
                RefreshControls();
                return;
            }
            if (!result.Succeeded)
            {
                state = ScreenState.Rejected;
                SetStatus(result.StatusText);
                RefreshControls();
                return;
            }

            WeaponCardView winner;
            string presentationIdentity;
            if (!TryBuildWinner(
                    result,
                    out winningItem,
                    out winner,
                    out presentationIdentity))
            {
                state = ScreenState.Complete;
                SetStatus(result.StatusText);
                ShowSecondaryRewards(result, null);
                CompleteLegacySession();
                RefreshControls();
                return;
            }

            currentPlan = StrongboxRollPlanner.Create(
                winner,
                presentationIdentity,
                roller.Settings);
            HideSecondaryRewards();
            state = ScreenState.Rolling;
            RefreshControls();
            roller.Prepare(currentPlan);
            roller.Play(selectedMode, null, null, RollCompleted);
        }

        private void RollCompleted()
        {
            state = ScreenState.Complete;
            SetStatus(currentResult == null
                ? "STRONGBOX OPENED"
                : currentResult.StatusText);
            ShowSecondaryRewards(currentResult, winningItem);
            CompleteLegacySession();
            RefreshControls();
        }

        private void ContinueOrBack()
        {
            CompleteLegacySession();
            menu.RequestContinueOrBack();
        }

        private void CompleteLegacySession()
        {
            StrongboxOpeningSceneSession session = menu.Session;
            if (session.Stage == StrongboxRevealStage.OpeningAnimation)
            {
                session.Advance(
                    session.Configuration.OpeningDurationSeconds + 0.01f);
            }
            if (session.Stage == StrongboxRevealStage.RewardReveal)
            {
                int rewardCount = session.Result == null
                    ? 0
                    : session.Result.Items.Count;
                float revealDuration = Mathf.Max(0, rewardCount - 1)
                    * session.Configuration.RevealIntervalSeconds
                    + session.Configuration.RevealCompleteHoldSeconds
                    + 0.01f;
                session.Advance(revealDuration);
            }
        }

        private bool TryBuildWinner(
            StrongboxOpeningPresentationResult result,
            out StrongboxRewardRevealItem item,
            out WeaponCardView view,
            out string presentationIdentity)
        {
            for (int index = 0; index < result.Items.Count; index++)
            {
                StrongboxRewardRevealItem candidate = result.Items[index];
                if (candidate.Kind != StrongboxRewardPresentationKind.Equipment)
                {
                    continue;
                }

                Sprite art = null;
                if (candidate.HasGunArtReference)
                {
                    art = GunArt.Preload(candidate.GunArtReferenceId).Sprite;
                }

                WeaponRarity rarity;
                string qualityId = ReadTokenAfter(candidate.Detail, "QUALITY ");
                WeaponRarityText.TryParseQualityId(qualityId, out rarity);
                int itemLevel = ReadIntegerAfter(candidate.Detail, "ITEM LEVEL ");

                item = candidate;
                view = new WeaponCardView(
                    candidate.Title,
                    art,
                    rarity,
                    itemLevel,
                    NoAugments);
                presentationIdentity = candidate.IsUniqueInstance
                    ? candidate.InstanceStableId
                    : candidate.ContentStableId;
                return true;
            }

            item = null;
            view = null;
            presentationIdentity = string.Empty;
            return false;
        }

        private void ShowSecondaryRewards(
            StrongboxOpeningPresentationResult result,
            StrongboxRewardRevealItem excluded)
        {
            if (result == null)
            {
                HideSecondaryRewards();
                return;
            }

            int required = 0;
            for (int index = 0; index < result.Items.Count; index++)
            {
                if (!ReferenceEquals(result.Items[index], excluded))
                {
                    required++;
                }
            }
            EnsureSecondaryCards(required);

            int cardIndex = 0;
            for (int index = 0; index < result.Items.Count; index++)
            {
                StrongboxRewardRevealItem reward = result.Items[index];
                if (ReferenceEquals(reward, excluded))
                {
                    continue;
                }

                ItemCard card = secondaryCards[cardIndex++];
                card.Show(
                    null,
                    reward.Title,
                    reward.Kind.ToString().ToUpperInvariant(),
                    reward.Detail,
                    reward.Quantity > 1L
                        ? "x" + reward.Quantity.ToString(CultureInfo.InvariantCulture)
                        : string.Empty,
                    NoAugments);
            }

            for (int index = cardIndex; index < secondaryCards.Count; index++)
            {
                secondaryCards[index].gameObject.SetActive(false);
            }
            if (secondaryRewardsRoot != null)
            {
                secondaryRewardsRoot.SetActive(cardIndex > 0);
            }
        }

        private void HideSecondaryRewards()
        {
            if (secondaryRewardsRoot != null)
            {
                secondaryRewardsRoot.SetActive(false);
            }
        }

        private void EnsureSecondaryCards(int count)
        {
            if (count <= secondaryCards.Count)
            {
                return;
            }
            if (secondaryCardsRoot == null || secondaryCardPrefab == null)
            {
                throw new InvalidOperationException(
                    "Secondary reward cards require a root and ItemCard prefab.");
            }

            while (secondaryCards.Count < count)
            {
                ItemCard card = Instantiate(
                    secondaryCardPrefab,
                    secondaryCardsRoot);
                card.name = "SecondaryReward_" + (secondaryCards.Count + 1);
                secondaryCards.Add(card);
            }
        }

        private void SelectMode(StrongboxOpenMode mode)
        {
            if (state == ScreenState.Rolling)
            {
                return;
            }

            selectedMode = mode;
            RefreshControls();
        }

        private void RefreshStaticText()
        {
            if (menu == null)
            {
                return;
            }
            if (titleText != null)
            {
                titleText.text = "STRONGBOX OPENING";
            }
            if (tierText != null)
            {
                tierText.text = menu.Session.Configuration.TierLabel;
            }
            if (previewText != null)
            {
                previewText.text = "PREVIEW";
                previewText.gameObject.SetActive(menu.IsPreviewOnly);
            }
            if (statusText != null && state == ScreenState.Ready)
            {
                statusText.text = "READY";
            }
        }

        private void RefreshControls()
        {
            bool rolling = state == ScreenState.Rolling;
            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(!rolling);
                actionButton.interactable = !rolling;
            }
            if (actionText != null)
            {
                switch (state)
                {
                    case ScreenState.Pending:
                        actionText.text = "RETRY";
                        break;
                    case ScreenState.Complete:
                    case ScreenState.Rejected:
                        actionText.text = "CONTINUE";
                        break;
                    default:
                        actionText.text = "OPEN";
                        break;
                }
            }

            SetModeButton(cinematicButton, StrongboxOpenMode.Cinematic, rolling);
            SetModeButton(fastButton, StrongboxOpenMode.Fast, rolling);
            SetModeButton(revealOnlyButton, StrongboxOpenMode.RevealOnly, rolling);
            if (replayButton != null)
            {
                replayButton.gameObject.SetActive(currentPlan != null && !rolling);
                replayButton.interactable = currentPlan != null && !rolling;
            }
        }

        private void SetModeButton(
            Button button,
            StrongboxOpenMode mode,
            bool rolling)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = !rolling && selectedMode != mode;
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value ?? string.Empty;
            }
        }

        private void WireControls()
        {
            if (wired)
            {
                return;
            }
            Add(actionButton, SubmitAction);
            Add(cinematicButton, SelectCinematic);
            Add(fastButton, SelectFast);
            Add(revealOnlyButton, SelectRevealOnly);
            Add(replayButton, ReplaySameResult);
            wired = true;
        }

        private void UnwireControls()
        {
            if (!wired)
            {
                return;
            }
            Remove(actionButton, SubmitAction);
            Remove(cinematicButton, SelectCinematic);
            Remove(fastButton, SelectFast);
            Remove(revealOnlyButton, SelectRevealOnly);
            Remove(replayButton, ReplaySameResult);
            wired = false;
        }

        private static void Add(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void Remove(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static string ReadTokenAfter(string text, string marker)
        {
            string source = text ?? string.Empty;
            int start = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return string.Empty;
            }

            start += marker.Length;
            int end = start;
            while (end < source.Length && !char.IsWhiteSpace(source[end]))
            {
                end++;
            }
            return source.Substring(start, end - start);
        }

        private static int ReadIntegerAfter(string text, string marker)
        {
            string token = ReadTokenAfter(text, marker);
            int value;
            return int.TryParse(
                token,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                ? value
                : -1;
        }
    }
}
