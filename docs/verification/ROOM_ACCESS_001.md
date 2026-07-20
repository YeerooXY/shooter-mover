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

## Static checks performed in the implementation environment

The implementation environment does not include Unity or a C# compiler. Before updating the PR, perform and record:

- Tree-sitter C# grammar parse for every changed/added C# compilation unit;
- JSON parse for the checked-in access example;
- trailing-whitespace/tab audit;
- StableId literal audit;
- duplicate Unity GUID audit for new metadata;
- changed-file boundary comparison against the original launch SHA;
- confirmation that `Stage1VisibleSliceController.cs` is untouched.

## Unity merge gate

Keep PR #259 as a draft until both focused commands produce XML files with zero failures and Unity compilation succeeds. No passing Unity result is claimed from an environment where Unity is unavailable.
