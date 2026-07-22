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

        public void Configure(
            StableId collectorEntity,
            StableId collectorParticipant)
        {
            if (collectorEntity == null)
                throw new ArgumentNullException(nameof(collectorEntity));
            if (collectorParticipant == null)
                throw new ArgumentNullException(nameof(collectorParticipant));

            collectorEntityStableId = collectorEntity.ToString();
            collectorParticipantStableId = collectorParticipant.ToString();
            entity = collectorEntity;
            participant = collectorParticipant;
            configurationError = null;
        }

        public void ConfigureForTests(string entityId, string participantId)
        {
            StableId parsedEntity;
            StableId parsedParticipant;
            if (!StableId.TryParse(entityId ?? string.Empty, out parsedEntity)
                || !StableId.TryParse(participantId ?? string.Empty, out parsedParticipant))
            {
                throw new ArgumentException("Collector test identities must be valid StableIds.");
            }
            Configure(parsedEntity, parsedParticipant);
        }

        private void OnValidate()
        {
            entity = null;
            participant = null;
            configurationError = null;
        }
    }
}
