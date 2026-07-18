using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;

namespace ShooterMover.Application.Missions.Run
{
    public interface ILevelRunLoadoutResolverV1
    {
        LevelRunLoadoutResolutionV1 Resolve(
            PlayerRouteProfilePayloadV1 routePayload);
    }

    /// <summary>
    /// Resolves the four-slot route handoff through the sole holdings authority.
    /// Identity lookup is by concrete equipment instance StableId only.
    /// </summary>
    public sealed class HoldingsLevelRunLoadoutResolverV1 :
        ILevelRunLoadoutResolverV1
    {
        private readonly IPlayerHoldingsAuthorityV1 holdingsAuthority;
        private readonly EquipmentCatalog equipmentCatalog;

        public HoldingsLevelRunLoadoutResolverV1(
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            EquipmentCatalog equipmentCatalog)
        {
            this.holdingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        }

        public LevelRunLoadoutResolutionV1 Resolve(
            PlayerRouteProfilePayloadV1 routePayload)
        {
            if (routePayload == null || !routePayload.HasValidFingerprint())
            {
                return LevelRunLoadoutResolutionV1.Reject(
                    "level-run-route-payload-invalid");
            }

            PlayerHoldingsSnapshotV1 snapshot = holdingsAuthority.ExportSnapshot();
            if (snapshot == null
                || !string.Equals(
                    snapshot.Fingerprint,
                    PlayerHoldingsSnapshotV1.ComputeFingerprint(snapshot),
                    StringComparison.Ordinal))
            {
                return LevelRunLoadoutResolutionV1.Reject(
                    "level-run-holdings-snapshot-invalid");
            }

            var holdingsByInstance =
                new Dictionary<string, UniqueHoldingSnapshotV1>(
                    StringComparer.Ordinal);
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding = snapshot.UniqueHoldings[index];
                if (holding != null
                    && holding.RewardKind
                        == ShooterMover.Domain.Rewards.Model.RewardGrantKindV1
                            .EquipmentReference
                    && holding.EquipmentInstance != null)
                {
                    holdingsByInstance[holding.InstanceStableId.ToString()] = holding;
                }
            }

            var resolved = new List<ResolvedLevelRunWeaponSlotV1>();
            for (int index = 0; index < routePayload.WeaponSlots.Count; index++)
            {
                PlayerRouteWeaponSlotV1 routeSlot = routePayload.WeaponSlots[index];
                UniqueHoldingSnapshotV1 holding;
                if (!holdingsByInstance.TryGetValue(
                    routeSlot.EquipmentInstanceStableId.ToString(),
                    out holding))
                {
                    return LevelRunLoadoutResolutionV1.Reject(
                        "level-run-equipment-instance-missing:"
                        + routeSlot.EquipmentInstanceStableId);
                }

                EquipmentInstance instance = holding.EquipmentInstance;
                if (instance.InstanceId
                    != routeSlot.EquipmentInstanceStableId)
                {
                    return LevelRunLoadoutResolutionV1.Reject(
                        "level-run-equipment-instance-identity-mismatch");
                }

                EquipmentDefinition definition =
                    equipmentCatalog.FindEquipmentDefinition(
                        instance.DefinitionId);
                if (definition == null
                    || definition.CategoryId != EquipmentCategoryIds.Weapon
                    || definition.RuntimeWeaponReferenceId == null)
                {
                    return LevelRunLoadoutResolutionV1.Reject(
                        "level-run-weapon-definition-unavailable:"
                        + instance.DefinitionId);
                }

                resolved.Add(new ResolvedLevelRunWeaponSlotV1(
                    routeSlot.WeaponSlotStableId,
                    instance.InstanceId,
                    instance.DefinitionId,
                    definition.RuntimeWeaponReferenceId,
                    definition.DisplayName));
            }

            return LevelRunLoadoutResolutionV1.Accept(resolved, 0);
        }
    }
}
