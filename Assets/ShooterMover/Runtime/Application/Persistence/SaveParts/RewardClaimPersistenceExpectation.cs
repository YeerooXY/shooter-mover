using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.SaveParts
{
    /// <summary>
    /// Scoped exact-component expectation consumed by the existing account validator.
    /// GameSaveFile validates the temporary candidate with this expectation
    /// before replacing the active file, and validates the active read-back while the same
    /// scope is still installed.
    /// </summary>
    public static class RewardClaimPersistenceExpectation
    {
        private sealed class Expectation
        {
            public StableId CharacterStableId;
            public Dictionary<StableId, string> ComponentFingerprints;
        }

        private sealed class Scope : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                lock (Gate)
                {
                    Current = null;
                }
            }
        }

        private static readonly object Gate = new object();
        private static Expectation Current;

        public static IDisposable Begin(
            StableId selectedCharacterStableId,
            IDictionary<StableId, string> exactComponentFingerprints)
        {
            if (selectedCharacterStableId == null)
                throw new ArgumentNullException(nameof(selectedCharacterStableId));
            if (exactComponentFingerprints == null || exactComponentFingerprints.Count == 0)
                throw new ArgumentException("At least one exact transfer component expectation is required.", nameof(exactComponentFingerprints));

            var copy = new Dictionary<StableId, string>();
            foreach (KeyValuePair<StableId, string> pair in exactComponentFingerprints)
            {
                if (pair.Key == null || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("Expected component identities and fingerprints must be non-empty.", nameof(exactComponentFingerprints));
                copy.Add(pair.Key, pair.Value.Trim());
            }

            lock (Gate)
            {
                if (Current != null)
                    throw new InvalidOperationException("A collected-run persistence expectation is already active.");
                Current = new Expectation
                {
                    CharacterStableId = selectedCharacterStableId,
                    ComponentFingerprints = copy,
                };
            }
            return new Scope();
        }

        public static SavePartValidationResult Validate(
            PlayerAccountSnapshot account)
        {
            Expectation expectation;
            lock (Gate)
            {
                expectation = Current;
            }
            if (expectation == null)
                return SavePartValidationResult.Accept();
            if (account == null)
                return SavePartValidationResult.Reject(
                    "collected-run-persistence-expected-account-null");

            CharacterInstanceSnapshot character = null;
            for (int index = 0; index < account.CharacterSlots.Count; index++)
            {
                CharacterInstanceSnapshot candidate = account.CharacterSlots[index];
                if (candidate != null
                    && candidate.CharacterInstanceStableId == expectation.CharacterStableId)
                {
                    character = candidate;
                    break;
                }
            }
            if (character == null)
                return SavePartValidationResult.Reject(
                    "collected-run-persistence-expected-character-missing:"
                    + expectation.CharacterStableId);

            foreach (KeyValuePair<StableId, string> pair in expectation.ComponentFingerprints)
            {
                SavePartSnapshot component;
                if (!character.TryGetComponent(pair.Key, out component)
                    || component == null)
                {
                    return SavePartValidationResult.Reject(
                        "collected-run-persistence-expected-component-missing:"
                        + pair.Key);
                }
                if (!string.Equals(
                    component.Fingerprint,
                    pair.Value,
                    StringComparison.Ordinal))
                {
                    return SavePartValidationResult.Reject(
                        "collected-run-persistence-expected-component-mismatch:"
                        + pair.Key);
                }
            }
            return SavePartValidationResult.Accept();
        }
    }
}
