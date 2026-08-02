using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Simulation;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed class StrongboxLiveCatalogPreviewTests
    {
        [Test]
        public void LiveCatalogueProducesAtLeastOneOpening()
        {
            string json = GunCatalogJson.Export(
                GunCatalogProvider.GunCatalog);

            AuthoritativeStrongboxSimulationGateway gateway;
            string diagnostic;
            Assert.That(
                AuthoritativeStrongboxSimulationGatewayFactory.TryCreate(
                    json,
                    out gateway,
                    out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(gateway, Is.Not.Null);

            StrongboxTier tier = StrongboxCatalog.GetByNumber(1);
            var scenario = new StrongboxSimulationScenario(
                1,
                tier.TierStableId,
                1,
                123456UL);
            StrongboxGeneratedEquipmentObservation observation;
            Assert.That(
                gateway.TryGenerate(
                    scenario,
                    0,
                    out observation,
                    out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(observation, Is.Not.Null);
        }
    }
}
