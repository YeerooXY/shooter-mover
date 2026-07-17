using System;
using System.Collections.Generic;
using ShooterMover.Domain.Characters.Selection;
using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Characters.Selection
{
    /// <summary>
    /// Vertical-slice character/class content. These are stable presentation/profile
    /// identities only; future gameplay systems may interpret the profile identities.
    /// </summary>
    public static class BuiltInCharacterSelectionCatalogV1
    {
        public static CharacterSelectionCatalogV1 Create()
        {
            StableId vanguardCharacter =
                StableId.Parse("character.frontier-vanguard");
            StableId customCharacter =
                StableId.Parse("character.custom-pilot");

            var characters = new List<CharacterSelectionDefinitionV1>
            {
                new CharacterSelectionDefinitionV1(
                    vanguardCharacter,
                    "Frontier Vanguard",
                    "A prepared expedition pilot with a stable profile identity.",
                    StableId.Parse("loadout-profile.frontier-vanguard-aggressive"),
                    CharacterVisual(
                        "CharacterSelect/character_choice_screen",
                        "visual-variant.frontier-vanguard-base",
                        "body-variant.frontier-vanguard")),
                new CharacterSelectionDefinitionV1(
                    customCharacter,
                    "Custom Pilot",
                    "A reusable blank pilot identity for later body and armor authoring.",
                    StableId.Parse("loadout-profile.custom-pilot-aggressive"),
                    CharacterVisual(
                        "CharacterSelect/character_creation_choice_screen",
                        "visual-variant.custom-pilot-base",
                        "body-variant.custom-pilot")),
            };

            var profiles = new List<CharacterClassProfileDefinitionV1>();
            AddProfiles(
                profiles,
                vanguardCharacter,
                "frontier-vanguard");
            AddProfiles(
                profiles,
                customCharacter,
                "custom-pilot");

            CharacterSelectionCatalogResultV1 result =
                CharacterSelectionCatalogV1.TryCreate(
                    vanguardCharacter,
                    characters,
                    profiles);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "Built-in character catalog is invalid: "
                    + result.RejectionCode);
            }

            return result.Catalog;
        }

        private static void AddProfiles(
            ICollection<CharacterClassProfileDefinitionV1> profiles,
            StableId characterStableId,
            string identitySuffix)
        {
            profiles.Add(new CharacterClassProfileDefinitionV1(
                StableId.Parse(
                    "loadout-profile." + identitySuffix + "-aggressive"),
                characterStableId,
                CharacterClassKindV1.Aggressive,
                "Aggressive",
                "A direct pressure profile for offense-focused future consumers.",
                ClassVisual(
                    "CharacterSelect/aggressive_class",
                    "visual-variant.class-aggressive")));

            profiles.Add(new CharacterClassProfileDefinitionV1(
                StableId.Parse(
                    "loadout-profile." + identitySuffix + "-defensive"),
                characterStableId,
                CharacterClassKindV1.Defensive,
                "Defensive",
                "A resilient profile for defense-focused future consumers.",
                ClassVisual(
                    "CharacterSelect/defensive_class",
                    "visual-variant.class-defensive")));

            profiles.Add(new CharacterClassProfileDefinitionV1(
                StableId.Parse(
                    "loadout-profile." + identitySuffix + "-healer"),
                characterStableId,
                CharacterClassKindV1.Healer,
                "Healer",
                "A support profile for healing-focused future consumers.",
                ClassVisual(
                    "CharacterSelect/healer_class",
                    "visual-variant.class-healer")));
        }

        private static CharacterVisualMetadataV1 CharacterVisual(
            string resourceKey,
            string visualVariantStableId,
            string bodyVariantStableId)
        {
            return new CharacterVisualMetadataV1(
                resourceKey,
                resourceKey,
                StableId.Parse(visualVariantStableId),
                StableId.Parse(bodyVariantStableId),
                null);
        }

        private static CharacterVisualMetadataV1 ClassVisual(
            string resourceKey,
            string visualVariantStableId)
        {
            return new CharacterVisualMetadataV1(
                resourceKey,
                resourceKey,
                StableId.Parse(visualVariantStableId),
                null,
                null);
        }
    }
}
