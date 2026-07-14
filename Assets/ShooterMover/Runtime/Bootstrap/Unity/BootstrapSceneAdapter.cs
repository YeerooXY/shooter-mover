using System;
using UnityEngine;

namespace ShooterMover.Bootstrap.Unity
{
    /// <summary>
    /// Owns exactly one BootstrapCompositionRoot for the active Bootstrap scene.
    /// Duplicate scene adapters disable and destroy their complete scene root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootstrapSceneAdapter : MonoBehaviour
    {
        private static BootstrapSceneAdapter activeOwner;

        private BootstrapCompositionRoot compositionRoot;

        public bool IsCompositionRootRunning
        {
            get
            {
                return activeOwner == this
                    && compositionRoot != null
                    && compositionRoot.IsRunning;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticOwner()
        {
            // Unity invokes SubsystemRegistration on every Play Mode entry, including
            // when domain reload is disabled. Never carry an owner from a prior session.
            activeOwner = null;
        }

        private void OnEnable()
        {
            if (!TryClaimOwnership())
            {
                RejectDuplicate();
                return;
            }

            try
            {
                // A retained component instance can survive when scene reload is disabled.
                // Dispose any stale instance before creating this session's root.
                ShutdownCompositionRoot();

                compositionRoot = new BootstrapCompositionRoot();
                compositionRoot.Start();
            }
            catch
            {
                ShutdownCompositionRoot();
                ReleaseOwnership();
                enabled = false;
                throw;
            }
        }

        private void OnDisable()
        {
            if (activeOwner != this)
            {
                return;
            }

            ShutdownCompositionRoot();
            ReleaseOwnership();
        }

        private void OnDestroy()
        {
            ShutdownCompositionRoot();
            ReleaseOwnership();
        }

        private bool TryClaimOwnership()
        {
            if (activeOwner != null && activeOwner != this)
            {
                return false;
            }

            activeOwner = this;
            return true;
        }

        private void RejectDuplicate()
        {
            ShutdownCompositionRoot();
            enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void ReleaseOwnership()
        {
            if (activeOwner == this)
            {
                activeOwner = null;
            }
        }

        private void ShutdownCompositionRoot()
        {
            BootstrapCompositionRoot root = compositionRoot;
            compositionRoot = null;

            if (root == null)
            {
                return;
            }

            Exception stopFailure = null;
            Exception disposeFailure = null;

            try
            {
                root.Stop();
            }
            catch (Exception exception)
            {
                stopFailure = exception;
            }

            try
            {
                root.Dispose();
            }
            catch (Exception exception)
            {
                disposeFailure = exception;
            }

            if (stopFailure != null && disposeFailure != null)
            {
                Debug.LogException(
                    new AggregateException(
                        "Bootstrap stop and disposal both failed.",
                        stopFailure,
                        disposeFailure),
                    this);
            }
            else if (stopFailure != null)
            {
                Debug.LogException(stopFailure, this);
            }
            else if (disposeFailure != null)
            {
                Debug.LogException(disposeFailure, this);
            }
        }
    }
}
