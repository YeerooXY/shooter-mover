# LEVEL-GRID-V2-ASSET-PUBLISH-ATOMIC-336 verification record

## Branch provenance

- source blocker: merged PR #336
- branch: `agent/level-grid-v2-asset-publish-atomic-336`
- exact starting `main` SHA: `a8a96fad360c57643b907649e1202aa7684dc09e`
- target: `main`
- pull request policy: draft only; no merge or auto-merge until Unity compilation and Editor tests execute

## Change contract

The Level Grid V2 compiler must never mutate the previously playable generated package or its authoritative `JsonRoomContentDefinition2D` Resource asset before a complete candidate has been written, imported and runtime-import validated.

The authoritative runtime entry remains the stable Resource asset path selected by the production catalogue. Generated JSON is immutable, versioned input owned by the exact published Resource asset. The authoring folder remains the source authority.

## Transaction boundary

```text
compile authoring folder in memory
→ validate compiled package through RoomContentJsonImporterV1
→ write a new immutable version folder
→ synchronously import every staged TextAsset
→ create and save a candidate JsonRoomContentDefinition2D asset
→ validate candidate.Import()
→ atomically replace the authoritative Resource asset file
→ synchronously import and validate the committed Resource asset
→ best-effort cleanup of obsolete versions and transaction files
```

The commit point is replacement of the authoritative Resource `.asset` file. Before that point, all writes are isolated to a new version folder and candidate asset. If replacement or committed validation fails, the previous Resource bytes are restored. Cleanup occurs after commit and cannot make a committed publish report failure.

## Invariants

- the authoring folder is the only write authority for source content;
- the stable Resource asset path is the only runtime selection authority;
- every published Resource asset references one immutable version folder;
- the previous Resource asset and every TextAsset it references remain untouched before commit;
- failed writes, imports, candidate saves or validation leave the previous playable asset intact;
- rollback restores the authoritative Resource bytes and reimports them before failure is reported;
- retry creates or reuses only the deterministic candidate version and cannot partially overwrite an active version;
- version cleanup is post-commit and best-effort;
- Editor-only APIs remain outside runtime assemblies;
- failure-injection tests exercise the real Editor publishing seam, not only pure compiler helpers.

## Failure-mode matrix

| Condition | Required behavior |
|---|---|
| Move / rename source folders | Stable room and door IDs determine output semantics; publish version is content-derived. |
| Delete / output shrink | New Resource references only the candidate version; old files are removed only after commit. |
| Duplicate identity | Existing compiler validation rejects before filesystem mutation. |
| Malformed or missing data | Existing compiler/importer validation rejects before filesystem mutation. |
| Stale generated data | Cannot enter the new Resource reference set; cleanup is post-commit. |
| Partial staged write | Candidate folder is abandoned or removed; current Resource remains unchanged. |
| Unity import failure | Candidate is rejected; current Resource remains unchanged. |
| Candidate asset save failure | Candidate is rejected; current Resource remains unchanged. |
| Replacement failure | Previous Resource remains authoritative. |
| Post-replacement validation failure | Previous Resource bytes are restored and synchronously reimported. |
| Retry | Deterministic package version is validated before reuse; no active files are overwritten. |
| Restart / domain reload | Stable Resource path reconstructs the same references to immutable versioned TextAssets. |
| Cleanup failure | Publish remains successful and logs a warning. |
| External change | Candidate and committed asset validation fail closed. |

## Planned checkpoints

1. assembly/test boundary and fault seam;
2. immutable staged publication and rollback;
3. failure-injection Editor tests;
4. documentation, tracked generated asset reconciliation and hostile final audit.

## Validation status

- static source review: in progress
- structural checks: in progress
- Unity compilation: not executed
- automated tests: not executed
- Unity Editor tests: not executed
- manual acceptance: not executed
