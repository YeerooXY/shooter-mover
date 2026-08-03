"use strict";

{
  const previousNormalize = normalize;
  const previousSetTool = setTool;
  const previousPlaceAt = placeAt;
  const previousRenderHeaderFields = renderHeaderFields;
  const previousRenderAssets = renderAssets;
  const previousRenderInspector = renderInspector;
  const previousDrawRoomTiles = drawRoomTiles;
  const previousValidate = validate;

  function roomIsStart(room = currentRoom()) {
    return LevelAuthoring.isStartRoom(state.level, room);
  }

  normalize = function normalizeAuthoringReferences() {
    previousNormalize();
    LevelAuthoring.repairLevelReferences(state.level, state.editor);
  };

  function desiredRoomGrid() {
    if (state.editor.viewMode === "map") {
      const rect = canvas.getBoundingClientRect();
      const center = screenToWorld([rect.width / 2, rect.height / 2]);
      return [
        Math.round(center[0] / MAP_SPACING[0]),
        Math.round(center[1] / MAP_SPACING[1]),
      ];
    }
    return [...(currentRoom()?.grid || [0, 0])];
  }

  function availableRoomGrid() {
    return LevelAuthoring.nearestFreeGrid(state.level.rooms, desiredRoomGrid());
  }

  function nextRoomNumber() {
    const usedNames = new Set(state.level.rooms.map(room => String(room.displayName || "").toUpperCase()));
    let number = state.level.rooms.length + 1;
    while (usedNames.has(`ROOM ${number}`)) number += 1;
    return number;
  }

  function nextRoomId() {
    const roomKey = safeId(
      state.level.targetFolder || state.level.name || "level",
      "level"
    ).replace(/\./g, "-");
    const usedIds = new Set(state.level.rooms.map(room => room.id));
    let number = state.level.rooms.length + 1;
    while (usedIds.has(`room.${roomKey}-${number}`)) number += 1;
    return `room.${roomKey}-${number}`;
  }

  addRoom = function addRoomNearViewport() {
    const mapView = state.editor.viewMode === "map";
    const grid = availableRoomGrid();
    let addedRoom = null;
    mutate(() => {
      const displayNumber = nextRoomNumber();
      addedRoom = newRoom(displayNumber - 1);
      addedRoom.id = nextRoomId();
      addedRoom.displayName = `ROOM ${displayNumber}`;
      addedRoom.grid = grid;
      addedRoom.playerStart = null;
      state.level.rooms.push(addedRoom);
      state.editor.activeRoomId = addedRoom.id;
      state.editor.selectedId = null;
    });

    if (!mapView) {
      fitRoom();
      renderAll();
    } else {
      saveCurrentView();
      renderCanvas();
      renderRooms();
      renderInspector();
      renderFooter();
    }
    setStatus(`${addedRoom.displayName} added near the center of the current view.`, "good");
  };

  duplicateRoom = function duplicateRoomNearViewport() {
    const source = currentRoom();
    const mapView = state.editor.viewMode === "map";
    const grid = availableRoomGrid();
    let copyRoom = null;
    mutate(() => {
      copyRoom = clone(source);
      copyRoom.id = uid("room");
      copyRoom.displayName = `${source.displayName} COPY`;
      copyRoom.grid = grid;
      copyRoom.playerStart = null;
      copyRoom.entities.forEach(entity => {
        entity.id = uid(entity.kind);
      });
      copyRoom.doors.forEach(door => {
        door.id = uid("door");
      });
      state.level.rooms.push(copyRoom);
      state.editor.activeRoomId = copyRoom.id;
      state.editor.selectedId = null;
    });

    if (!mapView) {
      fitRoom();
      renderAll();
    }
    setStatus(`${copyRoom.displayName} placed near the center of the current view.`, "good");
  };

  setTool = function setGuardedTool(tool) {
    if (tool === "enemy" && roomIsStart()) {
      previousSetTool("select");
      state.editor.assetCategory = "";
      setStatus("Enemies cannot be placed in the starter room.", "warn");
      return;
    }
    previousSetTool(tool);
  };

  placeAt = function placeWithStarterRoomRules(tool, position) {
    if (tool === "enemy" && roomIsStart()) {
      state.editor.selectedId = null;
      setStatus("Enemies cannot be placed in the starter room.", "warn");
      return null;
    }
    return previousPlaceAt(tool, position);
  };

  renderHeaderFields = function renderAuthoringHeader() {
    previousRenderHeaderFields();
    const locked = state.editor.viewMode === "room" && roomIsStart();
    const enemyButton = document.querySelector('#tools [data-tool="enemy"]');
    if (enemyButton) {
      enemyButton.disabled = locked;
      enemyButton.title = locked
        ? "Enemies cannot be placed in the starter room"
        : "Place enemies";
    }
    if (locked && state.editor.assetCategory === "enemy") {
      state.editor.assetCategory = "";
      const palette = document.querySelector("#asset-palette");
      if (palette) palette.style.display = "none";
    }

    if (state.editor.viewMode === "map" && state.editor.mapMode === "connect") {
      const hasSource = Boolean(state.editor.connectSourceDoorId);
      const help = document.querySelector("#map-mode-help");
      if (help) {
        help.textContent = hasSource
          ? "Click a door or anywhere in another room to place and connect the destination door"
          : "Click a door, or click inside a room to place a new door on its nearest edge";
      }
      const hud = document.querySelector("#room-hud");
      if (hud) {
        hud.innerHTML = `<b>LEVEL GRAPH</b> · ${hasSource ? "choose or place destination door" : "choose or place first door"}`;
      }
    }
  };

  renderAssets = function renderStarterRoomAssets() {
    previousRenderAssets();
    const locked = state.editor.viewMode === "room" && roomIsStart();
    document.querySelectorAll("#assetList [data-asset]").forEach(element => {
      const asset = state.assets.find(value => value.id === element.dataset.asset);
      if (asset?.type !== "enemy") return;
      element.style.opacity = locked ? ".45" : "";
      element.style.cursor = locked ? "not-allowed" : "";
      element.setAttribute("aria-disabled", locked ? "true" : "false");
      if (locked) {
        element.onclick = () => setStatus(
          "Enemies cannot be placed in the starter room.",
          "warn"
        );
      }
    });
  };

  function floorLabel(room) {
    const tileId = FloorData.defaultFloorTile(room);
    return state.assets.find(asset => asset.id === tileId)?.label || tileId;
  }

  renderInspector = function renderFloorFeedback() {
    previousRenderInspector();
    if (selected()) return;
    const room = currentRoom();
    const chip = document.querySelector("#inspector .tile-chip");
    const fillButton = document.querySelector('#inspector [data-action="fill-tiles"]');
    const clearButton = document.querySelector('#inspector [data-action="clear-tiles"]');
    if (!chip || !fillButton || !clearButton) return;

    const total = room.floor.width * room.floor.height;
    const summary = document.createElement("div");
    summary.className = "help floor-state-summary";
    summary.style.marginTop = "7px";
    summary.textContent = room.floor.count === total
      ? `Floor: all ${total} cells are filled with ${floorLabel(room)}.`
      : room.floor.count === 0
        ? `Floor: empty (0 of ${total} cells).`
        : `Floor: ${room.floor.count} of ${total} cells are filled.`;
    if (state.editor.viewMode === "map") {
      summary.textContent += " Open the room to preview or fine-tune it.";
    }
    chip.after(summary);

    fillButton.textContent = "Fill room floor";
    clearButton.textContent = "Clear room floor";
    fillButton.onclick = () => {
      const tile = selectedFloorObject() || FloorData.defaultFloorTile(room);
      mutate(() => FloorData.fillFloor(room, tile));
      setStatus(`${room.displayName}: filled all ${total} floor cells with ${floorLabel(room)}.`, "good");
    };
    clearButton.onclick = () => {
      mutate(() => FloorData.clearFloor(room));
      setStatus(`${room.displayName}: cleared all ${total} floor cells.`, "good");
    };
  };

  drawRoomTiles = function drawFloorWithEmptyState(room) {
    previousDrawRoomTiles(room);
    if (room.floor.count !== 0) return;
    const center = worldToScreen([0, 0]);
    ctx.save();
    ctx.fillStyle = "rgba(7,10,14,.22)";
    const topLeft = worldToScreen([-room.bounds.width / 2, room.bounds.height / 2]);
    const bottomRight = worldToScreen([room.bounds.width / 2, -room.bounds.height / 2]);
    ctx.fillRect(
      Math.min(topLeft[0], bottomRight[0]),
      Math.min(topLeft[1], bottomRight[1]),
      Math.abs(bottomRight[0] - topLeft[0]),
      Math.abs(bottomRight[1] - topLeft[1])
    );
    ctx.fillStyle = "rgba(238,245,252,.72)";
    ctx.font = "700 15px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText("NO FLOOR", center[0], center[1]);
    ctx.restore();
  };

  validate = function validateStarterRoomRules() {
    const issues = previousValidate();
    const roomIndex = state.level.rooms.findIndex(room => room.id === state.level.startRoomId);
    const startRoom = state.level.rooms[roomIndex];
    const enemies = startRoom?.entities?.filter(entity => entity.kind === "enemy") || [];
    if (enemies.length) {
      issues.push({
        severity: "error",
        message: `Start room ${startRoom.id} cannot contain enemies; remove ${enemies.length} enemy placement(s).`,
        path: `rooms[${roomIndex}].entities`,
      });
    }
    return issues;
  };

  function mapDoorObject(room, screenPoint) {
    const placement = LevelAuthoring.mapDoorPlacement(
      room,
      mapRoomCenter(room),
      screenToWorld(screenPoint),
      MAP_ROOM_HALF
    );
    const asset = assetForTool("door")
      || state.assets.find(value => value.id === "door.room-standard");
    return {
      id: uid("door"),
      kind: "door",
      position: placement.position,
      rotation: placement.rotation,
      side: placement.side,
      placementMode: "Fixed",
      traversable: true,
      visibleOnMap: true,
      runtimeObject: asset?.id || "door.room-standard",
      openWhen: "room-complete",
    };
  }

  function finishMapDoorChoice(doorId) {
    const sourceId = state.editor.connectSourceDoorId;
    const target = findDoor(doorId);
    if (!target) return;

    if (!sourceId) {
      state.editor.connectSourceDoorId = doorId;
      state.editor.selectedId = doorId;
      renderAll();
      setStatus(`Door placed in ${target.room.displayName}. Choose another room or door to connect it.`, "good");
      return;
    }

    const source = findDoor(sourceId);
    if (!source) {
      state.editor.connectSourceDoorId = doorId;
      state.editor.selectedId = doorId;
      renderAll();
      return;
    }
    if (source.room.id === target.room.id) {
      state.editor.connectSourceDoorId = doorId;
      state.editor.selectedId = doorId;
      renderAll();
      setStatus("Door placed. Choose a door in another room to create the connection.", "warn");
      return;
    }

    const duplicate = state.level.connections.some(connection =>
      (connection.fromDoorId === sourceId && connection.toDoorId === doorId)
      || (connection.toDoorId === sourceId && connection.fromDoorId === doorId)
    );
    if (duplicate) {
      setStatus("Those doors are already connected.", "warn");
      return;
    }

    mutate(() => {
      state.level.connections.push({
        id: uid("connection"),
        fromDoorId: sourceId,
        toDoorId: doorId,
        travelPolicy: "Bidirectional",
      });
      state.editor.connectSourceDoorId = null;
      state.editor.selectedId = doorId;
    });
    setStatus(`${source.room.displayName} connected to ${target.room.displayName}.`, "good");
  }

  canvas.addEventListener("pointerdown", event => {
    if (
      state.editor.viewMode !== "map"
      || state.editor.mapMode !== "connect"
      || event.button !== 0
    ) return;

    const point = eventPoint(event);
    const hit = mapHitTest(point);
    if (hit?.type !== "room") return;

    event.preventDefault();
    event.stopImmediatePropagation();
    let door = null;
    mutate(() => {
      door = mapDoorObject(hit.room, point);
      hit.room.doors.push(door);
      state.editor.activeRoomId = hit.room.id;
      state.editor.selectedId = door.id;
    });
    finishMapDoorChoice(door.id);
  }, true);

  document.querySelector("#addRoom").onclick = addRoom;
  document.querySelector("#duplicateRoom").onclick = duplicateRoom;
  normalize();
  renderAll();
}
