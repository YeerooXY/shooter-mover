"use strict";

(() => {
  const baseShowValidation = showValidation;
  let pointerContext = null;

  function classState(name) {
    return document.body.classList.contains(name);
  }

  function restoreClass(name, enabled) {
    document.body.classList.toggle(name, enabled);
  }

  function targetForIssue(issue) {
    const message = String(issue?.message || "");
    for (const room of state.level.rooms) {
      for (const entity of room.entities) {
        if (message.includes(entity.id)) return { room, id: entity.id };
      }
      for (const door of room.doors) {
        if (message.includes(door.id)) return { room, id: door.id };
      }
      if (message.includes(room.id)) return { room, id: "" };
    }

    const roomIndex = String(issue?.path || "").match(/rooms\[(\d+)\]/);
    if (roomIndex && state.level.rooms[Number(roomIndex[1])]) {
      return { room: state.level.rooms[Number(roomIndex[1])], id: "" };
    }

    if (issue?.path === "level.startRoomId") {
      const room = state.level.rooms.find(value => value.id === state.level.startRoomId);
      return room ? { room, id: "" } : { map: true };
    }
    if (issue?.path === "level.finalRoomId") {
      const room = state.level.rooms.find(value => value.id === state.level.finalRoomId);
      return room ? { room, id: "" } : { map: true };
    }
    if (
      message.toLowerCase().includes("door")
      || message.toLowerCase().includes("connection")
      || issue?.path === "level.finalExitDoorId"
    ) {
      return { map: true };
    }
    return null;
  }

  function focusTarget(target) {
    document.querySelector("#validationDialog")?.close();
    document.body.classList.remove(
      "left-drawer-open",
      "right-drawer-open",
      "drawer-open",
      "tools-popover-open",
      "view-menu-open"
    );

    if (target.map) {
      state.editor.selectedId = null;
      setViewMode("map", { focus: false });
      fitMap();
      renderAll();
      return;
    }
    if (!target.room) return;

    state.editor.activeRoomId = target.room.id;
    setViewMode("room", { focus: true });
    state.editor.selectedId = target.id || null;
    fitRoom();
    renderAll();

    if (target.id) {
      document.body.classList.add("right-drawer-open", "drawer-open");
      requestAnimationFrame(resizeCanvas);
    }
  }

  showValidation = function () {
    const result = baseShowValidation();
    const issues = result?.issues || [];
    requestAnimationFrame(() => {
      [...document.querySelectorAll("#validationList .validation-item")].forEach((element, index) => {
        const target = targetForIssue(issues[index]);
        if (!target) return;
        element.classList.add("validation-jump");
        element.title = "Show this in the editor";
        element.addEventListener("click", () => focusTarget(target));
      });
    });
    return result;
  };

  canvas.addEventListener("pointerdown", event => {
    pointerContext = {
      button: event.button,
      viewMode: state.editor.viewMode,
      tool: state.editor.tool,
      placementMode: state.editor.placementMode,
      selectedId: state.editor.selectedId,
      leftDrawer: classState("left-drawer-open"),
      rightDrawer: classState("right-drawer-open"),
      drawer: classState("drawer-open"),
      tools: classState("tools-popover-open")
    };
  }, true);

  canvas.addEventListener("pointerup", () => {
    const context = pointerContext;
    pointerContext = null;
    if (!context || context.viewMode !== "room") return;

    const placementTool = [
      "enemy",
      "prop",
      "wall",
      "door",
      "player",
      "teleporter"
    ].includes(context.tool);
    const createdSelection = !!state.editor.selectedId
      && state.editor.selectedId !== context.selectedId;
    const preserveInspector = context.button !== 0
      || context.tool === "pan"
      || context.tool === "tile"
      || context.tool === "tile-erase"
      || (placementTool && (context.placementMode === "paint" || !createdSelection));
    if (!preserveInspector) return;

    restoreClass("left-drawer-open", context.leftDrawer);
    restoreClass("right-drawer-open", context.rightDrawer);
    restoreClass("drawer-open", context.drawer);
    restoreClass("tools-popover-open", context.tools);
    requestAnimationFrame(resizeCanvas);
  });

  const button = document.querySelector("#validateBtn");
  if (button) button.onclick = showValidation;
})();
