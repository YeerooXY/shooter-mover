using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Guns.Presentation;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Shops;
using ShooterMover.UnityAdapters.Presentation.Guns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Shop
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class ShopMenu : MonoBehaviour
    {
        [SerializeField] private Texture2D shopTemplate;

        private readonly Dictionary<string, GunArtSpriteResolution>
            gunArtCache = new Dictionary<string, GunArtSpriteResolution>(
                StringComparer.Ordinal);
        private ShopScreenSession session;
        private IShopScreenRouteBridge routeAdapter;
        private PlayerRouteProfilePayload disconnectedPayload;
        private ShopScreenView projection;
        private ShopScreenActionResult lastAction;
        private ShopScreenRouteResult lastRoute;
        private EquipmentCatalog presentationEquipmentCatalog;
        private GunCatalog presentationGunCatalog;
        private int purchaseInputOrdinal;
        private bool explicitlyConfigured;
        private bool disconnectedReturnDispatched;
        private Vector2 stockScroll;
        private GUIStyle titleStyle;
        private GUIStyle balanceStyle;
        private GUIStyle sectionStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle augmentStyle;
        private GUIStyle feedbackStyle;

        public ShopScreenView Projection { get { return projection; } }
        public ShopScreenActionResult LastAction { get { return lastAction; } }
        public ShopScreenRouteResult LastRoute { get { return lastRoute; } }
        public bool IsBound { get { return session != null; } }
        public bool IsDisconnected
        {
            get { return session == null && disconnectedPayload != null; }
        }
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
            if (back)
            {
                NavigateBack();
            }
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
            GUILayout.BeginArea(new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height));
            GUILayout.BeginVertical(GUI.skin.window);
            GUILayout.Label("HUB SHOP", titleStyle);

            if (session == null || projection == null)
            {
                DrawDisconnected();
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }

            DrawHeader();
            GUILayout.Space(8f);
            DrawFeedback();
            GUILayout.Space(8f);

            stockScroll = GUILayout.BeginScrollView(stockScroll);
            DrawCategory("WEAPONS", "GUN");
            DrawCategory("ARMOR", "ARMOR");
            DrawCategory("OTHER EQUIPMENT", null);
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
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
            ShopScreenSession shopSession,
            IShopScreenRouteBridge adapter,
            Texture2D backplate = null)
        {
            explicitlyConfigured = true;
            session = shopSession
                ?? throw new ArgumentNullException(nameof(shopSession));
            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            disconnectedPayload = null;
            if (backplate != null)
            {
                shopTemplate = backplate;
            }
            purchaseInputOrdinal = 0;
            lastAction = null;
            lastRoute = null;
            projection = session.Open();
        }

        public void ConfigureGunPresentation(
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog)
        {
            presentationEquipmentCatalog = equipmentCatalog;
            presentationGunCatalog = gunCatalog;
            gunArtCache.Clear();
        }

        public void ConfigureDisconnected(
            PlayerRouteProfilePayload payload,
            IShopScreenRouteBridge adapter)
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

        public ShopScreenView OpenScreen()
        {
            EnsureInitialized();
            if (session == null)
            {
                return null;
            }
            projection = session.Open();
            return projection;
        }

        public ShopScreenActionResult Purchase(
            StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null || stockEntryStableId == null)
            {
                return null;
            }
            purchaseInputOrdinal++;
            StableId inputStableId =
                global::ShooterMover.Domain.Shops.Shop.DeriveStableId(
                    "shop-screen-input",
                    session.RunStableId.ToString(),
                    session.ShopStableId.ToString(),
                    stockEntryStableId.ToString(),
                    purchaseInputOrdinal.ToString(
                        "D8",
                        CultureInfo.InvariantCulture));
            return SubmitPurchase(inputStableId, stockEntryStableId);
        }

        public ShopScreenActionResult Retry(
            StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null
                || projection == null
                || stockEntryStableId == null)
            {
                return null;
            }
            ShopScreenStockCard card = projection.FindCard(
                stockEntryStableId);
            if (card == null || !card.CanRetry)
            {
                return null;
            }
            return SubmitPurchase(
                card.PurchaseTransactionStableId,
                stockEntryStableId);
        }

        public ShopScreenActionResult SubmitPurchase(
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
                new ShopScreenPurchaseInput(
                    inputStableId,
                    stockEntryStableId));
            projection = lastAction.Projection;
            return lastAction;
        }

        public ShopScreenRouteResult NavigateBack()
        {
            EnsureInitialized();
            if (session == null)
            {
                if (disconnectedPayload != null
                    && !disconnectedReturnDispatched)
                {
                    disconnectedReturnDispatched = true;
                    routeAdapter.Present(
                        ShopScreenRoute.Hub,
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

            ShopScreenSession handoffSession;
            IShopScreenRouteBridge handoffAdapter;
            EquipmentCatalog handoffEquipmentCatalog;
            GunCatalog handoffGunCatalog;
            if (ShopScreenLiveHandoff.TryConsume(
                out handoffSession,
                out handoffAdapter,
                out handoffEquipmentCatalog,
                out handoffGunCatalog))
            {
                session = handoffSession;
                routeAdapter = handoffAdapter;
                ConfigureGunPresentation(
                    handoffEquipmentCatalog,
                    handoffGunCatalog);
                projection = session.Open();
            }
        }

        private void DrawDisconnected()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                disconnectedPayload == null
                    ? "SHOP RUNTIME NOT BOUND"
                    : "AWAITING SHOP AUTHORITY COMPOSITION",
                feedbackStyle);
            GUILayout.Label(
                disconnectedPayload == null
                    ? "Prepare an authority-backed ShopScreenSession."
                    : "The Shop never creates fallback stock, money or inventory.",
                bodyStyle);
            if (disconnectedPayload != null
                && GUILayout.Button(
                    "BACK TO HUB",
                    GUILayout.MinHeight(48f)))
            {
                NavigateBack();
            }
            GUILayout.FlexibleSpace();
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
            GUILayout.Label(RefreshLabel(), balanceStyle);
            GUILayout.EndHorizontal();
        }

        private string RefreshLabel()
        {
            if (!projection.RefreshesAtUtc.HasValue)
            {
                return "STOCK REV "
                    + projection.RefreshOrdinal.ToString(
                        CultureInfo.InvariantCulture);
            }

            TimeSpan remaining =
                projection.RefreshesAtUtc.Value - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }
            return "NEW STOCK IN  "
                + ((int)remaining.TotalHours).ToString(
                    "00",
                    CultureInfo.InvariantCulture)
                + ":"
                + remaining.Minutes.ToString(
                    "00",
                    CultureInfo.InvariantCulture)
                + ":"
                + remaining.Seconds.ToString(
                    "00",
                    CultureInfo.InvariantCulture);
        }

        private void DrawFeedback()
        {
            if (projection.FeedbackKind == ShopScreenFeedbackKind.None)
            {
                return;
            }
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(projection.FeedbackText, feedbackStyle);
            if (!string.IsNullOrWhiteSpace(projection.FeedbackCode))
            {
                GUILayout.Label(projection.FeedbackCode, bodyStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawCategory(
            string heading,
            string categoryLabel)
        {
            var matching = new List<ShopScreenStockCard>();
            for (int index = 0; index < projection.Stock.Count; index++)
            {
                ShopScreenStockCard card = projection.Stock[index];
                bool match = categoryLabel == null
                    ? card.CategoryLabel != "GUN"
                        && card.CategoryLabel != "ARMOR"
                    : string.Equals(
                        card.CategoryLabel,
                        categoryLabel,
                        StringComparison.Ordinal);
                if (match)
                {
                    matching.Add(card);
                }
            }
            if (matching.Count == 0)
            {
                return;
            }

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

        private void DrawCard(ShopScreenStockCard card)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.MinWidth(260f),
                GUILayout.ExpandWidth(true));
            DrawGunArt(card);
            GUILayout.Label(card.DisplayName, cardTitleStyle);
            GUILayout.Label(
                card.QualityLabel
                + "  ·  LEVEL "
                + card.ItemLevel.ToString(
                    CultureInfo.InvariantCulture),
                bodyStyle);
            DrawAugments(card);
            GUILayout.Space(4f);
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

        private void DrawAugments(ShopScreenStockCard card)
        {
            if (card.HasGeneratedAugmentSignature)
            {
                GUILayout.Label(
                    "AUGMENTS  ·  LV "
                    + card.AugmentSharedLevel.ToString(
                        CultureInfo.InvariantCulture),
                    augmentStyle);
                GUILayout.Label(
                    BuildAugmentPips(
                        card.AugmentCapacity,
                        card.AugmentCount),
                    augmentStyle);
                return;
            }

            GUILayout.Label(
                card.AugmentCapacity == 0
                    ? "NO AUGMENT SLOTS"
                    : "AUGMENTS  "
                        + card.AugmentCount.ToString(
                            CultureInfo.InvariantCulture)
                        + "/"
                        + card.AugmentCapacity.ToString(
                            CultureInfo.InvariantCulture),
                augmentStyle);
        }

        private static string BuildAugmentPips(
            int capacity,
            int installed)
        {
            if (capacity <= 0)
            {
                return "—";
            }

            var builder = new StringBuilder();
            for (int index = 0; index < capacity; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(index < installed ? '◆' : '◇');
            }
            return builder.ToString();
        }

        private void DrawGunArt(ShopScreenStockCard card)
        {
            if (card == null
                || card.CategoryLabel != "GUN")
            {
                return;
            }

            GunArtReferenceView artProjection;
            string rejectionCode;
            if (!GunArtReferenceResolver.TryResolve(
                card.DefinitionStableId,
                presentationEquipmentCatalog,
                presentationGunCatalog,
                out artProjection,
                out rejectionCode))
            {
                return;
            }

            GunArtSpriteResolution resolution;
            if (!gunArtCache.TryGetValue(
                artProjection.ArtReferenceId,
                out resolution))
            {
                resolution = GunArt.Preload(
                    artProjection.ArtReferenceId);
                gunArtCache.Add(
                    artProjection.ArtReferenceId,
                    resolution);
            }
            if (resolution.Sprite == null
                || resolution.Sprite.texture == null)
            {
                return;
            }

            Rect artRect = GUILayoutUtility.GetRect(
                250f,
                112f,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(112f));
            GUI.DrawTexture(
                artRect,
                resolution.Sprite.texture,
                ScaleMode.ScaleToFit,
                true);
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
            if (titleStyle != null)
            {
                return;
            }
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
            augmentStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
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
    }
}
