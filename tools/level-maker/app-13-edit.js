"use strict";

(() => {
  let clipboard = null;

  function currentSelection() {
    return selected();
  }

  function selectedValue() {
    const selection = currentSelection();
    return selection?.entity || selection?.door || null;
  }

  function assetFor(value) {
    const id = typeof value === "string" ? value : value?.object;
    return state.catalog.find(asset => asset.id === id) || null;
  }

  function footprint(value) {
    if (value?.kind === "wall" && Number(value.length) > 0) {
      return {
        width: Math.max(1, Math.round(Number(value.length) || 1)),
        height: Math.max(1, Math.round(Number(value.thickness) || 1))
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

    const text = `${asset?.id || value?.object || value || ""} ${asset?.label || ""}`;
    const match = text.match(/(\d+)\s*[x×]\s*(\d+)/i);
    return match
      ? { width: Math.max(1, Number(match[1])), height: Math.max(1, Number(match[2])) }
      : { width: 1, height: 1 };
  }

  function footprintRect(position, size) {
    return {
      left: position[0] - size.width / 2,
      right: position[0] + size.width / 2,
      bottom: position[1] - size.height / 2,
      top: position[1] + size.height / 2
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

  function footprintAvailable(room, position, size, ignoreId = "") {
    const proposed = footprintRect(position, size);
    return !room.entities.some(entity => {
      if (!isBlocking(entity) || entity.id === ignoreId) return false;
      return rectanglesOverlap(proposed, footprintRect(entity.position, footprint(entity)));
    });
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

  function openInspector() {
    document.body.classList.remove(
      "left-drawer-open",
      "tools-popover-open",
      "view-menu-open"
    );
    document.body.classList.add("right-drawer-open", "drawer-open");
    requestAnimationFrame(resizeCanvas);
  }

  function copySelection() {
    const value = selectedValue();
    if (!value) return false;
    clipboard = {
      door: value.kind === "door",
      data: clone(value)
    };
    setStatus("Copied.", "good");
    return true;
  }

  function nearbyPositions(room, source, size) {
    const step = Math.max(1, Number(state.editor.snapSize) || 1);
    const offsets = [
      [1, 0], [0, 1], [-1, 0], [0, -1],
      [1, 1], [1, -1], [-1, 1], [-1, -1],
      [2, 0], [0, 2], [-2, 0], [0, -2]
    ];
    return offsets.map(offset => snapForFootprint(
      room,
      [source.position[0] + offset[0] * step, source.position[1] + offset[1] * step],
      size
    ));
  }

  function pasteSelection() {
    if (state.editor.viewMode !== "room") {
      setStatus("Open a room before pasting objects.", "warn");
      return false;
    }
    if (!clipboard) {
      setStatus("Copy an object first.", "warn");
      return false;
    }

    const room = currentRoom();
    const source = clipboard.data;
    if (clipboard.door) {
      const placement = nearbyPositions(room, source, { width: 1, height: 1 })
        .map(position => doorEdgePlacement(room, position))
        .find(candidate => doorPositionAvailable(room, candidate));
      if (!placement) {
        setStatus("No free door position nearby.", "warn");
        return false;
      }

      mutate(() => {
        const door = clone(source);
        door.id = uid("door");
        Object.assign(door, placement);
        room.doors.push(door);
        state.editor.selectedId = door.id;
      });
      openInspector();
      return true;
    }

    const size = source.kind === "enemy" || source.kind === "teleporter"
      ? { width: 1, height: 1 }
      : footprint(source);
    const candidates = nearbyPositions(room, source, size);
    const position = isBlocking(source)
      ? candidates.find(candidate => footprintAvailable(room, candidate, size))
      : candidates[0];
    if (!position) {
      setStatus("No free space nearby.", "warn");
      return false;
    }

    mutate(() => {
      const entity = clone(source);
      entity.id = uid(entity.kind || "entity");
      entity.position = position;
      room.entities.push(entity);
      state.editor.selectedId = entity.id;
    });
    openInspector();
    return true;
  }

  function duplicateSelection() {
    if (state.editor.viewMode !== "room") {
      setStatus("Open a room before duplicating objects.", "warn");
      return false;
    }
    return copySelection() && pasteSelection();
  }

  function nudgeSelection(deltaX, deltaY, largeStep) {
    const selection = currentSelection();
    const value = selection?.entity || selection?.door;
    if (!value || state.editor.viewMode !== "room") return false;

    const room = selection.room;
    const step = Math.max(0.25, Number(state.editor.snapSize) || 1) * (largeStep ? 4 : 1);
    const raw = [
      value.position[0] + deltaX * step,
      value.position[1] + deltaY * step
    ];

    if (selection.door) {
      const placement = doorEdgePlacement(room, raw);
      if (!doorPositionAvailable(room, placement, value.id)) {
        setStatus("Another door occupies that position.", "warn");
        return true;
      }
      mutate(() => Object.assign(value, placement));
      openInspector();
      return true;
    }

    const size = value.kind === "enemy" || value.kind === "teleporter"
      ? { width: 1, height: 1 }
      : footprint(value);
    const position = snapForFootprint(room, raw, size);
    if (isBlocking(value) && !footprintAvailable(room, position, size, value.id)) {
      setStatus("That space is occupied.", "warn");
      return true;
    }

    mutate(() => {
      value.position = position;
    });
    openInspector();
    return true;
  }

  document.addEventListener("keydown", event => {
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;

    const key = event.key.toLowerCase();
    const stop = () => {
      event.preventDefault();
      event.stopImmediatePropagation();
    };

    if ((event.ctrlKey || event.metaKey) && key === "c") {
      if (copySelection()) stop();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "v") {
      stop();
      pasteSelection();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "d") {
      stop();
      duplicateSelection();
      return;
    }
    if (event.ctrlKey || event.metaKey || event.altKey) return;

    const direction = {
      arrowleft: [-1, 0],
      arrowright: [1, 0],
      arrowup: [0, 1],
      arrowdown: [0, -1]
    }[key];
    if (direction && nudgeSelection(direction[0], direction[1], event.shiftKey)) stop();
  }, true);
})();
