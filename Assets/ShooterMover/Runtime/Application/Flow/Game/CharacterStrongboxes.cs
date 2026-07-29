using System;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Composition-only bridge from Results into the currently selected character's
    /// existing BOX authority. Implementations resolve and persist; they do not own BOX,
    /// holdings, wallet, account, or reward state.
    /// </summary>
    public interface ICharacterStrongboxes
    {
        bool TryResolve(
            out StrongboxOpeningActions authority,
            out string rejectionCode);

        bool TryResolveDurableOpeningExecutor(
            out IStrongboxDurableOpeningExecutor executor,
            out string rejectionCode);

        bool TryPersist(
            string strongboxSnapshotFingerprint,
            out string rejectionCode);
    }

    public static class CharacterStrongboxesRegistry
    {
        private static ICharacterStrongboxes current;

        public static bool IsConfigured
        {
            get { return current != null; }
        }

        public static void Configure(
            ICharacterStrongboxes bridge)
        {
            current = bridge
                ?? throw new ArgumentNullException(nameof(bridge));
        }

        public static void Clear(
            ICharacterStrongboxes expected = null)
        {
            if (expected == null || ReferenceEquals(current, expected))
            {
                current = null;
            }
        }

        public static bool TryResolve(
            out StrongboxOpeningActions authority,
            out string rejectionCode)
        {
            authority = null;
            rejectionCode = string.Empty;
            if (current == null)
            {
                rejectionCode = "character-strongbox-bridge-unavailable";
                return false;
            }
            return current.TryResolve(out authority, out rejectionCode)
                && authority != null;
        }

        public static bool TryPersist(
            string strongboxSnapshotFingerprint,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (current == null)
            {
                rejectionCode = "character-strongbox-bridge-unavailable";
                return false;
            }
            return current.TryPersist(
                strongboxSnapshotFingerprint,
                out rejectionCode);
        }

        public static bool TryResolveDurableOpeningExecutor(
            out IStrongboxDurableOpeningExecutor executor,
            out string rejectionCode)
        {
            executor = null;
            rejectionCode = string.Empty;
            if (current == null)
            {
                rejectionCode = "character-strongbox-bridge-unavailable";
                return false;
            }

            return current.TryResolveDurableOpeningExecutor(
                out executor,
                out rejectionCode)
                && executor != null;
        }
    }
}
