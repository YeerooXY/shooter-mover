#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1WeaponPresentationOwnershipPlayModeTests
    {
        private const string CompositionTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1";
        private const string PresentationTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1WeaponPresentationRepairV1";

        [Test]
        public void EmittedProjectileVisualsHaveOneOwner()
        {
            Type composition = FindType(CompositionTypeName);
            Type presentation = FindType(PresentationTypeName);
            const BindingFlags instance =
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;

            Assert.That(
                composition.GetMethod("PrepareEmittedEffects", instance),
                Is.Not.Null,
                "The composition must still attach authoritative hit/damage behavior.");
            Assert.That(
                composition.GetMethod("AddProjectilePresentation", instance),
                Is.Null);
            Assert.That(
                composition.GetMethod("RuntimeSprite", instance),
                Is.Null);
            Assert.That(
                composition.GetField("projectilePresentation", instance),
                Is.Null);
            Assert.That(
                composition.GetField("projectileSprites", instance),
                Is.Null);

            Assert.That(
                presentation.GetMethod("PrepareProjectile", instance),
                Is.Not.Null,
                "The typed presentation component must remain the sole visual projector.");
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            throw new InvalidOperationException(
                "Required type not found: " + fullName);
        }
    }
}
#endif
