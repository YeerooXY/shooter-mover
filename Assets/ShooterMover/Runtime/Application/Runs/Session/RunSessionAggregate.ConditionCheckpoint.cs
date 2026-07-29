namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregate
    {
        public RunConditionCheckpoint ExportConditionCheckpoint()
        {
            return new RunConditionCheckpoint(
                ExportCheckpoint(),
                ExportConditionRuntimeSnapshot());
        }
    }
}
