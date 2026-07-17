using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Shops;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Shop
{
    /// <summary>
    /// Hub Shop projection. The PNG is a visual backplate only. Every visible value and
    /// every interactive state comes from ShopScreenSessionV1, which delegates purchases
    /// to SHOP/MON/RAP/INV and returns their immutable facts.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed partial class ShopScreenControllerV1 : MonoBehaviour
    {
        private const float MaximumPanelWidth = 1500f;
        private const float MaximumPanelHeight = 900f;

        [Header("Non-authoritative artwork")]
        [SerializeField] private Texture2D shopTemplate;

        private ShopScreenSessionV1 session;
        private IShopScreenRouteAdapterV1 routeAdapter;
        private ShopScreenProjectionV1 projection;
        private ShopScreenActionResultV1 lastAction;
        private ShopScreenRouteResultV1 lastRoute;
        private int purchaseInputOrdinal;
        private bool explicitlyConfigured;
        private Vector2 stockScroll;

        private GUIStyle titleStyle;
        private GUIStyle balanceStyle;
        private GUIStyle sectionStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;
        private GUIStyle feedbackStyle;

        public ShopScreenProjectionV1 Projection
        {
            get { return projection; }
        }

        public ShopScreenActionResultV1 LastAction
        {
            get { return lastAction; }
        }

        public ShopScreenRouteResultV1 LastRoute
        {
            get { return lastRoute; }
        }

        public bool IsBound
        {
            get { return session != null; }
        }

        public Texture2D ShopTemplate
        {
            get { return shopTemplate; }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            bool keyboardBack = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            bool gamepadBack = Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardBack || gamepadBack)
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
                MaximumPanelWidth,
                Mathf.Max(520f, Screen.width - 32f));
            float height = Mathf.Min(
                MaximumPanelHeight,
                Mathf.Max(420f, Screen.height - 32f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel);
            GUILayout.BeginVertical(GUI.skin.window);
            GUILayout.Label("HUB SHOP", titleStyle);

            if (session == null || projection == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "SHOP RUNTIME NOT BOUND\n"
                    + "Prepare an authority-backed ShopScreenSessionV1 before loading this scene.",
                    feedbackStyle);
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
            if (GUILayout.Button("BACK TO HUB", GUILayout.MinHeight(48f)))
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
            if (backplate != null)
            {
                shopTemplate = backplate;
            }

            purchaseInputOrdinal = 0;
            lastAction = null;
            lastRoute = null;
            projection = session.Open();
        }

        public ShopScreenProjectionV1 OpenScreen()
        {
            EnsureInitialized();
            if (session == null)
            {
                return null;
            }

            projection = session.Open();
            return projection;
        }

        public ShopScreenActionResultV1 Purchase(StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null || stockEntryStableId == null)
            {
                return null;
            }

            purchaseInputOrdinal++;
            StableId inputStableId = ShopCanonicalV1.DeriveStableId(
                "shop-screen-input",
                session.RunStableId.ToString(),
                session.ShopStableId.ToString(),
                stockEntryStableId.ToString(),
                purchaseInputOrdinal.ToString("D8", CultureInfo.InvariantCulture));
            return SubmitPurchase(inputStableId, stockEntryStableId);
        }

        public ShopScreenActionResultV1 Retry(StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null || projection == null || stockEntryStableId == null)
            {
                return null;
            }

            ShopScreenStockCardV1 card = projection.FindCard(stockEntryStableId);
            if (card == null || !card.CanRetry)
            {
                return null;
            }

            return SubmitPurchase(
                card.PurchaseTransactionStableId,
                stockEntryStableId);
        }

        public ShopScreenActionResultV1 SubmitPurchase(
            StableId inputStableId,
            StableId stockEntryStableId)
        {
            EnsureInitialized();
            if (session == null || inputStableId == null || stockEntryStableId == null)
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
                return null;
            }

            lastRoute = session.NavigateBack();
            if (lastRoute.Emitted)
            {
                routeAdapter.Present(lastRoute.Route, lastRoute.Payload);
            }

            return lastRoute;
        }

        private void EnsureInitialized()
        {
            if (session != null || explicitlyConfigured)
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

    }
}
