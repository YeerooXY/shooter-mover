using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    [CreateAssetMenu(
        fileName = "DestructiblePropDestructionAnimation",
        menuName = "Shooter Mover/Props/Destruction Animation")]
    public sealed class DestructiblePropDestructionAnimation : ScriptableObject
    {
        public const float MinimumFrameSeconds = 0.01f;
        public const float MaximumFrameSeconds = 2f;

        [Tooltip("Ordered from the first destruction frame to the final frame.")]
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField] private float secondsPerFrame = 0.06f;
        [SerializeField] private Vector2 localOffset = Vector2.zero;
        [SerializeField] private Vector2 visualScale = Vector2.one;
        [SerializeField] private int sortingOrder = 50;
        [SerializeField] private bool useUnscaledTime;

        public int FrameCount => frames == null ? 0 : frames.Length;

        public float SecondsPerFrame => secondsPerFrame;

        public Vector2 LocalOffset => localOffset;

        public Vector2 VisualScale => visualScale;

        public int SortingOrder => sortingOrder;

        public bool UseUnscaledTime => useUnscaledTime;

        public bool HasFrames
        {
            get
            {
                if (frames == null || frames.Length == 0)
                {
                    return false;
                }

                for (int index = 0; index < frames.Length; index++)
                {
                    if (frames[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public Sprite GetFrame(int index)
        {
            if (frames == null || index < 0 || index >= frames.Length)
            {
                return null;
            }

            return frames[index];
        }

        public static DestructiblePropDestructionAnimation CreateRuntime(
            Sprite[] configuredFrames,
            float configuredSecondsPerFrame,
            Vector2 configuredLocalOffset,
            Vector2 configuredVisualScale,
            int configuredSortingOrder = 50,
            bool configuredUseUnscaledTime = false)
        {
            Validate(
                configuredFrames,
                configuredSecondsPerFrame,
                configuredVisualScale);

            DestructiblePropDestructionAnimation animation =
                CreateInstance<DestructiblePropDestructionAnimation>();
            animation.frames = (Sprite[])configuredFrames.Clone();
            animation.secondsPerFrame = configuredSecondsPerFrame;
            animation.localOffset = configuredLocalOffset;
            animation.visualScale = configuredVisualScale;
            animation.sortingOrder = configuredSortingOrder;
            animation.useUnscaledTime = configuredUseUnscaledTime;
            animation.hideFlags = HideFlags.HideAndDontSave;
            return animation;
        }

        private void OnValidate()
        {
            secondsPerFrame = Mathf.Clamp(
                secondsPerFrame,
                MinimumFrameSeconds,
                MaximumFrameSeconds);
            visualScale.x = Mathf.Max(0.001f, visualScale.x);
            visualScale.y = Mathf.Max(0.001f, visualScale.y);
            if (frames == null)
            {
                frames = Array.Empty<Sprite>();
            }
        }

        private static void Validate(
            Sprite[] configuredFrames,
            float configuredSecondsPerFrame,
            Vector2 configuredVisualScale)
        {
            if (configuredFrames == null)
            {
                throw new ArgumentNullException(nameof(configuredFrames));
            }

            if (float.IsNaN(configuredSecondsPerFrame)
                || float.IsInfinity(configuredSecondsPerFrame)
                || configuredSecondsPerFrame < MinimumFrameSeconds
                || configuredSecondsPerFrame > MaximumFrameSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredSecondsPerFrame));
            }

            if (!IsPositiveFinite(configuredVisualScale.x)
                || !IsPositiveFinite(configuredVisualScale.y))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredVisualScale));
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
