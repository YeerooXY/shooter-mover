using System;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Players
{
    /// <summary>
    /// Authoritative exact-gun source attached to a spawned player object. It owns no fallback,
    /// has no UI dependency, and cannot be rebound to a different exact instance during the player
    /// lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerGunSource : MonoBehaviour
    {
        private GunEquipmentViewLookup projectionLookup;

        public StableId CharacterInstanceId { get; private set; }
        public StableId ExactGunInstanceId { get; private set; }
        public string GunDefinitionId { get; private set; }
        public GunItem ExactInstance { get; private set; }
        public GunMark ResolvedMark { get; private set; }
        public string Diagnostic { get; private set; }
        public bool IsBound { get { return ExactInstance != null; } }

        public void Bind(
            StableId characterInstanceId,
            PlayerLoadoutLive runtime,
            GunItem exact,
            GunMark mark)
        {
            if (IsBound)
            {
                if (CharacterInstanceId == characterInstanceId
                    && exact != null
                    && ExactGunInstanceId == exact.InstanceId)
                {
                    return;
                }
                throw new InvalidOperationException(
                    "gameplay-canonical-player-source-duplicate-binding");
            }
            if (characterInstanceId == null)
            {
                throw new ArgumentNullException(nameof(characterInstanceId));
            }
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }
            if (exact == null)
            {
                throw new ArgumentNullException(nameof(exact));
            }
            if (mark == null)
            {
                throw new ArgumentNullException(nameof(mark));
            }

            GunItem owned = runtime.GunInventory.Find(
                exact.InstanceId);
            if (characterInstanceId
                    != runtime.RoutePayload.SelectedCharacterStableId
                || owned == null
                || owned.InstanceId != exact.InstanceId
                || !owned.GunDefinitionId.Equals(
                    exact.GunDefinitionId)
                || !string.Equals(
                    mark.Blueprint.DefinitionId.Value,
                    exact.GunDefinitionId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "gameplay-canonical-player-source-context-mismatch");
            }

            CharacterInstanceId = characterInstanceId;
            ExactGunInstanceId = exact.InstanceId;
            GunDefinitionId = exact.GunDefinitionId.Value;
            ExactInstance = owned;
            ResolvedMark = mark;
            projectionLookup = new GunEquipmentViewLookup(
                runtime.GunInventory,
                runtime.EquipmentCatalog,
                runtime.Holdings);
            Diagnostic = string.Empty;
        }

        public bool TryResolveLiveEquipment(
            out EquipmentInstance equipmentInstance,
            out string rejectionCode)
        {
            equipmentInstance = null;
            rejectionCode = string.Empty;
            if (!IsBound || projectionLookup == null)
            {
                rejectionCode = "gameplay-canonical-player-source-unbound";
                Diagnostic = rejectionCode;
                return false;
            }

            bool resolved = projectionLookup.TryResolve(
                new EquipmentInstanceId(ExactGunInstanceId),
                out equipmentInstance);
            if (!resolved
                || equipmentInstance == null
                || equipmentInstance.InstanceId != ExactGunInstanceId)
            {
                equipmentInstance = null;
                GunOperationAvailability availability =
                    projectionLookup.LastAvailability;
                if (!resolved
                    && availability != null
                    && !availability.IsAvailable
                    && !string.IsNullOrEmpty(availability.RejectionCode))
                {
                    rejectionCode = availability.RejectionCode;
                    Diagnostic = rejectionCode
                        + (string.IsNullOrEmpty(availability.Message)
                            ? string.Empty
                            : ": " + availability.Message);
                    return false;
                }

                rejectionCode =
                    "gameplay-canonical-live-equipment-unresolved:"
                    + ExactGunInstanceId;
                Diagnostic = rejectionCode;
                return false;
            }

            Diagnostic = string.Empty;
            return true;
        }
    }
}
