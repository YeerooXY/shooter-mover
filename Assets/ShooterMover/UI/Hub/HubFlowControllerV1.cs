using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Hub
{
    public sealed class HubRoutePlaceholderAdapterV1 :
        IHubRouteDestinationAdapterV1
    {
        public HubRouteV1 LastRoute { get; private set; }
        public PlayerRouteProfilePayloadV1 LastPayload { get; private set; }
        public int PresentCount { get; private set; }

        public void Present(
            HubRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            LastRoute = route;
            LastPayload = payload ?? throw new ArgumentNullException(nameof(payload));
            PresentCount++;
        }
    }

    /// <summary>
    /// Canonical Hub presentation. Production navigation is delegated to one
    /// IHubRouteTransactionPortV1, which in turn delegates route truth to the existing
    /// HubNavigationServiceV1. No Main Menu bootstrap or destination overlay is installed.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class HubFlowControllerV1 : MonoBehaviour
    {
        private const float MaximumPanelWidth = 1040f;
        private const float MaximumPanelHeight = 760f;

        private HubNavigationServiceV1 navigation;
        private IHubRouteDestinationAdapterV1 destinationAdapter;
        private IHubRouteTransactionPortV1 transactionPort;
        private HubNavigationResultV1 lastNavigationResult;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle fingerprintStyle;
        private Vector2 scrollPosition;

        public HubRouteV1 CurrentRoute
        {
            get { EnsureInitialized(); return navigation.CurrentRoute; }
        }

        public PlayerRouteProfilePayloadV1 Payload
        {
            get { EnsureInitialized(); return navigation.Payload; }
        }

        public HubNavigationResultV1 LastNavigationResult
        {
            get { return lastNavigationResult; }
        }

        public bool IsTransitionPending
        {
            get { return transactionPort != null && transactionPort.IsTransitionPending; }
        }

        public HubNavigationSnapshotV1 ExportSnapshot()
        {
            EnsureInitialized();
            return navigation.ExportSnapshot();
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
            if (keyboardBack || gamepadBack) NavigateBack();
        }

        private void OnGUI()
        {
            EnsureInitialized();
            EnsureStyles();

            int priorDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);

            float width = Mathf.Min(
                MaximumPanelWidth,
                Mathf.Max(360f, Screen.width - 24f));
            float height = Mathf.Min(
                MaximumPanelHeight,
                Mathf.Max(300f, Screen.height - 24f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("SHOOTER MOVER", titleStyle);
            GUILayout.Label("HUB", headingStyle);
            GUILayout.Label(
                navigation.Payload.SelectedCharacterStableId
                + "  /  "
                + navigation.Payload.LoadoutProfileStableId,
                bodyStyle);
            GUILayout.Label(
                "Route payload: " + ShortFingerprint(navigation.Payload.Fingerprint),
                fingerprintStyle);
            GUILayout.Space(16f);

            GUILayout.Label(
                "WEAPON LOADOUT — CONCRETE INSTANCE IDENTITIES",
                headingStyle);
            for (int index = 0;
                index < navigation.Payload.WeaponSlots.Count;
                index++)
            {
                PlayerRouteWeaponSlotV1 slot =
                    navigation.Payload.WeaponSlots[index];
                GUILayout.Label(
                    slot.WeaponSlotStableId
                    + "  →  "
                    + slot.EquipmentInstanceStableId,
                    bodyStyle);
            }

            GUILayout.Space(20f);
            GUI.enabled = !IsTransitionPending;
            GUILayout.BeginHorizontal();
            DrawButton(
                "INVENTORY",
                delegate { return OpenDestination(HubRouteV1.Inventory); });
            DrawButton(
                "SKILLS",
                delegate { return OpenDestination(HubRouteV1.Skills); });
            DrawButton(
                "SHOP",
                delegate { return OpenDestination(HubRouteV1.Shop); });
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawButton(
                "CRAFTING",
                delegate { return OpenDestination(HubRouteV1.Crafting); });
            DrawButton(
                "PLAY",
                delegate { return OpenDestination(HubRouteV1.Play); });
            GUILayout.EndHorizontal();
            GUILayout.Space(16f);
            DrawButton("BACK", NavigateBack);
            DrawButton("MAIN MENU", GoToMainMenu);
            GUI.enabled = true;

            if (IsTransitionPending)
            {
                GUILayout.Label(
                    "Loading the accepted destination… repeated input is locked.",
                    fingerprintStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        public void ConfigureForTests(
            PlayerRouteProfilePayloadV1 payload,
            IHubRouteDestinationAdapterV1 adapter)
        {
            navigation = new HubNavigationServiceV1(
                payload ?? throw new ArgumentNullException(nameof(payload)));
            destinationAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            transactionPort = null;
            lastNavigationResult = null;
            destinationAdapter.Present(
                navigation.CurrentRoute,
                navigation.Payload);
        }

        public void ConfigureProduction(
            IHubRouteTransactionPortV1 port)
        {
            transactionPort = port
                ?? throw new ArgumentNullException(nameof(port));
            navigation = port.Navigation
                ?? throw new ArgumentException(
                    "The production transaction port requires the existing navigation service.",
                    nameof(port));
            destinationAdapter = null;
            lastNavigationResult = null;
        }

        public bool OpenCharacterSelect()
        {
            return NavigateTo(HubRouteV1.CharacterSelect);
        }

        public bool ContinueToHub()
        {
            return NavigateTo(HubRouteV1.InventoryLoadoutHub);
        }

        public bool OpenDestination(HubRouteV1 route)
        {
            if (!HubNavigationServiceV1.IsHubDestination(route))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(route),
                    "Only Inventory, Skills, Shop, Crafting, or Play are Hub destinations.");
            }

            return NavigateTo(route);
        }

        public bool ReturnToHub()
        {
            EnsureInitialized();
            if (!HubNavigationServiceV1.IsHubDestination(
                navigation.CurrentRoute))
            {
                return false;
            }

            return NavigateBack();
        }

        public bool GoToMainMenu()
        {
            return NavigateTo(HubRouteV1.MainMenu);
        }

        public bool NavigateBack()
        {
            EnsureInitialized();
            if (transactionPort != null)
            {
                return transactionPort.TryNavigateBack();
            }

            lastNavigationResult = navigation.NavigateBack();
            PresentWhenChanged();
            return lastNavigationResult.Changed;
        }

        private bool NavigateTo(HubRouteV1 route)
        {
            EnsureInitialized();
            if (transactionPort != null)
            {
                return transactionPort.TryNavigateTo(route);
            }

            lastNavigationResult = navigation.TryNavigateTo(route);
            PresentWhenChanged();
            return lastNavigationResult.Changed;
        }

        private void PresentWhenChanged()
        {
            if (lastNavigationResult != null
                && lastNavigationResult.Changed
                && destinationAdapter != null)
            {
                destinationAdapter.Present(
                    navigation.CurrentRoute,
                    navigation.Payload);
            }
        }

        private void EnsureInitialized()
        {
            if (navigation != null) return;

            PlayerRouteProfilePayloadV1 payload =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.default-pilot"),
                    StableId.Parse("loadout-profile.hub-session-default"),
                    new List<StableId>
                    {
                        StableId.Parse("equipment-instance.hub-slot-1"),
                        StableId.Parse("equipment-instance.hub-slot-2"),
                        StableId.Parse("equipment-instance.hub-slot-3"),
                        StableId.Parse("equipment-instance.hub-slot-4"),
                    });
            destinationAdapter = new HubRoutePlaceholderAdapterV1();
            navigation = new HubNavigationServiceV1(payload);
            destinationAdapter.Present(
                navigation.CurrentRoute,
                navigation.Payload);
        }

        private static void DrawButton(
            string label,
            Func<bool> action)
        {
            if (GUILayout.Button(label, GUILayout.MinHeight(46f))) action();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                wordWrap = true,
            };
            fingerprintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
            };
        }

        private static string ShortFingerprint(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint)
                || fingerprint.Length <= 16)
            {
                return fingerprint ?? string.Empty;
            }

            return fingerprint.Substring(0, 16);
        }
    }
}
