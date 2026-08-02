"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-door-tools.css";
  document.head.appendChild(stylesheet);

  const baseRenderInspector = renderInspector;
  const baseDrawDoor = drawDoor;
  const baseDrawMapDoorSocket = drawMapDoorSocket;
  const baseDrawMapConnection = drawMapConnection;

  let hoverDoorId = "";
  let hoverConnectionId = "";

  function connectionsForDoor(doorId) {
    return state.connections.filter(connection =>
      connection.fromDoorId === doorId || connection.toDoorId === doorId
    );
  }

  function targetFor(connection, doorId) {
    const targetId = connection.fromDoorId === doorId
      ? connection.toDoorId
      : connection.fromDoorId;
    return findDoor(targetId);
  }

  function friendlyDoor(found) {
    if (!found) return "Missing door";
    const sameSide = found.room.doors.filter(door =>
      (door.side || "Door") === (found.door.side || "Door")
    );
    const index = Math.max(0, sameSide.indexOf(found.door)) + 1;
    return `${found.room.displayName} · ${found.door.side || "Door"}${sameSide.length > 1 ? ` ${index}` : ""}`;
  }

  function directionLabel(connection, selectedDoorId) {
    if (connection.travelPolicy !== "OneWay") return "Bidirectional";
    return connection.fromDoorId === selectedDoorId
      ? "One-way from this door"
      : "One-way into this door";
  }

  function openInspectorDrawer() {
    document.body.classList.remove("left-drawer-open", "tools-popover-open", "view-menu-open");
    document.body.classList.add("right-drawer-open", "drawer-open");
    requestAnimationFrame(resizeCanvas);
  }

  function jumpToDoor(doorId) {
    const found = findDoor(doorId);
    if (!found) {
      setStatus("The connected door could not be found.", "warn");
      return;
    }
    state.activeRoomId = found.room.id;
    setViewMode("room", { focus: true });
    state.editor.selectedId = found.door.id;
    fitRoom();
    renderAll();
    openInspectorDrawer();
    setStatus(`Opened ${friendlyDoor(found)}.`, "good");
  }

  function disconnect(connectionId) {
    const connection = state.connections.find(value => value.id === connectionId);
    if (!connection) return;
    mutate(() => {
      state.connections = state.connections.filter(value => value.id !== connectionId);
    });
    setStatus("Door connection removed.", "good");
  }

  renderInspector = function () {
    baseRenderInspector();
    const wrap = document.querySelector("#inspector");
    if (!wrap || wrap.querySelector(".multi-selection-inspector")) return;
    const door = selected()?.door;
    if (!door) return;

    const connections = connectionsForDoor(door.id);
    const panel = document.createElement("div");
    panel.className = "door-navigation-panel";
    if (!connections.length && door.id === state.level.finalExitDoorId) {
      panel.innerHTML = `<div class="door-nav-exit"><b>Final exit</b><span>This door leaves the level and does not require a room connection.</span></div>`;
    } else if (!connections.length) {
      panel.innerHTML = `<div class="door-nav-warning"><b>Unconnected door</b><span>Connect it before publishing unless it is intentionally unused.</span></div>`;
    } else {
      panel.innerHTML = `<div class="door-nav-heading">Connection tools</div>${connections.map(connection => {
        const target = targetFor(connection, door.id);
        const number = state.connections.indexOf(connection) + 1;
        return `<div class="door-nav-row" data-door-connection="${esc(connection.id)}">
          <span class="door-nav-number">${number}</span>
          <div class="door-nav-target"><b>${esc(friendlyDoor(target))}</b><span>${esc(directionLabel(connection, door.id))}</span></div>
          <button type="button" data-jump-door="${esc(target?.door.id || "")}" ${target ? "" : "disabled"}>Jump</button>
          <button type="button" class="danger" data-disconnect-door="${esc(connection.id)}">Disconnect</button>
        </div>`;
      }).join("")}`;
      panel.querySelectorAll("[data-jump-door]").forEach(button => {
        button.addEventListener("click", () => jumpToDoor(button.dataset.jumpDoor));
      });
      panel.querySelectorAll("[data-disconnect-door]").forEach(button => {
        button.addEventListener("click", () => disconnect(button.dataset.disconnectDoor));
      });
    }

    const existing = wrap.querySelector(".door-connection-panel");
    if (existing) existing.after(panel);
    else wrap.querySelector(".panel")?.appendChild(panel);
  };

  function doorsVisible() {
    const control = document.querySelector('#viewMenu [data-view-key="doors"]');
    return !control || control.checked;
  }

  function unconnectedOrdinaryDoor(door) {
    return door.id !== state.level.finalExitDoorId && connectionsForDoor(door.id).length === 0;
  }

  drawDoor = function (door) {
    baseDrawDoor(door);
    if (!doorsVisible() || !unconnectedOrdinaryDoor(door)) return;
    const point = worldToScreen(door.position);
    ctx.save();
    ctx.fillStyle = "#ff6677";
    ctx.strokeStyle = "#2a090d";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(point[0] + 13, point[1] - 13, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = "#2a090d";
    ctx.font = "900 10px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText("!", point[0] + 13, point[1] - 12.5);
    ctx.restore();
  };

  function selectedConnection(connection) {
    const doorId = selected()?.door?.id;
    return !!doorId && (
      connection.fromDoorId === doorId || connection.toDoorId === doorId
    );
  }

  function hoveredConnection(connection) {
    return connection.id === hoverConnectionId
      || (!!hoverDoorId && (
        connection.fromDoorId === hoverDoorId || connection.toDoorId === hoverDoorId
      ));
  }

  function connectionPoints(connection) {
    const source = findDoor(connection.fromDoorId);
    const target = findDoor(connection.toDoorId);
    if (!source || !target) return null;
    return {
      source,
      target,
      from: worldToScreen(mapDoorWorldPosition(source.room, source.door)),
      to: worldToScreen(mapDoorWorldPosition(target.room, target.door))
    };
  }

  function drawConnectionLine(connection, points) {
    if (!hoveredConnection(connection) || selectedConnection(connection)) return;
    ctx.save();
    ctx.strokeStyle = "rgba(235,248,255,.98)";
    ctx.lineWidth = 5;
    ctx.setLineDash([10, 6]);
    ctx.beginPath();
    ctx.moveTo(points.from[0], points.from[1]);
    ctx.lineTo(points.to[0], points.to[1]);
    ctx.stroke();
    ctx.restore();
  }

  function drawDirectionArrow(connection, points) {
    if (connection.travelPolicy !== "OneWay") return;
    const x = points.from[0] + (points.to[0] - points.from[0]) * 0.62;
    const y = points.from[1] + (points.to[1] - points.from[1]) * 0.62;
    const angle = Math.atan2(points.to[1] - points.from[1], points.to[0] - points.from[0]);
    const selected = selectedConnection(connection);
    const hovered = hoveredConnection(connection);
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(angle);
    ctx.fillStyle = selected ? "#ff5364" : hovered ? "#ffffff" : "#8ed8ff";
    ctx.strokeStyle = "#10202e";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(11, 0);
    ctx.lineTo(-7, -7);
    ctx.lineTo(-3, 0);
    ctx.lineTo(-7, 7);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.restore();
  }

  drawMapConnection = function (connection, previewEnd = null) {
    baseDrawMapConnection(connection, previewEnd);
    if (!doorsVisible() || previewEnd) return;
    const points = connectionPoints(connection);
    if (!points) return;
    drawConnectionLine(connection, points);
    drawDirectionArrow(connection, points);
  };

  function relatedToHover(doorId) {
    if (doorId === hoverDoorId) return true;
    if (hoverConnectionId) {
      const connection = state.connections.find(value => value.id === hoverConnectionId);
      return connection?.fromDoorId === doorId || connection?.toDoorId === doorId;
    }
    if (!hoverDoorId) return false;
    return state.connections.some(connection =>
      (connection.fromDoorId === hoverDoorId || connection.toDoorId === hoverDoorId)
      && (connection.fromDoorId === doorId || connection.toDoorId === doorId)
    );
  }

  drawMapDoorSocket = function (room, door) {
    baseDrawMapDoorSocket(room, door);
    if (!doorsVisible()) return;
    const point = worldToScreen(mapDoorWorldPosition(room, door));
    if (relatedToHover(door.id) && door.id !== selected()?.door?.id) {
      ctx.save();
      ctx.strokeStyle = "#ffffff";
      ctx.lineWidth = 3;
      ctx.setLineDash([4, 3]);
      ctx.beginPath();
      ctx.arc(point[0], point[1], 17, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }
    if (!unconnectedOrdinaryDoor(door)) return;
    ctx.save();
    ctx.strokeStyle = "#ff6677";
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.arc(point[0], point[1], 13, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = "#ff7d8b";
    ctx.font = "900 9px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText("!", point[0], point[1] + 0.5);
    ctx.restore();
  };

  function distanceToSegment(point, start, end) {
    const dx = end[0] - start[0];
    const dy = end[1] - start[1];
    const lengthSquared = dx * dx + dy * dy;
    if (!lengthSquared) return Math.hypot(point[0] - start[0], point[1] - start[1]);
    const amount = clamp(
      ((point[0] - start[0]) * dx + (point[1] - start[1]) * dy) / lengthSquared,
      0,
      1
    );
    return Math.hypot(
      point[0] - (start[0] + amount * dx),
      point[1] - (start[1] + amount * dy)
    );
  }

  function connectionAt(point) {
    let best = null;
    let distance = 9;
    for (const connection of state.connections) {
      const points = connectionPoints(connection);
      if (!points) continue;
      const current = distanceToSegment(point, points.from, points.to);
      if (current < distance) {
        distance = current;
        best = connection;
      }
    }
    return best;
  }

  function updateHover(event) {
    if (state.editor.viewMode !== "map" || !doorsVisible()) {
      if (hoverDoorId || hoverConnectionId) {
        hoverDoorId = "";
        hoverConnectionId = "";
        renderCanvas();
      }
      return;
    }
    const point = eventPoint(event);
    const hit = mapHitTest(point);
    const nextDoor = hit?.type === "door" ? hit.door.id : "";
    const nextConnection = nextDoor ? "" : (connectionAt(point)?.id || "");
    if (nextDoor === hoverDoorId && nextConnection === hoverConnectionId) return;
    hoverDoorId = nextDoor;
    hoverConnectionId = nextConnection;
    canvas.style.cursor = nextDoor || nextConnection ? "pointer" : (
      state.editor.mapMode === "arrange" ? "move" : state.editor.mapMode === "connect" ? "crosshair" : "default"
    );
    renderCanvas();
  }

  canvas.addEventListener("pointermove", updateHover);
  canvas.addEventListener("pointerleave", () => {
    if (!hoverDoorId && !hoverConnectionId) return;
    hoverDoorId = "";
    hoverConnectionId = "";
    renderCanvas();
  });

  window.addEventListener("keydown", event => {
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    if (event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
    if (event.key.toLowerCase() !== "j") return;
    const door = selected()?.door;
    const connections = door ? connectionsForDoor(door.id) : [];
    if (connections.length !== 1) return;
    const target = targetFor(connections[0], door.id);
    if (!target) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    jumpToDoor(target.door.id);
  }, true);
})();
