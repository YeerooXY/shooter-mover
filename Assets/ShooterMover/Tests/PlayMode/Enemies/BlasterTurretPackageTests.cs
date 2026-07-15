#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    /// <summary>
    /// Package scripts live in predefined Assembly-CSharp, so this fixture uses
    /// reflection instead of directly referencing package types from the test asmdef.
    /// </summary>
    public sealed class BlasterTurretPackageTests
    {
        private static readonly Type DefinitionType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretDefinition");
        private static readonly Type PackageType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretPackage");
        private static readonly Type CadenceType = Find(
            "ShooterMover.ContentPackages.Enemies.BlasterTurret.BlasterTurretCadence");

        [Test]
        public void PackageTypes_ArePresentInPredefinedAssembly()
        {
            Assert.That(DefinitionType, Is.Not.Null);
            Assert.That(PackageType, Is.Not.Null);
            Assert.That(CadenceType, Is.Not.Null);
            Assert.That(PackageType.GetMethod("RestartSession"), Is.Not.Null);
            TestContext.WriteLine("package-types definition=true package=true cadence=true restart=true");
        }

        [Test]
        public void Descriptor_IsStationaryImmovableBlasterInput()
        {
            object definition = CreateDefinition();
            object descriptor = Invoke(definition, "CreatePackageDescriptor");
            Assert.That(Read(descriptor, "DefinitionId").ToString(), Is.EqualTo("enemy.blaster-turret"));
            Assert.That(Read(descriptor, "Classification").ToString(), Is.EqualTo("Ordinary"));
            Assert.That(Read(descriptor, "WeightClass").ToString(), Is.EqualTo("Immovable"));
            Assert.That(Read(descriptor, "AttackReference").ToString(), Does.Contain("weapon.blaster-machine-gun"));
            TestContext.WriteLine("descriptor stationary=true immovable=true attack=weapon.blaster-machine-gun");
        }

        [Test]
        public void Cadence_ProducesWarningShotAndRecoveryDeterministically()
        {
            object cadence = Activator.CreateInstance(
                CadenceType,
                new object[] { 0.2d, 1.5d });
            object warning = Invoke(cadence, "Step", 0.2d, true);
            object shot = Invoke(cadence, "Step", 0.2d, true);
            object recovery = Invoke(cadence, "Step", 0.15d, true);
            Assert.That((bool)Read(warning, "WarningVisible"), Is.True);
            Assert.That((bool)Read(warning, "ShouldFire"), Is.False);
            Assert.That((bool)Read(shot, "ShouldFire"), Is.True);
            Assert.That((bool)Read(recovery, "ShouldFire"), Is.False);
            TestContext.WriteLine("cadence warning=true shot=true recovery=true");
        }

        [Test]
        public void SourceSurface_StaysBoundedAndColorIndependent()
        {
            string source = ReadProjectFile(
                "Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretPackage.cs");
            string presentation = ReadProjectFile(
                "Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretPresentation2D.cs");
            Assert.That(source, Does.Contain("RestartSession"));
            Assert.That(source, Does.Contain("BlasterMachineGunPackage.CreateBehaviorModule"));
            Assert.That(presentation, Does.Contain("WarningTickCount"));
            Assert.That(presentation, Does.Not.Contain("ColorUtility"));
            Assert.That(source, Does.Not.Contain("Physics.Raycast"));
            Assert.That(source, Does.Not.Contain("GameObject.Find"));
            TestContext.WriteLine("surface bounded-2d=true restart=true color-independent=true");
        }

        [Test]
        public void RuntimeContract_HasExpectedStationaryBounds()
        {
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumRange"), Is.EqualTo(250d));
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumWarningSeconds"), Is.EqualTo(10d));
            Assert.That(ReadStatic<double>(DefinitionType, "HardMaximumRecoverySeconds"), Is.EqualTo(60d));
            TestContext.WriteLine("bounds range=250 warning<=10 recovery<=60");
        }

        private static object CreateDefinition()
        {
            MethodInfo method = DefinitionType.GetMethod(
                "CreateRuntime",
                BindingFlags.Public | BindingFlags.Static);
            return method.Invoke(null, new object[] { 120d, 0.2d, 1.5d, 25d, 0.5d, 0.1d, 0.2d, 0.05d, 16 });
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == args.Length);
            return method.Invoke(instance, args);
        }

        private static object Read(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(instance, null);
        }

        private static T ReadStatic<T>(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(null);
        }

        private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static string ReadProjectFile(string path)
        {
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
#endif
