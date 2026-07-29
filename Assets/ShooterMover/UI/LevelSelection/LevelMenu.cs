using System;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.LevelSelection
{
    [DisallowMultipleComponent]
    public sealed class LevelMenu : MonoBehaviour
    {
        [SerializeField] private LevelSelectionCatalogDefinition levelCatalog;
        [SerializeField] private Texture2D backplate;

        private LevelSelectionActions service;
        private ILevelSelectionRouteBridge routeAdapter;
        private LevelSelectionResult lastResult;
        private LevelSelectionView view;
        private bool explicitlyConfigured;

        public LevelSelectionResult LastResult
        {
            get { return lastResult; }
        }

        public PlayerRouteProfilePayload Payload
        {
            get
            {
                EnsureInitialized();
                return service.Payload;
            }
        }

        public StableId SelectedModeStableId
        {
            get
            {
                EnsureInitialized();
                return service.SelectedModeStableId;
            }
        }

        public LevelSelectionCatalog Catalog
        {
            get
            {
                EnsureInitialized();
                return service.Catalog;
            }
        }

        public bool IsInputLocked
        {
            get
            {
                EnsureInitialized();
                return service.IsInputLocked;
            }
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
            view.Draw(
                service,
                lastResult,
                SelectLevelDefinition,
                NavigateBack);
        }

        public void Configure(
            PlayerRouteProfilePayload payload,
            StableId selectedModeStableId,
            LevelSelectionCatalog catalog,
            ILevelSelectionRouteBridge adapter)
        {
            explicitlyConfigured = true;
            service = new LevelSelectionActions(
                payload,
                selectedModeStableId,
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            view = new LevelSelectionView(backplate);
            lastResult = null;
        }

        public LevelSelectionResult SelectLevel(StableId levelStableId)
        {
            EnsureInitialized();
            lastResult = service.SelectLevel(levelStableId);
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        public LevelSelectionResult NavigateBack()
        {
            EnsureInitialized();
            lastResult = service.NavigateBack();
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        private LevelSelectionResult SelectLevelDefinition(
            LevelSelectionDefinition definition)
        {
            return SelectLevel(definition.LevelStableId);
        }

        private void EnsureInitialized()
        {
            if (service != null || explicitlyConfigured)
            {
                return;
            }

            LevelSelectionCatalog catalog = levelCatalog == null
                ? LevelSelectionCatalogDefinition.CreateDefaultCatalog()
                : levelCatalog.BuildCatalog();

            PlayerRouteProfilePayload payload;
            StableId selectedModeStableId;
            StableId ignoredLevelStableId;
            LevelSelectionRouteContext.TryRead(
                out payload,
                out selectedModeStableId,
                out ignoredLevelStableId);

            service = new LevelSelectionActions(
                payload,
                selectedModeStableId,
                catalog);
            routeAdapter = new UnityLevelSelectionRouteBridge();
            view = new LevelSelectionView(backplate);
        }

        private void EmitRouteWhenAccepted(LevelSelectionResult result)
        {
            if (result == null || !result.RouteEmitted)
            {
                return;
            }

            // Production and standalone adapters now observe the same immutable handoff.
            // Capturing before presentation avoids a gameplay scene racing the transition.
            LevelSelectionRouteContext.Capture(result);
            routeAdapter.Present(result);
        }
    }
}
