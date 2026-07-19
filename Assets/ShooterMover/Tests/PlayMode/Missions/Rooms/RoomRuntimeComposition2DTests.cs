#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Missions.Rooms
{
    public sealed partial class RoomRuntimeComposition2DTests
    {
private static readonly Type MobileDefinitionType = Find(
            "ShooterMover.ContentPackages.Enemies.MobileBlasterDroid.MobileBlasterDroidDefinition");

private static readonly Type MobileRuntimeType = Find(
            "ShooterMover.ContentPackages.Enemies.MobileBlasterDroid.MobileBlasterDroidRuntime2D");

private static readonly Type TurretDefinitionType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretDefinition");

private static readonly Type TurretPackageType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretPackage");

private static readonly Type BoundedProjectileType = Find(
            "ShooterMover.ContentPackages.Weapons.Shared.Runtime.BoundedProjectile2D");

private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

[UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    UnityEngine.Object.Destroy(created[index]);
                }
            }

            created.Clear();
            yield return null;
        }
    }
}
#endif
