using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregate
    {
        public RunConditionDeliveryResult DeliverConditionGameplayFact(
            RunConditionGameplayFactCommand command)
        {
            IRunConditionLivePort port = ResolveConditionPort();
            if (command == null)
            {
                return RejectDelivery(
                    port,
                    null,
                    RunConditionDeliveryStatus.Rejected,
                    "run-condition-delivery-command-null");
            }
            if (command.RunStableId != RunStableId)
            {
                return RejectDelivery(
                    port,
                    command,
                    RunConditionDeliveryStatus.WrongRun,
                    "run-condition-delivery-wrong-run");
            }
            if (command.RunLifecycleGeneration != lifecycleGeneration)
            {
                return RejectDelivery(
                    port,
                    command,
                    RunConditionDeliveryStatus.StaleLifecycle,
                    command.RunLifecycleGeneration < lifecycleGeneration
                        ? "run-condition-delivery-stale-generation"
                        : "run-condition-delivery-future-generation");
            }
            if (lifecycleState == RunSessionLifecycleState.Ended)
            {
                return RejectDelivery(
                    port,
                    command,
                    RunConditionDeliveryStatus.RunEnded,
                    "run-condition-delivery-after-end");
            }
            if (port == null)
            {
                return RejectDelivery(
                    null,
                    command,
                    RunConditionDeliveryStatus.Rejected,
                    "run-condition-authoritative-port-missing");
            }

            port.Bind(this);
            RunConditionDeliveryResult result = port.Deliver(command);
            if (result != null && result.Succeeded
                && command.AuthoritativeTick > authoritativeTick)
            {
                authoritativeTick = command.AuthoritativeTick;
            }
            port.Bind(this);
            return result ?? RejectDelivery(
                port,
                command,
                RunConditionDeliveryStatus.Rejected,
                "run-condition-delivery-null-result");
        }

        public RunConditionAdvanceResult AdvanceConditionRuntime(
            RunConditionAdvanceCommand command)
        {
            IRunConditionLivePort port = ResolveConditionPort();
            if (command == null)
            {
                return RejectAdvance(
                    port,
                    null,
                    RunConditionAdvanceStatus.Rejected,
                    "run-condition-advance-command-null");
            }
            if (command.RunStableId != RunStableId)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatus.WrongRun,
                    "run-condition-advance-wrong-run");
            }
            if (command.RunLifecycleGeneration != lifecycleGeneration)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatus.StaleLifecycle,
                    command.RunLifecycleGeneration < lifecycleGeneration
                        ? "run-condition-advance-stale-generation"
                        : "run-condition-advance-future-generation");
            }
            if (lifecycleState == RunSessionLifecycleState.Ended)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatus.RunEnded,
                    "run-condition-advance-after-end");
            }
            if (command.AuthoritativeTick < authoritativeTick)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatus.Rejected,
                    "run-condition-advance-tick-regression");
            }
            if (port == null)
            {
                return RejectAdvance(
                    null,
                    command,
                    RunConditionAdvanceStatus.Rejected,
                    "run-condition-authoritative-port-missing");
            }

            port.Bind(this);
            long previousTick = authoritativeTick;
            authoritativeTick = command.AuthoritativeTick;
            RunConditionAdvanceResult result;
            try
            {
                result = port.Advance(command);
            }
            catch
            {
                authoritativeTick = previousTick;
                port.Bind(this);
                throw;
            }
            if (result == null || !result.Succeeded)
            {
                authoritativeTick = previousTick;
            }
            port.Bind(this);
            return result ?? RejectAdvance(
                port,
                command,
                RunConditionAdvanceStatus.Rejected,
                "run-condition-advance-null-result");
        }

        public RunConditionLiveSnapshot ExportConditionRuntimeSnapshot()
        {
            IRunConditionLivePort port = ResolveConditionPort();
            if (port == null)
            {
                throw new InvalidOperationException(
                    "The run does not own an authoritative condition runtime.");
            }
            port.Bind(this);
            return port.ExportConditionSnapshot();
        }

        public LiveModifierSnapshot ExportConditionModifierProjection(
            StableId participantStableId)
        {
            IRunConditionLivePort port = ResolveConditionPort();
            if (port == null)
            {
                throw new InvalidOperationException(
                    "The run does not own an authoritative condition runtime.");
            }
            port.Bind(this);
            return port.ExportModifierProjection(participantStableId);
        }

        private IRunConditionLivePort ResolveConditionPort()
        {
            return RuntimePorts.ConditionalFacts as IRunConditionLivePort;
        }

        private static RunConditionDeliveryResult RejectDelivery(
            IRunConditionLivePort port,
            RunConditionGameplayFactCommand command,
            RunConditionDeliveryStatus status,
            string diagnostic)
        {
            return new RunConditionDeliveryResult(
                status,
                command,
                diagnostic,
                port == null ? null : port.ExportConditionSnapshot(),
                string.Empty);
        }

        private static RunConditionAdvanceResult RejectAdvance(
            IRunConditionLivePort port,
            RunConditionAdvanceCommand command,
            RunConditionAdvanceStatus status,
            string diagnostic)
        {
            return new RunConditionAdvanceResult(
                status,
                command,
                diagnostic,
                port == null ? null : port.ExportConditionSnapshot());
        }
    }
}
