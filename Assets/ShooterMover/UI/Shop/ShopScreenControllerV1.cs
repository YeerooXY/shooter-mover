using System;
using System.Globalization;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Shops;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Shop
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class ShopScreenControllerV1 : MonoBehaviour
    {
        [SerializeField] private Texture2D shopTemplate;

        private ShopScreenSessionV1 session;
        private IShopScreenRouteAdapterV1 routeAdapter;
        private PlayerRouteProfilePayloadV1 disconnectedPayload;
        private ShopScreenProjectionV1 projection;
        private ShopScreenActionResultV1 lastAction;
        private ShopScreenRouteResultV1 lastRoute;
        private int purchaseInputOrdinal;
        private bool explicitlyConfigured;
        private bool disconnectedReturnDispatched;
        private Vector2 stockScroll;
        private GUIStyle titleStyle;
        private GUIStyle balanceStyle;
        private GUIStyle sectionStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;
        private GUIStyle feedbackStyle;

        public ShopScreenProjectionV1 Projection { get { return projection; } }
        public ShopScreenActionResultV1 LastAction { get { return lastAction; } }
        public ShopScreenRouteResultV1 LastRoute { get { return lastRoute; } }
        public bool IsBound { get { return session != null; } }
        public bool IsDisconnected { get { return session == null && disconnectedPayload != null; } }
        public Texture2D ShopTemplate { get { return shopTemplate; } }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back) NavigateBack();
        }

        private void OnGUI()
        {
            EnsureInitialized();
            EnsureStyles();
            DrawBackplate();

            float width = Mathf.Min(
                1500f,
                Mathf.Max(520f, Screen.width - 32f));
            float height = Mathf.Min(
                900f,
                Mathf.Max(420f, Screen.height - 32f));
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height));
            GUILayout.BeginVertical(GUI.skin.window);
            GUILayout.Label("HUB SHOP", titleStyle);

            if (session == null || projection == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    disconnectedPayload == null
                        ? "SHOP RUNTIME NOT BOUND"
                        : "AWAITING SHOP AUTHORITY COMPOSITION",
                    feedbackStyle);
                GUILayout.Label(
                    disconnectedPayload == null
                        ? "Prepare an authority-backed ShopScreenSessionV1."
                        : "The real Shop screen and artwork are active. No fallback "
                            + "stock, wallet, inventory or reward authority was created.",
                    bodyStyle);
                if (disconnectedPayload != null
                    && GUILayout.Button(
                        "BACK TO HUB",
                        GUILayout.MinHeight(48f)))
                {
                    NavigateBack();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }

            DrawHeader();
            GUILayout.Space(10f);
            DrawFeedback();
            GUILayout.Space(10f);

            stockScroll = GUILayout.BeginScrollView(stockScroll);
            DrawCategory("WEAPONS", "WEAPON");
            DrawCategory("ARMOR", "ARMOR");
            DrawCategory("OTHER EQUIPMENT", null);
            GUILayout.EndScrollView();

            GUILayout.Space(10f);
            if (GUILayout.Button(
                "BACK TO HUB",
                GUILayout.MinHeight(48f)))
            {
                NavigateBack();
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        public void Configure(
            ShopScreenSessionV1 shopSession,
            IShopScreenRouteAdapterV1 adapter,
            Texture2D backplate = null)
        {
            explicitlyConfigured = true;
            session = shopSession
                ?? throw new ArgumentNullException(nameof(shopSession));
            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            disconnectedPayload = null;
            if (backplate != null) shopTemplate = backplate;
            purchaseInputOrdinal = 0;
            lastAction = null;
            lastRoute = null;
            projection = session.Open();
        }

        public void ConfigureDisconnected(
            PlayerRouteProfilePayloadV1 payload,
            IShopScreenRouteAdapterV1 adapter)
        {
            explicitlyConfigured = true;
            disconnectedPayload = payload
                ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The Shop route payload is invalid.",
                    nameof(payload));
            }

            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            session = null;
            projection = null;
            lastAction = null;
            lastRoute = null;
            disconnectedReturnDispatched = false;
        }

        public ShopScreenProjectionV1 OpenScreen()
        {
            EnsureInitialized();
            if (session == null) return null;
            projection = session.Open();
            return projection;
        }

        public ShopScreenActionResultV1 Purchase(
            StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null || stockEntryStableId == null) return null;
            purchaseInputOrdinal++;
            StableId inputStableId = ShopCanonicalV1.DeriveStableId(
                "shop-screen-input",
                session.RunStableId.ToString(),
                session.ShopStableId.ToString(),
                stockEntryStableId.ToString(),
                purchaseInputOrdinal.ToString(
                    "D8",
                    CultureInfo.InvariantCulture));
            return SubmitPurchase(inputStableId, stockEntryStableId);
        }

        public ShopScreenActionResultV1 Retry(
            StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null
                || projection == null
                || stockEntryStableId == null)
            {
                return null;
            }

            ShopScreenStockCardV1 card = projection.FindCard(
                stockEntryStableId);
            if (card == null || !card.CanRetry) return null;
            return SubmitPurchase(
                card.PurchaseTransactionStableId,
                stockEntryStableId);
        }

        public ShopScreenActionResultV1 SubmitPurchase(
            StableId inputStableId,
            StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null
                || inputStableId == null
                || stockEntryStableId == null)
            {
                return null;
            }

            lastAction = session.SubmitPurchase(
                new ShopScreenPurchaseInputV1(
                    inputStableId,
                    stockEntryStableId));
            projection = lastAction.Projection;
            return lastAction;
        }

        public ShopScreenRouteResultV1 NavigateBack()
        {
            EnsureInitialized();
            if (session == null)
            {
                if (disconnectedPayload != null
                    && !disconnectedReturnDispatched)
                {
                    disconnectedReturnDispatched = true;
                    routeAdapter.Present(
                        ShopScreenRouteV1.Hub,
                        disconnectedPayload);
                }
                return null;
            }

            lastRoute = session.NavigateBack();
            if (lastRoute.Emitted)
            {
                routeAdapter.Present(
                    lastRoute.Route,
                    lastRoute.Payload);
            }
            return lastRoute;
        }

        private void EnsureInitialized()
        {
            if (session != null
                || disconnectedPayload != null
                || explicitlyConfigured)
            {
                return;
            }

            ShopScreenSessionV1 handoffSession;
            IShopScreenRouteAdapterV1 handoffAdapter;
            if (ShopScreenRuntimeHandoffV1.TryConsume(
                out handoffSession,
                out handoffAdapter))
            {
                session = handoffSession;
                routeAdapter = handoffAdapter;
                projection = session.Open();
            }
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(
                "MONEY  "
                + projection.MoneyBalance.ToString(
                    "N0",
                    CultureInfo.InvariantCulture),
                balanceStyle,
                GUILayout.MinWidth(220f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "RUN " + ShortId(projection.RunStableId)
                + "   STOCK REV "
                + projection.RefreshOrdinal.ToString(
                    CultureInfo.InvariantCulture),
                detailStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawFeedback()
        {
            if (projection == null
                || projection.FeedbackKind
                    == ShopScreenFeedbackKindV1.None)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(projection.FeedbackText, feedbackStyle);
            if (!string.IsNullOrWhiteSpace(projection.FeedbackCode))
            {
                GUILayout.Label(projection.FeedbackCode, detailStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawCategory(string heading, string categoryLabel)
        {
            var matching =
                new System.Collections.Generic.List<
                    ShopScreenStockCardV1>();
            for (int index = 0; index < projection.Stock.Count; index++)
            {
                ShopScreenStockCardV1 card = projection.Stock[index];
                bool match = categoryLabel == null
                    ? card.CategoryLabel != "WEAPON"
                        && card.CategoryLabel != "ARMOR"
                    : string.Equals(
                        card.CategoryLabel,
                        categoryLabel,
                        StringComparison.Ordinal);
                if (match) matching.Add(card);
            }

            if (matching.Count == 0) return;

            GUILayout.Label(heading, sectionStyle);
            int columns = Screen.width >= 1200
                ? 3
                : Screen.width >= 760 ? 2 : 1;
            for (int index = 0; index < matching.Count; index += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int cardIndex = index + column;
                    if (cardIndex < matching.Count)
                    {
                        DrawCard(matching[cardIndex]);
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }
        }

        private void DrawCard(ShopScreenStockCardV1 card)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.MinWidth(260f),
                GUILayout.ExpandWidth(true));
            GUILayout.Label(card.DisplayName, cardTitleStyle);
            GUILayout.Label(
                card.CategoryLabel
                + "  ·  " + card.QualityLabel
                + "  ·  LEVEL "
                + card.ItemLevel.ToString(
                    CultureInfo.InvariantCulture),
                bodyStyle);
            GUILayout.Label(
                "Augments: "
                + card.AugmentCount.ToString(
                    CultureInfo.InvariantCulture),
                bodyStyle);
            GUILayout.Label(
                "Definition: " + card.DefinitionStableId,
                detailStyle);
            GUILayout.Label(
                "Instance: " + card.EquipmentInstanceStableId,
                detailStyle);
            GUILayout.Label(
                "PRICE  "
                + card.Price.ToString(
                    "N0",
                    CultureInfo.InvariantCulture),
                balanceStyle);

            if (card.IsSold)
            {
                GUILayout.Label("SOLD", feedbackStyle);
            }
            else if (card.CanRetry)
            {
                if (GUILayout.Button(
                    "RETRY PENDING PURCHASE",
                    GUILayout.MinHeight(42f)))
                {
                    Retry(card.StockEntryStableId);
                }
            }
            else
            {
                GUI.enabled = card.CanPurchase;
                if (GUILayout.Button(
                    "BUY",
                    GUILayout.MinHeight(42f)))
                {
                    Purchase(card.StockEntryStableId);
                }
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
        }

        private void DrawBackplate()
        {
            Rect full = new Rect(
                0f,
                0f,
                Screen.width,
                Screen.height);
            if (shopTemplate != null)
            {
                GUI.DrawTexture(
                    full,
                    shopTemplate,
                    ScaleMode.ScaleAndCrop,
                    true);
            }
            else
            {
                GUI.Box(full, GUIContent.none);
            }

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.38f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            balanceStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true,
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
            };
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }

        private static string ShortId(StableId value)
        {
            string text = value == null
                ? string.Empty
                : value.ToString();
            return text.Length <= 24
                ? text
                : text.Substring(0, 24);
        }
    }
}
