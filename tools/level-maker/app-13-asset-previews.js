"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-asset-previews.css";
  document.head.appendChild(stylesheet);

  const baseDrawEntity = drawEntity;
  const baseDrawWall = drawWall;
  const baseDrawDoor = drawDoor;
  const baseDrawRoomTiles = drawRoomTiles;
  const baseRenderCanvas = renderCanvas;
  const baseRenderAssets = renderAssets;
  const baseRenderInspector = renderInspector;

  const previewUrls = new Map();
  const imageCache = new Map();
  let hoverWorld = null;
  let hoverInside = false;
  let refreshQueued = false;

  function queueRefresh() {
    if (refreshQueued) return;
    refreshQueued = true;
    requestAnimationFrame(() => {
      refreshQueued = false;
      renderCanvas();
      renderAssets();
      renderInspector();
    });
  }

  function previewUrl(id) {
    return previewUrls.get(String(id || "")) || "";
  }

  function imageFor(id) {
    const key = String(id || "");
    const url = previewUrl(key);
    if (!url) return null;
    let entry = imageCache.get(key);
    if (!entry || entry.url !== url) {
      const image = new Image();
      entry = { url, image, state: "loading" };
      imageCache.set(key, entry);
      image.onload = () => {
        entry.state = "ready";
        queueRefresh();
      };
      image.onerror = () => {
        entry.state = "failed";
        queueRefresh();
      };
      image.src = url;
    }
    return entry.state === "ready" ? entry.image : null;
  }

  function assetForId(id) {
    return state.assets.find(asset => asset.id === id) || null;
  }

  function footprint(value) {
    if (value?.kind === "wall" && Number(value.length) > 0) {
      return {
        width: Math.max(0.1, Number(value.length) || 1),
        height: Math.max(0.1, Number(value.thickness) || 0.5)
      };
    }
    if (value?.kind === "enemy" || value?.kind === "teleporter") {
      return { width: 1, height: 1 };
    }
    const id = typeof value === "string" ? value : value?.object;
    const asset = assetForId(id);
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
    const text = `${asset?.id || id || ""} ${asset?.label || ""}`;
    const match = text.match(/(\d+)\s*[x×]\s*(\d+)/i);
    return match
      ? { width: Math.max(1, Number(match[1])), height: Math.max(1, Number(match[2])) }
      : { width: 1, height: 1 };
  }

  function drawContained(image, maxWidth, maxHeight, alpha = 1) {
    if (!image?.naturalWidth || !image?.naturalHeight || maxWidth <= 0 || maxHeight <= 0) return;
    const scale = Math.min(maxWidth / image.naturalWidth, maxHeight / image.naturalHeight);
    const width = image.naturalWidth * scale;
    const height = image.naturalHeight * scale;
    ctx.save();
    ctx.globalAlpha *= alpha;
    ctx.drawImage(image, -width / 2, -height / 2, width, height);
    ctx.restore();
  }

  function drawImageInScreenRect(image, left, top, width, height, alpha = 1) {
    if (!image?.naturalWidth || !image?.naturalHeight || width <= 0 || height <= 0) return;
    const scale = Math.min(width / image.naturalWidth, height / image.naturalHeight);
    const drawWidth = image.naturalWidth * scale;
    const drawHeight = image.naturalHeight * scale;
    ctx.save();
    ctx.globalAlpha *= alpha;
    ctx.drawImage(
      image,
      left + (width - drawWidth) / 2,
      top + (height - drawHeight) / 2,
      drawWidth,
      drawHeight
    );
    ctx.restore();
  }

  drawEntity = function (entity) {
    const image = imageFor(entity.object);
    if (!image || entity.kind === "teleporter") {
      baseDrawEntity(entity);
      return;
    }
    const size = footprint(entity);
    withTransform(entity, () => {
      drawContained(
        image,
        size.width * state.editor.zoom * 0.9,
        size.height * state.editor.zoom * 0.9,
        0.96
      );
    });
    labelEntity(entity);
  };

  drawWall = function (wall) {
    const image = imageFor(wall.object);
    if (!image) {
      baseDrawWall(wall);
      return;
    }
    const size = footprint(wall);
    withTransform(wall, () => {
      drawContained(
        image,
        size.width * state.editor.zoom * 0.94,
        size.height * state.editor.zoom * 0.94,
        0.96
      );
    });
    labelEntity(wall);
  };

  drawDoor = function (door) {
    baseDrawDoor(door);
    const image = imageFor(door.runtimeObject);
    if (!image) return;
    withTransform(door, () => {
      drawContained(image, 1.32 * state.editor.zoom, 0.42 * state.editor.zoom, 0.96);
    });
  };

  function visibleCell(room, x, y) {
    const rect = tileCellWorldRect(room, x, y);
    const topLeft = worldToScreen([rect.x, rect.y + 1]);
    const bottomRight = worldToScreen([rect.x + 1, rect.y]);
    const left = Math.min(topLeft[0], bottomRight[0]);
    const top = Math.min(topLeft[1], bottomRight[1]);
    const width = Math.abs(bottomRight[0] - topLeft[0]);
    const height = Math.abs(bottomRight[1] - topLeft[1]);
    const canvasRect = canvas.getBoundingClientRect();
    if (left + width < 0 || top + height < 0 || left > canvasRect.width || top > canvasRect.height) {
      return null;
    }
    return { left, top, width, height };
  }

  function drawFloorImages(room) {
    FloorData.prepareRoom(room);
    for (let y = 0; y < room.floor.height; y++) {
      for (let x = 0; x < room.floor.width; x++) {
        const object = FloorData.getFloorTile(room, x, y);
        if (!object) continue;
        const image = imageFor(object);
        if (!image) continue;
        const screen = visibleCell(room, x, y);
        if (!screen) continue;
        const inset = Math.min(3, Math.max(1, screen.width * 0.06));
        drawImageInScreenRect(image, screen.left + inset, screen.top + inset, Math.max(0, screen.width - inset * 2), Math.max(0, screen.height - inset * 2), 0.72);
      }
    }
  }

  drawRoomTiles = function (room) {
    baseDrawRoomTiles(room);
    drawFloorImages(room);
  };

  function snapForFootprint(room, position, size) {
    const columns = Math.max(1, Math.round(room.bounds.width));
    const rows = Math.max(1, Math.round(room.bounds.height));
    const width = Math.min(columns, Math.max(1, Math.round(size.width || 1)));
    const height = Math.min(rows, Math.max(1, Math.round(size.height || 1)));
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

  function brushPlacement(room, world) {
    const columns = Math.max(1, Math.round(room.bounds.width));
    const rows = Math.max(1, Math.round(room.bounds.height));
    const width = Math.min(columns, Math.max(1, Math.round(Number(state.editor.brushWidth) || 1)));
    const height = Math.min(rows, Math.max(1, Math.round(Number(state.editor.brushHeight) || 1)));
    const minX = -columns / 2;
    const minY = -rows / 2;
    const startX = clamp(Math.round(world[0] - minX - width / 2), 0, Math.max(0, columns - width));
    const startY = clamp(Math.round(world[1] - minY - height / 2), 0, Math.max(0, rows - height));
    return { startX, startY, width, height };
  }

  function drawPlacementAssetPreview() {
    if (!hoverInside || !hoverWorld || state.editor.viewMode !== "room") return;
    const tool = state.editor.tool;
    const room = currentRoom();
    if (["select", "pan", "tile-erase", "player", "teleporter"].includes(tool)) return;

    if (tool === "tile") {
      const image = imageFor(selectedFloorObject());
      if (!image) return;
      const placement = brushPlacement(room, hoverWorld);
      for (let y = placement.startY; y < placement.startY + placement.height; y += 1) {
        for (let x = placement.startX; x < placement.startX + placement.width; x += 1) {
          const screen = visibleCell(room, x, y);
          if (!screen) continue;
          drawImageInScreenRect(image, screen.left + 3, screen.top + 3,
            Math.max(0, screen.width - 6), Math.max(0, screen.height - 6), 0.32);
        }
      }
      return;
    }

    const asset = assetForTool(tool);
    const id = tool === "door" ? (asset?.id || "door.room-standard") : asset?.id;
    const image = imageFor(id);
    if (!image) return;

    if (tool === "door") {
      const cell = snapToRoomCellCenter(room, hoverWorld);
      const placement = doorEdgePlacement(room, cell);
      withTransform({ position: placement.position, rotation: placement.rotation }, () => {
        drawContained(image, 1.32 * state.editor.zoom, 0.42 * state.editor.zoom, 0.38);
      });
      return;
    }

    const size = tool === "enemy" ? { width: 1, height: 1 } : footprint(asset?.id);
    const position = snapForFootprint(room, hoverWorld, size);
    withTransform({ position, rotation: 0 }, () => {
      drawContained(image, size.width * state.editor.zoom * 0.9,
        size.height * state.editor.zoom * 0.9, 0.38);
    });
  }

  renderCanvas = function () {
    baseRenderCanvas();
    drawPlacementAssetPreview();
  };

  function thumbnail(url, label) {
    const image = document.createElement("img");
    image.className = "real-asset-thumbnail";
    image.src = url;
    image.alt = label || "Asset preview";
    image.loading = "lazy";
    image.addEventListener("error", () => image.remove());
    return image;
  }

  function enhanceAssetPickers() {
    document.querySelectorAll("[data-palette-asset]").forEach(button => {
      if (button.querySelector(".real-asset-thumbnail")) return;
      const id = button.dataset.paletteAsset;
      const url = previewUrl(id);
      if (!url) return;
      const icon = button.querySelector(":scope > span");
      const image = thumbnail(url, button.querySelector("b")?.textContent || id);
      icon?.replaceWith(image);
    });
    document.querySelectorAll("#assetList [data-asset]").forEach(row => {
      const id = row.dataset.asset;
      const url = previewUrl(id);
      if (!url) return;
      const icon = row.querySelector(".asset-icon");
      if (!icon || icon.querySelector(".real-asset-thumbnail")) return;
      icon.textContent = "";
      icon.appendChild(thumbnail(url, row.querySelector(".asset-name")?.textContent || id));
    });
  }

  renderAssets = function () {
    baseRenderAssets();
    enhanceAssetPickers();
  };

  function selectedAssetId() {
    const selection = selected();
    if (selection?.entity?.object) return selection.entity.object;
    if (selection?.door?.runtimeObject) return selection.door.runtimeObject;
    return "";
  }

  function enhanceInspector() {
    const wrap = document.querySelector("#inspector");
    if (!wrap || wrap.querySelector(".real-asset-inspector-preview")) return;
    const id = selectedAssetId();
    const url = previewUrl(id);
    if (!url) return;
    const panel = wrap.querySelector(":scope > .panel") || wrap.querySelector(".panel");
    const heading = panel?.querySelector("h2");
    if (!panel || !heading || panel.classList.contains("multi-selection-inspector")) return;
    const preview = document.createElement("div");
    preview.className = "real-asset-inspector-preview";
    preview.appendChild(thumbnail(url, assetForId(id)?.label || id));
    heading.after(preview);
  }

  renderInspector = function () {
    baseRenderInspector();
    enhanceInspector();
  };

  async function loadPreviews() {
    try {
      const response = await fetch("/api/asset-previews", { cache: "no-store" });
      if (!response.ok) throw new Error(`request failed (${response.status})`);
      const value = await response.json();
      previewUrls.clear();
      Object.entries(value.previews || {}).forEach(([id, url]) => previewUrls.set(id, url));
      imageCache.clear();
      renderAll();
    } catch (error) {
      console.info("Level Maker real asset previews are unavailable; using editor placeholders.", error);
    }
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

  loadPreviews();
})();
