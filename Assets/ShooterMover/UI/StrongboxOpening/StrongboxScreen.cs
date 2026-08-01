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
    /// uGUI presentation for the existing StrongboxMenu session. The screen owns only
    /// visuals and input; opening, rewards, persistence and navigation stay in the
    /// existing strongbox flow.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class StrongboxScreen : MonoBehaviour
    {
        private static readonly IReadOnlyList<AugmentLine> NoAugments =
            Array.Empty<AugmentLine>();

        [Header("Source")]
        [SerializeField] private StrongboxMenu menu;

        [Header("Header")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text tierText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text previewText;

        [Header("Box")]
        [SerializeField] private Image boxImage;
        [SerializeField] private Sprite closedBox;
        [SerializeField] private Sprite openBox;
        [SerializeField] private Image progressFill;

        [Header("Rewards")]
        [SerializeField] private RectTransform cardsRoot;
        [SerializeField] private ItemCard cardPrefab;

        [Header("Action")]
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionText;

        private readonly List<ItemCard> cards = new List<ItemCard>();
        private StrongboxOpeningPresentationResult lastResult;
        private StrongboxRevealStage lastStage;
        private int lastVisibleCount = -1;
        private bool buttonWired;
        private bool missingCardSetupLogged;

        public StrongboxMenu Menu { get { return menu; } }

        private void Awake()
        {
            if (ResolveMenu())
            {
                // The new screen drives the same session, so the old IMGUI component
                // stays as the authority adapter but no longer draws or handles input.
                _ = menu.Session;
                menu.enabled = false;
            }
        }

        private void OnEnable()
        {
            WireButton();
            Refresh(true);
        }

        private void OnDisable()
        {
            UnwireButton();
        }

        private void Update()
        {
            if (!ResolveMenu())
            {
                return;
            }

            StrongboxOpeningSceneSession session = menu.Session;
            session.Advance(Time.unscaledDeltaTime);
            HandleInput(session);
            Refresh(false);
        }

        private bool ResolveMenu()
        {
            if (menu != null)
            {
                return true;
            }

            menu = FindFirstObjectByType<StrongboxMenu>();
            if (menu != null)
            {
                _ = menu.Session;
                menu.enabled = false;
                return true;
            }

            Debug.LogError(
                "StrongboxScreen requires the scene StrongboxMenu.",
                this);
            enabled = false;
            return false;
        }

        private void HandleInput(StrongboxOpeningSceneSession session)
        {
            bool confirm = Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.spaceKey.wasPressedThisFrame);
            confirm |= Gamepad.current != null
                && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (confirm)
            {
                SubmitAction();
                return;
            }

            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back && session.Stage == StrongboxRevealStage.ContinueOrBack)
            {
                menu.RequestContinueOrBack();
            }
        }

        private void SubmitAction()
        {
            if (!ResolveMenu())
            {
                return;
            }

            StrongboxOpeningSceneSession session = menu.Session;
            if (session.Stage == StrongboxRevealStage.BoxClosed)
            {
                menu.RequestOpen();
            }
            else if (session.Result != null && session.Result.Pending)
            {
                menu.RetryPendingOpening();
            }
            else if (session.Stage == StrongboxRevealStage.ContinueOrBack)
            {
                menu.RequestContinueOrBack();
            }

            Refresh(true);
        }

        private void Refresh(bool force)
        {
            if (!ResolveMenu())
            {
                return;
            }

            StrongboxOpeningSceneSession session = menu.Session;
            if (titleText != null)
            {
                titleText.text = "STRONGBOX OPENING";
            }
            if (tierText != null)
            {
                tierText.text = session.Configuration.TierLabel;
            }
            if (previewText != null)
            {
                previewText.text = "PREVIEW";
                previewText.gameObject.SetActive(menu.IsPreviewOnly);
            }
            if (progressFill != null)
            {
                progressFill.fillAmount = session.OpeningProgress;
            }
            if (boxImage != null)
            {
                Sprite sprite = session.Stage == StrongboxRevealStage.BoxClosed
                    ? closedBox
                    : openBox;
                if (sprite != null)
                {
                    boxImage.sprite = sprite;
                }
            }

            SetStatus(session.Result);
            SetAction(session);

            if (force
                || lastResult != session.Result
                || lastStage != session.Stage
                || lastVisibleCount != session.VisibleRewardCount)
            {
                RebuildCards(session);
                lastResult = session.Result;
                lastStage = session.Stage;
                lastVisibleCount = session.VisibleRewardCount;
            }
        }

        private void SetStatus(StrongboxOpeningPresentationResult result)
        {
            if (statusText == null)
            {
                return;
            }

            if (result == null)
            {
                statusText.text = "READY TO OPEN";
                return;
            }

            statusText.text = result.StatusText;
        }

        private void SetAction(StrongboxOpeningSceneSession session)
        {
            string label = string.Empty;
            bool visible = false;
            bool interactable = false;

            if (session.Stage == StrongboxRevealStage.BoxClosed)
            {
                label = "OPEN";
                visible = true;
                interactable = true;
            }
            else if (session.Result != null && session.Result.Pending)
            {
                label = "RETRY";
                visible = true;
                interactable = true;
            }
            else if (session.Stage == StrongboxRevealStage.ContinueOrBack)
            {
                label = "CONTINUE";
                visible = true;
                interactable = true;
            }

            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(visible);
                actionButton.interactable = interactable;
            }
            if (actionText != null)
            {
                actionText.text = label;
            }
        }

        private void RebuildCards(StrongboxOpeningSceneSession session)
        {
            StrongboxOpeningPresentationResult result = session.Result;
            int visibleCount = result != null && result.Succeeded
                ? Mathf.Min(session.VisibleRewardCount, result.Items.Count)
                : 0;

            EnsureCards(visibleCount);
            for (int index = 0; index < cards.Count; index++)
            {
                bool visible = index < visibleCount;
                cards[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                StrongboxRewardRevealItem item = result.Items[index];
                Sprite art = null;
                if (item.HasGunArtReference)
                {
                    GunArtSpriteResolution resolution =
                        GunArt.Preload(item.GunArtReferenceId);
                    art = resolution.Sprite;
                }

                cards[index].Show(
                    art,
                    item.Title,
                    item.Kind.ToString().ToUpperInvariant(),
                    item.Detail,
                    item.Quantity > 1L
                        ? "x" + item.Quantity.ToString(
                            CultureInfo.InvariantCulture)
                        : string.Empty,
                    NoAugments);
            }
        }

        private void EnsureCards(int count)
        {
            if (count <= cards.Count)
            {
                return;
            }
            if (cardsRoot == null || cardPrefab == null)
            {
                if (!missingCardSetupLogged)
                {
                    missingCardSetupLogged = true;
                    Debug.LogError(
                        "StrongboxScreen requires a cards root and ItemCard prefab.",
                        this);
                }
                return;
            }

            while (cards.Count < count)
            {
                ItemCard card = Instantiate(cardPrefab, cardsRoot);
                card.name = "RewardCard_" + (cards.Count + 1);
                cards.Add(card);
            }
        }

        private void WireButton()
        {
            if (buttonWired || actionButton == null)
            {
                return;
            }

            actionButton.onClick.AddListener(SubmitAction);
            buttonWired = true;
        }

        private void UnwireButton()
        {
            if (!buttonWired || actionButton == null)
            {
                return;
            }

            actionButton.onClick.RemoveListener(SubmitAction);
            buttonWired = false;
        }
    }
}
