# STAT-001 deterministic statistical verification

## Purpose

STAT-001 adds a verification-only statistical suite for the reward, equipment,
shop, crafting, augment-upgrade, money, scrap, and replay contracts present at
base commit `d4674b6e170e4bd207b469bfda65acf847edf060`.

The suite calls the existing runtime generators, policies, wallets, holdings
service, reward application authority, shop service, crafting service, and
strongbox opening service. It does not implement an alternate reward algorithm,
change production balance values, or edit scenes or serialized production
content.

## Method

All simulations use fixed root seeds and deterministic IDs. Statistical
assertions use deliberately broad tolerance bands around authored fixture
weights or configured ranges. Exact random samples are not pinned. Re-running a
batch with identical canonical inputs must, however, produce the same aggregate
fingerprint and counters.

The helper fingerprint is test infrastructure only. It hashes the ordered
sequence of runtime result fingerprints and observed reward deltas so a 100- or
1,000-open replay is compared exactly without asserting one particular random
roll in isolation.

## Verification matrix

| Area | Runtime path exercised | Sample | Assertion |
|---|---|---:|---|
| Strongbox levels | `StrongboxPowerBudgetPolicyV1` through `StrongboxEquipmentGenerationResolverV1` | 1,000 opens / 2,000 items | Both below-mean and above-mean results occur inside broad bands; average offset remains near the configured mean. |
| Strongbox quality | Existing GEN quality selection through the strongbox resolver | 2,000 items | Authored 3:1 common/rare fixture remains inside a 15%–35% rare band. |
| Duplicate definitions | Independent strongbox equipment slots sampled with replacement | 1,000 two-item opens | Same-definition pairs occur inside a 35%–65% band and are accepted. |
| Unique instances | Runtime-derived equipment instance IDs | 2,000 items | Every instance ID is unique even when definition IDs repeat. |
| Augment compensation | `RollItemLevel` plus `RollAugmentSlots` | 2,000 seeded rolls | Below-mean items receive a materially higher mean slot count than above-mean items. |
| Soft requirements | GEN candidate soft activation with a nominal level of 50 | 100 lower + 100 higher contexts | Generation succeeds at character levels 45 and 55 while respecting the authored item-level range. |
| Shop rolls | `ShopRuntimeServiceV1.Open` with real GEN inventory generation | 1,000 inventories / 4,000 entries | Two equal-weight definitions remain inside 35%–65%; 3:1 quality weights remain inside 15%–35%; all prices are positive; replay fingerprint is exact. |
| Crafting gates | `CraftingRecipeV1.ResolveUnlockLevel` and `CraftingServiceV1.Craft` | 1,000 seeds + boundary integration | Unlocks remain reproducible in levels 55–57; each bucket remains inside 20%–46%; one level below is rejected and the exact resolved level crafts successfully. |
| Augment upgrade costs | `AugmentUpgradeCostPolicyV1.TryCalculateCost` | tiers 1–3, levels 2–10 | Repeated calculations match exactly, costs increase by level, and higher tiers remain more expensive. |
| Money and scrap | Full `StrongboxOpeningServiceV1` → RAP → wallet authorities | 100 and 1,000 opens | Ordered results and deltas replay exactly; money and scrap means remain inside broad configured-range bands. |
| Exact replay | Full strongbox opening transaction | one applied open + exact replay | Replay returns `ExactDuplicateNoChange` and changes no wallet, holdings, opening-service, or RAP sequence or balance. |

## Fixed seeds and tolerance policy

The root seeds are constants in the test files. Per-sample seeds are derived by a
small deterministic SplitMix-style mixer in
`StatisticalVerificationAssertions.Seed`. This avoids correlated sequential
inputs while preserving exact replay.

Tolerance bands are intentionally wider than ordinary sampling error for the
sample sizes used. Their purpose is to catch meaningful distribution drift,
selection exclusion, broken compensation, or accidental de-duplication without
turning harmless deterministic implementation changes into brittle golden-roll
failures.

No tolerance is used for invariants:

- generated equipment instance IDs must be unique;
- duplicate definition IDs must remain valid;
- generated rewards must stay inside configured min/max ranges;
- prices must be positive;
- repeated batches with the same inputs must have identical fingerprints;
- exact transaction replay must add no value and advance no authority sequence.

## Running the suite

Use the repository-pinned Unity editor `6000.3.19f1` and run EditMode tests with
the name filter:

```text
ShooterMover.Tests.EditMode.StatisticalVerification
```

Example batch invocation on a machine with the pinned Unity editor installed:

```powershell
Unity.exe -batchmode -nographics -quit `
  -projectPath <repository-root> `
  -runTests -testPlatform EditMode `
  -testFilter ShooterMover.Tests.EditMode.StatisticalVerification `
  -testResults Logs/stat-001-editmode.xml `
  -logFile Logs/stat-001-editmode.log
```

## Owned changes

STAT-001 owns only:

- `Assets/ShooterMover/Tests/EditMode/StatisticalVerification/**`
- `docs/verification/STAT-001_STATISTICAL_VERIFICATION.md`

Rollback is a straight removal of those verification files and their Unity
metadata. There is no runtime, save-schema, registry, scene, prefab, or
production-balance migration.
