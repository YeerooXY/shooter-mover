using System;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Components
{
    public static class KnownSaveComponentVersionGuard
    {
        /// <summary>
        /// Validates aggregate account integrity first, then validates every known
        /// component wrapper/content version. Unknown component identities remain
        /// opaque and accepted.
        /// </summary>
        public static SaveComponentValidationResult Validate(
            PlayerAccountSnapshot account)
        {
            SaveComponentValidationResult aggregate =
                PlayerAccountAggregateCodec.Validate(account);
            if (!aggregate.Succeeded)
            {
                return aggregate;
            }

            return ValidateKnownComponents(account);
        }

        /// <summary>
        /// Validates known component versions and any currently scoped exact transfer
        /// expectation. AtomicPlayerAccountStore invokes this both for the temporary
        /// candidate and the active read-back, so receipt/custody verification happens
        /// inside the atomic replacement protocol rather than after it.
        /// </summary>
        public static SaveComponentValidationResult ValidateKnownComponents(
            PlayerAccountSnapshot account)
        {
            if (account == null)
            {
                return SaveComponentValidationResult.Reject(
                    "account-snapshot-null");
            }

            for (int index = 0; index < account.CharacterSlots.Count; index++)
            {
                CharacterInstanceSnapshot character =
                    account.CharacterSlots[index];
                if (character == null)
                {
                    continue;
                }

                foreach (SaveComponentSnapshot component in
                    character.Components.Values)
                {
                    SaveComponentValidationResult result =
                        ValidateComponent(component);
                    if (!result.Succeeded)
                    {
                        return result;
                    }
                }
            }

            foreach (SaveComponentSnapshot component in
                account.AccountComponents.Values)
            {
                SaveComponentValidationResult result =
                    ValidateComponent(component);
                if (!result.Succeeded)
                {
                    return result;
                }
            }

            return CollectedRunRewardPersistenceExpectation.Validate(account);
        }

        public static SaveComponentValidationResult ValidateComponent(
            SaveComponentSnapshot component)
        {
            if (component == null)
            {
                return SaveComponentValidationResult.Reject(
                    "save-component-null");
            }

            SaveComponentDefinition expected = FindKnown(
                component.ComponentStableId);
            if (expected == null)
            {
                return SaveComponentValidationResult.Accept();
            }

            if (component.SchemaVersion != expected.SchemaVersion
                || !string.Equals(
                    component.ContentVersion,
                    expected.ContentVersion,
                    StringComparison.Ordinal))
            {
                return SaveComponentValidationResult.Reject(
                    "known-save-component-version-unsupported:"
                        + component.ComponentStableId);
            }

            return SaveComponentValidationResult.Accept();
        }

        private static SaveComponentDefinition FindKnown(StableId id)
        {
            SaveComponentDefinition[] known =
            {
                KnownSaveComponentDefinitions.PlayerExperience(),
                KnownSaveComponentDefinitions.PlayerHoldings(),
                KnownSaveComponentDefinitions.MoneyWallet(),
                KnownSaveComponentDefinitions.ScrapWallet(),
                KnownSaveComponentDefinitions.RankedSkillAllocation(),
                KnownSaveComponentDefinitions.ExactInstanceLoadout(),
                GeneratedEquipmentAugmentSignatureSaveComponent.Definition(),
                KnownSaveComponentDefinitions.StrongboxState(),
                CollectedRunRewardPreparedTransferSaveComponent.Definition(),
                CollectedRunRewardTransferReceiptSaveComponent.Definition(),
            };
            for (int index = 0; index < known.Length; index++)
            {
                if (known[index].ComponentStableId == id)
                {
                    return known[index];
                }
            }

            return null;
        }
    }
}
