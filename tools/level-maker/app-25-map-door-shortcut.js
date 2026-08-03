"use strict";

{
  function activateMapDoorMode() {
    const enteringDoorMode = state.editor.mapMode !== "connect";
    if (enteringDoorMode) state.editor.connectSourceDoorId = null;

    setMapMode("connect");
    renderAll();

    const source = state.editor.connectSourceDoorId
      ? findDoor(state.editor.connectSourceDoorId)
      : null;
    setStatus(
      source
        ? `Connecting from ${source.room.displayName}. Click a door or anywhere inside another room.`
        : "Door mode active. Click inside a room to place a door on its nearest edge, then click another room to connect it.",
      "good"
    );
  }

  const connectButton = document.querySelector('#map-tools [data-map-mode="connect"]');
  if (connectButton) {
    connectButton.textContent = "Doors";
    connectButton.title = "Place and connect doors on the level map (D)";
  }

  document.addEventListener("keydown", event => {
    if (state.editor.viewMode !== "map") return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    if (event.key.toLowerCase() !== "d") return;

    event.preventDefault();
    event.stopImmediatePropagation();
    activateMapDoorMode();
  }, true);
}
