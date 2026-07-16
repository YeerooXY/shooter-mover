using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    [DisallowMultipleComponent]
    public sealed class DestructiblePropDestructionPlayer2D : MonoBehaviour
    {
        private DestructibleProp2D prop;
        private Transform visualAnchor;
        private DestructiblePropDestructionAnimation animation;
        private GameObject effectRoot;
        private SpriteRenderer effectRenderer;
        private int frameIndex;
        private float frameElapsedSeconds;
        private bool playing;

        public bool IsConfigured => prop != null;

        public bool IsPlaying => playing;

        public int CurrentFrameIndex => frameIndex;

        public SpriteRenderer EffectRenderer => effectRenderer;

        public void Configure(
            DestructibleProp2D configuredProp,
            Transform configuredVisualAnchor,
            DestructiblePropDestructionAnimation configuredAnimation)
        {
            if (IsConfigured)
            {
                return;
            }

            if (configuredProp == null || configuredVisualAnchor == null)
            {
                return;
            }

            prop = configuredProp;
            visualAnchor = configuredVisualAnchor;
            animation = configuredAnimation;
            prop.Destroyed += HandleDestroyed;
            prop.Restarted += HandleRestarted;
        }

        private void Update()
        {
            if (!playing || animation == null || effectRenderer == null)
            {
                return;
            }

            frameElapsedSeconds += animation.UseUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            while (frameElapsedSeconds >= animation.SecondsPerFrame && playing)
            {
                frameElapsedSeconds -= animation.SecondsPerFrame;
                AdvanceFrame();
            }
        }

        private void HandleDestroyed(DestructiblePropDestructionResult ignored)
        {
            Play();
        }

        private void HandleRestarted()
        {
            StopAndClear();
        }

        private void Play()
        {
            StopAndClear();
            if (animation == null || !animation.HasFrames || visualAnchor == null)
            {
                return;
            }

            effectRoot = new GameObject(name + " Destruction Animation");
            effectRoot.transform.SetParent(transform, false);
            effectRoot.transform.position =
                visualAnchor.position + (Vector3)animation.LocalOffset;
            effectRoot.transform.rotation = visualAnchor.rotation;
            effectRoot.transform.localScale = new Vector3(
                animation.VisualScale.x,
                animation.VisualScale.y,
                1f);
            effectRenderer = effectRoot.AddComponent<SpriteRenderer>();
            effectRenderer.sortingOrder = animation.SortingOrder;
            frameIndex = 0;
            frameElapsedSeconds = 0f;
            playing = true;
            ShowCurrentFrame();
        }

        private void AdvanceFrame()
        {
            frameIndex++;
            if (frameIndex >= animation.FrameCount)
            {
                StopAndClear();
                return;
            }

            ShowCurrentFrame();
        }

        private void ShowCurrentFrame()
        {
            if (effectRenderer != null)
            {
                effectRenderer.sprite = animation.GetFrame(frameIndex);
            }
        }

        private void StopAndClear()
        {
            playing = false;
            frameIndex = 0;
            frameElapsedSeconds = 0f;
            effectRenderer = null;
            if (effectRoot != null)
            {
                Destroy(effectRoot);
                effectRoot = null;
            }
        }

        private void OnDestroy()
        {
            if (prop != null)
            {
                prop.Destroyed -= HandleDestroyed;
                prop.Restarted -= HandleRestarted;
            }

            StopAndClear();
            prop = null;
            visualAnchor = null;
            animation = null;
        }
    }
}
