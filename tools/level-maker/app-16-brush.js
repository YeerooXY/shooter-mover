"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-brush.css";
  document.head.appendChild(stylesheet);

  const maximumBrushSize = 200;
  const baseRenderCanvas = renderCanvas;
  const baseRenderHeaderFields = renderHeaderFields;
  const baseSetTool = setTool;

  let hoverWorld = null;
  let hoverInside = false;
  let stroke = null;

  function brushTool() {
    return state.editor.viewMode === "room"
      && (state.editor.tool === "tile" || state.editor.tool === "tile-erase");
  }

  function normalizedSize(value) {
    return clamp(Math.round(Number(value) || 1), 1, maximumBrushSize);
  }

  function ensureBrushState() {
    state.editor.brushWidth = normalizedSize(state.editor.brushWidth);
    state.editor.brushHeight = normalizedSize(state.editor.brushHeight);
  }

  function brushSize(room = currentRoom()) {
    ensureBrushState();
    return {
      width: Math.min(Math.max(1, Math.round(room.bounds.width)), state.editor.brushWidth),
      height: Math.min(Math.max(1, Math.round(room.bounds.height)), state.editor.brushHeight)
    };
  }

  function brushPlacement(room, world) {
    const columns = Math.max(1, Math.round(room.bounds.width));
    const rows = Math.max(1, Math.round(room.bounds.height));
    const size = brushSize(room);
    const minX = -columns / 2;
    const minY = -rows / 2;
    const startX = clamp(
      Math.round(world[0] - minX - size.width / 2),
      0,
      Math.max(0, columns - size.width)
    );
    const startY = clamp(
      Math.round(world[1] - minY - size.height / 2),
      0,
      Math.max(0, rows - size.height)
    );
    return {
      startX,
      startY,
      width: size.width,
      height: size.height,
      center: [
        minX + startX + size.width / 2,
        minY + startY + size.height / 2
      ],
      key: `${startX},${startY},${size.width},${size.height}`
    };
  }

  function regionKeys(placement) {
    const keys = new Set();
    for (let y = placement.startY; y < placement.startY + placement.height; y += 1) {
      for (let x = placement.startX; x < placement.startX + placement.width; x += 1) {
        keys.add(`${x},${y}`);
      }
    }
    return keys;
  }

  function stamp(room, placement, erase) {
    room.tileGridEnabled = true;
    const keys = regionKeys(placement);
    if (erase) {
      room.tiles = room.tiles.filter(tile => !keys.has(`${tile.x},${tile.y}`));
      return;
    }

    const object = selectedFloorObject();
    const tiles = new Map(room.tiles.map(tile => [`${tile.x},${tile.y}`, tile]));
    for (let y = placement.startY; y < placement.startY + placement.height; y += 1) {
      for (let x = placement.startX; x < placement.startX + placement.width; x += 1) {
        tiles.set(`${x},${y}`, { x, y, object });
      }
    }
    room.tiles = [...tiles.values()].sort((left, right) => left.y - right.y || left.x - right.x);
  }

  function drawBrushPreview() {
    if (!brushTool() || !hoverInside || !hoverWorld) return;
    const room = currentRoom();
    const placement = brushPlacement(room, hoverWorld);
    const topLeft = worldToScreen([
      placement.center[0] - placement.width / 2,
      placement.center[1] + placement.height / 2
    ]);
    const bottomRight = worldToScreen([
      placement.center[0] + placement.width / 2,
      placement.center[1] - placement.height / 2
    ]);
    const x = Math.min(topLeft[0], bottomRight[0]);
    const y = Math.min(topLeft[1], bottomRight[1]);
    const width = Math.abs(bottomRight[0] - topLeft[0]);
    const height = Math.abs(bottomRight[1] - topLeft[1]);
    const erase = state.editor.tool === "tile-erase";

    ctx.save();
    ctx.fillStyle = erase ? "rgba(255,105,120,.25)" : "rgba(92,226,145,.25)";
    ctx.strokeStyle = erase ? "#ff8e9a" : "#8dffbc";
    ctx.lineWidth = 2;
    ctx.setLineDash([7, 4]);
    ctx.fillRect(x + 1, y + 1, Math.max(0, width - 2), Math.max(0, height - 2));
    ctx.strokeRect(x + 1, y + 1, Math.max(0, width - 2), Math.max(0, height - 2));
    ctx.setLineDash([]);
    ctx.fillStyle = "#f4f8ff";
    ctx.font = "800 11px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(
      `${placement.width}×${placement.height}${erase ? " erase" : ""}`,
      x + width / 2,
      y + height / 2
    );
    ctx.restore();
  }

  renderCanvas = function () {
    if (brushTool()) {
      const tool = state.editor.tool;
      state.editor.tool = "select";
      try {
        baseRenderCanvas();
      } finally {
        state.editor.tool = tool;
      }
      drawBrushPreview();
      return;
    }
    baseRenderCanvas();
  };

  function installControls() {
    const controls = document.querySelector("#room-focus-tools");
    if (!controls || document.querySelector("#brushControls")) return;

    const wrap = document.createElement("span");
    wrap.id = "brushControls";
    wrap.className = "brush-controls";
    wrap.innerHTML = `
      <span class="divider"></span>
      <label for="brushWidth">Brush</label>
      <input id="brushWidth" type="number" min="1" max="${maximumBrushSize}" step="1" inputmode="numeric" aria-label="Brush width">
      <span class="brush-times">×</span>
      <input id="brushHeight" type="number" min="1" max="${maximumBrushSize}" step="1" inputmode="numeric" aria-label="Brush height">
      <button id="swapBrush" type="button" title="Swap brush width and height">↔</button>`;

    const gridDivider = [...controls.querySelectorAll(".divider")].at(-1);
    gridDivider?.before(wrap);

    const width = wrap.querySelector("#brushWidth");
    const height = wrap.querySelector("#brushHeight");
    const preview = () => {
      const nextWidth = Number(width.value);
      const nextHeight = Number(height.value);
      if (Number.isFinite(nextWidth) && nextWidth >= 1) {
        state.editor.brushWidth = Math.min(maximumBrushSize, Math.round(nextWidth));
      }
      if (Number.isFinite(nextHeight) && nextHeight >= 1) {
        state.editor.brushHeight = Math.min(maximumBrushSize, Math.round(nextHeight));
      }
      renderCanvas();
    };
    const commit = () => {
      state.editor.brushWidth = normalizedSize(width.value);
      state.editor.brushHeight = normalizedSize(height.value);
      syncControls();
      renderCanvas();
      scheduleRecoverySave();
    };
    width.addEventListener("input", preview);
    height.addEventListener("input", preview);
    width.addEventListener("change", commit);
    height.addEventListener("change", commit);
    wrap.querySelector("#swapBrush")?.addEventListener("click", () => {
      const nextWidth = state.editor.brushHeight;
      state.editor.brushHeight = state.editor.brushWidth;
      state.editor.brushWidth = nextWidth;
      syncControls();
      renderCanvas();
      scheduleRecoverySave();
    });
  }

  function syncControls() {
    ensureBrushState();
    installControls();
    const wrap = document.querySelector("#brushControls");
    const width = document.querySelector("#brushWidth");
    const height = document.querySelector("#brushHeight");
    if (width && document.activeElement !== width) width.value = String(state.editor.brushWidth);
    if (height && document.activeElement !== height) height.value = String(state.editor.brushHeight);
    wrap?.classList.toggle("active", brushTool());
  }

  setTool = function (tool) {
    baseSetTool(tool);
    syncControls();
  };

  renderHeaderFields = function () {
    baseRenderHeaderFields();
    syncControls();
  };

  function eventWorld(event) {
    return screenToWorld(eventPoint(event));
  }

  function restoreDrawers(saved) {
    if (!saved) return;
    document.body.classList.toggle("left-drawer-open", saved.left);
    document.body.classList.toggle("right-drawer-open", saved.right);
    document.body.classList.toggle("drawer-open", saved.drawer);
    document.body.classList.toggle("tools-popover-open", saved.tools);
    document.body.classList.toggle("view-menu-open", saved.view);
  }

  function beginStroke(event) {
    if (!brushTool() || event.altKey || event.button === 1) return false;
    if (event.button !== 0 && event.button !== 2) return false;

    event.preventDefault();
    event.stopImmediatePropagation();
    try {
      canvas.setPointerCapture(event.pointerId);
    } catch {}

    const room = currentRoom();
    const placement = brushPlacement(room, eventWorld(event));
    stroke = {
      pointerId: event.pointerId,
      erase: state.editor.tool === "tile-erase" || event.button === 2,
      continuous: state.editor.placementMode === "paint",
      lastKey: placement.key,
      before: snapshot(),
      drawers: {
        left: document.body.classList.contains("left-drawer-open"),
        right: document.body.classList.contains("right-drawer-open"),
        drawer: document.body.classList.contains("drawer-open"),
        tools: document.body.classList.contains("tools-popover-open"),
        view: document.body.classList.contains("view-menu-open")
      }
    };
    stamp(room, placement, stroke.erase);
    hoverWorld = eventWorld(event);
    renderCanvas();
    renderHeaderFields();
    renderFooter();
    return true;
  }

  function continueStroke(event) {
    if (!stroke || stroke.pointerId !== event.pointerId) return false;
    event.preventDefault();
    event.stopImmediatePropagation();
    hoverWorld = eventWorld(event);
    if (!stroke.continuous) {
      renderCanvas();
      return true;
    }

    const placement = brushPlacement(currentRoom(), hoverWorld);
    if (placement.key === stroke.lastKey) return true;
    stroke.lastKey = placement.key;
    stamp(currentRoom(), placement, stroke.erase);
    renderCanvas();
    renderHeaderFields();
    renderFooter();
    return true;
  }

  function finishStroke(event) {
    if (!stroke || stroke.pointerId !== event.pointerId) return false;
    event.preventDefault();
    event.stopImmediatePropagation();
    const completed = stroke;
    stroke = null;
    try {
      canvas.releasePointerCapture(event.pointerId);
    } catch {}

    if (completed.before !== snapshot()) pushHistory(completed.before);
    restoreDrawers(completed.drawers);
    saveCurrentView();
    renderAll();
    scheduleRecoverySave();
    return true;
  }

  canvas.addEventListener("pointerenter", event => {
    hoverInside = true;
    if (brushTool()) {
      hoverWorld = eventWorld(event);
      renderCanvas();
    }
  }, true);

  canvas.addEventListener("pointerleave", () => {
    hoverInside = false;
    if (brushTool() && !stroke) renderCanvas();
  }, true);

  canvas.addEventListener("pointermove", event => {
    if (continueStroke(event)) return;
    if (!brushTool()) return;
    hoverInside = true;
    hoverWorld = eventWorld(event);
    renderCanvas();
  }, true);

  canvas.addEventListener("pointerdown", beginStroke, true);
  canvas.addEventListener("pointerup", finishStroke, true);
  canvas.addEventListener("pointercancel", finishStroke, true);
  canvas.addEventListener("contextmenu", event => {
    if (!brushTool()) return;
    event.preventDefault();
    event.stopImmediatePropagation();
  }, true);

  ensureBrushState();
  installControls();
  syncControls();
})();
