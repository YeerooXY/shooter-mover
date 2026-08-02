"use strict";

(() => {
  const baseShowValidation = showValidation;

  function targetForIssue(issue) {
    const message = String(issue?.message || "");
    for (const room of state.rooms) {
      for (const entity of room.entities) {
        if (message.includes(entity.id)) return { room, id: entity.id };
      }
      for (const door of room.doors) {
        if (message.includes(door.id)) return { room, id: door.id };
      }
      if (message.includes(room.id)) return { room, id: "" };
    }

    const roomIndex = String(issue?.path || "").match(/rooms\[(\d+)\]/);
    if (roomIndex && state.rooms[Number(roomIndex[1])]) {
      return { room: state.rooms[Number(roomIndex[1])], id: "" };
    }

    if (issue?.path === "level.startRoomId") {
      const room = state.rooms.find(value => value.id === state.level.startRoomId);
      return room ? { room, id: "" } : { map: true };
    }
    if (issue?.path === "level.finalRoomId") {
      const room = state.rooms.find(value => value.id === state.level.finalRoomId);
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

    state.activeRoomId = target.room.id;
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

  const button = document.querySelector("#validateBtn");
  if (button) button.onclick = showValidation;
})();
