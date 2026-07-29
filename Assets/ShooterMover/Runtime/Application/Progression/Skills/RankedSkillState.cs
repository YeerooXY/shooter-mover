using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Progression.Skills
{
    public enum SkillAllocationRejection { None, UnknownSkill, WrongClass, MaximumRank, InsufficientPoints, MissingPrerequisite, CategoryGate, StaleVersion, DuplicateConflict, CommitUnverified }

    public sealed class AllocateSkillRankCommand
    {
        public AllocateSkillRankCommand(string operationId, string profileId, string skillId, long expectedVersion, int playerLevel)
        { if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("Stable identities are required."); if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel)); OperationId = operationId.Trim(); ProfileId = profileId.Trim(); SkillId = skillId.Trim(); ExpectedVersion = expectedVersion; PlayerLevel = playerLevel; Fingerprint = SkillFingerprint.Hash(OperationId + "|" + ProfileId + "|" + SkillId + "|" + ExpectedVersion + "|" + PlayerLevel); }
        public string OperationId { get; } public string ProfileId { get; } public string SkillId { get; } public long ExpectedVersion { get; } public int PlayerLevel { get; } public string Fingerprint { get; }
    }

    public sealed class SkillAllocationResult
    {
        public SkillAllocationResult(AllocateSkillRankCommand command, bool accepted, SkillAllocationRejection rejection, RankedSkillAllocationSnapshot snapshot, SkillEffectSnapshot effects)
        { CommandFingerprint = command.Fingerprint; Accepted = accepted; Rejection = rejection; Snapshot = snapshot; Effects = effects; Fingerprint = SkillFingerprint.Hash(CommandFingerprint + "|" + accepted + "|" + rejection + "|" + snapshot.Fingerprint + "|" + effects.Fingerprint); }
        public string CommandFingerprint { get; } public bool Accepted { get; } public SkillAllocationRejection Rejection { get; } public RankedSkillAllocationSnapshot Snapshot { get; } public SkillEffectSnapshot Effects { get; } public string Fingerprint { get; }
    }

    public sealed class RankedSkillAllocationState
    {
        private readonly RankedSkillCatalog catalog; private readonly SkillEffectProjector projector; private readonly Dictionary<string, RankedSkillAllocationSnapshot> snapshots = new Dictionary<string, RankedSkillAllocationSnapshot>(StringComparer.Ordinal); private readonly Dictionary<string, SkillAllocationResult> replay = new Dictionary<string, SkillAllocationResult>(StringComparer.Ordinal); private readonly Dictionary<string, string> commitUnverified = new Dictionary<string, string>(StringComparer.Ordinal);
        public RankedSkillAllocationState(RankedSkillCatalog catalog, SkillEffectProjector projector = null) { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); this.projector = projector ?? new SkillEffectProjector(); }
        public RankedSkillCatalog Catalog => catalog;
        public void Seed(RankedSkillAllocationSnapshot snapshot) { if (snapshot == null) throw new ArgumentNullException(nameof(snapshot)); snapshots[snapshot.ProfileId] = snapshot; commitUnverified.Remove(snapshot.ProfileId); }
        public RankedSkillAllocationSnapshot Get(string profileId) => snapshots[profileId];
        public bool TryGet(string profileId, out RankedSkillAllocationSnapshot snapshot) => snapshots.TryGetValue(profileId ?? string.Empty, out snapshot);
        public bool IsCommitUnverified(string profileId) => !string.IsNullOrWhiteSpace(profileId) && commitUnverified.ContainsKey(profileId.Trim());
        public SkillAllocationResult Allocate(AllocateSkillRankCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            RankedSkillAllocationSnapshot current;
            if (!snapshots.TryGetValue(command.ProfileId, out current)) throw new InvalidOperationException("The ranked-skill profile is not seeded.");
            if (commitUnverified.ContainsKey(command.ProfileId)) return Result(command, false, SkillAllocationRejection.CommitUnverified, current, false);
            SkillAllocationResult previous; if (replay.TryGetValue(command.OperationId, out previous)) return previous.CommandFingerprint == command.Fingerprint ? previous : Result(command, false, SkillAllocationRejection.DuplicateConflict, current, false);
            var rejection = Validate(command, current); if (rejection != SkillAllocationRejection.None) return Result(command, false, rejection, current, true);
            var ranks = current.Ranks.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal); ranks[command.SkillId] = current.RankOf(command.SkillId) + 1;
            var next = new RankedSkillAllocationSnapshot(current.ProfileId, current.ClassId, current.Version + 1, catalog.SchemaVersion, catalog.ContentVersion, ranks); snapshots[current.ProfileId] = next; return Result(command, true, SkillAllocationRejection.None, next, true);
        }
        private SkillAllocationRejection Validate(AllocateSkillRankCommand command, RankedSkillAllocationSnapshot current)
        {
            if (command.ExpectedVersion != current.Version) return SkillAllocationRejection.StaleVersion;
            if (!string.Equals(current.SchemaVersion, catalog.SchemaVersion, StringComparison.Ordinal) || !string.Equals(current.ContentVersion, catalog.ContentVersion, StringComparison.Ordinal)) return SkillAllocationRejection.StaleVersion;
            RankedSkillDefinition skill; if (!catalog.TryGet(command.SkillId, out skill)) return SkillAllocationRejection.UnknownSkill;
            if (!skill.IsEligible(current.ClassId)) return SkillAllocationRejection.WrongClass;
            if (current.RankOf(skill.Id) >= skill.EffectiveMaximumRank(current.ClassId)) return SkillAllocationRejection.MaximumRank;
            if (current.AllocatedPoints >= command.PlayerLevel) return SkillAllocationRejection.InsufficientPoints;
            if (skill.Prerequisites.Any(x => current.RankOf(x.SkillId) < x.RequiredRank)) return SkillAllocationRejection.MissingPrerequisite;
            foreach (var gate in skill.CategoryGates)
            { int invested = current.Ranks.Where(x => { RankedSkillDefinition item; return catalog.TryGet(x.Key, out item) && item.CategoryId == gate.CategoryId; }).Sum(x => x.Value); if (invested < gate.RequiredPoints) return SkillAllocationRejection.CategoryGate; }
            return SkillAllocationRejection.None;
        }
        private SkillAllocationResult Result(AllocateSkillRankCommand command, bool accepted, SkillAllocationRejection rejection, RankedSkillAllocationSnapshot snapshot, bool remember) { var result = new SkillAllocationResult(command, accepted, rejection, snapshot, projector.Project(catalog, snapshot)); if (remember) replay[command.OperationId] = result; return result; }
        internal void Replace(RankedSkillAllocationSnapshot snapshot) { if (snapshot == null) throw new ArgumentNullException(nameof(snapshot)); if (IsCommitUnverified(snapshot.ProfileId)) throw new InvalidOperationException("The ranked-skill profile has an unverified persistence commit."); snapshots[snapshot.ProfileId] = snapshot; }
        internal bool MarkCommitUnverified(string profileId, string snapshotFingerprint)
        {
            RankedSkillAllocationSnapshot current;
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(snapshotFingerprint)
                || !snapshots.TryGetValue(profileId.Trim(), out current)
                || !string.Equals(current.Fingerprint, snapshotFingerprint.Trim(), StringComparison.Ordinal)) return false;
            commitUnverified[current.ProfileId] = current.Fingerprint; return true;
        }
        internal bool RollBackAccepted(string operationId, RankedSkillAllocationSnapshot accepted, RankedSkillAllocationSnapshot restore)
        {
            if (string.IsNullOrWhiteSpace(operationId) || accepted == null || restore == null || !string.Equals(accepted.ProfileId, restore.ProfileId, StringComparison.Ordinal)) return false;
            RankedSkillAllocationSnapshot current; SkillAllocationResult receipt;
            if (!snapshots.TryGetValue(restore.ProfileId, out current) || !string.Equals(current.Fingerprint, accepted.Fingerprint, StringComparison.Ordinal)) return false;
            if (!replay.TryGetValue(operationId, out receipt) || !receipt.Accepted || !string.Equals(receipt.Snapshot.Fingerprint, accepted.Fingerprint, StringComparison.Ordinal)) return false;
            snapshots[restore.ProfileId] = restore; replay.Remove(operationId); return true;
        }
    }

    public interface ISkillRespecPaymentState { string CurrencyId { get; } string PaymentStateFingerprint(string profileId); SkillRespecPaymentResult TryCharge(string operationId, string profileId, long amount, string expectedPaymentStateFingerprint); }
    public sealed class SkillRespecPaymentResult { public SkillRespecPaymentResult(bool succeeded, string receiptId, string stateFingerprint) { Succeeded = succeeded; ReceiptId = receiptId ?? string.Empty; StateFingerprint = stateFingerprint ?? string.Empty; } public bool Succeeded { get; } public string ReceiptId { get; } public string StateFingerprint { get; } }
    public interface ISkillRespecCostPolicy { long CalculateCost(string profileId, int allocatedPoints, long allocationVersion); }

    public sealed class SkillRespecQuote
    {
        public SkillRespecQuote(string profileId, long allocationVersion, int allocatedPoints, long exactCost, string currencyId, string paymentStateFingerprint)
        { ProfileId = profileId; AllocationVersion = allocationVersion; AllocatedPoints = allocatedPoints; ExactCost = exactCost; CurrencyId = currencyId; PaymentStateFingerprint = paymentStateFingerprint; Fingerprint = SkillFingerprint.Hash(profileId + "|" + allocationVersion + "|" + allocatedPoints + "|" + exactCost + "|" + currencyId + "|" + paymentStateFingerprint); }
        public string ProfileId { get; } public long AllocationVersion { get; } public int AllocatedPoints { get; } public long ExactCost { get; } public string CurrencyId { get; } public string PaymentStateFingerprint { get; } public string Fingerprint { get; }
    }

    public enum SkillRespecRejection { None, DuplicateConflict, StaleQuote, PaymentFailed }
    public sealed class SkillRespecReceipt
    {
        public SkillRespecReceipt(string operationId, bool accepted, SkillRespecRejection rejection, SkillRespecQuote quote, RankedSkillAllocationSnapshot before, RankedSkillAllocationSnapshot after, SkillEffectSnapshot effects, string paymentReceiptId)
        { OperationId = operationId; Accepted = accepted; Rejection = rejection; Quote = quote; Before = before; After = after; Effects = effects; PaymentReceiptId = paymentReceiptId ?? string.Empty; Fingerprint = SkillFingerprint.Hash(operationId + "|" + accepted + "|" + rejection + "|" + quote.Fingerprint + "|" + before.Fingerprint + "|" + after.Fingerprint + "|" + PaymentReceiptId); }
        public string OperationId { get; } public bool Accepted { get; } public SkillRespecRejection Rejection { get; } public SkillRespecQuote Quote { get; } public RankedSkillAllocationSnapshot Before { get; } public RankedSkillAllocationSnapshot After { get; } public SkillEffectSnapshot Effects { get; } public string PaymentReceiptId { get; } public string Fingerprint { get; }
    }

    public sealed class SkillRespecOrchestrator
    {
        private readonly RankedSkillCatalog catalog; private readonly RankedSkillAllocationState allocation; private readonly ISkillRespecCostPolicy policy; private readonly ISkillRespecPaymentState payment; private readonly SkillEffectProjector projector; private readonly Dictionary<string, SkillRespecReceipt> replay = new Dictionary<string, SkillRespecReceipt>(StringComparer.Ordinal); private readonly Dictionary<string, string> commands = new Dictionary<string, string>(StringComparer.Ordinal);
        public SkillRespecOrchestrator(RankedSkillCatalog catalog, RankedSkillAllocationState allocation, ISkillRespecCostPolicy policy, ISkillRespecPaymentState payment, SkillEffectProjector projector = null) { this.catalog = catalog; this.allocation = allocation; this.policy = policy; this.payment = payment; this.projector = projector ?? new SkillEffectProjector(); }
        public SkillRespecQuote Quote(string profileId) { var current = allocation.Get(profileId); return new SkillRespecQuote(profileId, current.Version, current.AllocatedPoints, policy.CalculateCost(profileId, current.AllocatedPoints, current.Version), payment.CurrencyId, payment.PaymentStateFingerprint(profileId)); }
        public SkillRespecReceipt Execute(string operationId, SkillRespecQuote quote)
        {
            string command = SkillFingerprint.Hash(operationId + "|" + quote.Fingerprint); SkillRespecReceipt prior; if (replay.TryGetValue(operationId, out prior)) return commands[operationId] == command ? prior : Reject(operationId, quote, SkillRespecRejection.DuplicateConflict);
            var current = allocation.Get(quote.ProfileId); if (allocation.IsCommitUnverified(quote.ProfileId) || current.Version != quote.AllocationVersion || current.AllocatedPoints != quote.AllocatedPoints || payment.PaymentStateFingerprint(quote.ProfileId) != quote.PaymentStateFingerprint || policy.CalculateCost(quote.ProfileId, current.AllocatedPoints, current.Version) != quote.ExactCost) return Remember(operationId, command, Reject(operationId, quote, SkillRespecRejection.StaleQuote));
            var charged = payment.TryCharge(operationId, quote.ProfileId, quote.ExactCost, quote.PaymentStateFingerprint); if (!charged.Succeeded) return Remember(operationId, command, Reject(operationId, quote, SkillRespecRejection.PaymentFailed));
            var empty = new RankedSkillAllocationSnapshot(current.ProfileId, current.ClassId, current.Version + 1, catalog.SchemaVersion, catalog.ContentVersion, null); allocation.Replace(empty); return Remember(operationId, command, new SkillRespecReceipt(operationId, true, SkillRespecRejection.None, quote, current, empty, projector.Project(catalog, empty), charged.ReceiptId));
        }
        private SkillRespecReceipt Reject(string operationId, SkillRespecQuote quote, SkillRespecRejection rejection) { var current = allocation.Get(quote.ProfileId); return new SkillRespecReceipt(operationId, false, rejection, quote, current, current, projector.Project(catalog, current), string.Empty); }
        private SkillRespecReceipt Remember(string operationId, string command, SkillRespecReceipt receipt) { replay[operationId] = receipt; commands[operationId] = command; return receipt; }
    }

    public sealed class SkillMigrationResult
    {
        public SkillMigrationResult(RankedSkillAllocationSnapshot snapshot, int refundedPoints, IReadOnlyList<string> diagnostics) { Snapshot = snapshot; RefundedPoints = refundedPoints; Diagnostics = diagnostics; Fingerprint = SkillFingerprint.Hash(snapshot.Fingerprint + "|" + refundedPoints + "|" + string.Join(";", diagnostics)); }
        public RankedSkillAllocationSnapshot Snapshot { get; } public int RefundedPoints { get; } public IReadOnlyList<string> Diagnostics { get; } public string Fingerprint { get; }
    }

    public sealed class SkillAllocationMigrator
    {
        public SkillMigrationResult Migrate(RankedSkillAllocationSnapshot source, RankedSkillCatalog target)
        {
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal); var diagnostics = new List<string>(); int refunded = 0;
            foreach (var pair in source.Ranks)
            { RankedSkillDefinition skill; if (!target.TryGet(pair.Key, out skill)) { refunded += pair.Value; diagnostics.Add("removed:" + pair.Key + ":" + pair.Value); continue; } if (!skill.IsEligible(source.ClassId)) { refunded += pair.Value; diagnostics.Add("ineligible:" + pair.Key + ":" + pair.Value); continue; } int kept = Math.Min(pair.Value, skill.EffectiveMaximumRank(source.ClassId)); ranks[pair.Key] = kept; if (kept < pair.Value) { refunded += pair.Value - kept; diagnostics.Add("cap-reduced:" + pair.Key + ":" + (pair.Value - kept)); } }
            return new SkillMigrationResult(new RankedSkillAllocationSnapshot(source.ProfileId, source.ClassId, source.Version + 1, target.SchemaVersion, target.ContentVersion, ranks), refunded, diagnostics.AsReadOnly());
        }
    }

    public static class SkillLiveReconciliation
    { public static int ClampCurrentCharges(int currentCharges, int baseMaximumCharges, SkillEffectSnapshot effects) { int maximum = (int)effects.Apply("movement.maximum_charges", baseMaximumCharges); return Math.Max(0, Math.Min(currentCharges, maximum)); } }
}
