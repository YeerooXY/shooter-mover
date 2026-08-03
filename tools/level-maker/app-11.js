"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-placement-feedback.css";
  document.head.appendChild(stylesheet);

  const baseRenderCanvas = renderCanvas;
  const baseRenderAssets = renderAssets;
  const baseRenderHeaderFields = renderHeaderFields;
  const basePlaceAt = placeAt;
  const baseDrawMapDoorConnection = drawMapConnection;

  let hoverWorld = null;
  let hoverInside = false;
  let oneShotPlacement = false;
  let dragOrigin = null;
  let dragInvalid = false;

  function selectedAsset() {
    return state.assets?.find(asset => asset.id === state.editor.selectedAssetId) || null;
  }

  function footprintFrom(value) {
    const asset = typeof value === "string"
      ? state.assets?.find(item => item.id === value)
      : value;
    const explicit = asset?.footprint || asset?.gridSize || asset?.size;
    if (Array.isArray(explicit) && explicit.length >= 2) {
      return {
        width: Math.max(1, Math.round(Number(explicit[0]) || 1)),
        height: Math.max(1, Math.round(Number(explicit[1]) || 1))
      };
    }
    if (explicit && typeof explicit === "object") {
      return {
        width: Math.max(1, Math.round(Number(explicit.width ?? explicit.x) || 1)),
        height: Math.max(1, Math.round(Number(explicit.height ?? explicit.y) || 1))
      };
    }
    const text = `${asset?.id || ""} ${asset?.label || ""}`;
    const match = text.match(/(\d+)\s*[x×]\s*(\d+)/i);
    return match
      ? { width: Math.max(1, Number(match[1])), height: Math.max(1, Number(match[2])) }
      : { width: 1, height: 1 };
  }

  function entityFootprint(entity) {
    if (!entity) return { width: 1, height: 1 };
    if (entity.kind === "wall" && Number(entity.length) > 0) {
      return {
        width: Math.max(1, Math.round(Number(entity.length) || 1)),
        height: Math.max(1, Math.round(Number(entity.thickness) || 1))
      };
    }
    return footprintFrom(entity.object);
  }

  function snapForFootprint(room, position, footprint) {
    const columns = Math.max(1, Math.round(room.bounds.width));
    const rows = Math.max(1, Math.round(room.bounds.height));
    const width = Math.min(columns, Math.max(1, Math.round(footprint.width || 1)));
    const height = Math.min(rows, Math.max(1, Math.round(footprint.height || 1)));
    const minX = -columns / 2;
    const minY = -rows / 2;
    const startX = clamp(
      Math.round(position[0] - minX - width / 2),
      0,
      Math.max(0, columns - width)
    );
    const startY = clamp(
      Math.round(position[1] - minY - height / 2),
      0,
      Math.max(0, rows - height)
    );
    return [minX + startX + width / 2, minY + startY + height / 2];
  }

  function footprintRect(position, footprint) {
    return {
      left: position[0] - footprint.width / 2,
      right: position[0] + footprint.width / 2,
      bottom: position[1] - footprint.height / 2,
      top: position[1] + footprint.height / 2
    };
  }

  function rectsOverlap(left, right) {
    const epsilon = 0.001;
    return left.left < right.right - epsilon
      && left.right > right.left + epsilon
      && left.bottom < right.top - epsilon
      && left.top > right.bottom + epsilon;
  }

  function isBlockingEntity(entity) {
    return entity?.kind === "prop" || entity?.kind === "wall";
  }

  function footprintAvailable(room, position, footprint, ignoreId = "") {
    const proposed = footprintRect(position, footprint);
    return !room.entities.some(entity => {
      if (!isBlockingEntity(entity) || entity.id === ignoreId) return false;
      return rectsOverlap(proposed, footprintRect(entity.position, entityFootprint(entity)));
    });
  }

  function doorPlacement(room, position) {
    const halfWidth = room.bounds.width / 2;
    const halfHeight = room.bounds.height / 2;
    const nearest = [
      ["East", Math.abs(halfWidth - position[0])],
      ["West", Math.abs(-halfWidth - position[0])],
      ["North", Math.abs(halfHeight - position[1])],
      ["South", Math.abs(-halfHeight - position[1])]
    ].sort((left, right) => left[1] - right[1])[0][0];
    const cell = snapForFootprint(room, position, { width: 1, height: 1 });
    if (nearest === "East") return { side: nearest, position: [halfWidth, cell[1]], rotation: 90 };
    if (nearest === "West") return { side: nearest, position: [-halfWidth, cell[1]], rotation: 90 };
    if (nearest === "North") return { side: nearest, position: [cell[0], halfHeight], rotation: 0 };
    return { side: nearest, position: [cell[0], -halfHeight], rotation: 0 };
  }

  function doorPositionAvailable(room, placement, ignoreId = "") {
    return !room.doors.some(door => {
      if (door.id === ignoreId) return false;
      return Math.hypot(
        door.position[0] - placement.position[0],
        door.position[1] - placement.position[1]
      ) < 0.25;
    });
  }

  function createPlacedEntity(tool, rawPosition) {
    const room = currentRoom();
    const asset = assetForTool(tool);
    if (tool === "prop" || tool === "wall") {
      if (!asset) {
        setStatus("Choose an asset first.", "warn");
        return false;
      }
      const footprint = footprintFrom(asset);
      const position = snapForFootprint(room, rawPosition, footprint);
      if (!footprintAvailable(room, position, footprint)) {
        setStatus("That space is already occupied.", "warn");
        return false;
      }
      const entity = {
        id: uid("prop"),
        kind: "prop",
        object: asset.id,
        position,
        rotation: 0,
        blocksMovement: true,
        layer: "default"
      };
      room.entities.push(entity);
      state.editor.selectedId = entity.id;
      return true;
    }

    if (tool === "enemy") {
      if (!asset) {
        setStatus("Choose an enemy first.", "warn");
        return false;
      }
      const entity = {
        id: uid("enemy"),
        kind: "enemy",
        object: asset.id,
        position: snapForFootprint(room, rawPosition, { width: 1, height: 1 }),
        rotation: 0,
        tier: 1,
        optional: false
      };
      room.entities.push(entity);
      state.editor.selectedId = entity.id;
      return true;
    }

    if (tool === "door") {
      const placement = doorPlacement(room, rawPosition);
      if (!doorPositionAvailable(room, placement)) {
        setStatus("A door already occupies that position.", "warn");
        return false;
      }
      const door = {
        id: uid("door"),
        kind: "door",
        position: placement.position,
        rotation: placement.rotation,
        side: placement.side,
        placementMode: "Fixed",
        traversable: true,
        visibleOnMap: true,
        runtimeObject: asset?.id || "door.room-standard",
        openWhen: "room-complete"
      };
      room.doors.push(door);
      state.editor.selectedId = door.id;
      return true;
    }

    if (tool === "player") {
      room.playerStart = {
        position: snapForFootprint(room, rawPosition, { width: 1, height: 1 }),
        rotation: 0
      };
      state.editor.selectedId = null;
      return true;
    }

    if (tool === "teleporter") {
      const entity = {
        id: uid("teleporter"),
        kind: "teleporter",
        position: snapForFootprint(room, rawPosition, { width: 1, height: 1 }),
        rotation: 0,
        pairId: "A",
        enabled: true
      };
      room.entities.push(entity);
      state.editor.selectedId = entity.id;
      return true;
    }

    basePlaceAt(tool, rawPosition);
    return true;
  }

  placeAt = function (tool, rawPosition) {
    const placed = createPlacedEntity(tool, rawPosition);
    if (placed && state.editor.placementMode === "single"
      && ["enemy", "prop", "wall", "door", "player", "teleporter"].includes(tool)) {
      oneShotPlacement = true;
    }
  };

  snapToRoomCellCenter = function (room, position) {
    const selection = selected()?.entity;
    const footprint = selection && isBlockingEntity(selection)
      ? entityFootprint(selection)
      : footprintFrom(selectedAsset());
    return snapForFootprint(room, position, footprint);
  };

  function drawWorldRect(position, footprint, valid, label = "") {
    const rect = footprintRect(position, footprint);
    const topLeft = worldToScreen([rect.left, rect.top]);
    const bottomRight = worldToScreen([rect.right, rect.bottom]);
    const x = Math.min(topLeft[0], bottomRight[0]);
    const y = Math.min(topLeft[1], bottomRight[1]);
    const width = Math.abs(bottomRight[0] - topLeft[0]);
    const height = Math.abs(bottomRight[1] - topLeft[1]);
    ctx.save();
    ctx.fillStyle = valid ? "rgba(92, 226, 145, .28)" : "rgba(255, 105, 120, .32)";
    ctx.strokeStyle = valid ? "rgba(135, 255, 182, .95)" : "rgba(255, 140, 151, .95)";
    ctx.lineWidth = 2;
    ctx.setLineDash([6, 4]);
    ctx.fillRect(x + 1, y + 1, Math.max(0, width - 2), Math.max(0, height - 2));
    ctx.strokeRect(x + 1, y + 1, Math.max(0, width - 2), Math.max(0, height - 2));
    ctx.setLineDash([]);
    if (label) {
      ctx.font = "700 10px system-ui";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillStyle = "#f4f8ff";
      ctx.fillText(label, x + width / 2, y + height / 2);
    }
    ctx.restore();
  }

  function drawPlacementPreview() {
    if (!hoverInside || !hoverWorld || state.editor.viewMode !== "room") return;
    const tool = state.editor.tool;
    if (tool === "select" || tool === "pan") return;
    const room = currentRoom();
    const asset = assetForTool(tool);

    if (tool === "prop" || tool === "wall") {
      const footprint = footprintFrom(asset);
      const position = snapForFootprint(room, hoverWorld, footprint);
      const valid = !!asset && footprintAvailable(room, position, footprint);
      drawWorldRect(position, footprint, valid, valid ? (asset?.label || "") : "Occupied");
      return;
    }

    if (tool === "enemy") {
      const position = snapForFootprint(room, hoverWorld, { width: 1, height: 1 });
      drawWorldRect(position, { width: 1, height: 1 }, !!asset, asset?.label || "Enemy");
      return;
    }

    if (tool === "tile" || tool === "tile-erase") {
      const cell = tileCellFromWorld(room, hoverWorld);
      if (!cell) return;
      const rect = tileCellWorldRect(room, cell.x, cell.y);
      drawWorldRect(
        [rect.x + 0.5, rect.y + 0.5],
        { width: 1, height: 1 },
        tool === "tile",
        tool === "tile" ? "" : "Erase"
      );
      return;
    }

    if (tool === "door") {
      const placement = doorPlacement(room, hoverWorld);
      const valid = doorPositionAvailable(room, placement);
      const screen = worldToScreen(placement.position);
      const zoom = state.editor.zoom;
      ctx.save();
      ctx.translate(screen[0], screen[1]);
      ctx.rotate(-deg2rad(placement.rotation));
      ctx.fillStyle = valid ? "rgba(92, 226, 145, .34)" : "rgba(255, 105, 120, .36)";
      ctx.strokeStyle = valid ? "#8dffbc" : "#ff8e9a";
      ctx.lineWidth = 2;
      ctx.setLineDash([6, 4]);
      ctx.fillRect(-0.75 * zoom, -0.175 * zoom, 1.5 * zoom, 0.35 * zoom);
      ctx.strokeRect(-0.75 * zoom, -0.175 * zoom, 1.5 * zoom, 0.35 * zoom);
      ctx.restore();
      return;
    }

    const position = snapForFootprint(room, hoverWorld, { width: 1, height: 1 });
    drawWorldRect(
      position,
      { width: 1, height: 1 },
      true,
      tool === "player" ? "Spawn" : "Teleport"
    );
  }

  function drawInvalidDrag() {
    if (!dragInvalid || !dragOrigin) return;
    const found = findEntity(dragOrigin.id);
    if (!found) return;
    drawWorldRect(found.entity.position, entityFootprint(found.entity), false, "Occupied");
  }

  renderCanvas = function () {
    baseRenderCanvas();
    drawPlacementPreview();
    drawInvalidDrag();
  };

  function connectionNumbersForDoor(doorId) {
    const numbers = [];
    state.level.connections.forEach((connection, index) => {
      if (connection.fromDoorId === doorId || connection.toDoorId === doorId) numbers.push(index + 1);
    });
    return numbers;
  }

  drawMapDoorSocket = function (room, door) {
    const point = worldToScreen(mapDoorWorldPosition(room, door));
    const numbers = connectionNumbersForDoor(door.id);
    const connected = numbers.length > 0;
    const radius = connected ? 11 : 8;
    ctx.save();
    ctx.fillStyle = connected ? "#79c9ff" : "#ffd166";
    ctx.strokeStyle = door.id === state.editor.selectedId ? "#ffffff" : connected ? "#d9f2ff" : "#604d20";
    ctx.lineWidth = door.id === state.editor.selectedId ? 3 : 2;
    ctx.beginPath();
    ctx.arc(point[0], point[1], radius, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    if (connected) {
      ctx.fillStyle = "#0d1722";
      ctx.font = "900 10px system-ui";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText(numbers.length > 1 ? numbers.join("/") : String(numbers[0]), point[0], point[1] + 0.5);
    }
    ctx.restore();
  };

  drawMapConnection = function (connection, previewEnd = null) {
    baseDrawMapDoorConnection(connection, previewEnd);
    if (previewEnd) return;
    const source = findDoor(connection.fromDoorId);
    const target = findDoor(connection.toDoorId);
    if (!source || !target) return;
    const a = worldToScreen(mapDoorWorldPosition(source.room, source.door));
    const b = worldToScreen(mapDoorWorldPosition(target.room, target.door));
    const number = state.level.connections.indexOf(connection) + 1;
    const x = (a[0] + b[0]) / 2;
    const y = (a[1] + b[1]) / 2;
    ctx.save();
    ctx.fillStyle = "#79c9ff";
    ctx.strokeStyle = "#d9f2ff";
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(x, y, 10, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = "#0d1722";
    ctx.font = "900 9px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(String(number), x, y + 0.5);
    ctx.restore();
  };

  function updatePalette() {
    const palette = document.querySelector("#asset-palette");
    if (!palette) return;
    const open = state.editor.viewMode === "room"
      && !!state.editor.assetCategory
      && palette.style.display !== "none";
    document.body.classList.toggle("asset-palette-open", open);
    if (!open || state.editor.assetCategory !== "floor") return;
    if (palette.querySelector(".palette-floor-actions")) return;
    const actions = document.createElement("div");
    actions.className = "palette-floor-actions";
    actions.innerHTML = `
      <button type="button" data-fill-room>Fill room <kbd>Shift+F</kbd></button>
      <button type="button" data-clear-room>Clear floor</button>`;
    palette.querySelector(".palette-title")?.after(actions);
    actions.querySelector("[data-fill-room]").onclick = () => {
      mutate(() => fillRoomTiles(currentRoom(), selectedFloorObject()));
      setStatus("Room floor filled.", "good");
    };
    actions.querySelector("[data-clear-room]").onclick = () => {
      mutate(() => {
        currentRoom().tileGridEnabled = true;
        currentRoom().tiles = [];
      });
      setStatus("Room floor cleared.", "good");
    };
  }

  renderAssets = function () {
    baseRenderAssets();
    updatePalette();
  };

  renderHeaderFields = function () {
    baseRenderHeaderFields();
    updatePalette();
  };

  function handleFillShortcut(event) {
    if (!event.shiftKey || event.key.toLowerCase() !== "f") return;
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    mutate(() => fillRoomTiles(currentRoom(), selectedFloorObject()));
    setStatus("Room floor filled.", "good");
  }

  canvas.addEventListener("pointermove", event => {
    hoverInside = true;
    hoverWorld = screenToWorld(eventPoint(event));
    if (state.editor.viewMode === "room") renderCanvas();
  });
  canvas.addEventListener("pointerleave", () => {
    hoverInside = false;
    hoverWorld = null;
    renderCanvas();
  });

  canvas.addEventListener("pointerdown", () => {
    if (state.editor.tool !== "select" || state.editor.viewMode !== "room") return;
    const selection = selected()?.entity;
    if (!selection) return;
    dragOrigin = {
      id: selection.id,
      position: [...selection.position]
    };
    dragInvalid = false;
  });

  canvas.addEventListener("pointermove", () => {
    if (!dragOrigin || pointer.mode !== "drag") return;
    const found = findEntity(dragOrigin.id);
    if (!found) return;
    if (found.entity.kind === "enemy" || found.entity.kind === "teleporter" || isBlockingEntity(found.entity)) {
      found.entity.position = snapForFootprint(found.room, found.entity.position, entityFootprint(found.entity));
    }
    dragInvalid = isBlockingEntity(found.entity)
      && !footprintAvailable(found.room, found.entity.position, entityFootprint(found.entity), found.entity.id);
    renderCanvas();
  });

  canvas.addEventListener("pointerup", () => {
    if (dragOrigin) {
      const found = findEntity(dragOrigin.id);
      if (found && dragInvalid) {
        found.entity.position = [...dragOrigin.position];
        setStatus("That space is already occupied.", "warn");
        renderAll();
      }
      dragOrigin = null;
      dragInvalid = false;
    }
    if (oneShotPlacement) {
      oneShotPlacement = false;
      setTool("select");
      renderHeaderFields();
      renderCanvas();
    }
  });

  document.addEventListener("keydown", handleFillShortcut, true);
  updatePalette();
})();
