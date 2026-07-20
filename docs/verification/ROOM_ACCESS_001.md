# ROOM-ACCESS-001 verification

## Launch and correction boundary

- Repository: `YeerooXY/shooter-mover`
- Base branch: `main`
- Exact original launch SHA: `af83d72e80d216dbe78678754d6a66189967127f`
- Existing PR: `#259`
- Working branch: `agent/room-access-001-keys-locks-conditions`
- Unity baseline: `6000.3.19f1`
- Correction: fail-closed validation for external holding, objective, switch, consume-holding, and collected-drop references.

## Focused suites

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Missions.Rooms.RoomAccessAuthorityV1Tests -testResults Temp/room-access-001-authority-editmode.xml -logFile Temp/room-access-001-authority-editmode.log

Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Missions.Rooms.RoomAccessJsonImporterV1Tests -testResults Temp/room-access-001-json-editmode.xml -logFile Temp/room-access-001-json-editmode.log
```

Do not add `-quit`; Unity test execution exits after `-runTests`.

Both commands were attempted in the implementation environment. Each exited with status `127` because the `Unity` executable is not installed. No XML files were produced and no passing Unity result is claimed.

## Coverage authored

Authority tests cover:

- key-present door closed before pickup and open after the holding appears;
- consumptive unlock and exact replay without a second consume call;
- conflicting operation identity rejection without additional consumption;
- deterministic `all`, `any`, and `not` evaluation;
- exact room-entered, room-complete, terminal entity, registered collected drop, objective, and switch facts;
- independent conditions on different doors;
- projection from real ROOM-LIVE immutable semantics;
- direct definition construction rejects unknown external condition references;
- direct definition construction rejects unknown `consume_holding` references.

Importer tests cover:

1. known holding/key import;
2. unknown `holding-present` rejection at `.subject`;
3. unknown `holding-consumed` rejection at `.subject`;
4. unknown `consume_holding` rejection at `.consume_holding`;
5. known switch import;
6. misspelled switch rejection at `.subject`;
7. unknown objective rejection;
8. unknown collected-drop rejection;
9. registration-order-independent registry and definition fingerprints;
10. existing room, exact-terminal, progression, return, final-exit, canonical round-trip, ordering, child-reference, cycle, and composite-condition behavior.

Additional provenance coverage proves canonical version 2 documents reject a mismatched reference-registry fingerprint.

## Static checks completed

- Tree-sitter C# grammar parse passed for all six correction C# compilation units.
- The checked-in version 1 access JSON example parsed successfully.
- StableId literal audit passed for the correction sources.
- No tabs or trailing whitespace exist in the correction files.
- The new Unity metadata GUID is valid, unique in repository search, and its remote blob matches the audited local file.
- Every modified remote blob hash matches the locally audited source.
- The branch remains ahead-only from the exact original launch SHA.
- The changed-file comparison contains 20 added files and no modifications or deletions relative to `main`.
- `Stage1VisibleSliceController.cs` is absent from the changed-file list.

## Unity merge gate

Keep PR #259 as a draft until both focused commands produce XML files with zero failures and Unity compilation succeeds. No passing Unity result is claimed from an environment where Unity is unavailable.
