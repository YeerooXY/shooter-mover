using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Progression.Context;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.Application.Progression.Context
{
    /// <summary>
    /// Fixed provider for direct simulation, tests, and explicitly authored contexts.
    /// </summary>
    public sealed class DirectProgressionContextProvider : IProgressionContextProvider
    {
        public DirectProgressionContextProvider(ProgressionContext context)
        {
            CurrentContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ProgressionContext CurrentContext { get; }
    }

    /// <summary>
    /// Mutable session provider whose state changes only through explicit validated replacement.
    /// </summary>
    public sealed class SessionProgressionContextProvider : IProgressionContextProvider
    {
        private readonly object _sync = new object();
        private ProgressionContextSnapshot _currentSnapshot;

        public SessionProgressionContextProvider(ProgressionContext initialContext)
        {
            if (initialContext == null)
            {
                throw new ArgumentNullException(nameof(initialContext));
            }

            _currentSnapshot = ProgressionContextSnapshot.Create(0, initialContext);
        }

        public ProgressionContext CurrentContext
        {
            get
            {
                lock (_sync)
                {
                    return _currentSnapshot.Context;
                }
            }
        }

        public ProgressionContextSnapshot CurrentSnapshot
        {
            get
            {
                lock (_sync)
                {
                    return _currentSnapshot;
                }
            }
        }

        /// <summary>
        /// Validates raw explicit values, then replaces the current context if they differ.
        /// </summary>
        public ProgressionContextChangeFact TryReplace(
            int characterLevel,
            int regionLevel,
            StableId difficultyId,
            int difficultyValue,
            IEnumerable<StableId> progressionTags = null)
        {
            ProgressionContext replacement;
            ProgressionContextValidationResult validation;
            if (!ProgressionContext.TryCreate(
                characterLevel,
                regionLevel,
                difficultyId,
                difficultyValue,
                progressionTags,
                out replacement,
                out validation))
            {
                lock (_sync)
                {
                    return ProgressionContextChangeFact.Rejected(
                        _currentSnapshot,
                        validation);
                }
            }

            return TryReplace(replacement);
        }

        /// <summary>
        /// Replaces the current immutable context, or returns an explicit no-change/rejection fact.
        /// </summary>
        public ProgressionContextChangeFact TryReplace(ProgressionContext replacement)
        {
            lock (_sync)
            {
                if (replacement == null)
                {
                    return ProgressionContextChangeFact.Rejected(
                        _currentSnapshot,
                        ProgressionContextValidationResult.Failure(
                            ProgressionContextValidationCode.ContextMissing,
                            nameof(replacement),
                            "Replacement progression context is required."));
                }

                if (_currentSnapshot.Context.Equals(replacement))
                {
                    return ProgressionContextChangeFact.DuplicateNoChange(_currentSnapshot);
                }

                if (_currentSnapshot.Sequence == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Progression-context replacement sequence is exhausted.");
                }

                ProgressionContextSnapshot previousSnapshot = _currentSnapshot;
                ProgressionContextSnapshot nextSnapshot = ProgressionContextSnapshot.Create(
                    previousSnapshot.Sequence + 1,
                    replacement);
                _currentSnapshot = nextSnapshot;
                return ProgressionContextChangeFact.Applied(
                    previousSnapshot,
                    nextSnapshot);
            }
        }
    }
}
