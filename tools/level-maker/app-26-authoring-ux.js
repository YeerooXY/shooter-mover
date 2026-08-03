"use strict";

{
  const previousNewRoom = newRoom;
  const previousNormalize = normalize;
  const previousSelected = selected;
  const previousHitTest = hitTest;
  const previousSetTool = setTool;
  const previousPlaceAt = placeAt;
  const previousDeleteSelected = deleteSelected;
  const previousRenderHeaderFields = renderHeaderFields;
  const previousRenderAssets = renderAssets;
  const previousRenderInspector = renderInspector;
  const previousRenderFooter = renderFooter;
  const previousRenderCanvas = renderCanvas;
  const previousValidate = validate;

  const SPAWN_PREFIX = "player-start:";
  let mapHover = null;
  let mapDoorDrag = null;
  let spawnDrag = null;
  let lastMapPointer = null;
  let controlDown = false;

  const stylesheet = document.createElement("style");
  stylesheet.textContent = `
    #asset-group-switch {
      position: absolute;
      top: 14px;
      left: 14px;
      z-index: 36;
      display: flex;
      gap: 6px;
      padding: 5px;
    }
    #asset-group-switch button.active {
      border-color: #79c9ff;
      background: #22384c;
      color: #f4fbff;
    }
    #asset-palette.authoring-group-palette {
      top: 58px !important;
      left: 14px !important;
      right: auto !important;
      width: min(430px, calc(100% - 28px));
      max-height: min(420px, calc(100% - 90px));
      overflow: auto;
      z-index: 35;
    }
    #asset-palette .group-palette-note {
      margin: 0 0 8px;
      color: #9fb1c7;
      font-size: 11px;
    }
    #asset-palette .palette-asset[disabled] {
      opacity: .42;
      cursor: not-allowed;
    }
    body:not(.room-focus) #asset-group-switch,
    body:not(.room-focus) #asset-palette.authoring-group-palette {
      display: none !important;
    }
  `;
  document.head.appendChild(stylesheet);

  function startRoom() {
    return state.level.rooms.find(room => room.id === state.level.startRoomId)
      || state.level.rooms[0]
      || null;
  }

  function roomIsStart(room = currentRoom()) {
    return room?.id === state.level.startRoomId;
  }

  function spawnId(room) {
    return `${SPAWN_PREFIX}${room.id}`;
  }

  function spawnRoomForSelection(id = state.editor.selectedId) {
    if (!String(id || "").startsWith(SPAWN_PREFIX)) return null;
    return state.level.rooms.find(room => spawnId(room) === id && room.playerStart) || null;
  }

  function spawnProxy(room) {
    const proxy = {
      id: spawnId(room),
      kind: "player",
      object: "player-start",
    };
    Object.defineProperties(proxy, {
      position: {
        enumerable: true,
        get: () => room.playerStart.position,
        set: value => {
          room.playerStart.position = [Number(value?.[0]) || 0, Number(value?.[1]) || 0];
        },
      },
      rotation: {
        enumerable: true,
        get: () => Number(room.playerStart.rotation) || 0,
        set: value => {
          room.playerStart.rotation = Number(value) || 0;
        },
      },
    });
    return proxy;
  }

  function clampSpawnPosition(room, position) {
    const halfWidth = room.bounds.width / 2;
    const halfHeight = room.bounds.height / 2;
    return [
      clamp(Number(position?.[0]) || 0, -halfWidth + 0.5, halfWidth - 0.5),
      clamp(Number(position?.[1]) || 0, -halfHeight + 0.5, halfHeight - 0.5),
    ];
  }

  function clearPrivateMultiSelection() {
    state.editor.selectedId = null;
    previousRenderCanvas();
  }

  newRoom = function makeWalkableRoom(index = 0) {
    const room = previousNewRoom(index);
    FloorData.prepareRoom(room);
    if (room.floor.count === 0) {
      FloorData.fillFloor(room, FloorData.defaultFloorTile(room));
    }
    return room;
  };

  normalize = function normalizeSingleLevelSpawn() {
    previousNormalize();
    AuthoringUx.normalizeSingleSpawn(state.level);
  };

  selected = function selectPlayerSpawn() {
    const ordinary = previousSelected();
    if (ordinary) return ordinary;
    const room = spawnRoomForSelection();
    return room ? { room, entity: spawnProxy(room), spawn: room.playerStart } : null;
  };

  hitTest = function hitTestWithPlayerSpawn(screen) {
    const ordinary = previousHitTest(screen);
    if (ordinary) return ordinary;
    const room = currentRoom();
    if (!room.playerStart) return null;
    const point = worldToScreen(room.playerStart.position);
    return Math.hypot(screen[0] - point[0], screen[1] - point[1]) <= 18
      ? spawnProxy(room)
      : null;
  };

  setTool = function setAuthoringTool(tool) {
    if (tool === "player" && !roomIsStart()) {
      previousSetTool("select");
      state.editor.assetCategory = "";
      state.editor.assetGroup = "interactive";
      setStatus("The level has one player spawn, and it belongs to the starter room.", "warn");
      renderGroupedPalette();
      return;
    }

    previousSetTool(tool);
    const asset = state.assets.find(value => value.id === state.editor.selectedAssetId);
    if (tool === "player") state.editor.assetGroup = "interactive";
    else if (tool === "teleporter" || tool === "tile" || tool === "tile-erase" || tool === "door" || tool === "wall") {
      state.editor.assetGroup = "static";
    } else if (asset) {
      state.editor.assetGroup = AuthoringUx.assetGroup(asset);
    }
    state.editor.assetCategory = "";
    renderGroupedPalette();
  };

  placeAt = function placeSinglePlayerSpawn(tool, position) {
    if (tool !== "player") return previousPlaceAt(tool, position);
    const room = currentRoom();
    if (!roomIsStart(room)) {
      setStatus("Open the starter room to position the level spawn.", "warn");
      return null;
    }

    clearPrivateMultiSelection();
    room.playerStart = {
      position: clampSpawnPosition(room, snapPoint(position)),
      rotation: Number(room.playerStart?.rotation) || 0,
    };
    state.editor.selectedId = spawnId(room);
    setStatus("Player spawn moved. This is the only spawn in the level.", "good");
    return spawnProxy(room);
  };

  deleteSelected = function protectRequiredPlayerSpawn() {
    if (spawnRoomForSelection()) {
      setStatus("The starter-room spawn is required. Move it instead of deleting it.", "warn");
      return;
    }
    previousDeleteSelected();
  };

  function installAssetGroups() {
    if (document.querySelector("#asset-group-switch")) return;
    const group = document.createElement("div");
    group.id = "asset-group-switch";
    group.className = "floating";
    group.innerHTML = `
      <button type="button" data-asset-group="interactive">Interactive items</button>
      <button type="button" data-asset-group="static">Static items</button>`;
    document.querySelector("#stage-wrap")?.appendChild(group);
    group.querySelectorAll("[data-asset-group]").forEach(button => {
      button.addEventListener("click", () => {
        state.editor.assetGroup = button.dataset.assetGroup;
        state.editor.assetCategory = "";
        renderGroupedPalette();
        scheduleRecoverySave();
      });
    });
  }

  function assetTool(asset) {
    if (asset.type === "enemy") return "enemy";
    if (asset.type === "floor") return "tile";
    if (asset.type === "door") return "door";
    if (asset.type === "prop" && String(asset.id).startsWith("prop.wall-")) return "wall";
    return "prop";
  }

  function specialCards(group) {
    if (group === "interactive") {
      return [{ tool: "player", label: "Player spawn", icon: "●", disabled: !roomIsStart() }];
    }
    return [
      { tool: "teleporter", label: "Teleporter", icon: "◎" },
      { tool: "tile-erase", label: "Erase floor", icon: "⌫" },
    ];
  }

  function renderGroupedPalette() {
    installAssetGroups();
    const switcher = document.querySelector("#asset-group-switch");
    const palette = document.querySelector("#asset-palette");
    if (!switcher || !palette) return;

    const group = state.editor.assetGroup === "interactive" ? "interactive" : "static";
    state.editor.assetGroup = group;
    switcher.querySelectorAll("[data-asset-group]").forEach(button => {
      button.classList.toggle("active", button.dataset.assetGroup === group);
    });

    if (state.editor.viewMode !== "room") {
      palette.style.display = "none";
      return;
    }

    const assets = state.assets.filter(asset => AuthoringUx.assetGroup(asset) === group);
    const lockedEnemyRoom = roomIsStart();
    palette.classList.add("authoring-group-palette");
    palette.style.display = "block";
    palette.innerHTML = `
      <div class="palette-title">${group === "interactive" ? "Interactive items" : "Static items"}</div>
      <div class="group-palette-note">${group === "interactive"
        ? "Enemies, keys, pickups and gameplay interactives."
        : "Floors, walls, doors, props, decor and room utilities."}</div>
      <div class="palette-grid">
        ${specialCards(group).map(card => `
          <button class="palette-asset" type="button" data-special-tool="${card.tool}" ${card.disabled ? "disabled" : ""}>
            <span>${card.icon}</span><b>${card.label}</b>
          </button>`).join("")}
        ${assets.map(asset => {
          const disabled = asset.type === "enemy" && lockedEnemyRoom;
          return `<button class="palette-asset ${asset.id === state.editor.selectedAssetId ? "selected" : ""}"
            type="button" data-group-asset="${esc(asset.id)}" ${disabled ? "disabled" : ""} title="${esc(asset.id)}">
            <span>${iconFor(asset.type)}</span><b>${esc(asset.label || asset.id)}</b>
          </button>`;
        }).join("")}
      </div>`;

    palette.querySelectorAll("[data-special-tool]").forEach(button => {
      button.addEventListener("click", () => setTool(button.dataset.specialTool));
    });
    palette.querySelectorAll("[data-group-asset]").forEach(button => {
      button.addEventListener("click", () => {
        const asset = state.assets.find(value => value.id === button.dataset.groupAsset);
        if (!asset) return;
        state.editor.selectedAssetId = asset.id;
        setTool(assetTool(asset));
        renderAssets();
        renderHeaderFields();
        renderCanvas();
        renderFooter();
        scheduleRecoverySave();
      });
    });
  }

  renderHeaderFields = function renderPersistentAssetGroups() {
    previousRenderHeaderFields();
    state.editor.assetCategory = "";
    const playerButton = document.querySelector('#tools [data-tool="player"]');
    if (playerButton) {
      playerButton.disabled = state.editor.viewMode === "room" && !roomIsStart();
      playerButton.title = playerButton.disabled
        ? "The only player spawn belongs to the starter room"
        : "Move the single player spawn";
    }
    renderGroupedPalette();
  };

  renderAssets = function renderGroupedAssets() {
    previousRenderAssets();
    state.editor.assetCategory = "";
    renderGroupedPalette();
  };

  function renderSpawnInspector(room) {
    const wrap = document.querySelector("#inspector");
    const spawn = room.playerStart;
    wrap.innerHTML = `<div class="panel">
      <h2>Player spawn</h2>
      <div class="notice">This is the level's single spawn. Placing it again moves this marker; it never creates another spawn.</div>
      <div class="section">
        <div class="section-title">Transform</div>
        <div class="grid2">
          <div><label>X</label><input data-spawn="x" type="number" step=".25" value="${spawn.position[0]}"></div>
          <div><label>Y</label><input data-spawn="y" type="number" step=".25" value="${spawn.position[1]}"></div>
        </div>
        <label>Rotation (degrees)</label><input data-spawn="rotation" type="number" step="15" value="${spawn.rotation || 0}">
      </div>
      <button type="button" data-center-spawn>Center in room</button>
    </div>`;
    wrap.querySelectorAll("[data-spawn]").forEach(input => {
      input.addEventListener("change", () => mutate(() => {
        const key = input.dataset.spawn;
        if (key === "rotation") spawn.rotation = Number(input.value) || 0;
        else {
          const next = [...spawn.position];
          next[key === "x" ? 0 : 1] = Number(input.value) || 0;
          spawn.position = clampSpawnPosition(room, next);
        }
      }));
    });
    wrap.querySelector("[data-center-spawn]")?.addEventListener("click", () => {
      mutate(() => {
        spawn.position = [0, 0];
      });
      setStatus("Player spawn centered in the starter room.", "good");
    });
  }

  renderInspector = function renderSelectablePlayerSpawn() {
    const room = spawnRoomForSelection();
    if (room) {
      renderSpawnInspector(room);
      return;
    }
    previousRenderInspector();
  };

  renderFooter = function renderPlayerSpawnSelection() {
    previousRenderFooter();
    const room = spawnRoomForSelection();
    if (!room) return;
    const hud = document.querySelector("#selection-hud");
    if (hud) {
      hud.hidden = false;
      hud.textContent = "Player spawn";
    }
  };

  function mapPlacement(room, screenPoint, centered = false) {
    const placement = LevelAuthoring.mapDoorPlacement(
      room,
      mapRoomCenter(room),
      screenToWorld(screenPoint),
      MAP_ROOM_HALF
    );
    return centered ? AuthoringUx.centerDoorPlacement(room, placement.side) : placement;
  }

  function makeMapDoor(room, screenPoint, centered = false) {
    const placement = mapPlacement(room, screenPoint, centered);
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

  function chooseMapDoor(doorId) {
    const target = findDoor(doorId);
    if (!target) return;
    const sourceId = state.editor.connectSourceDoorId;
    if (!sourceId) {
      state.editor.connectSourceDoorId = doorId;
      state.editor.activeRoomId = target.room.id;
      state.editor.selectedId = doorId;
      renderAll();
      setStatus(`Door placed in ${target.room.displayName}. Choose another room or door.`, "good");
      return;
    }

    const source = findDoor(sourceId);
    if (!source || source.room.id === target.room.id) {
      state.editor.connectSourceDoorId = doorId;
      state.editor.activeRoomId = target.room.id;
      state.editor.selectedId = doorId;
      renderAll();
      setStatus("Door placed. Choose a door in another room to connect it.", "warn");
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
      state.editor.activeRoomId = target.room.id;
      state.editor.selectedId = doorId;
    });
    setStatus(`${source.room.displayName} connected to ${target.room.displayName}.`, "good");
  }

  function drawMapDoorPreview() {
    if (!mapHover || state.editor.viewMode !== "map") return;
    const room = state.level.rooms.find(value => value.id === mapHover.roomId);
    if (!room) return;
    const placement = mapPlacement(room, mapHover.screen, controlDown);
    const point = worldToScreen(mapDoorWorldPosition(room, placement));
    const vertical = placement.side === "East" || placement.side === "West";
    ctx.save();
    ctx.translate(point[0], point[1]);
    ctx.fillStyle = "rgba(255,209,102,.42)";
    ctx.strokeStyle = "#fff0ae";
    ctx.lineWidth = 2;
    ctx.setLineDash([5, 3]);
    ctx.fillRect(vertical ? -4 : -13, vertical ? -13 : -4, vertical ? 8 : 26, vertical ? 26 : 8);
    ctx.strokeRect(vertical ? -4 : -13, vertical ? -13 : -4, vertical ? 8 : 26, vertical ? 26 : 8);
    ctx.restore();
  }

  renderCanvas = function renderDoorPlacementPreview() {
    previousRenderCanvas();
    drawMapDoorPreview();
  };

  function updateMapHover(event) {
    if (event.target !== canvas || state.editor.viewMode !== "map" || mapDoorDrag) {
      if (mapHover) {
        mapHover = null;
        renderCanvas();
      }
      return;
    }
    lastMapPointer = eventPoint(event);
    const hit = mapHitTest(lastMapPointer);
    const next = (state.editor.mapMode === "connect" && hit?.type === "room")
      ? { roomId: hit.room.id, screen: lastMapPointer }
      : null;
    const changed = next?.roomId !== mapHover?.roomId
      || next?.screen?.[0] !== mapHover?.screen?.[0]
      || next?.screen?.[1] !== mapHover?.screen?.[1];
    mapHover = next;
    if (changed) renderCanvas();
  }

  function beginSpawnDrag(event) {
    if (event.target !== canvas || state.editor.viewMode !== "room" || state.editor.tool !== "select") return false;
    const room = currentRoom();
    if (!room.playerStart) return false;
    const point = eventPoint(event);
    if (previousHitTest(point)) return false;
    const screen = worldToScreen(room.playerStart.position);
    if (Math.hypot(point[0] - screen[0], point[1] - screen[1]) > 18) return false;

    event.preventDefault();
    event.stopPropagation();
    clearPrivateMultiSelection();
    state.editor.selectedId = spawnId(room);
    spawnDrag = {
      room,
      before: snapshot(),
      offset: screenToWorld(point).map((value, index) => value - room.playerStart.position[index]),
    };
    document.body.classList.add("right-drawer-open", "drawer-open");
    renderAll();
    return true;
  }

  function updateSpawnDrag(event) {
    if (!spawnDrag) return false;
    event.preventDefault();
    event.stopPropagation();
    const raw = screenToWorld(eventPoint(event));
    const position = [
      raw[0] - spawnDrag.offset[0],
      raw[1] - spawnDrag.offset[1],
    ];
    spawnDrag.room.playerStart.position = clampSpawnPosition(spawnDrag.room, snapPoint(position));
    renderCanvas();
    renderInspector();
    renderFooter();
    return true;
  }

  function finishSpawnDrag(event) {
    if (!spawnDrag) return false;
    event.preventDefault();
    event.stopPropagation();
    if (spawnDrag.before !== snapshot()) pushHistory(spawnDrag.before);
    spawnDrag = null;
    renderAll();
    scheduleRecoverySave();
    return true;
  }

  function beginMapDoorDrag(event) {
    if (event.target !== canvas || state.editor.viewMode !== "map" || state.editor.mapMode !== "arrange" || event.button !== 0) return false;
    const point = eventPoint(event);
    const hit = mapHitTest(point);
    if (hit?.type !== "door") return false;

    event.preventDefault();
    event.stopPropagation();
    mapDoorDrag = { room: hit.room, door: hit.door, before: snapshot() };
    state.editor.activeRoomId = hit.room.id;
    state.editor.selectedId = hit.door.id;
    Object.assign(hit.door, mapPlacement(hit.room, point, event.ctrlKey));
    renderAll();
    setStatus("Moving door on the map. Hold Ctrl to snap it to the center of a side.", "good");
    return true;
  }

  function updateMapDoorDrag(event) {
    if (!mapDoorDrag) return false;
    event.preventDefault();
    event.stopPropagation();
    Object.assign(
      mapDoorDrag.door,
      mapPlacement(mapDoorDrag.room, eventPoint(event), event.ctrlKey)
    );
    renderCanvas();
    renderInspector();
    renderFooter();
    return true;
  }

  function finishMapDoorDrag(event) {
    if (!mapDoorDrag) return false;
    event.preventDefault();
    event.stopPropagation();
    if (mapDoorDrag.before !== snapshot()) pushHistory(mapDoorDrag.before);
    mapDoorDrag = null;
    renderAll();
    scheduleRecoverySave();
    return true;
  }

  document.addEventListener("pointerdown", event => {
    if (beginSpawnDrag(event)) return;

    if (
      event.target === canvas
      && state.editor.viewMode === "map"
      && state.editor.mapMode === "connect"
      && event.button === 0
      && event.ctrlKey
    ) {
      const point = eventPoint(event);
      const hit = mapHitTest(point);
      if (hit?.type === "room") {
        event.preventDefault();
        event.stopPropagation();
        let door = null;
        mutate(() => {
          door = makeMapDoor(hit.room, point, true);
          hit.room.doors.push(door);
          state.editor.activeRoomId = hit.room.id;
          state.editor.selectedId = door.id;
        });
        chooseMapDoor(door.id);
        return;
      }
    }

    beginMapDoorDrag(event);
  }, true);

  document.addEventListener("pointermove", event => {
    if (updateSpawnDrag(event)) return;
    if (updateMapDoorDrag(event)) return;
    updateMapHover(event);
  }, true);

  document.addEventListener("pointerup", event => {
    if (finishSpawnDrag(event)) return;
    finishMapDoorDrag(event);
  }, true);

  document.addEventListener("pointercancel", event => {
    if (finishSpawnDrag(event)) return;
    finishMapDoorDrag(event);
  }, true);

  document.addEventListener("keydown", event => {
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    const key = event.key.toLowerCase();

    if (key === "control") {
      controlDown = true;
      if (mapHover) renderCanvas();
      return;
    }

    if (key === "a" && !event.ctrlKey && !event.metaKey && !event.altKey) {
      event.preventDefault();
      event.stopImmediatePropagation();
      if (state.editor.viewMode === "map") {
        state.editor.connectSourceDoorId = null;
        setMapMode("arrange");
        renderAll();
        setStatus("Arrange mode: drag rooms or their door sockets. Hold Ctrl to center a door on its side.", "good");
      } else {
        setTool("select");
        setStatus("Select tool active.", "good");
      }
      return;
    }

    if (spawnRoomForSelection() && (key === "delete" || key === "backspace" || ((event.ctrlKey || event.metaKey) && ["c", "d", "x"].includes(key)))) {
      event.preventDefault();
      event.stopImmediatePropagation();
      setStatus("The single player spawn can be selected and moved, but not copied or deleted.", "warn");
    }
  }, true);

  document.addEventListener("keyup", event => {
    if (event.key.toLowerCase() !== "control") return;
    controlDown = false;
    if (mapHover) renderCanvas();
  }, true);

  validate = function validateSingleSpawn() {
    const issues = previousValidate();
    const spawnRooms = state.level.rooms.filter(room => room.playerStart);
    if (spawnRooms.length !== 1) {
      issues.push({
        severity: "error",
        message: `The level must contain exactly one player spawn; found ${spawnRooms.length}.`,
        path: "level.startRoomId",
      });
    } else if (spawnRooms[0].id !== state.level.startRoomId) {
      issues.push({
        severity: "error",
        message: "The player spawn must be inside the configured starter room.",
        path: "level.startRoomId",
      });
    }
    return issues;
  };

  state.editor.assetGroup = state.editor.assetGroup === "interactive" ? "interactive" : "static";
  installAssetGroups();
  normalize();
  renderAll();
}
