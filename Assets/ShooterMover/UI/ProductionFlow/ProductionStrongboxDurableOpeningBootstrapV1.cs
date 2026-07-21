using System;
using System.Collections;
using System.Reflection;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.UI.StrongboxOpening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Late-binds the canonical Strongbox Opening screen to the durable selected-character
    /// executor. The existing flow coordinator still owns routing and the immutable exact
    /// selection; this adapter only replaces the raw BOX callback after normal scene bind.
    /// </summary>
    internal static class ProductionStrongboxDurableOpeningBootstrapV1
    {
        private static readonly FieldInfo BindingField =
            typeof(ProductionFlowCoordinatorV1).GetField(
                "strongboxBinding",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool installed;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (installed)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
            installed = false;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (installed)
            {
                return;
            }
            installed = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TrySchedule(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            TrySchedule(scene);
        }

        private static void TrySchedule(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    ProductionFlowScenePathsV1.StrongboxOpening,
                    StringComparison.Ordinal))
            {
                return;
            }
            var host = new GameObject(
                "BOX-PERSIST-001 Durable Opening Binder");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<DeferredBinder>().Begin(scene);
        }

        private sealed class DeferredBinder : MonoBehaviour
        {
            private Scene scene;

            public void Begin(Scene targetScene)
            {
                scene = targetScene;
                StartCoroutine(BindAfterCanonicalFlow());
            }

            private IEnumerator BindAfterCanonicalFlow()
            {
                yield return null;
                try
                {
                    Bind();
                }
                finally
                {
                    Destroy(gameObject);
                }
            }

            private void Bind()
            {
                if (!scene.IsValid() || BindingField == null)
                {
                    return;
                }
                ProductionFlowCoordinatorV1 flow = FindInScene<
                    ProductionFlowCoordinatorV1>(scene);
                if (flow == null)
                {
                    ProductionFlowCoordinatorV1[] flows =
                        UnityEngine.Object.FindObjectsByType<
                            ProductionFlowCoordinatorV1>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None);
                    flow = flows.Length == 0 ? null : flows[0];
                }
                var binding = flow == null
                    ? null
                    : BindingField.GetValue(flow)
                        as ProductionStrongboxOpeningBindingV1;
                StrongboxOpeningController controller =
                    FindInScene<StrongboxOpeningController>(scene);
                if (binding == null || controller == null)
                {
                    return;
                }

                ProductionCharacterAccountCompositionV1[] compositions =
                    UnityEngine.Object.FindObjectsByType<
                        ProductionCharacterAccountCompositionV1>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                CharacterCompositionCoordinatorV1 composition =
                    compositions.Length == 0
                        ? null
                        : compositions[0].Composition;
                if (composition == null)
                {
                    return;
                }

                var durable = new StrongboxDurableOpeningCoordinatorV1(
                    composition);
                StrongboxOpeningPreviewConfigurationV1 configuration =
                    controller.Session.Configuration;
                controller.ConfigureForTests(
                    configuration,
                    delegate
                    {
                        return StrongboxRewardRevealProjectorV1.Project(
                            durable.OpenAndPersist(
                                binding.SelectedStrongbox,
                                binding.OpeningService,
                                binding.Command),
                            binding.EquipmentCatalog);
                    });
            }

            private static T FindInScene<T>(Scene target)
                where T : Component
            {
                GameObject[] roots = target.GetRootGameObjects();
                for (int index = 0; index < roots.Length; index++)
                {
                    T value = roots[index].GetComponentInChildren<T>(true);
                    if (value != null)
                    {
                        return value;
                    }
                }
                return null;
            }
        }
    }
}
