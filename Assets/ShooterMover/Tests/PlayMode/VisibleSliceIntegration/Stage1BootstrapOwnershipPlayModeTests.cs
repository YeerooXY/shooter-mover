#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1BootstrapOwnershipPlayModeTests
    {
        private const string CompositionTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1";
        private const string PresentationTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1WeaponPresentationRepairV1";
        private const string RemovedInstallerTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1HubLoadoutInstallerV1";

        [Test]
        public void Stage1UsesOneRuntimeInitializeOwner()
        {
            Type composition = FindType(CompositionTypeName);
            Type presentation = FindType(PresentationTypeName);

            Assert.That(FindTypeOrNull(RemovedInstallerTypeName), Is.Null);
            Assert.That(
                RuntimeInitializeMethodNames(composition),
                Is.EquivalentTo(new[] { "ResetStatics", "InstallSceneHook" }));
            Assert.That(RuntimeInitializeMethodNames(presentation), Is.Empty);
            Assert.That(
                composition.GetMethod(
                    "InstallForScene",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                presentation.GetMethod(
                    "InstallForScene",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
        }

        private static IReadOnlyList<string> RuntimeInitializeMethodNames(
            Type type)
        {
            var names = new List<string>();
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic);
            for (int index = 0; index < methods.Length; index++)
            {
                if (methods[index].GetCustomAttributes(
                        typeof(RuntimeInitializeOnLoadMethodAttribute),
                        false).Length > 0)
                {
                    names.Add(methods[index].Name);
                }
            }
            return names;
        }

        private static Type FindType(string fullName)
        {
            Type type = FindTypeOrNull(fullName);
            if (type == null)
            {
                throw new InvalidOperationException(
                    "Required type not found: " + fullName);
            }
            return type;
        }

        private static Type FindTypeOrNull(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }
    }
}
#endif
