# LEVEL_SELECTION_V1

## Purpose

`LEVELSEL-002` adds a metadata-driven Level Selection boundary between the
selected play mode and a destination scene. It consumes the immutable HUB-001
`PlayerRouteProfilePayloadV1` and a stable selected-mode identity. It does not
create, mutate, or duplicate XP, inventory, equipment, reward, wallet, run, or
gameplay authority.

## Launch boundary

- Dispatch-time `origin/main`: `45b276a8c415508ac8c0ddd8283234a6bbb2948e`
- Branch: `agent/levelsel-002-level-selection`
- PR base: `main`
- HUB-001 is merged on the launch SHA.
- PLAY-001 is developed in parallel. Its accepted Solo result exposes
  `SelectedModeStableId` and the exact HUB payload; composition passes those two
  values to `LevelSelectionControllerV1.Configure(...)` or captures an accepted
  `LevelSelectionResultV1` through the route context.
- No PLAY-001 file or route contract is modified or duplicated here.

## Metadata contract

`LevelSelectionDefinitionV1` carries:

- stable level identity;
- display name and description;
- canonical Unity scene path;
- locked/unlocked availability;
- live/prototype release state;
- gameplay/prototype route kind;
- recommended player level, equipment level, party size, and difficulty label;
- deterministic sort order.

`LevelSelectionCatalogV1` rejects null/empty catalogs, duplicate stable IDs,
missing or malformed scene routes, and invalid live/prototype route
combinations. It sorts definitions by sort order and stable ID, then computes a
SHA-256 fingerprint over all route-relevant metadata.

The authored default catalog resolves exactly:

| Level | Stable identity | State | Scene |
|---|---|---|---|
| Level 1 | `level.stage-1` | unlocked, live gameplay | `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity` |
| Level 2 | `level.stage-2` | unlocked, prototype | `Assets/ShooterMover/Scenes/Prototypes/Level2Prototype.unity` |

## Route behavior

`LevelSelectionServiceV1` retains the exact incoming
`PlayerRouteProfilePayloadV1` reference and selected `StableId` mode identity.

- Selecting an unlocked live level emits one `GameplayScene` result.
- Selecting an unlocked prototype emits one `PrototypeScene` result.
- Selecting a locked level emits no route and does not lock input.
- Back emits one `PlaySelection` result targeting
  `Assets/ShooterMover/Scenes/Flow/PlaySelection/PlaySelection.unity`.
- After the first accepted terminal route, all repeated input returns
  `InputLocked`; adapters therefore receive at most one load instruction.
- Missing payloads, invalid fingerprints, or missing mode identities fail
  closed. No fallback profile or loadout is created.

`LevelSelectionRouteContextV1.CaptureEntry(...)` lets the PLAY composition seed
Level Selection with the exact immutable HUB payload and selected mode before the
scene loads. `UnityLevelSelectionRouteAdapterV1` then captures the accepted
outgoing projection (payload, mode identity, and optional level identity) before
asking the injected scene loader to load the metadata-owned path. The context is not inventory truth
and exposes no mutation operation.

## Presentation

`LevelSelection.unity` binds the authored catalog and the copied
`level_selection.png` backplate. The artwork is passive. Real IMGUI controls,
labels, availability feedback, prototype state, recommendations, stable IDs,
and scene routes are rendered as overlays by
`LevelSelectionControllerV1`.

`Level2Prototype.unity` is an intentionally bounded placeholder. It is visibly
labeled **PROTOTYPE**, starts no combat or reward flow, and has a one-shot Back
action to Level Selection while retaining the same route context.

## Build-registration limitation

`ProjectSettings/EditorBuildSettings.asset` is outside task ownership. The
runtime adapter uses canonical scene paths, but player-build registration must
be completed by a separately owned integration task. This task does not edit
build settings or the accepted Stage 1 scene/controller.

## Focused verification

Required Unity commands:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Flow.LevelSelection" -testResults "artifacts/test-results/LEVELSEL-002-EditMode.xml" -logFile "artifacts/logs/LEVELSEL-002-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.LevelSelection" -testResults "artifacts/test-results/LEVELSEL-002-PlayMode.xml" -logFile "artifacts/logs/LEVELSEL-002-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Do not claim Unity proof unless both XML files exist and report zero failures.

## Manual proof

- Navigate Solo to Level Selection with a populated HUB payload.
- Confirm the supplied artwork is a backplate and real controls remain readable.
- Open Level 2, verify the **PROTOTYPE** label, then return safely.
- Open Level 1 and verify exactly the accepted Stage 1 scene loads once.
- Verify profile, loadout, mode, and selected-level identities reach the
  destination boundary unchanged.

## Rollback

Revert the LEVELSEL-002 branch additions. No data migration, authority rollback,
shared contract change, Stage 1 scene restoration, or ProjectSettings change is
required.
