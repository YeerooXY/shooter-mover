#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1HubAuthorityBootstrapPlayModeTests
    {
        private const string CompositionTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1";
        private const string HubCompositionTypeName =
            "ShooterMover.UI.ProductionFlow.ProductionHubLoadoutCompositionV1";
        private const string CompositionPath =
            "Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.cs";
        private const string CatalogsPath =
            "Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.Catalogs.cs";
        private const string PresentationPath =
            "Assets/ShooterMover/Production/Stage1/Stage1WeaponPresentationRepairV1.cs";

        [Test]
        public void Level1ComposesDirectlyFromHubAuthorities()
        {
            Type composition = FindType(CompositionTypeName);
            Type hubComposition = FindType(HubCompositionTypeName);
            const BindingFlags instance =
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            const BindingFlags staticPublic =
                BindingFlags.Static | BindingFlags.Public;

            Assert.That(
                hubComposition.GetMethod(
                    "TryResolveCurrent",
                    staticPublic),
                Is.Not.Null,
                "Scene consumers need a synchronous Hub-authority resolution path.");
            Assert.That(
                composition.GetMethod("BuildWeaponEffectEmitter", instance),
                Is.Not.Null);
            Assert.That(
                composition.GetMethod("BuildInventoryAndWeaponAuthority", instance),
                Is.Null);
            Assert.That(
                composition.GetMethod("BuildEquipmentCatalog", instance),
                Is.Null);
            Assert.That(
                composition.GetMethod("BuildWeaponCatalog", instance),
                Is.Null);

            string compositionSource = File.ReadAllText(CompositionPath);
            string catalogsSource = File.ReadAllText(CatalogsPath);
            string presentationSource = File.ReadAllText(PresentationPath);

            Assert.That(
                compositionSource,
                Does.Contain("if (!TryAdoptHubLoadout())"));
            Assert.That(
                compositionSource,
                Does.Not.Contain("authority.demo-cutover-player-holdings"));
            Assert.That(
                catalogsSource,
                Does.Not.Contain("equipment.demo-cutover-"));
            Assert.That(
                catalogsSource,
                Does.Not.Contain("demo-cutover-family"));
            Assert.That(
                presentationSource,
                Does.Not.Contain("TryAdoptHubLoadout"),
                "Presentation must not perform an authority handoff.");
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
