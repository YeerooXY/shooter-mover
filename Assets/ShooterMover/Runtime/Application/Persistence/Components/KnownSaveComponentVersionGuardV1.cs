using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Components
{
    public static class KnownSaveComponentVersionGuardV1
    {
        public static SaveComponentValidationResultV1 Validate(
            PlayerAccountSnapshotV1 account)
        {
            SaveComponentValidationResultV1 aggregate =
                PlayerAccountAggregateCodecV1.Validate(account);
            if (!aggregate.Succeeded)
            {
                return aggregate;
            }

            for (int index = 0; index < account.CharacterSlots.Count; index++)
            {
                CharacterInstanceSnapshotV1 character =
                    account.CharacterSlots[index];
                if (character == null) continue;
                foreach (SaveComponentSnapshotV1 component in
                    character.Components.Values)
                {
                    SaveComponentValidationResultV1 result =
                        ValidateComponent(component);
                    if (!result.Succeeded) return result;
                }
            }

            foreach (SaveComponentSnapshotV1 component in
                account.AccountComponents.Values)
            {
                SaveComponentValidationResultV1 result =
                    ValidateComponent(component);
                if (!result.Succeeded) return result;
            }
            return SaveComponentValidationResultV1.Accept();
        }

        public static SaveComponentValidationResultV1 ValidateComponent(
            SaveComponentSnapshotV1 component)
        {
            if (component == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "save-component-null");
            }
            SaveComponentDefinitionV1 expected = FindKnown(
                component.ComponentStableId);
            if (expected == null)
            {
                return SaveComponentValidationResultV1.Accept();
            }
            if (component.SchemaVersion != expected.SchemaVersion
                || !string.Equals(
                    component.ContentVersion,
                    expected.ContentVersion,
                    StringComparison.Ordinal))
            {
                return SaveComponentValidationResultV1.Reject(
                    "known-save-component-version-unsupported:"
                        + component.ComponentStableId);
            }
            return SaveComponentValidationResultV1.Accept();
        }

        private static SaveComponentDefinitionV1 FindKnown(StableId id)
        {
            SaveComponentDefinitionV1[] known =
            {
                KnownSaveComponentDefinitionsV1.PlayerExperience(),
                KnownSaveComponentDefinitionsV1.PlayerHoldings(),
                KnownSaveComponentDefinitionsV1.MoneyWallet(),
                KnownSaveComponentDefinitionsV1.ScrapWallet(),
                KnownSaveComponentDefinitionsV1.RankedSkillAllocation(),
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(),
                KnownSaveComponentDefinitionsV1.StrongboxState(),
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
