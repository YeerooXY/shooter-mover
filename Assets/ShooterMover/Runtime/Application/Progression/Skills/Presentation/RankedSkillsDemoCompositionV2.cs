using System;

namespace ShooterMover.Application.Progression.Skills.Presentation
{
    public enum RankedSkillsDemoClassV2 { Striker, CombatMedic, Juggernaut }

    public sealed class RankedSkillsDebugStateV2
    {
        public RankedSkillsDebugStateV2(bool controlsAvailable, int targetLevel, long targetCredits, string resetNotice)
        { ControlsAvailable = controlsAvailable; TargetLevel = targetLevel; TargetCredits = targetCredits; ResetNotice = resetNotice ?? string.Empty; }
        public bool ControlsAvailable { get; }
        public int TargetLevel { get; }
        public long TargetCredits { get; }
        public string ResetNotice { get; }
    }

    public interface IRankedSkillsDemoXpAdapterV2
    {
        int CurrentLevel { get; }
        long CurrentCumulativeXp { get; }
        long CumulativeXpThresholdForLevel(int level);
        void GrantToThreshold(string operationId, long cumulativeXpThreshold);
    }

    public interface IRankedSkillsDemoCreditAdapterV2 : IRankedSkillsCreditProjectionV2
    { void SetDevelopmentBalance(string operationId, string profileId, long balance); }

    public interface IRankedSkillsDemoSessionV2
    {
        string ProfileId { get; }
        string ClassId { get; }
        IRankedSkillsScreenFacadeV2 Screen { get; }
        RankedSkillsDebugStateV2 DebugState { get; }
        void ApplyTargetLevel(int targetLevel);
        void SetDemoCredits(long balance);
        void ResetProfile();
        void ChangeClass(RankedSkillsDemoClassV2 targetClass);
    }

    public interface IRankedSkillsDemoSessionFactoryV2
    { IRankedSkillsDemoSessionV2 Create(RankedSkillsDemoClassV2 selectedClass, bool developmentControlsEnabled); }

    public sealed class RankedSkillsDemoSessionHostV2
    {
        private readonly IRankedSkillsDemoSessionFactoryV2 factory;
        private IRankedSkillsDemoSessionV2 session;
        public RankedSkillsDemoSessionHostV2(IRankedSkillsDemoSessionFactoryV2 factory) { this.factory = factory ?? throw new ArgumentNullException(nameof(factory)); }
        public bool HasSession => session != null;
        public IRankedSkillsDemoSessionV2 GetOrCreate(RankedSkillsDemoClassV2 initialClass, bool developmentControlsEnabled) => session ?? (session = factory.Create(initialClass, developmentControlsEnabled));
        public void ResetHost() { session = null; }
    }

    public static class RankedSkillsDebugOperationIdsV2
    {
        public static string ApplyLevel(string profileId, int targetLevel) => "skillui002:level:" + profileId + ":" + targetLevel;
        public static string SetCredits(string profileId, long balance) => "skillui002:credits:" + profileId + ":" + balance;
        public static string Allocate(string profileId, string skillId, long expectedVersion) => "skillui002:allocate:" + profileId + ":" + skillId + ":" + expectedVersion;
        public static string Respec(string profileId, string quoteFingerprint) => "skillui002:respec:" + profileId + ":" + quoteFingerprint;
    }
}
