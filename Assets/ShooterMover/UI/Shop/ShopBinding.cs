using System;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Shops;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.UI.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Shop
{
    [DefaultExecutionOrder(15000)]
    [DisallowMultipleComponent]
    public sealed class ShopBinding : MonoBehaviour
    {
        private ShopMenu menu;
        private bool bound;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    FlowScenePaths.Shop,
                    StringComparison.Ordinal))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                ShopMenu menu = roots[index]
                    .GetComponentInChildren<ShopMenu>(true);
                if (menu == null)
                {
                    continue;
                }
                if (menu.GetComponent<ShopBinding>() == null)
                {
                    menu.gameObject.AddComponent<ShopBinding>();
                }
                return;
            }
        }

        private void Awake()
        {
            menu = GetComponent<ShopMenu>();
        }

        private void Update()
        {
            if (bound || menu == null)
            {
                return;
            }
            bound = TryBind();
            if (bound)
            {
                enabled = false;
            }
        }

        private bool TryBind()
        {
            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            CharacterSetupFlow composition;
            if (!CharacterSave.TryResolveCurrent(
                    out graph,
                    out profile,
                    out composition)
                || graph == null
                || graph.IsDisposed
                || graph.Character == null
                || graph.RoutePayload == null
                || graph.ExperienceAuthority == null
                || graph.LoadoutRuntime == null
                || graph.Shop == null
                || profile == null
                || composition == null)
            {
                return false;
            }

            GameFlow flow = UnityEngine.Object
                .FindFirstObjectByType<GameFlow>(
                    FindObjectsInactive.Include);
            if (flow == null || flow.Transitions == null)
            {
                return false;
            }

            CharacterShopLive shop = graph.Shop;
            ShopRefreshWindow window = ShopRefreshSchedule.Resolve(
                DateTime.UtcNow);
            StableId characterId =
                graph.Character.CharacterInstanceStableId;
            var session = new ShopScreenSession(
                graph.RoutePayload,
                window.StockId(
                    characterId,
                    shop.Definition.ShopStableId),
                characterId,
                shop.Authority,
                graph.MoneyWallet,
                shop.Definition,
                graph.LoadoutRuntime.EquipmentCatalog,
                graph.ExperienceAuthority.CurrentContext,
                shop.OfferAugments,
                window.RefreshesAtUtc,
                new CharacterShopSave());
            menu.Configure(
                session,
                new ShopNavigation(flow));
            menu.ConfigureGunPresentation(
                graph.LoadoutRuntime.EquipmentCatalog,
                graph.LoadoutRuntime.GunCatalog);
            return true;
        }

        private sealed class ShopNavigation :
            IShopScreenRouteBridge
        {
            private readonly GameFlow flow;

            public ShopNavigation(GameFlow flow)
            {
                this.flow = flow
                    ?? throw new ArgumentNullException(nameof(flow));
            }

            public void Present(
                ShopScreenRoute route,
                PlayerRouteProfilePayload payload)
            {
                if (route == ShopScreenRoute.Hub
                    && payload != null
                    && flow.Transitions != null)
                {
                    flow.Transitions.TryReturnToHub(payload);
                }
            }
        }

        private sealed class CharacterShopSave : IShopSave
        {
            public bool Persist(
                string mutationFingerprint,
                out string rejectionCode)
            {
                CharacterSetupResult result =
                    CharacterSave.PersistCurrent(
                        "shop-purchase",
                        mutationFingerprint);
                if (result != null && result.Succeeded)
                {
                    rejectionCode = string.Empty;
                    return true;
                }

                rejectionCode = result == null
                    ? "shop-purchase-save-result-null"
                    : string.IsNullOrWhiteSpace(result.Diagnostic)
                        ? "shop-purchase-save-rejected"
                        : result.Diagnostic;
                return false;
            }
        }
    }
}
