using System;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Explicit collector actor and participant identities. Tags, hierarchy names, Unity
    /// instance IDs, and collider callback counts are never authoritative identity inputs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunPickupCollector2D : MonoBehaviour
    {
        [SerializeField] private string collectorEntityStableId = "actor.player";
        [SerializeField] private string collectorParticipantStableId = "participant.player";

        private StableId entity;
        private StableId participant;
        private string configurationError;

        public bool TryGetIdentities(
            out StableId collectorEntity,
            out StableId collectorParticipant)
        {
            if (entity == null
                && participant == null
                && string.IsNullOrEmpty(configurationError))
            {
                if (!StableId.TryParse(
                    collectorEntityStableId == null
                        ? string.Empty
                        : collectorEntityStableId.Trim(),
                    out entity))
                {
                    configurationError = "Collector entity StableId is invalid.";
                }
                else if (!StableId.TryParse(
                    collectorParticipantStableId == null
                        ? string.Empty
                        : collectorParticipantStableId.Trim(),
                    out participant))
                {
                    configurationError = "Collector participant StableId is invalid.";
                }
            }

            collectorEntity = entity;
            collectorParticipant = participant;
            return collectorEntity != null && collectorParticipant != null;
        }

        public string ConfigurationError
        {
            get { return configurationError ?? string.Empty; }
        }

        public void ConfigureForTests(string entityId, string participantId)
        {
            collectorEntityStableId = entityId;
            collectorParticipantStableId = participantId;
            entity = null;
            participant = null;
            configurationError = null;
            StableId ignoredEntity;
            StableId ignoredParticipant;
            if (!TryGetIdentities(out ignoredEntity, out ignoredParticipant))
                throw new ArgumentException(configurationError);
        }

        private void OnValidate()
        {
            entity = null;
            participant = null;
            configurationError = null;
        }
    }
}
