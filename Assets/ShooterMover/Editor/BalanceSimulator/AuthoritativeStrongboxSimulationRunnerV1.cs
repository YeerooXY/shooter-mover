using System;
using ShooterMover.Application.Rewards.Strongboxes.Simulation;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Small editor-facing invocation surface for authoritative full-opening simulation.
    /// Catalog loading, metadata projection, production gateway construction, simulation,
    /// and structural report validation are performed as one explicit operation.
    /// </summary>
    public static class AuthoritativeStrongboxSimulationRunnerV1
    {
        public const string DefinitionConditionedUnsupportedDiagnostic =
            "strongbox-simulation-definition-conditioned-unsupported";

        public static bool TryRun(
            string weaponCatalogJson,
            StrongboxSimulationRequest request,
            out StrongboxSimulationReport report,
            out string diagnostic)
        {
            report = null;
            diagnostic = string.Empty;
            if (request == null)
            {
                diagnostic = "strongbox-simulation-request-null";
                return false;
            }
            if (request.Mode == StrongboxSimulationMode.DefinitionConditioned)
            {
                diagnostic = DefinitionConditionedUnsupportedDiagnostic;
                return false;
            }
            if (request.Mode != StrongboxSimulationMode.FullOpening)
            {
                diagnostic = "strongbox-simulation-runner-mode-requires-coordinator";
                return false;
            }

            AuthoritativeStrongboxSimulationProductionGatewayV1 gateway;
            if (!AuthoritativeStrongboxSimulationGatewayFactoryV1.TryCreate(
                    weaponCatalogJson,
                    out gateway,
                    out diagnostic)
                || gateway == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "strongbox-simulation-gateway-create-rejected"
                    : diagnostic;
                return false;
            }

            try
            {
                report = new StrongboxBatchSimulator().Run(request, gateway);
                StrongboxSimulationReportValidator.ThrowIfInvalid(report);
                return true;
            }
            catch (Exception exception)
            {
                report = null;
                diagnostic = "strongbox-simulation-run-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }
        }
    }
}
