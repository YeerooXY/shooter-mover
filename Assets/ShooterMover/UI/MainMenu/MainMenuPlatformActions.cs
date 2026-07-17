using System;
using System.Reflection;
using ShooterMover.Application.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ShooterMover.UI.MainMenu
{
    public interface IMainMenuPlatformActions
    {
        void LoadPlayScene(
            string scenePath,
            bool reducedEffects,
            bool grayscale);

        void Quit();
    }

    /// <summary>
    /// Unity boundary for exact-path scene loading and application exit. The settings
    /// bridge invokes the existing public Stage 1 presentation setters after load.
    /// </summary>
    public sealed class UnityMainMenuPlatformActions : IMainMenuPlatformActions
    {
        private bool loadRequested;

        public void LoadPlayScene(
            string scenePath,
            bool reducedEffects,
            bool grayscale)
        {
            if (!string.Equals(
                scenePath,
                MainMenuFlowState.PlayScenePath,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "MENU-001 may load only the accepted Stage 1 visible-slice scene.",
                    nameof(scenePath));
            }

            if (loadRequested)
            {
                return;
            }

            loadRequested = true;
            MainMenuSceneSettingsBridge.Create(
                reducedEffects,
                grayscale);

#if UNITY_EDITOR
            if (UnityEngine.Application.isPlaying)
            {
                EditorSceneManager.LoadSceneAsyncInPlayMode(
                    scenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif

            AsyncOperation operation =
                SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError(
                    "Unable to load main-menu Play target at " + scenePath + ".");
            }
        }

        public void Quit()
        {
#if UNITY_EDITOR
            if (UnityEngine.Application.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
#endif
            UnityEngine.Application.Quit();
        }
    }

    /// <summary>
    /// One-shot, scene-transition-only adapter. It does not retain settings authority:
    /// after the target scene loads it finds a component exposing both accepted public
    /// bool setters, invokes them, unsubscribes, and destroys itself.
    /// </summary>
    internal sealed class MainMenuSceneSettingsBridge : MonoBehaviour
    {
        private const string ReducedEffectsSetterName = "SetReducedEffects";
        private const string GrayscaleSetterName = "SetGrayscale";

        private static MainMenuSceneSettingsBridge pending;

        private bool reducedEffects;
        private bool grayscale;
        private bool configured;

        public static void Create(bool reducedEffects, bool grayscale)
        {
            if (pending != null)
            {
                pending.reducedEffects = reducedEffects;
                pending.grayscale = grayscale;
                return;
            }

            GameObject bridgeObject = new GameObject(
                "MENU-001 Scene Settings Bridge");
            DontDestroyOnLoad(bridgeObject);
            MainMenuSceneSettingsBridge bridge =
                bridgeObject.AddComponent<MainMenuSceneSettingsBridge>();
            pending = bridge;
            bridge.Configure(reducedEffects, grayscale);
        }

        private void Configure(bool reduced, bool gray)
        {
            reducedEffects = reduced;
            grayscale = gray;
            configured = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!configured
                || !string.Equals(
                    scene.path,
                    MainMenuFlowState.PlayScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            bool applied = TryApplyToScene(scene);
            if (!applied)
            {
                Debug.LogWarning(
                    "MENU-001 loaded the Stage 1 scene but did not find the "
                    + "existing reduced-effects/grayscale setter surface.");
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Destroy(gameObject);
        }

        private bool TryApplyToScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours =
                    roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int behaviourIndex = 0;
                    behaviourIndex < behaviours.Length;
                    behaviourIndex++)
                {
                    MonoBehaviour behaviour = behaviours[behaviourIndex];
                    if (behaviour == null)
                    {
                        continue;
                    }

                    Type type = behaviour.GetType();
                    MethodInfo reducedSetter = FindBoolSetter(
                        type,
                        ReducedEffectsSetterName);
                    MethodInfo grayscaleSetter = FindBoolSetter(
                        type,
                        GrayscaleSetterName);
                    if (reducedSetter == null || grayscaleSetter == null)
                    {
                        continue;
                    }

                    reducedSetter.Invoke(
                        behaviour,
                        new object[] { reducedEffects });
                    grayscaleSetter.Invoke(
                        behaviour,
                        new object[] { grayscale });
                    return true;
                }
            }

            return false;
        }

        private static MethodInfo FindBoolSetter(
            Type type,
            string methodName)
        {
            return type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (pending == this)
            {
                pending = null;
            }
        }
    }
}
