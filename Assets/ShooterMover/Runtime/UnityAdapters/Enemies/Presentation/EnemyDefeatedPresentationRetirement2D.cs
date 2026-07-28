using System;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies.Presentation
{
    /// <summary>
    /// Keeps an already-terminal room-owned enemy presentation alive long enough for its
    /// normal terminal projection to render. It owns no death, collision, reward, room-clear,
    /// or persistence state; those authorities commit before this handoff begins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDefeatedPresentationRetirement2D :
        MonoBehaviour,
        IRoomDefeatedPresentationRetirement2D
    {
        [SerializeField] private float retirementSeconds = 0.35f;

        private Action release;
        private float remainingSeconds;
        private bool retiring;

        public float RetirementSeconds { get { return retirementSeconds; } }

        public bool IsRetiring { get { return retiring; } }

        public bool TryValidate(out string reason)
        {
            if (float.IsNaN(retirementSeconds)
                || float.IsInfinity(retirementSeconds)
                || retirementSeconds <= 0f)
            {
                reason = "Enemy defeated-presentation retirement duration must be finite and positive.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        bool IRoomDefeatedPresentationRetirement2D.TryBeginRetirement(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (retiring || !isActiveAndEnabled) return false;

            string reason;
            if (!TryValidate(out reason))
            {
                throw new InvalidOperationException(reason);
            }

            release = callback;
            remainingSeconds = retirementSeconds;
            retiring = true;
            return true;
        }

        private void Update()
        {
            if (!retiring) return;

            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (remainingSeconds > 0f) return;

            Action callback = release;
            release = null;
            retiring = false;
            if (callback != null)
            {
                callback();
            }
        }

        private void OnDisable()
        {
            release = null;
            remainingSeconds = 0f;
            retiring = false;
        }
    }
}
