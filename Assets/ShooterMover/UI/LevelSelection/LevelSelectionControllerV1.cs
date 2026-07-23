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
    public sealed class LevelSelectionControllerV1 : MonoBehaviour
    {
        [SerializeField]
        private LevelSelectionCatalogDefinitionV1 levelCatalog;

        [SerializeField]
        private Texture2D backplate;

        private LevelSelectionServiceV1 service;
        private ILevelSelectionRouteAdapterV1 routeAdapter;
        private LevelSelectionResultV1 lastResult;
        private LevelSelectionViewV1 view;
        private bool explicitlyConfigured;

        public LevelSelectionResultV1 LastResult
        {
            get { return lastResult; }
        }

        public PlayerRouteProfilePayloadV1 Payload
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

        public LevelSelectionCatalogV1 Catalog
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
            PlayerRouteProfilePayloadV1 payload,
            StableId selectedModeStableId,
            LevelSelectionCatalogV1 catalog,
            ILevelSelectionRouteAdapterV1 adapter)
        {
            explicitlyConfigured = true;
            service = new LevelSelectionServiceV1(
                payload,
                selectedModeStableId,
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            view = new LevelSelectionViewV1(backplate);
            lastResult = null;
        }

        public LevelSelectionResultV1 SelectLevel(StableId levelStableId)
        {
            EnsureInitialized();
            lastResult = service.SelectLevel(levelStableId);
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        public LevelSelectionResultV1 NavigateBack()
        {
            EnsureInitialized();
            lastResult = service.NavigateBack();
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        private LevelSelectionResultV1 SelectLevelDefinition(
            LevelSelectionDefinitionV1 definition)
        {
            return SelectLevel(definition.LevelStableId);
        }

        private void EnsureInitialized()
        {
            if (service != null || explicitlyConfigured)
            {
                return;
            }

            LevelSelectionCatalogV1 catalog = levelCatalog == null
                ? LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog()
                : levelCatalog.BuildCatalog();

            PlayerRouteProfilePayloadV1 payload;
            StableId selectedModeStableId;
            StableId ignoredLevelStableId;
            LevelSelectionRouteContextV1.TryRead(
                out payload,
                out selectedModeStableId,
                out ignoredLevelStableId);

            service = new LevelSelectionServiceV1(
                payload,
                selectedModeStableId,
                catalog);
            routeAdapter = new UnityLevelSelectionRouteAdapterV1();
            view = new LevelSelectionViewV1(backplate);
        }

        private void EmitRouteWhenAccepted(LevelSelectionResultV1 result)
        {
            if (result == null || !result.RouteEmitted)
            {
                return;
            }

            routeAdapter.Present(result);
        }
    }
}
