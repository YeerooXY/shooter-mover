# LEVEL-GRID-EDITOR-001 manual acceptance

## Branch and baseline

- Branch: `agent/level-grid-editor-001`
- Starting `main` SHA: `7defe07dfea16a4435567f0dc053b195d6b5705e`
- Unity version: `6000.3.19f1`
- Target PR base: `main`
- Merge policy: draft only; do not merge or enable auto-merge

## Scope boundary

The dedicated Level Grid Editor is an Editor-only visual frontend over the existing
scene-authoring authority:

```text
LevelDesignSceneAuthoringRoot2D
LevelRoomAuthoring2D
LevelDoorEndpointAuthoring2D
LevelDoorLinkAuthoring2D
```

It does not introduce an independent graph asset, a JSON editing surface, a runtime
compiler, or a playable-room importer. Export and publish buttons execute the existing
transactional exporter commands.

## Manual Unity acceptance procedure

Run the following in Unity `6000.3.19f1` on a scene containing a valid
`LevelDesignSceneAuthoringRoot2D`.

1. Open **Tools → Shooter Mover → Level Design → Open Level Grid Editor**.
2. Select an existing level root using the toolbar object field or
   **Select Level Root** menu.
3. Confirm foundation and Grid V2 validation results appear immediately without first
   pressing a validation button.
4. Right-click the empty canvas and create three rooms.
5. Drag the three room cards to separate integer grid coordinates.
6. Right-click the middle room and add two door endpoints.
7. Add matching endpoints to the other rooms and drag endpoint-to-endpoint to connect
   all three rooms.
8. Move the final room to another side of the middle room.
9. Confirm connected edge-managed endpoints with automatic facing enabled reflow to
   the expected sides.
10. Select **Keep Placement** on one endpoint and move the room again.
11. Confirm that endpoint retains its authored side and placement while validation
    reports any resulting facing mismatch.
12. Select and delete the middle connection line.
13. Confirm both physical endpoints remain and are shown as unresolved.
14. Undo once and confirm the connection is restored.
15. Delete the middle room.
16. Confirm both neighbouring endpoints remain unresolved and attached connection
    lines are removed.
17. Undo once and confirm the room, its endpoints, and the connections are restored.
18. Click **Export Draft** and complete the existing transactional exporter flow.
19. Compare the exported package against the scene graph: exact room IDs, room
    coordinates and slots, exact door IDs, and exact endpoint-to-endpoint links must
    match.
20. Save the scene, close Unity, and reopen the project.
21. Reopen the editor and confirm the selected root, scene graph, room coordinates,
    endpoints, links, and saved pan/zoom state reconstruct correctly.

## Targeted regression acceptance

1. Create an intentionally invalid endpoint referenced by two connection objects.
2. Delete that endpoint from the dedicated editor.
3. Confirm both connection objects are removed, the opposite endpoints remain, and one
   Undo restores the endpoint plus both links.
4. Place a fixed endpoint beneath a translated or rotated helper Transform. Record its
   world position using the Inspector.
5. Simulate/load legacy parent-relative fixed-position data, allow validation/domain
   reload to migrate it, and confirm the endpoint does not move.
6. Run **Production** validation and confirm unresolved traversable endpoints are red.
7. Select or modify an unrelated project object outside the active level root and
   confirm the production validation display is not silently replaced by Draft.
8. Create a foundation problem such as a missing room bounds collider. Confirm the room
   card is marked and the foundation problem offers working **Select** and **Frame**
   actions.
9. Select a validation problem and confirm its explanation remains visible in the
   embedded inspector after Unity Selection focuses the affected object.
10. Rename a room using the delayed field, then press Undo once. Confirm the whole
    committed rename is undone rather than stepping through individual keystrokes.
11. Verify a fixed endpoint outside the room bounds is drawn outside the room card
    rather than clamped onto its edge.
12. Exercise a scene with approximately 100 rooms, 300 endpoints, and 150 links and
    confirm ordinary selection, panning, and unrelated project edits do not repeatedly
    trigger full draft validation.

## Focused EditMode coverage

`LevelGridEditorWindowV2Tests` and
`LevelGridEditorTargetedFixesV2Tests` cover:

- opening with no selected root;
- projection of existing scene rooms and doors;
- room movement with stable identity and unchanged folder slot;
- next-free per-coordinate folder-slot selection;
- multiple doors on one side;
- rejection of already-connected endpoints;
- exact endpoint-to-endpoint link creation;
- connection, door, and room deletion preservation semantics;
- deletion of every link attached to an invalid multiply-connected endpoint;
- one-step Undo for room movement, door creation, and connection creation;
- exact V2 and foundation duplicate-object selection by stable ID plus diagnostic path;
- immediate draft validation when selecting a root;
- foundation problems reflected on room cards;
- absence of an independent JSON/runtime graph across every editor partial;
- fixed door positions stored relative to the owning room;
- position-preserving migration from legacy parent-relative fixed-door data.

## Targeted self-audit repairs

- The dedicated editor now delegates endpoint deletion to the established safe door
  operation, which removes every attached link and deletes the endpoint GameObject.
- Fixed-door coordinates carry an explicit serialized space version. Legacy values are
  migrated from the current world position without moving the endpoint.
- Root selection performs one immediate draft validation.
- Ordinary object-change callbacks only invalidate the cached projection. The existing
  live validator owns change-driven validation and now refreshes both foundation and
  Grid V2 results while preserving the current validation purpose.
- Foundation issues participate in room-card state, exact selection, framing, and the
  embedded problem inspector.
- Validation-problem selection survives the Unity Selection synchronization needed to
  focus the affected component.
- Fixed endpoints are projected from room-local authored coordinates without clamping
  out-of-bounds positions.
- Text and numeric inspector fields use delayed commit behavior to avoid per-keystroke
  mutation and validation.

## Validation record

| Check | Result | Notes |
|---|---|---|
| Static source review | Completed | Re-audited safe deletion reuse, fixed-position migration, validation ownership, foundation diagnostics, selection synchronization, projection math, Undo grouping, and scope boundaries. |
| Structural source checks | Completed | Balanced delimiters/preprocessor guards and scanned editor sources for forbidden independent graph/runtime-import dependencies. |
| Unity compilation/domain reload | Not run | Unity Editor is not available in the connector-only implementation environment. |
| EditMode tests | Not run | Requires Unity `6000.3.19f1`. |
| Manual editor acceptance | Not run | Follow the main and targeted regression procedures above. |
| 100-room / 300-door / 150-link interaction check | Not run | Target-scale responsiveness still requires an interactive Unity run. |

## Known limitations pending Unity acceptance

- Visual room spacing uses a consistent editor canvas cell size rather than deriving
  each card's pixel dimensions from the room's world-space `cellSize`; authored
  footprint dimensions remain authoritative.
- Runtime import remains explicitly not connected on this branch and belongs to
  `LEVEL-GRID-V2-RUNTIME-001`.
