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
private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            Assert.That(instance, Is.Not.Null, methodName);
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(candidate =>
                    candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, instance.GetType().FullName + "." + methodName);
            return Invoke(method, instance, arguments);
        }

private static object Invoke(
            MethodInfo method,
            object instance,
            object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }

                throw;
            }
        }

private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

private static StableId Operation(string suffix)
        {
            return StableId.Parse("operation.room-live-playmode-" + suffix);
        }

private sealed class PlayerFixture
        {
            public PlayerFixture(
                StableId playerStableId,
                EnemyTarget2DAdapter target,
                Collider2D collider)
            {
                PlayerStableId = playerStableId;
                Target = target;
                Collider = collider;
            }

            public StableId PlayerStableId { get; }

            public EnemyTarget2DAdapter Target { get; }

            public Collider2D Collider { get; }
        }
    }
}
#endif
