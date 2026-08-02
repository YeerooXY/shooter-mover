using System;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.SaveParts
{
    public static class KnownSavePartVersionGuard
    {
        /// <summary>
        /// Validates aggregate account integrity first, then validates every known
        /// component wrapper/content version. Unknown component identities remain
        /// opaque and accepted.
        /// </summary>
        public static SavePartValidationResult Validate(
            PlayerAccountSnapshot account)
        {
            SavePartValidationResult aggregate =
                PlayerAccountAggregateCodec.Validate(account);
            if (!aggregate.Succeeded)
            {
                return aggregate;
            }

            return ValidateKnownComponents(account);
        }

        /// <summary>
        /// Validates known component versions and any currently scoped exact transfer
        /// expectation. GameSaveFile invokes this both for the temporary
        /// candidate and the active read-back, so receipt/custody verification happens
        /// inside the atomic replacement protocol rather than after it.
        /// </summary>
        public static SavePartValidationResult ValidateKnownComponents(
            PlayerAccountSnapshot account)
        {
            if (account == null)
            {
                return SavePartValidationResult.Reject(
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

                foreach (SavePartSnapshot component in
                    character.Components.Values)
                {
                    SavePartValidationResult result =
                        ValidateComponent(component);
                    if (!result.Succeeded)
                    {
                        return result;
                    }
                }
            }

            foreach (SavePartSnapshot component in
                account.AccountComponents.Values)
            {
                SavePartValidationResult result =
                    ValidateComponent(component);
                if (!result.Succeeded)
                {
                    return result;
                }
            }

            return RewardClaimPersistenceExpectation.Validate(account);
        }

        public static SavePartValidationResult ValidateComponent(
            SavePartSnapshot component)
        {
            if (component == null)
            {
                return SavePartValidationResult.Reject(
                    "save-part-null");
            }

            SavePartDefinition expected = FindKnown(
                component.ComponentStableId);
            if (expected == null)
            {
                return SavePartValidationResult.Accept();
            }

            if (component.SchemaVersion != expected.SchemaVersion
                || !string.Equals(
                    component.ContentVersion,
                    expected.ContentVersion,
                    StringComparison.Ordinal))
            {
                return SavePartValidationResult.Reject(
                    "known-save-part-version-unsupported:"
                        + component.ComponentStableId);
            }

            return SavePartValidationResult.Accept();
        }

        private static SavePartDefinition FindKnown(StableId id)
        {
            SavePartDefinition[] known =
            {
                GameSaveParts.PlayerExperience(),
                GameSaveParts.PlayerHoldings(),
                GameSaveParts.MoneyWallet(),
                GameSaveParts.ScrapWallet(),
                GameSaveParts.RankedSkillAllocation(),
                GunAugmentSavePart.Definition(),
                GameSaveParts.StrongboxState(),
                RewardClaimPreparedTransferSavePart.Definition(),
                RewardClaimTransferReceiptSavePart.Definition(),
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
