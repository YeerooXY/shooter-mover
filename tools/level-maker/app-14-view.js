"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-editor-speed.css";
  document.head.appendChild(stylesheet);

  const storageKey = "shooter-mover.level-maker.view.v1";
  const defaults = {
    labels: true,
    grid: true,
    enemies: true,
    props: true,
    doors: true,
    spawn: true
  };
  let view = { ...defaults };
  try {
    view = { ...view, ...JSON.parse(localStorage.getItem(storageKey) || "null") };
  } catch {}

  const baseRenderCanvas = renderCanvas;
  const baseDrawGrid = drawGrid;
  const baseDrawEntity = drawEntity;
  const baseDrawWall = drawWall;
  const baseDrawDoor = drawDoor;
  const baseDrawPlayer = drawPlayer;
  const baseDrawMapDoorSocket = drawMapDoorSocket;
  const baseDrawMapConnection = drawMapConnection;
  const baseHitTest = hitTest;
  const baseMapHitTest = mapHitTest;

  function saveView() {
    try {
      localStorage.setItem(storageKey, JSON.stringify(view));
    } catch {}
  }

  function entityVisible(entity) {
    return entity?.kind === "enemy" ? view.enemies : view.props;
  }

  drawGrid = function (rect) {
    if (view.grid) {
      baseDrawGrid(rect);
      return;
    }
    ctx.fillStyle = "#0d1015";
    ctx.fillRect(0, 0, rect.width, rect.height);
  };

  drawEntity = function (entity) {
    if (!entityVisible(entity)) return;
    baseDrawEntity(entity);
  };

  drawWall = function (wall) {
    if (!view.props) return;
    baseDrawWall(wall);
  };

  drawDoor = function (door) {
    if (!view.doors) return;
    baseDrawDoor(door);
  };

  drawPlayer = function (player) {
    if (!view.spawn) return;
    baseDrawPlayer(player);
  };

  drawMapDoorSocket = function (room, door) {
    if (!view.doors) return;
    baseDrawMapDoorSocket(room, door);
  };

  drawMapConnection = function (connection, previewEnd = null) {
    if (!view.doors) return;
    baseDrawMapConnection(connection, previewEnd);
  };

  hitTest = function (screen) {
    const room = currentRoom();
    const entities = room.entities;
    const doors = room.doors;
    try {
      room.entities = entities.filter(entityVisible);
      if (!view.doors) room.doors = [];
      return baseHitTest(screen);
    } finally {
      room.entities = entities;
      room.doors = doors;
    }
  };

  mapHitTest = function (screen) {
    const doorLists = state.level.rooms.map(room => ({ room, doors: room.doors }));
    try {
      if (!view.doors) {
        doorLists.forEach(value => {
          value.room.doors = [];
        });
      }
      return baseMapHitTest(screen);
    } finally {
      doorLists.forEach(value => {
        value.room.doors = value.doors;
      });
    }
  };

  function footprint(value) {
    if (value?.kind === "wall" && Number(value.length) > 0) {
      return {
        width: Math.max(1, Math.round(Number(value.length) || 1)),
        height: Math.max(1, Math.round(Number(value.thickness) || 1))
      };
    }

    const asset = state.assets.find(item => item.id === value?.object);
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

  function drawSelectionOutline() {
    if (state.editor.viewMode !== "room") return;
    const selection = selected();
    const value = selection?.entity || selection?.door;
    if (!value) return;
    if (value.kind === "door" && !view.doors) return;
    if (value.kind === "enemy" && !view.enemies) return;
    if (value.kind !== "door" && value.kind !== "enemy" && !view.props) return;

    ctx.save();
    ctx.strokeStyle = "#ffffff";
    ctx.fillStyle = "rgba(121,201,255,.08)";
    ctx.lineWidth = 3;
    ctx.setLineDash([7, 4]);

    if (value.kind === "door") {
      const point = worldToScreen(value.position);
      const width = 1.72 * state.editor.zoom;
      const height = 0.55 * state.editor.zoom;
      ctx.translate(point[0], point[1]);
      ctx.rotate(-deg2rad(value.rotation || 0));
      ctx.fillRect(-width / 2, -height / 2, width, height);
      ctx.strokeRect(-width / 2, -height / 2, width, height);
      ctx.restore();
      return;
    }

    const size = value.kind === "enemy" || value.kind === "teleporter"
      ? { width: 1, height: 1 }
      : footprint(value);
    const topLeft = worldToScreen([
      value.position[0] - size.width / 2,
      value.position[1] + size.height / 2
    ]);
    const bottomRight = worldToScreen([
      value.position[0] + size.width / 2,
      value.position[1] - size.height / 2
    ]);
    const x = Math.min(topLeft[0], bottomRight[0]);
    const y = Math.min(topLeft[1], bottomRight[1]);
    const width = Math.abs(bottomRight[0] - topLeft[0]);
    const height = Math.abs(bottomRight[1] - topLeft[1]);
    ctx.fillRect(x + 2, y + 2, Math.max(0, width - 4), Math.max(0, height - 4));
    ctx.strokeRect(x + 1.5, y + 1.5, Math.max(0, width - 3), Math.max(0, height - 3));
    ctx.restore();
  }

  renderCanvas = function () {
    const starts = state.editor.viewMode === "map" && !view.spawn
      ? state.level.rooms.map(room => ({ room, start: room.playerStart }))
      : [];
    const fillText = ctx.fillText;
    try {
      starts.forEach(value => {
        value.room.playerStart = null;
      });
      if (!view.labels) ctx.fillText = () => {};
      baseRenderCanvas();
    } finally {
      ctx.fillText = fillText;
      starts.forEach(value => {
        value.room.playerStart = value.start;
      });
    }
    drawSelectionOutline();
  };

  function installViewMenu() {
    const controls = document.querySelector("#room-focus-tools");
    if (!controls || document.querySelector("#toggleViewMenu")) return;

    const button = document.createElement("button");
    button.id = "toggleViewMenu";
    button.type = "button";
    button.textContent = "View";
    const anchor = document.querySelector("#toggleToolsPopover")
      || document.querySelector("#toggleAssetsDrawer");
    anchor?.after(button);

    const menu = document.createElement("div");
    menu.id = "viewMenu";
    menu.className = "floating view-menu";
    menu.innerHTML = `<b>View</b>${[
      ["labels", "Labels"],
      ["grid", "Background grid"],
      ["enemies", "Enemies"],
      ["props", "Props & walls"],
      ["doors", "Doors & connections"],
      ["spawn", "Spawn"]
    ].map(([key, label]) => `
      <label><input type="checkbox" data-view-key="${key}"> ${label}</label>`).join("")}
      <button type="button" data-show-all>Show all</button>`;
    document.querySelector("#stage-wrap")?.appendChild(menu);

    const sync = () => {
      menu.querySelectorAll("[data-view-key]").forEach(input => {
        input.checked = !!view[input.dataset.viewKey];
      });
    };

    button.addEventListener("click", event => {
      event.stopPropagation();
      document.body.classList.toggle("view-menu-open");
      sync();
    });
    menu.addEventListener("click", event => event.stopPropagation());
    menu.querySelectorAll("[data-view-key]").forEach(input => {
      input.addEventListener("change", () => {
        view[input.dataset.viewKey] = input.checked;
        saveView();
        renderCanvas();
      });
    });
    menu.querySelector("[data-show-all]")?.addEventListener("click", () => {
      view = { ...defaults };
      saveView();
      sync();
      renderCanvas();
    });
    document.addEventListener("click", () => {
      document.body.classList.remove("view-menu-open");
    });
    sync();
  }

  installViewMenu();
})();
