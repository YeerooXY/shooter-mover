using System;
using ShooterMover.Domain.Movement;
using UnityEngine;

namespace ShooterMover.Presentation.VisibleSliceCameraReadability
{
    /// <summary>
    /// Task-local orthographic camera owner. The rig writes only its configured Camera
    /// transform and lens; all gameplay and accessibility inputs are read-only injections.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class VisibleSliceCameraRig : MonoBehaviour
    {
        private const float RuntimeFollowSharpness = 18f;

        private Camera ownedCamera;
        private IVisibleSliceCameraFollowSource followSource;
        private IVisibleSliceThrusterStatusReader thrusterStatusReader;
        private IVisibleSliceReducedEffectsSource reducedEffectsSource;
        private VisibleSliceCameraConfiguration configuration;
        private float cameraDepth;
        private bool isConfigured;
        private bool hasFrame;
        private long restartGeneration;

        public bool IsConfigured
        {
            get { return isConfigured; }
        }

        public bool HasFrame
        {
            get { return hasFrame; }
        }

        public long RestartGeneration
        {
            get { return restartGeneration; }
        }

        public VisibleSliceCameraFrame LastFrame { get; private set; }

        public bool ReducedEffectsEnabled
        {
            get
            {
                return reducedEffectsSource != null
                    && reducedEffectsSource.ReducedEffectsEnabled;
            }
        }

        public void Configure(
            Camera cameraToOwn,
            IVisibleSliceCameraFollowSource injectedFollowSource,
            IVisibleSliceThrusterStatusReader injectedThrusterStatusReader,
            IVisibleSliceReducedEffectsSource injectedReducedEffectsSource,
            VisibleSliceCameraConfiguration injectedConfiguration)
        {
            if (cameraToOwn == null)
            {
                throw new ArgumentNullException(nameof(cameraToOwn));
            }

            if (!object.ReferenceEquals(cameraToOwn.gameObject, gameObject))
            {
                throw new ArgumentException(
                    "The visible-slice camera rig may own only the Camera on its own GameObject.",
                    nameof(cameraToOwn));
            }

            if (injectedFollowSource == null)
            {
                throw new ArgumentNullException(nameof(injectedFollowSource));
            }

            if (injectedConfiguration == null)
            {
                throw new ArgumentNullException(nameof(injectedConfiguration));
            }

            ownedCamera = cameraToOwn;
            followSource = injectedFollowSource;
            thrusterStatusReader = injectedThrusterStatusReader;
            reducedEffectsSource = injectedReducedEffectsSource;
            configuration = injectedConfiguration;
            cameraDepth = ownedCamera.transform.position.z;
            ownedCamera.orthographic = true;
            ownedCamera.orthographicSize = configuration.OrthographicSize;
            isConfigured = true;
            hasFrame = false;
            LastFrame = null;

            ApplyFrame(configuration.ReferenceAspect);
        }

        public bool ApplyFrameForResolution(int pixelWidth, int pixelHeight)
        {
            EnsureConfigured();
            return ApplyFrame(configuration.ResolveAspect(pixelWidth, pixelHeight));
        }

        public bool ApplyFrame(float aspect)
        {
            return ApplyFrameInternal(aspect, false);
        }

        private bool ApplyFrameInternal(float aspect, bool smoothRuntimeMotion)
        {
            EnsureConfigured();

            Vector2 actorWorldPosition;
            if (!followSource.TryReadWorldPosition(out actorWorldPosition))
            {
                hasFrame = false;
                LastFrame = null;
                return false;
            }

            ThrusterStatusSnapshot thrusterStatus = thrusterStatusReader == null
                ? null
                : thrusterStatusReader.ReadSnapshot();
            VisibleSliceCameraFrame frame = VisibleSliceCameraFrameSolver.Solve(
                configuration,
                actorWorldPosition,
                thrusterStatus,
                aspect);

            Transform cameraTransform = ownedCamera.transform;
            Vector2 appliedCenter = frame.Center;
            if (smoothRuntimeMotion && hasFrame)
            {
                float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
                float blend = deltaTime <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-RuntimeFollowSharpness * deltaTime);
                appliedCenter = Vector2.Lerp(
                    new Vector2(cameraTransform.position.x, cameraTransform.position.y),
                    frame.Center,
                    blend);
            }
            cameraTransform.position = new Vector3(
                appliedCenter.x,
                appliedCenter.y,
                cameraDepth);
            ownedCamera.orthographic = true;
            ownedCamera.orthographicSize = configuration.OrthographicSize;
            LastFrame = frame;
            hasFrame = true;
            return true;
        }

        /// <summary>
        /// Resets presentation-only frame history and deterministically reapplies the
        /// reference framing. It does not reset or write the followed actor.
        /// </summary>
        public bool Restart()
        {
            EnsureConfigured();
            restartGeneration = restartGeneration == long.MaxValue
                ? long.MaxValue
                : restartGeneration + 1L;
            hasFrame = false;
            LastFrame = null;
            return ApplyFrame(configuration.ReferenceAspect);
        }

        public VisibleSliceWarningPresentation ProjectWarning(
            Vector3 warningWorldPosition,
            VisibleSliceWarningSignal signal)
        {
            EnsureConfigured();
            return VisibleSliceWarningProjector.ProjectWorld(
                ownedCamera,
                warningWorldPosition,
                signal,
                configuration,
                ReducedEffectsEnabled);
        }

        private void LateUpdate()
        {
            if (!isConfigured
                || ownedCamera == null
                || followSource == null
                || configuration == null)
            {
                return;
            }

            ApplyFrameInternal(ownedCamera.aspect, true);
        }

        private void OnDestroy()
        {
            isConfigured = false;
            hasFrame = false;
            LastFrame = null;
            ownedCamera = null;
            followSource = null;
            thrusterStatusReader = null;
            reducedEffectsSource = null;
            configuration = null;
        }

        private void EnsureConfigured()
        {
            if (!isConfigured
                || ownedCamera == null
                || followSource == null
                || configuration == null)
            {
                throw new InvalidOperationException(
                    "Visible-slice camera rig must be explicitly configured before use.");
            }
        }
    }
}
