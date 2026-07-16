# Wave 2 dispatch validation

Validated exact source commit:

`6d04451883127dcf597c4f6fec199aeaec2a7f9e`

Environment:

- Windows
- Unity `6000.3.19f1`
- clean coordinator worktree created from `origin/main`

Results:

| Check | Result |
|---|---|
| Repository layout | Passed |
| Unity assembly graph | Passed |
| Duplicate Unity asset GUID audit | Passed, zero duplicates |
| Cold Unity import and script compile | Passed, exit code 0 |
| Full EditMode | 568/568 passed |
| OBJ-001 `PlacedObjectAuthoring2DTests` | 11/11 passed |
| Full PlayMode | 263/266 passed |

The three full PlayMode failures are outside Wave 1 ownership and are not caused
by the merged foundation changes:

1. `RocketLauncherPackageTests.PackageSurface_IsBounded2DAndWarningCannotObstructScreen`
   expects the projectile prefab to contain no `SpriteRenderer`, while the
   accepted visible-projectile implementation now contains one.
2. `EvidenceEntrypointSmokeTests.PowerShellContractTests_CoverParsingPathsStaleOutputAndExitPropagation`
   receives empty output from its nested PowerShell contract invocation.
3. `VisibleSliceGeneralCombatHudTests.OwnedRuntimeSource_DoesNotReferenceOrDuplicateWp010`
   contains a stale source-shape/line-count expectation (`196`, actual `134`).

These failures should receive a separately owned test-maintenance task. Wave 2
agents must not repair them opportunistically.
