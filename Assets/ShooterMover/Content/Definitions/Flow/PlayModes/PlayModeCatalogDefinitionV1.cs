using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Flow.PlayModes
{
    [Serializable]
    public sealed class PlayModeDefinitionRecordV1
    {
        [SerializeField]
        private string modeStableId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        [TextArea]
        private string description = string.Empty;

        [SerializeField]
        private PlayModeAvailabilityV1 availability =
            PlayModeAvailabilityV1.PrototypeUnavailable;

        [SerializeField]
        private PlayModeDestinationV1 destination = PlayModeDestinationV1.None;

        [SerializeField]
        private int sortOrder;

        public PlayModeDefinitionV1 Build()
        {
            StableId parsedModeStableId;
            if (!StableId.TryParse(modeStableId, out parsedModeStableId))
            {
                throw new InvalidOperationException(
                    "Play mode identity is missing or malformed: " + modeStableId);
            }

            return new PlayModeDefinitionV1(
                parsedModeStableId,
                displayName,
                description,
                availability,
                destination,
                sortOrder);
        }
    }

    [CreateAssetMenu(
        fileName = "PlayModeCatalogV1",
        menuName = "Shooter Mover/Flow/Play Mode Catalog V1")]
    public sealed class PlayModeCatalogDefinitionV1 : ScriptableObject
    {
        [SerializeField]
        private List<PlayModeDefinitionRecordV1> playModes =
            new List<PlayModeDefinitionRecordV1>();

        public PlayModeCatalogV1 BuildCatalog()
        {
            var definitions = new List<PlayModeDefinitionV1>(playModes.Count);
            for (int index = 0; index < playModes.Count; index++)
            {
                PlayModeDefinitionRecordV1 record = playModes[index];
                if (record == null)
                {
                    throw new InvalidOperationException(
                        "Play mode catalog entries cannot contain null.");
                }

                definitions.Add(record.Build());
            }

            return new PlayModeCatalogV1(definitions);
        }

        public static PlayModeCatalogV1 CreateDefaultCatalog()
        {
            return new PlayModeCatalogV1(new[]
            {
                new PlayModeDefinitionV1(
                    StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText),
                    "SOLO",
                    "Continue alone to level selection with the current profile and loadout.",
                    PlayModeAvailabilityV1.Available,
                    PlayModeDestinationV1.LevelSelection,
                    10),
                new PlayModeDefinitionV1(
                    StableId.Parse(
                        PlaySelectionServiceV1.MultiplayerModeStableIdText),
                    "MULTIPLAYER / CO-OP",
                    "Prototype placeholder. Networking, lobbies, and matchmaking are not implemented.",
                    PlayModeAvailabilityV1.PrototypeUnavailable,
                    PlayModeDestinationV1.None,
                    20),
            });
        }
    }
}
