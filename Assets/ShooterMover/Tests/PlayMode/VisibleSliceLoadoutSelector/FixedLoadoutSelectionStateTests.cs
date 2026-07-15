using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.UI.VisibleSliceLoadoutSelector.Core;

namespace ShooterMover.Tests.PlayMode.VisibleSliceLoadoutSelector
{
    public sealed class FixedLoadoutSelectionStateTests
    {
        private const string DefaultFixtureId = "loadout.stage1-default-comparison";
        private const string RicochetFixtureId = "loadout.stage1-ricochet-comparison";

        private const string BlasterId = "weapon.blaster-machine-gun";
        private const string ShotgunId = "weapon.shotgun";
        private const string RocketId = "weapon.rocket-launcher";
        private const string ArcId = "weapon.arc-gun";
        private const string RicochetId = "weapon.ricochet-gun";

        [Test]
        public void ApprovedProjection_ContainsExactlyTheTwoWp008ComparisonsInSlotOrder()
        {
            FixedLoadoutSelectionState state = CreateState();

            Assert.That(state.OptionCount, Is.EqualTo(2));
            Assert.That(state.Options[0].FixtureId, Is.EqualTo(DefaultFixtureId));
            Assert.That(
                state.Options[0].OrderedWeaponIds,
                Is.EqualTo(new[] { BlasterId, ShotgunId, RocketId, ArcId }));
            Assert.That(state.Options[1].FixtureId, Is.EqualTo(RicochetFixtureId));
            Assert.That(
                state.Options[1].OrderedWeaponIds,
                Is.EqualTo(new[] { BlasterId, RicochetId, ShotgunId, RocketId }));

            TestContext.WriteLine("wp-008-identity " + state.Options[0].ToTraceString());
            TestContext.WriteLine("wp-008-identity " + state.Options[1].ToTraceString());
        }

        [Test]
        public void NavigationWrapsInBothDirections()
        {
            FixedLoadoutSelectionState state = CreateState();

            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Apply(LoadoutSelectorCommand.Previous), Is.True);
            Assert.That(state.SelectedIndex, Is.EqualTo(1));
            Assert.That(state.Apply(LoadoutSelectorCommand.Next), Is.True);
            Assert.That(state.SelectedIndex, Is.Zero);

            TestContext.WriteLine("navigation-trace default->previous:last->next:default");
        }

        [Test]
        public void ConfirmAndCancelAreTerminalUntilRestart()
        {
            FixedLoadoutSelectionState confirmed = CreateState();
            Assert.That(confirmed.Apply(LoadoutSelectorCommand.Next), Is.True);
            Assert.That(confirmed.Apply(LoadoutSelectorCommand.Confirm), Is.True);
            Assert.That(confirmed.Phase, Is.EqualTo(LoadoutSelectorPhase.Confirmed));
            Assert.That(confirmed.Confirmed.FixtureId, Is.EqualTo(RicochetFixtureId));
            Assert.That(confirmed.Apply(LoadoutSelectorCommand.Previous), Is.False);
            Assert.That(confirmed.Apply(LoadoutSelectorCommand.Cancel), Is.False);

            FixedLoadoutSelectionState cancelled = CreateState();
            Assert.That(cancelled.Apply(LoadoutSelectorCommand.Cancel), Is.True);
            Assert.That(cancelled.Phase, Is.EqualTo(LoadoutSelectorPhase.Cancelled));
            Assert.That(cancelled.Confirmed, Is.Null);
            Assert.That(cancelled.Apply(LoadoutSelectorCommand.Next), Is.False);
            Assert.That(cancelled.Apply(LoadoutSelectorCommand.Confirm), Is.False);

            TestContext.WriteLine("terminal-trace confirmed-lock=true cancelled-lock=true");
        }

