"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-multiselect.css";
  document.head.appendChild(stylesheet);

  const baseRenderCanvas = renderCanvas;
  const baseRenderInspector = renderInspector;
  const baseRenderFooter = renderFooter;

  let selectedIds = new Set();
  let selectionRoomId = state.editor.activeRoomId;
  let lastPrimaryId = state.editor.selectedId;
  let marquee = null;
  let groupDrag = null;
  let groupClipboard = null;

  function roomObjects(room = currentRoom()) {
    return [...room.entities, ...room.doors];
  }

  function objectMap(room = currentRoom()) {
    return new Map(roomObjects(room).map(value => [value.id, value]));
  }

  function assetFor(value) {
    return state.assets.find(asset => asset.id === value?.object) || null;
  }

  function footprint(value) {
    if (value?.kind === "door") {
      const vertical = Math.abs((Number(value.rotation) || 0) % 180) > 45;
      return vertical ? { width: 0.5, height: 1.5 } : { width: 1.5, height: 0.5 };
    }
    if (value?.kind === "enemy" || value?.kind === "teleporter") {
      return { width: 1, height: 1 };
    }
    if (value?.kind === "wall" && Number(value.length) > 0) {
      return {
        width: Math.max(0.1, Number(value.length) || 1),
        height: Math.max(0.1, Number(value.thickness) || 0.5)
      };
    }

    const asset = assetFor(value);
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

    const text = `${asset?.id || value?.object || ""} ${asset?.label || ""}`;
    const match = text.match(/(\d+)\s*[x×]\s*(\d+)/i);
    return match
      ? { width: Math.max(1, Number(match[1])), height: Math.max(1, Number(match[2])) }
      : { width: 1, height: 1 };
  }

  function objectRect(value, position = value.position) {
    const size = footprint(value);
    return {
      left: position[0] - size.width / 2,
      right: position[0] + size.width / 2,
      bottom: position[1] - size.height / 2,
      top: position[1] + size.height / 2
    };
  }

  function normalizedRect(left, right) {
    return {
      left: Math.min(left[0], right[0]),
      right: Math.max(left[0], right[0]),
      bottom: Math.min(left[1], right[1]),
      top: Math.max(left[1], right[1])
    };
  }

  function rectanglesOverlap(left, right) {
    const epsilon = 0.001;
    return left.left < right.right - epsilon
      && left.right > right.left + epsilon
      && left.bottom < right.top - epsilon
      && left.top > right.bottom + epsilon;
  }

  function isBlocking(value) {
    return value?.kind === "prop" || value?.kind === "wall";
  }

  function syncSelection() {
    const room = currentRoom();
    if (selectionRoomId !== room.id || state.editor.viewMode !== "room") {
      selectedIds.clear();
      selectionRoomId = room.id;
    }

    const objects = objectMap(room);
    for (const id of [...selectedIds]) {
      if (!objects.has(id)) selectedIds.delete(id);
    }

    const primary = state.editor.selectedId;
    if (primary !== lastPrimaryId) {
      if (primary && objects.has(primary)) {
        if (!selectedIds.has(primary)) selectedIds = new Set([primary]);
      } else if (!primary && !marquee && !groupDrag) {
        selectedIds.clear();
      }
    }

    if (!selectedIds.size && primary && objects.has(primary)) selectedIds.add(primary);
    if (selectedIds.size && (!primary || !selectedIds.has(primary))) {
      state.editor.selectedId = [...selectedIds].at(-1) || null;
    }

    lastPrimaryId = state.editor.selectedId;
    document.body.classList.toggle("multi-select-active", selectedIds.size > 1);
    return objects;
  }

  function selectionValues() {
    const objects = syncSelection();
    return [...selectedIds].map(id => objects.get(id)).filter(Boolean);
  }

  function setSelection(ids, primaryId = null, { render = true, open = true } = {}) {
    const objects = objectMap();
    selectedIds = new Set([...ids].filter(id => objects.has(id)));
    selectionRoomId = currentRoom().id;
    state.editor.selectedId = primaryId && selectedIds.has(primaryId)
      ? primaryId
      : ([...selectedIds].at(-1) || null);
    lastPrimaryId = state.editor.selectedId;
    document.body.classList.toggle("multi-select-active", selectedIds.size > 1);

    if (open && selectedIds.size) {
      document.body.classList.remove("left-drawer-open", "tools-popover-open", "view-menu-open");
      document.body.classList.add("right-drawer-open", "drawer-open");
      requestAnimationFrame(resizeCanvas);
    } else if (!selectedIds.size) {
      document.body.classList.remove("right-drawer-open");
      if (!document.body.classList.contains("left-drawer-open")) {
        document.body.classList.remove("drawer-open");
      }
      requestAnimationFrame(resizeCanvas);
    }

    if (render) {
      renderInspector();
      renderCanvas();
      renderFooter();
    }
  }

  function selectionBounds(values = selectionValues(), positions = null) {
    if (!values.length) return null;
    const rects = values.map(value => objectRect(value, positions?.get(value.id)?.position || value.position));
    return {
      left: Math.min(...rects.map(rect => rect.left)),
      right: Math.max(...rects.map(rect => rect.right)),
      bottom: Math.min(...rects.map(rect => rect.bottom)),
      top: Math.max(...rects.map(rect => rect.top))
    };
  }

  function drawWorldRect(rect, stroke, fill, dash = [7, 4], lineWidth = 2) {
    const topLeft = worldToScreen([rect.left, rect.top]);
    const bottomRight = worldToScreen([rect.right, rect.bottom]);
    const x = Math.min(topLeft[0], bottomRight[0]);
    const y = Math.min(topLeft[1], bottomRight[1]);
    const width = Math.abs(bottomRight[0] - topLeft[0]);
    const height = Math.abs(bottomRight[1] - topLeft[1]);
    ctx.save();
    ctx.strokeStyle = stroke;
    ctx.fillStyle = fill;
    ctx.lineWidth = lineWidth;
    ctx.setLineDash(dash);
    ctx.fillRect(x + 1, y + 1, Math.max(0, width - 2), Math.max(0, height - 2));
    ctx.strokeRect(x + 1, y + 1, Math.max(0, width - 2), Math.max(0, height - 2));
    ctx.restore();
    return { x, y, width, height };
  }

  function drawMultiSelection() {
    const values = selectionValues();
    if (values.length <= 1 || state.editor.viewMode !== "room") return;

    const invalid = groupDrag && groupDrag.valid === false;
    for (const value of values) {
      drawWorldRect(
        objectRect(value),
        invalid ? "rgba(255,126,139,.95)" : "rgba(121,201,255,.95)",
        invalid ? "rgba(255,83,100,.08)" : "rgba(121,201,255,.06)",
        [5, 4],
        2
      );
    }

    const bounds = selectionBounds(values);
    if (!bounds) return;
    const screen = drawWorldRect(
      bounds,
      invalid ? "#ff5364" : "#ffffff",
      invalid ? "rgba(255,83,100,.05)" : "rgba(255,255,255,.025)",
      [10, 5],
      3
    );
    ctx.save();
    ctx.fillStyle = invalid ? "#ff7f8d" : "#dceeff";
    ctx.font = "900 10px system-ui";
    ctx.textAlign = "left";
    ctx.textBaseline = "bottom";
    ctx.fillText(`${values.length} selected`, screen.x + 3, screen.y - 5);
    ctx.restore();
  }

  function drawMarquee() {
    if (!marquee) return;
    const rect = normalizedRect(marquee.startWorld, marquee.endWorld);
    const screen = drawWorldRect(
      rect,
      "#79c9ff",
      "rgba(121,201,255,.12)",
      [6, 4],
      2
    );
    ctx.save();
    ctx.fillStyle = "#cfeaff";
    ctx.font = "800 9px system-ui";
    ctx.textAlign = "left";
    ctx.fillText("Select", screen.x + 4, screen.y + 13);
    ctx.restore();
  }

  renderCanvas = function () {
    baseRenderCanvas();
    syncSelection();
    drawMultiSelection();
    drawMarquee();
  };

  function kindCounts(values) {
    const counts = new Map();
    values.forEach(value => {
      const kind = value.kind === "door" ? "doors" : value.kind === "enemy" ? "enemies" : value.kind === "wall" ? "walls" : value.kind === "teleporter" ? "teleporters" : "props";
      counts.set(kind, (counts.get(kind) || 0) + 1);
    });
    return [...counts.entries()];
  }

  renderInspector = function () {
    const values = selectionValues();
    if (values.length <= 1) {
      baseRenderInspector();
      return;
    }

    const bounds = selectionBounds(values);
    const counts = kindCounts(values);
    const wrap = document.querySelector("#inspector");
    wrap.innerHTML = `<div class="panel multi-selection-inspector">
      <h2>${values.length} objects selected</h2>
      <div class="multi-kind-list">${counts.map(([kind, count]) => `<span>${count} ${esc(kind)}</span>`).join("")}</div>
      <div class="section">
        <div class="section-title">Group bounds</div>
        <div class="multi-bounds">${round(bounds.right - bounds.left, 2)} × ${round(bounds.top - bounds.bottom, 2)} world units</div>
        <div class="help">Drag any highlighted object to move the group. Shift-click adds or removes objects.</div>
      </div>
      <div class="multi-actions">
        <button type="button" class="primary" data-multi-duplicate>Duplicate <kbd>Ctrl+D</kbd></button>
        <button type="button" data-multi-copy>Copy <kbd>Ctrl+C</kbd></button>
        <button type="button" data-multi-clear>Clear selection</button>
        <button type="button" class="danger" data-multi-delete>Delete selected</button>
      </div>
    </div>`;
    wrap.querySelector("[data-multi-duplicate]")?.addEventListener("click", duplicateGroup);
    wrap.querySelector("[data-multi-copy]")?.addEventListener("click", copyGroup);
    wrap.querySelector("[data-multi-clear]")?.addEventListener("click", () => setSelection([]));
    wrap.querySelector("[data-multi-delete]")?.addEventListener("click", deleteGroup);
  };

  renderFooter = function () {
    baseRenderFooter();
    const values = selectionValues();
    const hud = document.querySelector("#selection-hud");
    if (!hud) return;
    hud.hidden = values.length === 0;
    if (values.length > 1) hud.textContent = `${values.length} objects selected`;
  };

  function withinRoom(room, value, position) {
    if (value.kind === "door") return true;
    const rect = objectRect(value, position);
    const halfWidth = room.bounds.width / 2;
    const halfHeight = room.bounds.height / 2;
    return rect.left >= -halfWidth - 0.001
      && rect.right <= halfWidth + 0.001
      && rect.bottom >= -halfHeight - 0.001
      && rect.top <= halfHeight + 0.001;
  }

  function candidatePlacements(values, originals, delta, room) {
    const candidates = new Map();
    values.forEach(value => {
      const original = originals.get(value.id);
      const raw = [original.position[0] + delta[0], original.position[1] + delta[1]];
      if (value.kind === "door") {
        const placement = doorEdgePlacement(room, raw);
        candidates.set(value.id, {
          position: placement.position,
          side: placement.side,
          rotation: placement.rotation
        });
      } else {
        candidates.set(value.id, { position: raw });
      }
    });
    return candidates;
  }

  function placementsValid(values, candidates, room, ignoredIds = selectedIds) {
    const ignored = ignoredIds instanceof Set ? ignoredIds : new Set(ignoredIds || []);
    for (const value of values) {
      const candidate = candidates.get(value.id);
      if (!candidate || !withinRoom(room, value, candidate.position)) return false;
    }

    const stationaryBlocking = room.entities.filter(value => isBlocking(value) && !ignored.has(value.id));
    const movingBlocking = values.filter(isBlocking);
    for (const value of movingBlocking) {
      const rect = objectRect(value, candidates.get(value.id).position);
      if (stationaryBlocking.some(other => rectanglesOverlap(rect, objectRect(other)))) return false;
    }
    for (let left = 0; left < movingBlocking.length; left += 1) {
      for (let right = left + 1; right < movingBlocking.length; right += 1) {
        const a = movingBlocking[left];
        const b = movingBlocking[right];
        if (rectanglesOverlap(
          objectRect(a, candidates.get(a.id).position),
          objectRect(b, candidates.get(b.id).position)
        )) return false;
      }
    }

    const stationaryDoors = room.doors.filter(door => !ignored.has(door.id));
    const movingDoors = values.filter(value => value.kind === "door");
    for (const door of movingDoors) {
      const position = candidates.get(door.id).position;
      if (stationaryDoors.some(other => Math.hypot(
        other.position[0] - position[0],
        other.position[1] - position[1]
      ) < 0.25)) return false;
    }
    for (let left = 0; left < movingDoors.length; left += 1) {
      for (let right = left + 1; right < movingDoors.length; right += 1) {
        const a = candidates.get(movingDoors[left].id).position;
        const b = candidates.get(movingDoors[right].id).position;
        if (Math.hypot(a[0] - b[0], a[1] - b[1]) < 0.25) return false;
      }
    }
    return true;
  }

  function applyPlacements(values, candidates) {
    values.forEach(value => {
      const candidate = candidates.get(value.id);
      value.position = [...candidate.position];
      if (value.kind === "door") {
        value.side = candidate.side;
        value.rotation = candidate.rotation;
      }
    });
  }

  function restoreOriginals(values, originals) {
    values.forEach(value => {
      const original = originals.get(value.id);
      if (!original) return;
      value.position = [...original.position];
      if (value.kind === "door") {
        value.side = original.side;
        value.rotation = original.rotation;
      }
    });
  }

  function snappedDelta(startWorld, currentWorld) {
    const step = Math.max(0.25, Number(state.editor.snapSize) || 1);
    return [
      Math.round((currentWorld[0] - startWorld[0]) / step) * step,
      Math.round((currentWorld[1] - startWorld[1]) / step) * step
    ];
  }

  function beginGroupDrag(event) {
    const values = selectionValues();
    const originals = new Map(values.map(value => [value.id, {
      position: [...value.position],
      side: value.side,
      rotation: value.rotation
    }]));
    groupDrag = {
      pointerId: event.pointerId,
      startWorld: screenToWorld(eventPoint(event)),
      originals,
      before: snapshot(),
      delta: [0, 0],
      moved: false,
      valid: true
    };
    canvas.setPointerCapture?.(event.pointerId);
    canvas.style.cursor = "grabbing";
  }

  function onPointerDown(event) {
    if (state.editor.viewMode !== "room" || state.editor.tool !== "select" || event.button !== 0 || event.altKey) return;
    syncSelection();
    const point = eventPoint(event);
    const hit = hitTest(point);

    if (event.shiftKey && hit) {
      const next = new Set(selectedIds);
      if (next.has(hit.id)) next.delete(hit.id);
      else next.add(hit.id);
      setSelection(next, next.has(hit.id) ? hit.id : null);
      event.preventDefault();
      event.stopImmediatePropagation();
      return;
    }

    if (hit && selectedIds.size > 1 && selectedIds.has(hit.id)) {
      state.editor.selectedId = hit.id;
      lastPrimaryId = hit.id;
      beginGroupDrag(event);
      event.preventDefault();
      event.stopImmediatePropagation();
      return;
    }

    if (hit) {
      if (selectedIds.size > 1 || !selectedIds.has(hit.id)) {
        setSelection([hit.id], hit.id, { render: false, open: false });
      }
      return;
    }

    marquee = {
      pointerId: event.pointerId,
      startScreen: point,
      endScreen: point,
      startWorld: screenToWorld(point),
      endWorld: screenToWorld(point),
      additive: event.shiftKey,
      base: new Set(event.shiftKey ? selectedIds : [])
    };
    if (!event.shiftKey) setSelection([], null, { render: false, open: false });
    canvas.setPointerCapture?.(event.pointerId);
    event.preventDefault();
    event.stopImmediatePropagation();
    renderCanvas();
  }

  function onPointerMove(event) {
    if (marquee?.pointerId === event.pointerId) {
      marquee.endScreen = eventPoint(event);
      marquee.endWorld = screenToWorld(marquee.endScreen);
      event.preventDefault();
      event.stopImmediatePropagation();
      renderCanvas();
      return;
    }

    if (groupDrag?.pointerId !== event.pointerId) return;
    const values = selectionValues();
    const delta = snappedDelta(groupDrag.startWorld, screenToWorld(eventPoint(event)));
    if (delta[0] === groupDrag.delta[0] && delta[1] === groupDrag.delta[1]) {
      event.preventDefault();
      event.stopImmediatePropagation();
      return;
    }

    groupDrag.delta = delta;
    groupDrag.moved = Math.abs(delta[0]) > 0.001 || Math.abs(delta[1]) > 0.001;
    const candidates = candidatePlacements(values, groupDrag.originals, delta, currentRoom());
    groupDrag.valid = placementsValid(values, candidates, currentRoom());
    applyPlacements(values, candidates);
    event.preventDefault();
    event.stopImmediatePropagation();
    renderCanvas();
    renderInspector();
    renderFooter();
  }

  function finishMarquee() {
    const current = marquee;
    marquee = null;
    const moved = Math.hypot(
      current.endScreen[0] - current.startScreen[0],
      current.endScreen[1] - current.startScreen[1]
    ) >= 5;
    if (!moved) {
      setSelection(current.additive ? current.base : []);
      return;
    }

    const rect = normalizedRect(current.startWorld, current.endWorld);
    const hits = roomObjects().filter(value => rectanglesOverlap(rect, objectRect(value))).map(value => value.id);
    const next = new Set(current.base);
    hits.forEach(id => next.add(id));
    setSelection(next, hits.at(-1) || [...next].at(-1) || null);
  }

  function finishGroupDrag() {
    const drag = groupDrag;
    groupDrag = null;
    const values = selectionValues();
    if (!drag.moved) {
      canvas.style.cursor = "default";
      renderAll();
      return;
    }

    if (!drag.valid) {
      restoreOriginals(values, drag.originals);
      setStatus("The group cannot be moved there.", "warn");
    } else {
      pushHistory(drag.before);
      normalize();
      setStatus(`Moved ${values.length} objects.`, "good");
    }
    canvas.style.cursor = "default";
    renderAll();
  }

  function onPointerUp(event) {
    if (event.type === "pointercancel") {
      if (groupDrag?.pointerId === event.pointerId) {
        const values = selectionValues();
        restoreOriginals(values, groupDrag.originals);
        groupDrag = null;
        canvas.style.cursor = "default";
        event.preventDefault();
        event.stopImmediatePropagation();
        renderAll();
        return;
      }
      if (marquee?.pointerId === event.pointerId) {
        marquee = null;
        event.preventDefault();
        event.stopImmediatePropagation();
        renderAll();
        return;
      }
    }
    if (marquee?.pointerId === event.pointerId) {
      event.preventDefault();
      event.stopImmediatePropagation();
      finishMarquee();
      return;
    }
    if (groupDrag?.pointerId === event.pointerId) {
      event.preventDefault();
      event.stopImmediatePropagation();
      finishGroupDrag();
    }
  }

  canvas.addEventListener("pointerdown", onPointerDown, true);
  canvas.addEventListener("pointermove", onPointerMove, true);
  canvas.addEventListener("pointerup", onPointerUp, true);
  canvas.addEventListener("pointercancel", onPointerUp, true);

  function copyGroup() {
    const values = selectionValues();
    if (values.length <= 1) return false;
    groupClipboard = values.map(value => ({
      door: value.kind === "door",
      data: clone(value)
    }));
    setStatus(`Copied ${values.length} objects.`, "good");
    return true;
  }

  function pasteOffsets() {
    const step = Math.max(1, Number(state.editor.snapSize) || 1);
    return [
      [step, 0], [0, step], [-step, 0], [0, -step],
      [step, step], [step, -step], [-step, step], [-step, -step],
      [step * 2, 0], [0, step * 2], [-step * 2, 0], [0, -step * 2],
      [step * 3, 0], [0, step * 3]
    ];
  }

  function pasteGroup() {
    if (state.editor.viewMode !== "room" || !groupClipboard?.length) return false;
    const room = currentRoom();
    const sourceValues = groupClipboard.map(item => item.data);
    const originals = new Map(sourceValues.map(value => [value.id, {
      position: [...value.position],
      side: value.side,
      rotation: value.rotation
    }]));

    let candidates = null;
    for (const delta of pasteOffsets()) {
      const proposed = candidatePlacements(sourceValues, originals, delta, room);
      if (placementsValid(sourceValues, proposed, room, new Set())) {
        candidates = proposed;
        break;
      }
    }
    if (!candidates) {
      setStatus("No free space was found for the copied group.", "warn");
      return false;
    }

    let newIds = [];
    mutate(() => {
      newIds = sourceValues.map(source => {
        const value = clone(source);
        value.id = uid(value.kind === "door" ? "door" : value.kind || "entity");
        const candidate = candidates.get(source.id);
        value.position = [...candidate.position];
        if (value.kind === "door") {
          value.side = candidate.side;
          value.rotation = candidate.rotation;
          room.doors.push(value);
        } else {
          room.entities.push(value);
        }
        return value.id;
      });
      selectedIds = new Set(newIds);
      state.editor.selectedId = newIds.at(-1) || null;
      lastPrimaryId = state.editor.selectedId;
    });
    setSelection(newIds, newIds.at(-1));
    setStatus(`Pasted ${newIds.length} objects.`, "good");
    return true;
  }

  function duplicateGroup() {
    return copyGroup() && pasteGroup();
  }

  function deleteGroup() {
    const ids = new Set(selectedIds);
    if (ids.size <= 1) return false;
    mutate(() => {
      for (const room of state.level.rooms) {
        room.entities = room.entities.filter(value => !ids.has(value.id));
        room.doors = room.doors.filter(value => !ids.has(value.id));
      }
      state.level.connections = state.level.connections.filter(connection =>
        !ids.has(connection.fromDoorId) && !ids.has(connection.toDoorId)
      );
      if (ids.has(state.level.finalExitDoorId)) state.level.finalExitDoorId = "";
      selectedIds.clear();
      state.editor.selectedId = null;
      lastPrimaryId = null;
    });
    setStatus(`Deleted ${ids.size} objects.`, "good");
    return true;
  }

  function nudgeGroup(deltaX, deltaY, large) {
    const values = selectionValues();
    if (values.length <= 1 || state.editor.viewMode !== "room") return false;
    const step = Math.max(0.25, Number(state.editor.snapSize) || 1) * (large ? 4 : 1);
    const delta = [deltaX * step, deltaY * step];
    const originals = new Map(values.map(value => [value.id, {
      position: [...value.position],
      side: value.side,
      rotation: value.rotation
    }]));
    const candidates = candidatePlacements(values, originals, delta, currentRoom());
    if (!placementsValid(values, candidates, currentRoom())) {
      setStatus("The group cannot be moved there.", "warn");
      return true;
    }
    mutate(() => applyPlacements(values, candidates));
    setStatus(`Moved ${values.length} objects.`, "good");
    return true;
  }

  function selectAll() {
    if (state.editor.viewMode !== "room" || state.editor.tool !== "select") return false;
    const ids = roomObjects().map(value => value.id);
    setSelection(ids, ids.at(-1) || null);
    setStatus(`Selected ${ids.length} objects.`, "good");
    return true;
  }

  window.addEventListener("keydown", event => {
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    const key = event.key.toLowerCase();
    const count = selectionValues().length;
    const stop = () => {
      event.preventDefault();
      event.stopImmediatePropagation();
    };

    if ((event.ctrlKey || event.metaKey) && key === "a") {
      if (selectAll()) stop();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "c" && count > 1) {
      stop();
      copyGroup();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "v" && groupClipboard?.length > 1) {
      stop();
      pasteGroup();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "d" && count > 1) {
      stop();
      duplicateGroup();
      return;
    }
    if (event.ctrlKey || event.metaKey || event.altKey) return;

    if ((key === "delete" || key === "backspace") && count > 1) {
      stop();
      deleteGroup();
      return;
    }
    if (key === "escape" && count > 1) {
      stop();
      setSelection([]);
      return;
    }

    const direction = {
      arrowleft: [-1, 0],
      arrowright: [1, 0],
      arrowup: [0, 1],
      arrowdown: [0, -1]
    }[key];
    if (direction && count > 1 && nudgeGroup(direction[0], direction[1], event.shiftKey)) stop();
  }, true);
})();
