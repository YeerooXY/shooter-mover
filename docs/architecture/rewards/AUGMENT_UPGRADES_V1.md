# Augment Upgrades V1

Status: AUG-001 runtime contract and implementation baseline
Owner: `ShooterMover.Application.Equipment.Upgrades.AugmentUpgradeServiceV1`

## Scope

AUG-001 upgrades one installed augment on one owned immutable equipment instance.
The operation spends money through MON-001, retires the old holding through
INV-001, and delivers a deterministic immutable replacement through RAP-001.
There is no UI, shop, reroll, star, scene, or Unity asset behavior in this
boundary.

## Configured levels and tiers

Upgrade limits are read from the existing `AugmentDefinition.TierRange` and
`AugmentDefinition.LevelRange`. Runtime code does not contain a ten-level or
three-tier ceiling. A catalog may use the common three-tier, ten-level design or
another positive maximum.

Money costs are supplied by `AugmentUpgradeCostPolicyV1`. The policy contains a
canonical fingerprint and one `AugmentTierCostCurveV1` for every supported tier.
Different tiers may use different curves. The default step policy accepts only
`current + 1`; a policy may explicitly permit a multi-level target, in which
case the quote sums every intervening step cost.

## Quote contract

`AugmentUpgradeQuoteRequestV1` identifies:

- the owned equipment instance;
- the installed augment instance (the stable slot identity);
- the requested target level.

A successful `AugmentUpgradeQuoteV1` retains:

- equipment instance StableId and immutable fingerprint;
- sorted augment slot index, augment instance StableId, definition StableId,
  and tier;
- current and target levels;
- current money balance and wallet sequence;
- current holdings sequence;
- exact money cost;
- equipment-catalog fingerprint;
- cost-policy fingerprint;
- canonical quote fingerprint and derived quote StableId.

Quoting does not reserve money, inventory, or a level. It is a deterministic
snapshot that confirmation must prove is still current.

## Confirmation and stale-data protection

`AugmentUpgradeConfirmationV1` binds a caller-owned confirmation StableId to the
complete quote and the quote fingerprint the caller actually observed.
Confirmation rejects before value mutation when any of the following is true:

- the confirmation or quote is malformed;
- the quote fingerprint, catalog fingerprint, or cost-policy fingerprint is
  stale;
- the equipment is no longer owned;
- the immutable equipment fingerprint changed;
- the augment slot/identity disappeared or changed;
- current tier or level differs from the quote;
- the target is not an allowed next step;
- the target exceeds the augment definition's configured maximum;
- the recomputed exact cost differs;
- wallet or holdings sequence differs;
- money is insufficient;
- the immutable replacement fails the existing equipment validator.

Rejected preflight confirmations do not call MON, INV, or RAP.

## Immutable replacement

Equipment and augment objects are never edited in place.

A successful preparation:

1. creates a new `AugmentInstance` value with the same augment instance,
   definition, and tier and only the intended level changed;
2. creates a new `EquipmentInstance` with a deterministic replacement instance
   StableId;
3. preserves equipment definition, item level, quality, every unrelated
   augment, and all installed augment identities;
4. validates the replacement through the existing equipment catalog validator.

INV-001 permanently remembers every unique instance identity, including removed
ones. Reusing the retired equipment instance StableId would therefore be a
unique-instance collision. AUG-001 derives a new immutable equipment identity
while retaining the original definition and installed-augment identities.

The removal command carries the original holding provenance. The RAP equipment
grant creates the replacement holding provenance from the deterministic upgrade
grant and source-operation identities, matching the existing INV/RAP contract.

## Application order and atomic retry

After complete validation, AUG-001 deterministically prepares all identities and
commands, then:

1. commits the immutable replacement reward to RAP;
2. spends the exact money cost once through MON with the quoted sequence;
3. removes the old equipment once through INV with the quoted sequence;
4. claims the replacement equipment through RAP with the post-removal holdings
   sequence.

Transaction, operation, source, commitment, claim, grant, and replacement
instance identities are SHA-256-derived from the confirmation, quote, and
replacement fingerprints. They are independent of clocks, random GUIDs, scene
objects, and retry count.

MON, INV, and RAP do not expose a shared distributed lock. AUG-001 follows RAP's
caller-observable atomic roll-forward model:

- predictable failures are rejected before value mutation;
- success is reported only after money spend, old-item retirement, and RAP
  replacement application are all confirmed;
- an interruption after RAP preflight returns `PendingRetry`;
- `Retry` reuses the exact money transaction, removal transaction, replacement
  instance, commitment, and claim identities;
- exact applied child transactions are treated as resolved, so retry cannot
  spend or remove twice;
- no compensating money grant or inventory rollback is invented.

A save-game composition that persists in-flight upgrades must persist AUG's
prepared confirmation/fact records together with MON, INV, and RAP snapshots.
Filesystem persistence is outside AUG-001.

## Duplicate semantics

- Repeating the exact confirmation StableId and canonical payload returns
  `ExactDuplicateNoChange` and the original deterministic identities.
- Reusing a confirmation StableId with different canonical content returns
  `ConflictingDuplicate` without mutation.
- Retrying an Applied confirmation is a no-change replay.
- Retrying a pending confirmation resumes only the same prepared operation.

## Terminal facts

`AugmentUpgradeFactV1` reports machine-readable status plus:

- confirmation and quote fingerprints;
- money and holdings-removal transaction identities;
- replacement equipment identity and fingerprint;
- RAP commitment and claim identities;
- exact cost;
- before/after wallet and holdings sequences;
- deterministic rejection code and fact fingerprint.

`Applied` is the only successful original status. `PendingRetry` is explicitly
non-terminal from the caller's point of view and must not be presented as a
completed upgrade.
