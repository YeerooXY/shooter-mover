using System;
using NUnit.Framework;
using ShooterMover.UI.Game;

namespace ShooterMover.Tests.EditMode.Flow
{
    public sealed class CurrentCharacterApiTests
    {
        [Test]
        public void ExposesGameFacingCharacterNames()
        {
            Type type = typeof(CurrentCharacter);

            Assert.That(type.GetProperty("SlotIndex"), Is.Not.Null);
            Assert.That(type.GetProperty("CharacterId"), Is.Not.Null);
            Assert.That(type.GetProperty("ClassId"), Is.Not.Null);
            Assert.That(type.GetProperty("Level"), Is.Not.Null);
            Assert.That(type.GetProperty("Money"), Is.Not.Null);
            Assert.That(type.GetProperty("Scrap"), Is.Not.Null);
            Assert.That(type.GetProperty("Loadout"), Is.Not.Null);
            Assert.That(type.GetProperty("Holdings"), Is.Not.Null);
            Assert.That(type.GetMethod("FindGun"), Is.Not.Null);

            Assert.That(type.GetProperty("ExperienceAuthority"), Is.Null);
            Assert.That(type.GetProperty("MoneyWallet"), Is.Null);
            Assert.That(type.GetProperty("ScrapWallet"), Is.Null);
            Assert.That(type.GetProperty("LoadoutRuntime"), Is.Null);
        }
    }
}
