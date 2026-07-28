using System;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Default collection feedback for registry-sprite pickups. It animates presentation
    /// only and invokes completion exactly once so the presenter can retire the view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunPickupTransformAcceptedFeedback2D : MonoBehaviour,
        IRunRewardPickupAcceptedFeedbackV1
    {
        private const float DurationSeconds = 0.24f;

        private SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        private Color[] startingColors = Array.Empty<Color>();
        private Action completion;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private Vector3 startScale;
        private float elapsed;
        private bool playing;

        public bool IsPlaying { get { return playing; } }

        public bool TryPlayAcceptedCollectionFeedback(
            Transform attractionTarget,
            Action completed)
        {
            if (playing || attractionTarget == null || completed == null)
            {
                return false;
            }

            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            startingColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                startingColors[index] = renderers[index] == null
                    ? Color.white
                    : renderers[index].color;
            }

            completion = completed;
            startPosition = transform.position;
            targetPosition = attractionTarget.position;
            startScale = transform.localScale;
            elapsed = 0f;
            playing = true;
            return true;
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / DurationSeconds);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
            transform.localScale = startScale * Mathf.Lerp(1f, 0.2f, eased);

            for (int index = 0; index < renderers.Length; index++)
            {
                SpriteRenderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }
                Color color = startingColors[index];
                color.a *= 1f - progress;
                renderer.color = color;
            }

            if (progress < 1f)
            {
                return;
            }

            playing = false;
            Action handler = completion;
            completion = null;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
