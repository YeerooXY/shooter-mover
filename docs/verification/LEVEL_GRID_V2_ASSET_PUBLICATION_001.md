# LEVEL-GRID-V2-ASSET-PUBLICATION-001 verification record

## Branch provenance

- repair target: merged PR #336 generated-asset publication blocker
- exact starting `main` SHA: `a8a96fad360c57643b907649e1202aa7684dc09e`
- branch: `agent/level-grid-v2-asset-publication-001`
- target: `main`
- pull request policy: draft; no merge or auto-merge

## Change contract

```text
compile in memory
→ write a new versioned generated folder
→ import and load every TextAsset
→ validate a staged runtime asset
→ switch the authoritative Resource asset
→ clean old output afterward
```

The previously playable Resource asset and all generated TextAssets it references must remain intact through every pre-commit failure. A failure after filesystem replacement must restore and verify the previous asset before returning failure. Cleanup after a verified switch is non-authoritative and best-effort.

## Ownership and boundaries

| Concept | Authority |
|---|---|
| authored topology and room content | exported Level Grid V2 folder |
| deterministic compiled package | `LevelGridV2Compiler` result in memory |
| generated JSON version | content-addressed immutable folder below the selected generated root |
| playable runtime selection | one `JsonRoomContentDefinition2D` at the selected Resource path |
| old-version deletion eligibility | references from every `JsonRoomContentDefinition2D` in AssetDatabase |
| transaction commit | replaced Resource asset synchronously imports and passes its real importer |
| rollback | asset compiler restores exact previous `.asset` bytes and revalidates them |

- pure compiler stays in `ShooterMover.Application` with `noEngineReferences`;
- runtime asset stays in `ShooterMover.UnityAdapters`;
- AssetDatabase, staging, atomic replacement and cleanup stay in the Editor-only tooling assembly;
- failure-injection tests stay in a dedicated Editor-only test assembly;
- player builds continue loading only the Resource asset and its serialized TextAsset references.

## Invariant ledger

- no generated file referenced by the current playable asset is overwritten before commit;
- generated versions are immutable and content-addressed;
- an existing version is accepted only after exact content and importer validation;
- the Resource asset GUID remains stable when replacing an existing asset;
- an external destination or `.meta` change is never overwritten silently;
- every post-replacement failure restores and validates the previous authority;
- a newly created destination is removed completely on rollback;
- cleanup failure cannot report a committed publication as failed;
- cleanup retains output referenced by any runtime asset, not only the destination being compiled;
- one transaction never removes another transaction's stage or marked version;
- retry reuses only a complete validated version and cannot duplicate runtime authority;
- Editor dependencies do not enter runtime assemblies.

## Failure-mode matrix

| Condition | Required behavior |
|---|---|
| move / rename authored folders | publication owns no authoring identity; the established stable-ID exporter remains authoritative |
| delete or output shrink | old files remain until commit, then only unreferenced versions/files are reconciled |
| duplicate package version | reuse only after exact TextAsset and importer validation |
| malformed existing version | fail closed; do not rebuild or overwrite the immutable path |
| missing imported TextAsset | fail before Resource switch and delete only transaction-owned output |
| malformed staged runtime asset | fail before Resource switch |
| destination wrong type | fail before generated output is written |
| destination changed externally | hash mismatch blocks switch before mutation |
| failure after file replacement | atomically restore previous bytes and validate restored asset |
| failure creating a new destination | remove destination and generated `.meta` |
| retry after pre-commit failure | new stage; reuse final version only if complete and valid |
| stale publishing marker | recover only when a committed runtime asset references the exact valid version; otherwise fail closed |
| cleanup failure | return the committed asset and warn |
| another runtime asset references an old version | retain that version |
| another compiler transaction is staging | do not enumerate or delete its runtime stage; skip marked versions |
| restart/domain reload | Resource asset holds ordinary serialized GUID references to immutable TextAssets |

## Production changes

- `LevelGridV2AssetCompiler` is now the compile-validation and publication orchestration surface.
- `LevelGridV2AssetCompilerPublication` owns version staging, runtime staging, destination snapshotting, atomic replacement and rollback.
- `LevelGridV2AssetCompilerCleanup` owns reference-aware reconciliation, version hashing and transaction-owned cleanup.
- Foundation editor tooling now has a dedicated Editor-only asmdef.
- the existing EditMode assembly explicitly references the editor tooling assembly.
- a separate Editor tooling test asmdef exercises the real Unity editor boundary.

## Tests authored

`LevelGridV2AssetCompilerPublicationTests` covers:

1. failure immediately before the authoritative switch preserves exact previous `.asset` bytes, manifest reference and importer validity;
2. injected failure immediately after filesystem replacement restores exact previous bytes and importer validity;
3. successful recompilation switches to a different immutable version and deletes the unreferenced old version;
4. cleanup retains an old immutable version while another runtime asset still references it;
5. injected post-commit cleanup failure returns the new valid asset and logs a warning;
6. an existing wrong-type destination fails before generated output is created.

The tests use the tracked valid compiler-ready source, real files below `Assets/`, synchronous `AssetDatabase` import, real TextAsset loading, real ScriptableObject serialization and the retained V1 importer.

## Validation executed in this environment

- static source review: completed;
- structural delimiter and preprocessor checks: completed and passed for every authored C# file;
- asmdef JSON parsing: completed and passed;
- namespace and assembly reference audit against merged PRs #336 and #337: completed;
- compilation: not executed; no Unity or C# compiler is available in this environment;
- automated tests: not executed;
- framework-specific Unity EditMode tests: not executed;
- manual acceptance: not executed;
- performance testing: not executed.

## Required Unity proof

Keep the follow-up PR draft until Unity 6000.3.19f1 provides:

1. project import and domain reload with no assembly or serialization errors;
2. passing `ShooterMover.Tests.EditorTooling` EditMode XML;
3. passing existing `ShooterMover.Tests.EditMode` and relevant PlayMode tests;
4. manual compile of the tracked Combat Loop package;
5. failure-injection confirmation that the old playable Resource remains valid;
6. successful normal Level Selection and three-room route after publication.

## Generated and metadata files

This repair does not regenerate or replace the currently tracked playable JSON/Resource assets because Unity execution is unavailable. It changes the next publication route. New `.meta` files are tracked only for the new handwritten C#, asmdefs and test folder/files.

## Unverified behavior

- Unity's platform implementation of `File.Replace` in the target Editor environment;
- AssetDatabase refresh/import behavior after same-directory atomic replacement;
- exact GUID retention and object reload in a real Unity domain;
- EditMode failure-injection execution;
- live Resource loading after a successful compile.

## Remaining design debt

A process crash can leave a transaction-owned staging folder or publishing marker. Marked final versions are intentionally fail-closed unless a committed runtime asset references them. A later maintenance command could provide explicit stale-stage inspection and quarantine, but automatic age-based deletion is deliberately outside this repair.
