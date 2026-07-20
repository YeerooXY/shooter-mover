using System;
using ShooterMover.TestSupport.VisibleSlice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Orders the two runtime-attached Level 1 components explicitly. Unity does not
    /// guarantee ordering between independent AfterSceneLoad hooks, so this installer
    /// always creates the gameplay composition before the Hub-loadout weapon consumer.
    /// </summary>
    internal static class Stage1HubLoadoutInstallerV1
    {
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (!string.Equals(
                scene.path,
                Stage1VisibleSliceController.ScenePath,
                StringComparison.Ordinal))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Stage1VisibleSliceController controller =
                    roots[index].GetComponentInChildren<
                        Stage1VisibleSliceController>(true);
                if (controller == null)
                {
                    continue;
                }

                if (controller.GetComponent<
                    Stage1PlayableLoopCompositionV1>() == null)
                {
                    controller.gameObject.AddComponent<
                        Stage1PlayableLoopCompositionV1>();
                }
                if (controller.GetComponent<
                    Stage1WeaponPresentationRepairV1>() == null)
                {
                    controller.gameObject.AddComponent<
                        Stage1WeaponPresentationRepairV1>();
                }
                return;
            }
        }
    }
}
