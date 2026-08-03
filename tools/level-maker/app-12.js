"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-context-inspector.css";
  document.head.appendChild(stylesheet);

  const baseRenderInspector = renderInspector;
  const baseRenderCanvas = renderCanvas;
  const baseRenderHeaderFields = renderHeaderFields;
  const baseDrawMapConnection = drawMapConnection;

  let pointerStart = null;
  let highlightedDoorId = "";

  function selectedValue() {
    const value = selected();
    return value?.entity || value?.door || null;
  }

  function selectedDoor() {
    return selected()?.door || null;
  }

  function objectLabel(object) {
    if (!object) return "";
    if (object.kind === "door") return object.id === state.level.finalExitDoorId ? "Final exit" : "Door";
    if (object.kind === "teleporter") return "Teleporter";
    const asset = state.assets?.find(item => item.id === object.object);
    if (asset?.label) return asset.label;
    return String(object.object || object.id || object.kind || "Object")
      .split(".").pop()
      .replace(/-/g, " ")
      .replace(/\b\w/g, character => character.toUpperCase());
  }

  function connectionsForDoor(doorId) {
    return state.level.connections.filter(connection =>
      connection.fromDoorId === doorId || connection.toDoorId === doorId
    );
  }

  function otherDoor(connection, doorId) {
    if (!connection) return null;
    const otherId = connection.fromDoorId === doorId
      ? connection.toDoorId
      : connection.fromDoorId;
    return findDoor(otherId);
  }

  function friendlyDoor(found) {
    if (!found) return "Missing door";
    const sameSide = found.room.doors.filter(door =>
      (door.side || "Door") === (found.door.side || "Door")
    );
    const index = Math.max(0, sameSide.indexOf(found.door)) + 1;
    const suffix = sameSide.length > 1 ? ` ${index}` : "";
    return `${found.room.displayName} · ${found.door.side || "Door"} door${suffix}`;
  }

  function openInspectorDrawer() {
    if (state.editor.viewMode !== "room") return;
    document.body.classList.remove("left-drawer-open", "tools-popover-open");
    document.body.classList.add("right-drawer-open", "drawer-open");
    requestAnimationFrame(resizeCanvas);
  }

  function closeInspectorDrawer() {
    document.body.classList.remove("right-drawer-open");
    if (!document.body.classList.contains("left-drawer-open")) {
      document.body.classList.remove("drawer-open");
    }
    requestAnimationFrame(resizeCanvas);
  }

  function toggleToolsPopover() {
    const opening = !document.body.classList.contains("tools-popover-open");
    document.body.classList.remove("right-drawer-open", "left-drawer-open", "drawer-open");
    document.body.classList.toggle("tools-popover-open", opening);
    document.querySelector("#toggleToolsPopover")?.classList.toggle("active", opening);
    requestAnimationFrame(resizeCanvas);
  }

  function installToolsButton() {
    const controls = document.querySelector("#room-focus-tools");
    if (!controls || controls.querySelector("#toggleToolsPopover")) return;
    const button = document.createElement("button");
    button.id = "toggleToolsPopover";
    button.type = "button";
    button.textContent = "Tools";
    button.title = "Show editing and placement tools";
    const assets = document.querySelector("#toggleAssetsDrawer");
    assets?.after(button);
    button.addEventListener("click", toggleToolsPopover);

    document.querySelector("#toggleAssetsDrawer")?.addEventListener("click", () => {
      document.body.classList.remove("tools-popover-open");
    });
    document.querySelector("#toggleInspectorDrawer")?.addEventListener("click", () => {
      document.body.classList.remove("tools-popover-open");
    });
    document.querySelectorAll("#tools button").forEach(toolButton => {
      toolButton.addEventListener("click", () => {
        document.body.classList.remove("tools-popover-open");
        button.classList.remove("active");
      });
    });
  }

  function showDoorConnection(door) {
    if (!door) return;
    highlightedDoorId = door.id;
    state.editor.selectedId = door.id;
    setViewMode("map", { focus: false });
    setMapMode("open");
    fitMap();
    renderAll();
  }

  function enhanceEnemyInspector(wrap, entity) {
    const tier = wrap.querySelector('[data-i="tier"]');
    const section = tier?.closest(".section");
    if (section) {
      section.classList.add("primary-config-section");
      const title = section.querySelector(".section-title");
      if (title) title.textContent = "Enemy settings";
    }
    const label = tier?.previousElementSibling;
    if (label?.tagName === "LABEL") label.textContent = "Tier";

    const heading = wrap.querySelector("h2");
    if (heading) {
      heading.innerHTML = `${esc(objectLabel(entity))}<span class="inspector-kind-badge">Enemy</span>`;
    }
  }

  function enhanceDoorInspector(wrap, door) {
    highlightedDoorId = door.id;
    const heading = wrap.querySelector("h2");
    if (heading) {
      heading.innerHTML = `${door.id === state.level.finalExitDoorId ? "Final exit" : "Door"}`
        + `${door.id === state.level.finalExitDoorId ? '<span class="inspector-kind-badge final">Exit</span>' : ''}`;
    }

    const connectButton = wrap.querySelector('[data-connect-door]');
    if (connectButton) connectButton.textContent = "Connect to door";

    const connections = connectionsForDoor(door.id);
    const summary = document.createElement("div");
    summary.className = "door-connection-panel";
    if (!connections.length) {
      summary.innerHTML = `<b>Not connected</b><span>Use Connect to door to choose a destination.</span>`;
    } else {
      const rows = connections.map(connection => {
        const target = otherDoor(connection, door.id);
        const number = state.level.connections.indexOf(connection) + 1;
        return `<div class="door-connection-row">
          <span class="connection-number">${number}</span>
          <b>${esc(friendlyDoor(target))}</b>
        </div>`;
      }).join("");
      summary.innerHTML = `<div class="door-connection-heading">Connected</div>${rows}
        <button type="button" class="primary" data-show-door-connection>Show connection</button>`;
      summary.querySelector("[data-show-door-connection]")?.addEventListener("click", () => showDoorConnection(door));
    }

    const actions = wrap.querySelector(".door-action-row");
    (actions || heading)?.after(summary);
  }

  renderInspector = function () {
    baseRenderInspector();
    const wrap = document.querySelector("#inspector");
    const value = selected();
    if (!wrap || !value) return;

    if (value.entity?.kind === "enemy") enhanceEnemyInspector(wrap, value.entity);
    if (value.door) enhanceDoorInspector(wrap, value.door);
  };

  function connectionNumbers(doorId) {
    const numbers = [];
    state.level.connections.forEach((connection, index) => {
      if (connection.fromDoorId === doorId || connection.toDoorId === doorId) numbers.push(index + 1);
    });
    return numbers;
  }

  function highlightedConnection(connection) {
    const selectedId = selectedDoor()?.id || highlightedDoorId;
    return !!selectedId && (
      connection.fromDoorId === selectedId || connection.toDoorId === selectedId
    );
  }

  function highlightedDoor(doorId) {
    const selectedId = selectedDoor()?.id || highlightedDoorId;
    if (!selectedId) return false;
    return state.level.connections.some(connection => highlightedConnection(connection)
      && (connection.fromDoorId === doorId || connection.toDoorId === doorId));
  }

  drawDoor = function (door) {
    const finalExit = door.id === state.level.finalExitDoorId;
    withTransform(door, () => {
      const width = 1.5 * state.editor.zoom;
      const height = 0.35 * state.editor.zoom;
      ctx.fillStyle = finalExit ? "#48e58b" : "#ffd166";
      ctx.strokeStyle = isSelected(door.id) ? "#ffffff" : finalExit ? "#d9ffe8" : "#6b5724";
      ctx.lineWidth = isSelected(door.id) || finalExit ? 3 : 1;
      ctx.fillRect(-width / 2, -height / 2, width, height);
      ctx.strokeRect(-width / 2, -height / 2, width, height);
      if (finalExit) {
        ctx.strokeStyle = "rgba(72,229,139,.5)";
        ctx.lineWidth = 7;
        ctx.strokeRect(-width / 2, -height / 2, width, height);
      }
    });

    const point = worldToScreen(door.position);
    ctx.save();
    ctx.fillStyle = finalExit ? "#7effad" : "#f2f5fb";
    ctx.font = finalExit ? "900 10px system-ui" : "10px system-ui";
    ctx.textAlign = "center";
    ctx.fillText(finalExit ? "★ EXIT" : "Door", point[0], point[1] + 25);
    ctx.restore();
  };

  drawPlayer = function (player) {
    const point = worldToScreen(player.position);
    const rotation = -deg2rad(player.rotation || 0);
    ctx.save();
    ctx.translate(point[0], point[1]);
    ctx.rotate(rotation);
    ctx.fillStyle = "rgba(88,244,154,.2)";
    ctx.strokeStyle = "#74ffad";
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.arc(0, 0, 18, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = "#74ffad";
    ctx.beginPath();
    ctx.moveTo(14, 0);
    ctx.lineTo(-9, -9);
    ctx.lineTo(-9, 9);
    ctx.closePath();
    ctx.fill();
    ctx.restore();

    ctx.save();
    ctx.fillStyle = "#b8ffd3";
    ctx.font = "900 10px system-ui";
    ctx.textAlign = "center";
    ctx.fillText("SPAWN", point[0], point[1] + 32);
    ctx.restore();
  };

  drawMapDoorSocket = function (room, door) {
    const point = worldToScreen(mapDoorWorldPosition(room, door));
    const numbers = connectionNumbers(door.id);
    const connected = numbers.length > 0;
    const finalExit = door.id === state.level.finalExitDoorId;
    const highlighted = highlightedDoor(door.id);
    const radius = finalExit ? 14 : connected ? 11 : 8;

    ctx.save();
    ctx.fillStyle = finalExit ? "#48e58b" : connected ? "#79c9ff" : "#ffd166";
    ctx.strokeStyle = highlighted ? "#ff5364" : door.id === state.editor.selectedId
      ? "#ffffff"
      : finalExit ? "#d9ffe8" : connected ? "#d9f2ff" : "#604d20";
    ctx.lineWidth = highlighted || door.id === state.editor.selectedId || finalExit ? 3 : 2;
    ctx.beginPath();
    ctx.arc(point[0], point[1], radius, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    if (finalExit) {
      ctx.strokeStyle = "rgba(72,229,139,.48)";
      ctx.lineWidth = 5;
      ctx.beginPath();
      ctx.arc(point[0], point[1], radius + 4, 0, Math.PI * 2);
      ctx.stroke();
    }

    ctx.fillStyle = "#0d1722";
    ctx.font = "900 10px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    if (connected) ctx.fillText(numbers.length > 1 ? numbers.join("/") : String(numbers[0]), point[0], point[1] + 0.5);
    else if (finalExit) ctx.fillText("★", point[0], point[1] + 0.5);

    if (finalExit) {
      ctx.fillStyle = "#a8ffca";
      ctx.font = "900 8px system-ui";
      ctx.fillText("EXIT", point[0], point[1] + radius + 10);
    }
    ctx.restore();
  };

  drawMapConnection = function (connection, previewEnd = null) {
    baseDrawMapConnection(connection, previewEnd);
    if (previewEnd || !highlightedConnection(connection)) return;
    const source = findDoor(connection.fromDoorId);
    const target = findDoor(connection.toDoorId);
    if (!source || !target) return;
    const from = worldToScreen(mapDoorWorldPosition(source.room, source.door));
    const to = worldToScreen(mapDoorWorldPosition(target.room, target.door));
    const number = state.level.connections.indexOf(connection) + 1;
    const center = [(from[0] + to[0]) / 2, (from[1] + to[1]) / 2];

    ctx.save();
    ctx.strokeStyle = "#ff5364";
    ctx.lineWidth = 5;
    ctx.beginPath();
    ctx.moveTo(from[0], from[1]);
    ctx.lineTo(to[0], to[1]);
    ctx.stroke();
    ctx.fillStyle = "#ff5364";
    ctx.strokeStyle = "#ffe0e4";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(center[0], center[1], 12, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = "#2a090d";
    ctx.font = "900 10px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(String(number), center[0], center[1] + 0.5);
    ctx.restore();
  };

  function drawMapSpawns() {
    const half = [4.5, 3];
    for (const room of state.level.rooms) {
      if (!room.playerStart) continue;
      const center = mapRoomCenter(room);
      const horizontal = clamp(room.playerStart.position?.[0] / (room.bounds.width / 2 || 1), -0.78, 0.78);
      const vertical = clamp(room.playerStart.position?.[1] / (room.bounds.height / 2 || 1), -0.72, 0.72);
      const world = [center[0] + horizontal * half[0], center[1] + vertical * half[1]];
      const point = worldToScreen(world);
      ctx.save();
      ctx.fillStyle = "#54ef96";
      ctx.strokeStyle = "#d8ffe7";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(point[0], point[1], 9, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = "#092116";
      ctx.font = "900 9px system-ui";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText("S", point[0], point[1] + 0.5);
      ctx.restore();
    }
  }

  renderCanvas = function () {
    baseRenderCanvas();
    if (state.editor.viewMode === "map") drawMapSpawns();
  };

  renderHeaderFields = function () {
    baseRenderHeaderFields();
    installToolsButton();
    const toolsButton = document.querySelector("#toggleToolsPopover");
    toolsButton?.classList.toggle("active", document.body.classList.contains("tools-popover-open"));
  };

  canvas.addEventListener("pointerdown", event => {
    pointerStart = {
      x: event.clientX,
      y: event.clientY,
      tool: state.editor.tool,
      view: state.editor.viewMode
    };
  }, true);

  canvas.addEventListener("pointerup", event => {
    const start = pointerStart;
    pointerStart = null;
    if (!start || start.view !== "room" || state.editor.viewMode !== "room") return;
    const movement = Math.hypot(event.clientX - start.x, event.clientY - start.y);
    const value = selectedValue();

    if (value) {
      highlightedDoorId = value.kind === "door" ? value.id : "";
      renderInspector();
      openInspectorDrawer();
      renderCanvas();
      return;
    }

    if (start.tool === "select" && movement < 6) {
      highlightedDoorId = "";
      closeInspectorDrawer();
      renderCanvas();
    }
  });

  document.addEventListener("keydown", event => {
    if (event.key !== "Escape") return;
    document.body.classList.remove("tools-popover-open");
  }, true);

  installToolsButton();
})();
