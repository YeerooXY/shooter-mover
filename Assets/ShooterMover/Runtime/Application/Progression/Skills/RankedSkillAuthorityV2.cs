using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Progression.Skills
{
    public enum SkillAllocationRejectionV2 { None, UnknownSkill, WrongClass, MaximumRank, InsufficientPoints, MissingPrerequisite, CategoryGate, StaleVersion, DuplicateConflict }

    public sealed class AllocateSkillRankCommandV2
    {
        public AllocateSkillRankCommandV2(string operationId, string profileId, string skillId, long expectedVersion, int playerLevel)
        { if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("Stable identities are required."); if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel)); OperationId = operationId.Trim(); ProfileId = profileId.Trim(); SkillId = skillId.Trim(); ExpectedVersion = expectedVersion; PlayerLevel = playerLevel; Fingerprint = SkillFingerprintV2.Hash(OperationId + "|" + ProfileId + "|" + SkillId + "|" + ExpectedVersion + "|" + PlayerLevel); }
        public string OperationId { get; } public string ProfileId { get; } public string SkillId { get; } public long ExpectedVersion { get; } public int PlayerLevel { get; } public string Fingerprint { get; }
    }

    public sealed class SkillAllocationResultV2
    {
        public SkillAllocationResultV2(AllocateSkillRankCommandV2 command, bool accepted, SkillAllocationRejectionV2 rejection, RankedSkillAllocationSnapshotV2 snapshot, SkillEffectSnapshotV2 effects)
        { CommandFingerprint = command.Fingerprint; Accepted = accepted; Rejection = rejection; Snapshot = snapshot; Effects = effects; Fingerprint = SkillFingerprintV2.Hash(CommandFingerprint + "|" + accepted + "|" + rejection + "|" + snapshot.Fingerprint + "|" + effects.Fingerprint); }
        public string CommandFingerprint { get; } public bool Accepted { get; } public SkillAllocationRejectionV2 Rejection { get; } public RankedSkillAllocationSnapshotV2 Snapshot { get; } public SkillEffectSnapshotV2 Effects { get; } public string Fingerprint { get; }
    }

    public sealed class RankedSkillAllocationAuthorityV2
    {
        private readonly RankedSkillCatalogV2 catalog; private readonly SkillEffectProjectorV2 projector; private readonly Dictionary<string, RankedSkillAllocationSnapshotV2> snapshots = new Dictionary<string, RankedSkillAllocationSnapshotV2>(StringComparer.Ordinal); private readonly Dictionary<string, SkillAllocationResultV2> replay = new Dictionary<string, SkillAllocationResultV2>(StringComparer.Ordinal);
        public RankedSkillAllocationAuthorityV2(RankedSkillCatalogV2 catalog, SkillEffectProjectorV2 projector = null) { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); this.projector = projector ?? new SkillEffectProjectorV2(); }
        public void Seed(RankedSkillAllocationSnapshotV2 snapshot) { snapshots[snapshot.ProfileId] = snapshot; }
        public RankedSkillAllocationSnapshotV2 Get(string profileId) => snapshots[profileId];
        public SkillAllocationResultV2 Allocate(AllocateSkillRankCommandV2 command)
        {
            SkillAllocationResultV2 previous; if (replay.TryGetValue(command.OperationId, out previous)) return previous.CommandFingerprint == command.Fingerprint ? previous : Result(command, false, SkillAllocationRejectionV2.DuplicateConflict, snapshots[command.ProfileId], false);
            var current = snapshots[command.ProfileId]; var rejection = Validate(command, current); if (rejection != SkillAllocationRejectionV2.None) return Result(command, false, rejection, current, true);
            var ranks = current.Ranks.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal); ranks[command.SkillId] = current.RankOf(command.SkillId) + 1;
            var next = new RankedSkillAllocationSnapshotV2(current.ProfileId, current.ClassId, current.Version + 1, catalog.SchemaVersion, catalog.ContentVersion, ranks); snapshots[current.ProfileId] = next; return Result(command, true, SkillAllocationRejectionV2.None, next, true);
        }
        private SkillAllocationRejectionV2 Validate(AllocateSkillRankCommandV2 command, RankedSkillAllocationSnapshotV2 current)
        {
            if (command.ExpectedVersion != current.Version) return SkillAllocationRejectionV2.StaleVersion;
            RankedSkillDefinitionV2 skill; if (!catalog.TryGet(command.SkillId, out skill)) return SkillAllocationRejectionV2.UnknownSkill;
            if (!skill.IsEligible(current.ClassId)) return SkillAllocationRejectionV2.WrongClass;
            if (current.RankOf(skill.Id) >= skill.EffectiveMaximumRank(current.ClassId)) return SkillAllocationRejectionV2.MaximumRank;
            if (current.AllocatedPoints >= command.PlayerLevel) return SkillAllocationRejectionV2.InsufficientPoints;
            if (skill.Prerequisites.Any(x => current.RankOf(x.SkillId) < x.RequiredRank)) return SkillAllocationRejectionV2.MissingPrerequisite;
            foreach (var gate in skill.CategoryGates)
            { int invested = current.Ranks.Where(x => { RankedSkillDefinitionV2 item; return catalog.TryGet(x.Key, out item) && item.CategoryId == gate.CategoryId; }).Sum(x => x.Value); if (invested < gate.RequiredPoints) return SkillAllocationRejectionV2.CategoryGate; }
            return SkillAllocationRejectionV2.None;
        }
        private SkillAllocationResultV2 Result(AllocateSkillRankCommandV2 command, bool accepted, SkillAllocationRejectionV2 rejection, RankedSkillAllocationSnapshotV2 snapshot, bool remember) { var result = new SkillAllocationResultV2(command, accepted, rejection, snapshot, projector.Project(catalog, snapshot)); if (remember) replay[command.OperationId] = result; return result; }
        internal void Replace(RankedSkillAllocationSnapshotV2 snapshot) { snapshots[snapshot.ProfileId] = snapshot; }
    }

    public interface ISkillRespecPaymentAuthorityV2 { string CurrencyId { get; } string PaymentStateFingerprint(string profileId); SkillRespecPaymentResultV2 TryCharge(string operationId, string profileId, long amount, string expectedPaymentStateFingerprint); }
    public sealed class SkillRespecPaymentResultV2 { public SkillRespecPaymentResultV2(bool succeeded, string receiptId, string stateFingerprint) { Succeeded = succeeded; ReceiptId = receiptId ?? string.Empty; StateFingerprint = stateFingerprint ?? string.Empty; } public bool Succeeded { get; } public string ReceiptId { get; } public string StateFingerprint { get; } }
    public interface ISkillRespecCostPolicyV2 { long CalculateCost(string profileId, int allocatedPoints, long allocationVersion); }

    public sealed class SkillRespecQuoteV2
    {
        public SkillRespecQuoteV2(string profileId, long allocationVersion, int allocatedPoints, long exactCost, string currencyId, string paymentStateFingerprint)
        { ProfileId = profileId; AllocationVersion = allocationVersion; AllocatedPoints = allocatedPoints; ExactCost = exactCost; CurrencyId = currencyId; PaymentStateFingerprint = paymentStateFingerprint; Fingerprint = SkillFingerprintV2.Hash(profileId + "|" + allocationVersion + "|" + allocatedPoints + "|" + exactCost + "|" + currencyId + "|" + paymentStateFingerprint); }
        public string ProfileId { get; } public long AllocationVersion { get; } public int AllocatedPoints { get; } public long ExactCost { get; } public string CurrencyId { get; } public string PaymentStateFingerprint { get; } public string Fingerprint { get; }
    }

    public enum SkillRespecRejectionV2 { None, DuplicateConflict, StaleQuote, PaymentFailed }
    public sealed class SkillRespecReceiptV2
    {
        public SkillRespecReceiptV2(string operationId, bool accepted, SkillRespecRejectionV2 rejection, SkillRespecQuoteV2 quote, RankedSkillAllocationSnapshotV2 before, RankedSkillAllocationSnapshotV2 after, SkillEffectSnapshotV2 effects, string paymentReceiptId)
        { OperationId = operationId; Accepted = accepted; Rejection = rejection; Quote = quote; Before = before; After = after; Effects = effects; PaymentReceiptId = paymentReceiptId ?? string.Empty; Fingerprint = SkillFingerprintV2.Hash(operationId + "|" + accepted + "|" + rejection + "|" + quote.Fingerprint + "|" + before.Fingerprint + "|" + after.Fingerprint + "|" + PaymentReceiptId); }
        public string OperationId { get; } public bool Accepted { get; } public SkillRespecRejectionV2 Rejection { get; } public SkillRespecQuoteV2 Quote { get; } public RankedSkillAllocationSnapshotV2 Before { get; } public RankedSkillAllocationSnapshotV2 After { get; } public SkillEffectSnapshotV2 Effects { get; } public string PaymentReceiptId { get; } public string Fingerprint { get; }
    }

    public sealed class SkillRespecOrchestratorV2
    {
        private readonly RankedSkillCatalogV2 catalog; private readonly RankedSkillAllocationAuthorityV2 allocation; private readonly ISkillRespecCostPolicyV2 policy; private readonly ISkillRespecPaymentAuthorityV2 payment; private readonly SkillEffectProjectorV2 projector; private readonly Dictionary<string, SkillRespecReceiptV2> replay = new Dictionary<string, SkillRespecReceiptV2>(StringComparer.Ordinal); private readonly Dictionary<string, string> commands = new Dictionary<string, string>(StringComparer.Ordinal);
        public SkillRespecOrchestratorV2(RankedSkillCatalogV2 catalog, RankedSkillAllocationAuthorityV2 allocation, ISkillRespecCostPolicyV2 policy, ISkillRespecPaymentAuthorityV2 payment, SkillEffectProjectorV2 projector = null) { this.catalog = catalog; this.allocation = allocation; this.policy = policy; this.payment = payment; this.projector = projector ?? new SkillEffectProjectorV2(); }
        public SkillRespecQuoteV2 Quote(string profileId) { var current = allocation.Get(profileId); return new SkillRespecQuoteV2(profileId, current.Version, current.AllocatedPoints, policy.CalculateCost(profileId, current.AllocatedPoints, current.Version), payment.CurrencyId, payment.PaymentStateFingerprint(profileId)); }
        public SkillRespecReceiptV2 Execute(string operationId, SkillRespecQuoteV2 quote)
        {
            string command = SkillFingerprintV2.Hash(operationId + "|" + quote.Fingerprint); SkillRespecReceiptV2 prior; if (replay.TryGetValue(operationId, out prior)) return commands[operationId] == command ? prior : Reject(operationId, quote, SkillRespecRejectionV2.DuplicateConflict);
            var current = allocation.Get(quote.ProfileId); if (current.Version != quote.AllocationVersion || current.AllocatedPoints != quote.AllocatedPoints || payment.PaymentStateFingerprint(quote.ProfileId) != quote.PaymentStateFingerprint || policy.CalculateCost(quote.ProfileId, current.AllocatedPoints, current.Version) != quote.ExactCost) return Remember(operationId, command, Reject(operationId, quote, SkillRespecRejectionV2.StaleQuote));
            var charged = payment.TryCharge(operationId, quote.ProfileId, quote.ExactCost, quote.PaymentStateFingerprint); if (!charged.Succeeded) return Remember(operationId, command, Reject(operationId, quote, SkillRespecRejectionV2.PaymentFailed));
            var empty = new RankedSkillAllocationSnapshotV2(current.ProfileId, current.ClassId, current.Version + 1, catalog.SchemaVersion, catalog.ContentVersion, null); allocation.Replace(empty); return Remember(operationId, command, new SkillRespecReceiptV2(operationId, true, SkillRespecRejectionV2.None, quote, current, empty, projector.Project(catalog, empty), charged.ReceiptId));
        }
        private SkillRespecReceiptV2 Reject(string operationId, SkillRespecQuoteV2 quote, SkillRespecRejectionV2 rejection) { var current = allocation.Get(quote.ProfileId); return new SkillRespecReceiptV2(operationId, false, rejection, quote, current, current, projector.Project(catalog, current), string.Empty); }
        private SkillRespecReceiptV2 Remember(string operationId, string command, SkillRespecReceiptV2 receipt) { replay[operationId] = receipt; commands[operationId] = command; return receipt; }
    }

    public sealed class SkillMigrationResultV2
    {
        public SkillMigrationResultV2(RankedSkillAllocationSnapshotV2 snapshot, int refundedPoints, IReadOnlyList<string> diagnostics) { Snapshot = snapshot; RefundedPoints = refundedPoints; Diagnostics = diagnostics; Fingerprint = SkillFingerprintV2.Hash(snapshot.Fingerprint + "|" + refundedPoints + "|" + string.Join(";", diagnostics)); }
        public RankedSkillAllocationSnapshotV2 Snapshot { get; } public int RefundedPoints { get; } public IReadOnlyList<string> Diagnostics { get; } public string Fingerprint { get; }
    }

    public sealed class SkillAllocationMigratorV2
    {
        public SkillMigrationResultV2 Migrate(RankedSkillAllocationSnapshotV2 source, RankedSkillCatalogV2 target)
        {
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal); var diagnostics = new List<string>(); int refunded = 0;
            foreach (var pair in source.Ranks)
            { RankedSkillDefinitionV2 skill; if (!target.TryGet(pair.Key, out skill)) { refunded += pair.Value; diagnostics.Add("removed:" + pair.Key + ":" + pair.Value); continue; } if (!skill.IsEligible(source.ClassId)) { refunded += pair.Value; diagnostics.Add("ineligible:" + pair.Key + ":" + pair.Value); continue; } int kept = Math.Min(pair.Value, skill.EffectiveMaximumRank(source.ClassId)); ranks[pair.Key] = kept; if (kept < pair.Value) { refunded += pair.Value - kept; diagnostics.Add("cap-reduced:" + pair.Key + ":" + (pair.Value - kept)); } }
            return new SkillMigrationResultV2(new RankedSkillAllocationSnapshotV2(source.ProfileId, source.ClassId, source.Version + 1, target.SchemaVersion, target.ContentVersion, ranks), refunded, diagnostics.AsReadOnly());
        }
    }

    public static class SkillRuntimeReconciliationV2
    { public static int ClampCurrentCharges(int currentCharges, int baseMaximumCharges, SkillEffectSnapshotV2 effects) { int maximum = (int)effects.Apply("movement.maximum_charges", baseMaximumCharges); return Math.Max(0, Math.Min(currentCharges, maximum)); } }
}
