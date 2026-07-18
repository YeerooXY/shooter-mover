using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Progression.Skills.Presentation
{
    public sealed class RankedSkillsDemoXpAuthorityV2 : IRankedSkillsDemoXpAdapterV2, IRankedSkillsPlayerProgressSourceV2
    {
        private readonly HashSet<string> operations = new HashSet<string>(StringComparer.Ordinal);
        public RankedSkillsDemoXpAuthorityV2(string profileId,string classId,int level=1){ProfileId=profileId;ClassId=classId;SetFreshLevel(level);} public string ProfileId{get;private set;} public string ClassId{get;private set;} public int PlayerLevel=>CurrentLevel; public long CumulativeXp=>CurrentCumulativeXp; public int CurrentLevel{get;private set;} public long CurrentCumulativeXp{get;private set;}
        public long CumulativeXpThresholdForLevel(int level){if(level<1||level>100)throw new ArgumentOutOfRangeException(nameof(level));long n=level-1L;return 100L*n*n+400L*n;}
        public void GrantToThreshold(string operationId,long threshold){if(string.IsNullOrWhiteSpace(operationId))throw new ArgumentException("Operation id is required.",nameof(operationId));if(!operations.Add(operationId))return;if(threshold<CurrentCumulativeXp)throw new InvalidOperationException("XP grants cannot reduce cumulative XP.");CurrentCumulativeXp=threshold;CurrentLevel=Enumerable.Range(1,100).Last(x=>CumulativeXpThresholdForLevel(x)<=threshold);} private void SetFreshLevel(int level){CurrentLevel=Math.Max(1,Math.Min(100,level));CurrentCumulativeXp=CumulativeXpThresholdForLevel(CurrentLevel);}
    }

    public sealed class RankedSkillsDemoCreditAuthorityV2 : IRankedSkillsDemoCreditAdapterV2, ISkillRespecPaymentAuthorityV2
    {
        private sealed class ChargeRecord{public string Fingerprint;public SkillRespecPaymentResultV2 Result;}
        private readonly Dictionary<string,long> balances=new Dictionary<string,long>(StringComparer.Ordinal); private readonly Dictionary<string,ChargeRecord> charges=new Dictionary<string,ChargeRecord>(StringComparer.Ordinal); private readonly HashSet<string> sets=new HashSet<string>(StringComparer.Ordinal);
        public string CurrencyId=>"credits"; public long Balance(string profileId){long value;return balances.TryGetValue(profileId,out value)?value:0L;} public string PaymentStateFingerprint(string profileId)=>SkillFingerprintV2.Hash(profileId+"|"+Balance(profileId));
        public void SetDevelopmentBalance(string operationId,string profileId,long balance){if(balance<0)throw new ArgumentOutOfRangeException(nameof(balance));if(sets.Add(operationId))balances[profileId]=balance;}
        public SkillRespecPaymentResultV2 TryCharge(string operationId,string profileId,long amount,string expected)
        {string fp=SkillFingerprintV2.Hash(profileId+"|"+amount+"|"+expected);ChargeRecord prior;if(charges.TryGetValue(operationId,out prior))return prior.Fingerprint==fp?prior.Result:new SkillRespecPaymentResultV2(false,string.Empty,PaymentStateFingerprint(profileId));bool valid=amount>=0&&expected==PaymentStateFingerprint(profileId)&&Balance(profileId)>=amount;if(valid)balances[profileId]=Balance(profileId)-amount;var result=new SkillRespecPaymentResultV2(valid,valid?"demo-credit-receipt:"+operationId:string.Empty,PaymentStateFingerprint(profileId));charges[operationId]=new ChargeRecord{Fingerprint=fp,Result=result};return result;}
    }

    public sealed class RankedSkillsDemoRespecCostPolicyV2 : ISkillRespecCostPolicyV2
    { public long CalculateCost(string profileId,int allocatedPoints,long allocationVersion)=>allocatedPoints==0?0L:100L+allocatedPoints*25L; }

    public sealed class RankedSkillsDemoSessionFactoryV2 : IRankedSkillsDemoSessionFactoryV2
    { public IRankedSkillsDemoSessionV2 Create(RankedSkillsDemoClassV2 selectedClass,bool developmentControlsEnabled)=>new RankedSkillsDemoSessionV2(selectedClass,developmentControlsEnabled,RankedSkillsImportedCatalogAdapterV2.Load()); }

    public sealed class RankedSkillsDemoSessionV2 : IRankedSkillsDemoSessionV2
    {
        private readonly bool development; private readonly RankedSkillsImportedCatalogBundleV2 bundle; private RankedSkillsDemoClassV2 selectedClass; private RankedSkillAllocationAuthorityV2 allocation; private RankedSkillsDemoXpAuthorityV2 xp; private RankedSkillsDemoCreditAuthorityV2 credits; private IRankedSkillsScreenFacadeV2 screen; private int generation;
        public RankedSkillsDemoSessionV2(RankedSkillsDemoClassV2 selectedClass,bool developmentControlsEnabled,RankedSkillsImportedCatalogBundleV2 bundle){development=developmentControlsEnabled;this.bundle=bundle??throw new ArgumentNullException(nameof(bundle));this.selectedClass=selectedClass;Rebuild(1,10000L);}
        public string ProfileId=>xp.ProfileId; public string ClassId=>xp.ClassId; public IRankedSkillsScreenFacadeV2 Screen=>screen; public RankedSkillsDebugStateV2 DebugState=>new RankedSkillsDebugStateV2(development,xp.CurrentLevel,credits.Balance(ProfileId),"Lowering level or changing class creates a fresh profile. Catalog "+bundle.Fingerprint);
        public void ApplyTargetLevel(int targetLevel){Guard();targetLevel=Math.Max(1,Math.Min(100,targetLevel));if(targetLevel<xp.CurrentLevel){Rebuild(targetLevel,credits.Balance(ProfileId));return;}xp.GrantToThreshold(RankedSkillsDebugOperationIdsV2.ApplyLevel(ProfileId,targetLevel),xp.CumulativeXpThresholdForLevel(targetLevel));}
        public void SetDemoCredits(long balance){Guard();credits.SetDevelopmentBalance(RankedSkillsDebugOperationIdsV2.SetCredits(ProfileId,balance),ProfileId,balance);} public void ResetProfile(){Guard();Rebuild(1,10000L);} public void ChangeClass(RankedSkillsDemoClassV2 targetClass){Guard();selectedClass=targetClass;Rebuild(1,10000L);}
        private void Rebuild(int level,long balance){generation++;string classId=ClassIdOf(selectedClass);string profileId="skillui002.demo."+selectedClass.ToString().ToLowerInvariant()+".g"+generation;allocation=new RankedSkillAllocationAuthorityV2(bundle.Catalog);allocation.Seed(RankedSkillAllocationSnapshotV2.Empty(profileId,classId,bundle.Catalog));xp=new RankedSkillsDemoXpAuthorityV2(profileId,classId,level);credits=new RankedSkillsDemoCreditAuthorityV2();credits.SetDevelopmentBalance("seed:"+profileId,profileId,balance);var respec=new SkillRespecOrchestratorV2(bundle.Catalog,allocation,new RankedSkillsDemoRespecCostPolicyV2(),credits);screen=new RankedSkillsScreenFacadeV2(bundle,allocation,respec,xp,credits);}
        private void Guard(){if(!development)throw new InvalidOperationException("Demo mutation controls are unavailable outside editor/development composition.");}
        private static string ClassIdOf(RankedSkillsDemoClassV2 value){switch(value){case RankedSkillsDemoClassV2.CombatMedic:return "class.combat_medic";case RankedSkillsDemoClassV2.Juggernaut:return "class.juggernaut";default:return "class.striker";}}
    }
}
