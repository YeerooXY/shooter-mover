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
    public static class BuiltInCharacterSelectionCatalog
    {
        public static CharacterSelectionCatalog Create()
        {
            StableId vanguardCharacter =
                StableId.Parse("character.frontier-vanguard");
            StableId customCharacter =
                StableId.Parse("character.custom-pilot");

            var characters = new List<CharacterSelectionDefinition>
            {
                new CharacterSelectionDefinition(
                    vanguardCharacter,
                    "Frontier Vanguard",
                    "A prepared expedition pilot with a stable profile identity.",
                    StableId.Parse("loadout-profile.frontier-vanguard-aggressive"),
                    CharacterVisual(
                        "CharacterSelect/character_choice_screen",
                        "visual-variant.frontier-vanguard-base",
                        "body-variant.frontier-vanguard")),
                new CharacterSelectionDefinition(
                    customCharacter,
                    "Custom Pilot",
                    "A reusable blank pilot identity for later body and armor authoring.",
                    StableId.Parse("loadout-profile.custom-pilot-aggressive"),
                    CharacterVisual(
                        "CharacterSelect/character_creation_choice_screen",
                        "visual-variant.custom-pilot-base",
                        "body-variant.custom-pilot")),
            };

            var profiles = new List<CharacterClassProfileDefinition>();
            AddProfiles(
                profiles,
                vanguardCharacter,
                "frontier-vanguard");
            AddProfiles(
                profiles,
                customCharacter,
                "custom-pilot");

            CharacterSelectionCatalogResult result =
                CharacterSelectionCatalog.TryCreate(
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
            ICollection<CharacterClassProfileDefinition> profiles,
            StableId characterStableId,
            string identitySuffix)
        {
            profiles.Add(new CharacterClassProfileDefinition(
                StableId.Parse(
                    "loadout-profile." + identitySuffix + "-aggressive"),
                characterStableId,
                CharacterClassKind.Aggressive,
                "Aggressive",
                "A direct pressure profile for offense-focused future consumers.",
                ClassVisual(
                    "CharacterSelect/aggressive_class",
                    "visual-variant.class-aggressive")));

            profiles.Add(new CharacterClassProfileDefinition(
                StableId.Parse(
                    "loadout-profile." + identitySuffix + "-defensive"),
                characterStableId,
                CharacterClassKind.Defensive,
                "Defensive",
                "A resilient profile for defense-focused future consumers.",
                ClassVisual(
                    "CharacterSelect/defensive_class",
                    "visual-variant.class-defensive")));

            profiles.Add(new CharacterClassProfileDefinition(
                StableId.Parse(
                    "loadout-profile." + identitySuffix + "-healer"),
                characterStableId,
                CharacterClassKind.Healer,
                "Healer",
                "A support profile for healing-focused future consumers.",
                ClassVisual(
                    "CharacterSelect/healer_class",
                    "visual-variant.class-healer")));
        }

        private static CharacterVisualMetadata CharacterVisual(
            string resourceKey,
            string visualVariantStableId,
            string bodyVariantStableId)
        {
            return new CharacterVisualMetadata(
                resourceKey,
                resourceKey,
                StableId.Parse(visualVariantStableId),
                StableId.Parse(bodyVariantStableId),
                null);
        }

        private static CharacterVisualMetadata ClassVisual(
            string resourceKey,
            string visualVariantStableId)
        {
            return new CharacterVisualMetadata(
                resourceKey,
                resourceKey,
                StableId.Parse(visualVariantStableId),
                null,
                null);
        }
    }
}
