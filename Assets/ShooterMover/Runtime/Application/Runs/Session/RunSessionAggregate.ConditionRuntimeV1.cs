using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregateV1
    {
        public RunConditionDeliveryResultV1 DeliverConditionGameplayFact(
            RunConditionGameplayFactCommandV1 command)
        {
            IRunConditionRuntimePortV1 port = ResolveConditionPort();
            if (command == null)
            {
                return RejectDelivery(
                    port,
                    null,
                    RunConditionDeliveryStatusV1.Rejected,
                    "run-condition-delivery-command-null");
            }
            if (command.RunStableId != RunStableId)
            {
                return RejectDelivery(
                    port,
                    command,
                    RunConditionDeliveryStatusV1.WrongRun,
                    "run-condition-delivery-wrong-run");
            }
            if (command.RunLifecycleGeneration != lifecycleGeneration)
            {
                return RejectDelivery(
                    port,
                    command,
                    RunConditionDeliveryStatusV1.StaleLifecycle,
                    command.RunLifecycleGeneration < lifecycleGeneration
                        ? "run-condition-delivery-stale-generation"
                        : "run-condition-delivery-future-generation");
            }
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                return RejectDelivery(
                    port,
                    command,
                    RunConditionDeliveryStatusV1.RunEnded,
                    "run-condition-delivery-after-end");
            }
            if (port == null)
            {
                return RejectDelivery(
                    null,
                    command,
                    RunConditionDeliveryStatusV1.Rejected,
                    "run-condition-authoritative-port-missing");
            }

            port.Bind(this);
            RunConditionDeliveryResultV1 result = port.Deliver(command);
            if (result != null && result.Succeeded
                && command.AuthoritativeTick > authoritativeTick)
            {
                authoritativeTick = command.AuthoritativeTick;
            }
            port.Bind(this);
            return result ?? RejectDelivery(
                port,
                command,
                RunConditionDeliveryStatusV1.Rejected,
                "run-condition-delivery-null-result");
        }

        public RunConditionAdvanceResultV1 AdvanceConditionRuntime(
            RunConditionAdvanceCommandV1 command)
        {
            IRunConditionRuntimePortV1 port = ResolveConditionPort();
            if (command == null)
            {
                return RejectAdvance(
                    port,
                    null,
                    RunConditionAdvanceStatusV1.Rejected,
                    "run-condition-advance-command-null");
            }
            if (command.RunStableId != RunStableId)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatusV1.WrongRun,
                    "run-condition-advance-wrong-run");
            }
            if (command.RunLifecycleGeneration != lifecycleGeneration)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatusV1.StaleLifecycle,
                    command.RunLifecycleGeneration < lifecycleGeneration
                        ? "run-condition-advance-stale-generation"
                        : "run-condition-advance-future-generation");
            }
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatusV1.RunEnded,
                    "run-condition-advance-after-end");
            }
            if (command.AuthoritativeTick < authoritativeTick)
            {
                return RejectAdvance(
                    port,
                    command,
                    RunConditionAdvanceStatusV1.Rejected,
                    "run-condition-advance-tick-regression");
            }
            if (port == null)
            {
                return RejectAdvance(
                    null,
                    command,
                    RunConditionAdvanceStatusV1.Rejected,
                    "run-condition-authoritative-port-missing");
            }

            port.Bind(this);
            long previousTick = authoritativeTick;
            authoritativeTick = command.AuthoritativeTick;
            RunConditionAdvanceResultV1 result;
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
                RunConditionAdvanceStatusV1.Rejected,
                "run-condition-advance-null-result");
        }

        public RunConditionRuntimeSnapshotV1 ExportConditionRuntimeSnapshot()
        {
            IRunConditionRuntimePortV1 port = ResolveConditionPort();
            if (port == null)
            {
                throw new InvalidOperationException(
                    "The run does not own an authoritative condition runtime.");
            }
            port.Bind(this);
            return port.ExportConditionSnapshot();
        }

        public RuntimeModifierSnapshotV1 ExportConditionModifierProjection(
            StableId participantStableId)
        {
            IRunConditionRuntimePortV1 port = ResolveConditionPort();
            if (port == null)
            {
                throw new InvalidOperationException(
                    "The run does not own an authoritative condition runtime.");
            }
            port.Bind(this);
            return port.ExportModifierProjection(participantStableId);
        }

        private IRunConditionRuntimePortV1 ResolveConditionPort()
        {
            return RuntimePorts.ConditionalFacts as IRunConditionRuntimePortV1;
        }

        private static RunConditionDeliveryResultV1 RejectDelivery(
            IRunConditionRuntimePortV1 port,
            RunConditionGameplayFactCommandV1 command,
            RunConditionDeliveryStatusV1 status,
            string diagnostic)
        {
            return new RunConditionDeliveryResultV1(
                status,
                command,
                diagnostic,
                port == null ? null : port.ExportConditionSnapshot(),
                string.Empty);
        }

        private static RunConditionAdvanceResultV1 RejectAdvance(
            IRunConditionRuntimePortV1 port,
            RunConditionAdvanceCommandV1 command,
            RunConditionAdvanceStatusV1 status,
            string diagnostic)
        {
            return new RunConditionAdvanceResultV1(
                status,
                command,
                diagnostic,
                port == null ? null : port.ExportConditionSnapshot());
        }
    }
}
