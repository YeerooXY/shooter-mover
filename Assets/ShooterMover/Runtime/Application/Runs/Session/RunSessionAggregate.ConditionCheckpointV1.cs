namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregateV1
    {
        public RunConditionCheckpointV1 ExportConditionCheckpoint()
        {
            return new RunConditionCheckpointV1(
                ExportCheckpoint(),
                ExportConditionRuntimeSnapshot());
        }
    }
}
