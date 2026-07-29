using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Flow.PlayModes
{
    [Serializable]
    public sealed class PlayModeDefinitionRecord
    {
        [SerializeField]
        private string modeStableId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        [TextArea]
        private string description = string.Empty;

        [SerializeField]
        private PlayModeAvailability availability =
            PlayModeAvailability.PrototypeUnavailable;

        [SerializeField]
        private PlayModeDestination destination = PlayModeDestination.None;

        [SerializeField]
        private int sortOrder;

        public PlayModeDefinition Build()
        {
            StableId parsedModeStableId;
            if (!StableId.TryParse(modeStableId, out parsedModeStableId))
            {
                throw new InvalidOperationException(
                    "Play mode identity is missing or malformed: " + modeStableId);
            }

            return new PlayModeDefinition(
                parsedModeStableId,
                displayName,
                description,
                availability,
                destination,
                sortOrder);
        }
    }

    [CreateAssetMenu(
        fileName = "PlayModeCatalog",
        menuName = "Shooter Mover/Flow/Play Mode Catalog V1")]
    public sealed class PlayModeCatalogDefinition : ScriptableObject
    {
        [SerializeField]
        private List<PlayModeDefinitionRecord> playModes =
            new List<PlayModeDefinitionRecord>();

        public PlayModeCatalog BuildCatalog()
        {
            var definitions = new List<PlayModeDefinition>(playModes.Count);
            for (int index = 0; index < playModes.Count; index++)
            {
                PlayModeDefinitionRecord record = playModes[index];
                if (record == null)
                {
                    throw new InvalidOperationException(
                        "Play mode catalog entries cannot contain null.");
                }

                definitions.Add(record.Build());
            }

            return new PlayModeCatalog(definitions);
        }

        public static PlayModeCatalog CreateDefaultCatalog()
        {
            return new PlayModeCatalog(new[]
            {
                new PlayModeDefinition(
                    StableId.Parse(PlaySelectionActions.SoloModeStableIdText),
                    "SOLO",
                    "Continue alone to level selection with the current profile and loadout.",
                    PlayModeAvailability.Available,
                    PlayModeDestination.LevelSelection,
                    10),
                new PlayModeDefinition(
                    StableId.Parse(
                        PlaySelectionActions.MultiplayerModeStableIdText),
                    "MULTIPLAYER / CO-OP",
                    "Prototype placeholder. Networking, lobbies, and matchmaking are not implemented.",
                    PlayModeAvailability.PrototypeUnavailable,
                    PlayModeDestination.None,
                    20),
            });
        }
    }
}