        [Test]
        public void ResetForRestartRestoresWp008DefaultAndClearsConfirmation()
        {
            FixedLoadoutSelectionState state = CreateState();
            Assert.That(state.Apply(LoadoutSelectorCommand.Next), Is.True);
            Assert.That(state.Apply(LoadoutSelectorCommand.Confirm), Is.True);

            state.ResetForRestart();

            Assert.That(state.Phase, Is.EqualTo(LoadoutSelectorPhase.Browsing));
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Current.FixtureId, Is.EqualTo(DefaultFixtureId));
            Assert.That(state.Confirmed, Is.Null);
            TestContext.WriteLine("restart-trace selected=loadout.stage1-default-comparison confirmed=null");
        }

        [Test]
        public void InvalidCommandAndUnknownFixtureAreNeutral()
        {
            FixedLoadoutSelectionState state = CreateState();

            Assert.That(state.Apply(LoadoutSelectorCommand.None), Is.False);
            Assert.That(state.Apply((LoadoutSelectorCommand)999), Is.False);
            Assert.That(state.TrySelectFixture("loadout.unknown"), Is.False);
            Assert.That(state.TrySelectFixture(null), Is.False);
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(LoadoutSelectorPhase.Browsing));
            Assert.That(state.Confirmed, Is.Null);
        }

        [Test]
        public void OptionDefensivelyCopiesOrderedWeaponIds()
        {
            string[] ids = { BlasterId, ShotgunId, RocketId, ArcId };
            FixedLoadoutOption option = new FixedLoadoutOption(DefaultFixtureId, ids);

            ids[0] = RicochetId;

            Assert.That(option.GetWeaponId(0), Is.EqualTo(BlasterId));
            Assert.Throws<NotSupportedException>(
                () => ((IList<string>)option.OrderedWeaponIds)[0] = RicochetId);
        }

        [Test]
        public void SourceAudit_ConsumesWp008DirectlyWithoutReflectionOrPersistenceApis()
        {
            string selectorPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/UI/VisibleSliceLoadoutSelector/VisibleSliceLoadoutSelector.cs");
            string wp008Path = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/ContentPackages/Weapons/Stage1Loadouts/Stage1WeaponLoadoutFixtures.cs");

            string selectorSource = File.ReadAllText(selectorPath);
            string wp008Source = File.ReadAllText(wp008Path);

            StringAssert.Contains("Stage1WeaponLoadoutCatalog.Approved", selectorSource);
            StringAssert.Contains("catalog.FixedFixtures", selectorSource);
            StringAssert.Contains("catalog.DefaultFixture", selectorSource);
            StringAssert.Contains("Action<Stage1WeaponLoadoutFixture>", selectorSource);
            StringAssert.Contains(DefaultFixtureId, wp008Source);
            StringAssert.Contains(RicochetFixtureId, wp008Source);

            string[] forbidden =
            {
                "System.Reflection",
                "Assembly-CSharp",
                "Stage1Presentation",
                "PlayerPrefs",
                "SceneManager",
                "LoadScene",
                "File.Write",
                "Directory.Create",
                "inventory",
                "reward",
                "reroll",
            };

            for (int index = 0; index < forbidden.Length; index++)
            {
                StringAssert.DoesNotContain(
                    forbidden[index],
                    selectorSource,
                    "Forbidden selector dependency/API: " + forbidden[index]);
            }

            TestContext.WriteLine(
                "static-audit direct-wp008=true reflection=false persistence=false wp010=false scene-load=false");
        }

        private static FixedLoadoutSelectionState CreateState()
        {
            return new FixedLoadoutSelectionState(
                new[]
                {
                    new FixedLoadoutOption(
                        DefaultFixtureId,
                        new[] { BlasterId, ShotgunId, RocketId, ArcId }),
                    new FixedLoadoutOption(
                        RicochetFixtureId,
                        new[] { BlasterId, RicochetId, ShotgunId, RocketId }),
                },
                DefaultFixtureId);
        }
    }
}
