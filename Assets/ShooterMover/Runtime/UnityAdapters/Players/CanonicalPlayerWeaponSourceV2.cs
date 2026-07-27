using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Players
{
    /// <summary>
    /// Authoritative exact-weapon source attached to a spawned player object. It owns no fallback,
    /// has no UI dependency, and cannot be rebound to a different exact instance during the player
    /// lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanonicalPlayerWeaponSourceV2 : MonoBehaviour
    {
        private CanonicalWeaponEquipmentProjectionLookupV2 projectionLookup;

        public StableId CharacterInstanceId { get; private set; }
        public StableId ExactWeaponInstanceId { get; private set; }
        public string WeaponDefinitionId { get; private set; }
        public WeaponEquipmentInstance ExactInstance { get; private set; }
        public ProductionWeaponMarkV1 ResolvedMark { get; private set; }
        public string Diagnostic { get; private set; }
        public bool IsBound { get { return ExactInstance != null; } }

        public void Bind(
            StableId characterInstanceId,
            ProductionPlayerLoadoutRuntimeV1 runtime,
            WeaponEquipmentInstance exact,
            ProductionWeaponMarkV1 mark)
        {
            if (IsBound)
            {
                if (CharacterInstanceId == characterInstanceId
                    && exact != null
                    && ExactWeaponInstanceId == exact.InstanceId)
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

            WeaponEquipmentInstance owned = runtime.WeaponHoldings.Find(
                exact.InstanceId);
            if (characterInstanceId
                    != runtime.RoutePayload.SelectedCharacterStableId
                || owned == null
                || owned.InstanceId != exact.InstanceId
                || !owned.WeaponDefinitionId.Equals(
                    exact.WeaponDefinitionId)
                || !string.Equals(
                    mark.Blueprint.DefinitionId.Value,
                    exact.WeaponDefinitionId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "gameplay-canonical-player-source-context-mismatch");
            }

            CharacterInstanceId = characterInstanceId;
            ExactWeaponInstanceId = exact.InstanceId;
            WeaponDefinitionId = exact.WeaponDefinitionId.Value;
            ExactInstance = owned;
            ResolvedMark = mark;
            projectionLookup = new CanonicalWeaponEquipmentProjectionLookupV2(
                runtime.WeaponHoldings,
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
                new EquipmentInstanceId(ExactWeaponInstanceId),
                out equipmentInstance);
            if (!resolved
                || equipmentInstance == null
                || equipmentInstance.InstanceId != ExactWeaponInstanceId)
            {
                equipmentInstance = null;
                CanonicalWeaponOperationAvailabilityV1 availability =
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
                    + ExactWeaponInstanceId;
                Diagnostic = rejectionCode;
                return false;
            }

            Diagnostic = string.Empty;
            return true;
        }
    }
}
