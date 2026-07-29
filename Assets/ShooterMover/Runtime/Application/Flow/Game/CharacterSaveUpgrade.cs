using System;
using System.Collections.Generic;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class CharacterSaveUpgradeResult
    {
        internal CharacterSaveUpgradeResult(
            bool succeeded,
            bool changed,
            int migratedCharacterCount,
            string diagnostic,
            PlayerAccountSnapshot account)
        {
            Succeeded = succeeded;
            Changed = changed;
            MigratedCharacterCount = migratedCharacterCount;
            Diagnostic = diagnostic ?? string.Empty;
            Account = account;
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public int MigratedCharacterCount { get; }
        public string Diagnostic { get; }
        public PlayerAccountSnapshot Account { get; }
    }

    /// <summary>
    /// Versioned, additive migration for account-backed characters created before a newly required
    /// save part existed. Existing component payloads are never replaced. Missing required
    /// components are exported from the normal starter graph for the exact character identity,
    /// then the complete aggregate is validated before the caller may publish or persist it.
    /// </summary>
    public static class CharacterSaveUpgrade
    {
        public const int MigrationVersion = 1;

        public static CharacterSaveUpgradeResult Migrate(
            PlayerAccountSnapshot account,
            IStarterCharacterLiveGraphFactory starterFactory)
        {
            if (account == null)
            {
                return Failure(
                    "required-character-component-backfill-account-null",
                    null);
            }
            if (starterFactory == null)
            {
                return Failure(
                    "required-character-component-backfill-factory-null",
                    account);
            }

            var coordinatorRequiredIds = new HashSet<StableId>(
                CharacterSetupFlow.RequiredCharacterComponentIds);
            PlayerAccountSnapshot next = account;
            int migratedCharacters = 0;
            for (int slotIndex = 0;
                 slotIndex < PlayerAccountSnapshot.CharacterSlotCount;
                 slotIndex++)
            {
                CharacterInstanceSnapshot character = next.CharacterAt(slotIndex);
                if (character == null)
                {
                    continue;
                }

                ICharacterLiveGraph starter = null;
                try
                {
                    starter = starterFactory.CreateStarter(
                        slotIndex,
                        character.CharacterInstanceStableId,
                        character.ClassDefinitionStableId,
                        character.DisplayName,
                        null);
                    if (starter == null
                        || starter.IsDisposed
                        || starter.SaveAdapters == null)
                    {
                        return Failure(
                            "required-character-component-backfill-starter-invalid:"
                                + slotIndex,
                            account);
                    }

                    var missing = new List<SavePartSnapshot>();
                    var seen = new HashSet<StableId>();
                    for (int adapterIndex = 0;
                         adapterIndex < starter.SaveAdapters.Count;
                         adapterIndex++)
                    {
                        ISavePart adapter =
                            starter.SaveAdapters[adapterIndex];
                        if (adapter == null || adapter.Definition == null)
                        {
                            return Failure(
                                "required-character-component-backfill-adapter-null:"
                                    + slotIndex,
                                account);
                        }

                        StableId componentId =
                            adapter.Definition.ComponentStableId;
                        bool required = adapter.Definition.IsRequired
                            || coordinatorRequiredIds.Contains(componentId);
                        if (!required)
                        {
                            continue;
                        }
                        if (!seen.Add(componentId))
                        {
                            return Failure(
                                "required-character-component-backfill-adapter-duplicate:"
                                    + componentId,
                                account);
                        }

                        SavePartSnapshot ignored;
                        if (character.TryGetComponent(componentId, out ignored))
                        {
                            continue;
                        }
                        missing.Add(adapter.ExportComponent());
                    }

                    if (missing.Count == 0)
                    {
                        continue;
                    }
                    missing.Sort(delegate(
                        SavePartSnapshot left,
                        SavePartSnapshot right)
                    {
                        return string.CompareOrdinal(
                            left.ComponentStableId.ToString(),
                            right.ComponentStableId.ToString());
                    });

                    CharacterInstanceSnapshot migrated = character;
                    for (int componentIndex = 0;
                         componentIndex < missing.Count;
                         componentIndex++)
                    {
                        migrated = migrated.WithComponent(missing[componentIndex]);
                    }

                    for (int requiredIndex = 0;
                         requiredIndex
                            < CharacterSetupFlow
                                .RequiredCharacterComponentIds.Count;
                         requiredIndex++)
                    {
                        StableId requiredId = CharacterSetupFlow
                            .RequiredCharacterComponentIds[requiredIndex];
                        SavePartSnapshot ignored;
                        if (!migrated.TryGetComponent(requiredId, out ignored))
                        {
                            return Failure(
                                "required-character-component-backfill-source-missing:"
                                    + slotIndex
                                    + ":"
                                    + requiredId,
                                account);
                        }
                    }

                    SavePartValidationResult characterValidation =
                        GameSaveRules.ValidateCharacter(migrated);
                    if (characterValidation == null
                        || !characterValidation.Succeeded)
                    {
                        return Failure(
                            "required-character-component-backfill-character-invalid:"
                                + slotIndex
                                + ":"
                                + (characterValidation == null
                                    ? "result-null"
                                    : characterValidation.RejectionCode),
                            account);
                    }

                    next = next.WithCharacter(slotIndex, migrated);
                    migratedCharacters++;
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception))
                    {
                        throw;
                    }
                    return Failure(
                        "required-character-component-backfill-threw:"
                            + slotIndex
                            + ":"
                            + DescribeException(exception),
                        account);
                }
                finally
                {
                    if (starter != null)
                    {
                        starter.Dispose();
                    }
                }
            }

            SavePartValidationResult aggregateValidation =
                GameSaveRules.Validate(next);
            if (aggregateValidation == null || !aggregateValidation.Succeeded)
            {
                return Failure(
                    "required-character-component-backfill-account-invalid:"
                        + (aggregateValidation == null
                            ? "result-null"
                            : aggregateValidation.RejectionCode),
                    account);
            }

            return new CharacterSaveUpgradeResult(
                true,
                migratedCharacters > 0,
                migratedCharacters,
                string.Empty,
                next);
        }

        private static CharacterSaveUpgradeResult Failure(
            string diagnostic,
            PlayerAccountSnapshot account)
        {
            return new CharacterSaveUpgradeResult(
                false,
                false,
                0,
                diagnostic,
                account);
        }

        private static string DescribeException(Exception exception)
        {
            Exception root = exception == null
                ? null
                : exception.GetBaseException() ?? exception;
            if (root == null)
            {
                return "Exception";
            }
            string description = exception.GetType().Name;
            if (!ReferenceEquals(root, exception))
            {
                description += "->" + root.GetType().Name;
            }
            return string.IsNullOrWhiteSpace(root.Message)
                ? description
                : description + ":" + root.Message.Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .Trim();
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}
