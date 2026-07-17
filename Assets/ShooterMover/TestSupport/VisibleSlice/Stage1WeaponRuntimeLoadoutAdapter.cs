using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Firing;
using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Catalog;
using UnityEngine;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// Stage 1 composition only: converts the legacy package-selection fixture into
    /// concrete JSON catalog definition IDs. No tuning or firing behavior lives here.
    /// </summary>
    public static class Stage1WeaponRuntimeLoadoutAdapter
    {
        private static readonly ReadOnlyDictionary<StableId, string> DefinitionIds =
            new ReadOnlyDictionary<StableId, string>(
                new Dictionary<StableId, string>
                {
                    { Stage1WeaponPackageDescriptor.BlasterMachineGunId, "blaster.mk1" },
                    { Stage1WeaponPackageDescriptor.ShotgunId, "shotgun.mk1" },
                    { Stage1WeaponPackageDescriptor.RocketLauncherId, "rocket_launcher.mk1" },
                    { Stage1WeaponPackageDescriptor.ArcGunId, "arc_rifle.mk1" },
                    { Stage1WeaponPackageDescriptor.RicochetGunId, "ricochet_weapon.mk1" },
                });

        public static WeaponDefinitionLoadout FromFixture(Stage1WeaponLoadoutFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            var slots = new List<WeaponDefinitionLoadoutSlot>(WeaponMountContractRules.MountCount);
            for (int index = 0; index < fixture.Count; index++)
            {
                Stage1WeaponLoadoutSlot source = fixture.GetByHudIndex(index);
                string definitionId;
                if (!DefinitionIds.TryGetValue(source.WeaponId, out definitionId))
                {
                    throw new KeyNotFoundException(
                        "Stage 1 package has no concrete catalog definition mapping: "
                        + source.WeaponId);
                }

                slots.Add(new WeaponDefinitionLoadoutSlot(source.Slot, definitionId));
            }
            return new WeaponDefinitionLoadout(slots);
        }
    }

    /// <summary>
    /// Imports the Stage 1 runtime projection through the production JSON importer.
    /// The returned WeaponCatalog remains the sole typed catalog authority.
    /// </summary>
    public static class Stage1WeaponCatalogRuntimeProvider
    {
        public const string ResourcePath =
            "WeaponCatalog/stage1_weapon_baseline_v01";

        private static WeaponCatalog cached;

        public static WeaponCatalog Load()
        {
            if (cached != null)
            {
                return cached;
            }

            TextAsset source = Resources.Load<TextAsset>(ResourcePath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Missing Stage 1 weapon catalog JSON resource at Resources/"
                    + ResourcePath
                    + ".json.");
            }

            WeaponCatalogImportResult result =
                WeaponCatalogJsonImporter.Import(source.text);
            if (!result.IsSuccess || result.Catalog == null)
            {
                var builder = new StringBuilder(
                    "Stage 1 weapon catalog JSON was rejected.");
                for (int index = 0; index < result.Issues.Count; index++)
                {
                    builder.Append("\n- ")
                        .Append(result.Issues[index].Code)
                        .Append(": ")
                        .Append(result.Issues[index].Message);
                }
                throw new InvalidOperationException(builder.ToString());
            }

            cached = result.Catalog;
            return cached;
        }

        public static void ResetForTests()
        {
            cached = null;
        }
    }

    public static class Stage1WeaponCombatChannelProjection
    {
        public static CombatChannel Resolve(string damageType)
        {
            switch (damageType)
            {
                case "Kinetic":
                    return CombatChannel.Kinetic;
                case "Thermal":
                    return CombatChannel.Thermal;
                case "Energized":
                    return CombatChannel.Electrical;
                case "Chemical":
                    return CombatChannel.Environmental;
                case "Photonic":
                    return CombatChannel.Electrical;
                case "Omni-Phase":
                    return CombatChannel.Electrical;
                default:
                    throw new InvalidOperationException(
                        "Unsupported weapon damage type for Stage 1 combat: "
                        + (damageType ?? "<null>"));
            }
        }
    }
}
