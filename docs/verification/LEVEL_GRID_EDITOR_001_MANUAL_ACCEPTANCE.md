# LEVEL-GRID-EDITOR-001 manual acceptance

## Branch and baseline

- Branch: `agent/level-grid-editor-001`
- Starting `main` SHA: `7defe07dfea16a4435567f0dc053b195d6b5705e`
- Unity version: `6000.3.19f1`
- Target PR base: `main`
- Merge policy: draft only; do not merge or enable auto-merge

## Scope boundary

The dedicated Level Grid Editor is an Editor-only visual frontend over the existing scene-authoring authority:

```text
LevelDesignSceneAuthoringRoot2D
LevelRoomAuthoring2D
LevelDoorEndpointAuthoring2D
LevelDoorLinkAuthoring2D
```

It does not introduce an independent graph asset, a JSON editing surface, a runtime compiler, or a playable-room importer. Export and publish buttons execute the existing transactional exporter commands.

## Manual Unity acceptance procedure

Run the following in Unity `6000.3.19f1` on a scene containing a valid `LevelDesignSceneAuthoringRoot2D`.

1. Open **Tools → Shooter Mover → Level Design → Open Level Grid Editor**.
2. Select an existing level root using the toolbar object field or **Select Level Root** menu.
3. Right-click the empty canvas and create three rooms.
4. Drag the three room cards to separate integer grid coordinates.
5. Right-click the middle room and add two door endpoints.
6. Add matching endpoints to the other rooms and drag endpoint-to-endpoint to connect all three rooms.
7. Move the final room to another side of the middle room.
8. Confirm connected edge-managed endpoints with automatic facing enabled reflow to the expected sides.
9. Select **Keep Placement** on one endpoint and move the room again.
10. Confirm that endpoint retains its authored side and placement while validation reports any resulting facing mismatch.
11. Select and delete the middle connection line.
12. Confirm both physical endpoints remain and are shown as unresolved.
13. Undo once and confirm the connection is restored.
14. Delete the middle room.
15. Confirm both neighbouring endpoints remain unresolved and attached connection lines are removed.
16. Undo once and confirm the room, its endpoints, and the connections are restored.
17. Click **Export Draft** and complete the existing transactional exporter flow.
18. Compare the exported package against the scene graph: exact room IDs, room coordinates and slots, exact door IDs, and exact endpoint-to-endpoint links must match.
19. Save the scene, close Unity, and reopen the project.
20. Reopen the editor and confirm the selected root, scene graph, room coordinates, endpoints, links, and saved pan/zoom state reconstruct correctly.

## Focused EditMode coverage

`LevelGridEditorWindowV2Tests` covers:

- opening with no selected root;
- projection of existing scene rooms and doors;
- room movement with stable identity and unchanged folder slot;
- next-free per-coordinate folder-slot selection;
- multiple doors on one side;
- rejection of already-connected endpoints;
- exact endpoint-to-endpoint link creation;
- connection, door, and room deletion preservation semantics;
- one-step Undo for room movement, door creation, and connection creation;
- exact duplicate-object problem selection by stable ID plus diagnostic hierarchy path;
- absence of an independent JSON/runtime graph;
- fixed door positions stored relative to the owning room even through an intermediate helper parent.

## Validation record

| Check | Result | Notes |
|---|---|---|
| Static source review | Completed | Reviewed editor-only boundary, authority reuse, Undo grouping, dirty-state updates, cached projection, validation integration, and exporter command reuse. |
| Unity compilation/domain reload | Not run | Unity Editor is not available in the connector-only implementation environment. |
| EditMode tests | Not run | Requires Unity `6000.3.19f1`. |
| Manual editor acceptance | Not run | Follow the 20-step procedure above in Unity. |
| 100-room / 300-door / 150-link interaction check | Not run | Projection is change-driven and cached, but target-scale responsiveness still requires an interactive Unity run. |

## Known limitations pending Unity acceptance

- Visual spacing uses a consistent editor canvas cell size rather than deriving each card's pixel dimensions from the room's world-space `cellSize`; authored footprint dimensions remain authoritative.
- Foundation diagnostics remain rendered through the existing foundation issue text and existing Problems workflow; V2 diagnostics support exact stable-ID plus hierarchy-path focus directly in the dedicated editor.
- Canvas rejection feedback is intentionally transient and non-modal.
- Runtime import remains explicitly not connected on this branch and belongs to `LEVEL-GRID-V2-RUNTIME-001`.
