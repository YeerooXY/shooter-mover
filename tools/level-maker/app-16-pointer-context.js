"use strict";

(() => {
  let pointerContext = null;

  function classState(name) {
    return document.body.classList.contains(name);
  }

  function restoreClass(name, enabled) {
    document.body.classList.toggle(name, enabled);
  }

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
})();
