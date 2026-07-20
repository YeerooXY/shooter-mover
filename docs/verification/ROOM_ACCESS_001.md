# ROOM-ACCESS-001 verification

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Base branch: `main`
- Exact launch SHA: `af83d72e80d216dbe78678754d6a66189967127f`
- Working branch: `agent/room-access-001-keys-locks-conditions`
- Unity baseline: `6000.3.19f1`

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
- exact room-entered, room-complete, terminal entity, collected drop, objective, and switch facts;
- independent conditions on different doors;
- projection from real ROOM-LIVE immutable semantics.

Importer tests cover:

- data-only key and consumptive door authoring;
- return, progression, and final-exit selector meaning;
- canonical JSON round trip;
- reorder-independent fingerprints;
- precise unknown-reference diagnostics;
- circular condition rejection;
- exact terminal subject validation.

## Connector-side static checks

The implementation environment does not include Unity or a C# compiler. The following checks are performed before PR creation:

- Tree-sitter C# grammar parse for every added C# compilation unit;
- JSON parse for the checked-in access example;
- trailing-whitespace/tab audit;
- duplicate Unity GUID audit across the added metadata;
- changed-file boundary comparison against the exact launch SHA.

No Unity compilation or passing XML result is claimed until the focused commands above are run in a Unity-capable environment.
