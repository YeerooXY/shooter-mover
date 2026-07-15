using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Presentation.LoadoutPreview;

namespace ShooterMover.Tests.EditMode.Presentation.LoadoutPreview
{
    public sealed class TemporaryLoadoutMenuTests
    {
        [Test]
        public void Catalog_IsDeterministicAndEveryChoiceHasExactlyFourStableSlots()
        {
            IReadOnlyList<TemporaryLoadoutChoice> choices = TemporaryLoadoutCatalog.Choices;
            Assert.That(choices.Count, Is.EqualTo(3));

            string[] expected =
            {
                "loadout.comparison-a[S1=weapon.blaster-machine-gun;S2=weapon.shotgun;S3=weapon.rocket-launcher;S4=weapon.arc-gun]",
                "loadout.comparison-b[S1=weapon.blaster-machine-gun;S2=weapon.ricochet-gun;S3=weapon.arc-gun;S4=weapon.shotgun]",
                "loadout.comparison-c[S1=weapon.rocket-launcher;S2=weapon.shotgun;S3=weapon.ricochet-gun;S4=weapon.blaster-machine-gun]",
            };

            HashSet<string> choiceIds = new HashSet<string>();
            for (int index = 0; index < choices.Count; index++)
            {
                TemporaryLoadoutChoice choice = choices[index];
                Assert.That(choice.SlotCount, Is.EqualTo(FourMountStatusSnapshot.SlotCount));
                Assert.That(choice.ToTraceString(), Is.EqualTo(expected[index]));
                Assert.That(choiceIds.Add(choice.ChoiceId.ToString()), Is.True);
            }
        }

        [Test]
        public void Presenter_WrapsSelectionAndClosesAfterConfirm()
        {
            TemporaryLoadoutMenuPresenter presenter = new TemporaryLoadoutMenuPresenter();
            Assert.That(presenter.SelectedIndex, Is.EqualTo(TemporaryLoadoutCatalog.DefaultIndex));

            Assert.That(presenter.MovePrevious(), Is.True);
            Assert.That(presenter.SelectedIndex, Is.EqualTo(presenter.ChoiceCount - 1));
            Assert.That(presenter.MoveNext(), Is.True);
            Assert.That(presenter.SelectedIndex, Is.EqualTo(TemporaryLoadoutCatalog.DefaultIndex));

            Assert.That(presenter.Confirm(), Is.True);
            Assert.That(presenter.Phase, Is.EqualTo(TemporaryLoadoutMenuPhase.Confirmed));
            Assert.That(presenter.ConfirmedChoice, Is.SameAs(presenter.CurrentChoice));
            Assert.That(presenter.MoveNext(), Is.False);
            Assert.That(presenter.Cancel(), Is.False);
        }

        [Test]
        public void Restart_AlwaysClearsTerminalStateAndRestoresDefaultChoice()
        {
            TemporaryLoadoutMenuPresenter presenter = new TemporaryLoadoutMenuPresenter();
            Assert.That(presenter.MoveNext(), Is.True);
            Assert.That(presenter.Cancel(), Is.True);
            Assert.That(presenter.Phase, Is.EqualTo(TemporaryLoadoutMenuPhase.Cancelled));

            presenter.ResetForRestart();

            Assert.That(presenter.Phase, Is.EqualTo(TemporaryLoadoutMenuPhase.Browsing));
            Assert.That(presenter.SelectedIndex, Is.EqualTo(TemporaryLoadoutCatalog.DefaultIndex));
            Assert.That(presenter.ConfirmedChoice, Is.Null);
            Assert.That(presenter.CurrentChoice.ChoiceId, Is.EqualTo(StableId.Parse("loadout.comparison-a")));
        }

        [Test]
        public void IdentityResolver_CoversAllAcceptedWeaponPackageIdsAndFailsClosed()
        {
            string[] acceptedIds =
            {
                "weapon.blaster-machine-gun",
                "weapon.shotgun",
                "weapon.rocket-launcher",
                "weapon.arc-gun",
                "weapon.ricochet-gun",
            };

            HashSet<string> glyphs = new HashSet<string>();
            for (int index = 0; index < acceptedIds.Length; index++)
            {
                TemporaryWeaponIdentityCue cue;
                Assert.That(
                    TemporaryWeaponIdentityResolver.TryResolve(StableId.Parse(acceptedIds[index]), out cue),
                    Is.True);
                Assert.That(cue.Label, Is.Not.Empty);
                Assert.That(glyphs.Add(cue.Glyph), Is.True);
            }

            TemporaryWeaponIdentityCue unknown = TemporaryWeaponIdentityResolver.ResolveOrUnknown(
                StableId.Parse("weapon.unregistered-preview"));
            Assert.That(unknown.Label, Is.EqualTo("UNKNOWN"));
            Assert.That(unknown.Glyph, Is.EqualTo("?"));
        }
    }
}
