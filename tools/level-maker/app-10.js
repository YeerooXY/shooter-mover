"use strict";

(() => {
  const stylesheet = document.createElement("link");
  stylesheet.rel = "stylesheet";
  stylesheet.href = "style-canvas-first.css";
  document.head.appendChild(stylesheet);

  const baseRenderHeaderFields = renderHeaderFields;
  const baseRenderFooter = renderFooter;
  const baseRenderInspector = renderInspector;
  const baseRenderLogic = renderLogic;

  function objectLabel(object) {
    if (!object) return "";
    if (object.kind === "door") return "Door";
    if (object.kind === "teleporter") return "Teleporter";
    const asset = state.catalog?.find(value => value.id === object.object);
    if (asset?.label) return asset.label;
    const value = object.object || object.id || object.kind || "Object";
    return String(value)
      .split(".").pop()
      .replace(/-/g, " ")
      .replace(/\b\w/g, character => character.toUpperCase());
  }

  function selectedObject() {
    const value = selected();
    return value?.entity || value?.door || null;
  }

  renderHeaderFields = function () {
    baseRenderHeaderFields();

    const room = currentRoom();
    const roomHud = document.querySelector("#room-hud");
    if (roomHud) {
      roomHud.innerHTML = state.editor.viewMode === "map"
        ? `<b>${esc(state.level.name)}</b> · ${state.rooms.length} rooms`
        : `<b>${esc(room.displayName)}</b> · ${room.bounds.width} × ${room.bounds.height}`;
    }

    const asset = state.catalog?.find(value => value.id === state.editor.selectedAssetId);
    const assetChip = document.querySelector("#selected-asset-chip");
    if (assetChip) {
      assetChip.textContent = asset ? `${iconFor(asset.type)} ${asset.label || objectLabel({ object: asset.id })}` : "";
      assetChip.hidden = !asset;
    }

    const mapHelp = document.querySelector("#map-mode-help");
    if (mapHelp) {
      mapHelp.textContent = "";
      mapHelp.hidden = true;
    }

    const inspectorButton = document.querySelector("#toggleInspectorDrawer");
    if (inspectorButton) inspectorButton.textContent = objectLabel(selectedObject()) || "Room";
  };

  renderFooter = function () {
    baseRenderFooter();
    const object = selectedObject();
    const selectionHud = document.querySelector("#selection-hud");
    if (!selectionHud) return;
    selectionHud.textContent = objectLabel(object);
    selectionHud.hidden = !object;
  };

  renderInspector = function () {
    baseRenderInspector();
    if (selected()) return;
    document.querySelector("#inspector .notice")?.remove();
  };

  renderLogic = function () {
    baseRenderLogic();
    const empty = document.querySelector("#connectionList > .help");
    if (empty) empty.textContent = "No connections";
  };

  const startTitle = document.querySelector("#startDialog h2");
  if (startTitle) startTitle.textContent = "Levels";

  const projectMenu = document.querySelector("#projectMenu");
  projectMenu?.querySelectorAll("button").forEach(button => {
    button.addEventListener("click", () => projectMenu.removeAttribute("open"));
  });

  const playtest = document.querySelector("#exportBtn");
  if (playtest) {
    playtest.textContent = "▶ Playtest";
    playtest.title = "Write this level into the Unity project so it appears in Levels";
  }

  const hiddenSave = document.querySelector("#saveBtn");
  if (hiddenSave) hiddenSave.hidden = true;

  const mapHelp = document.querySelector("#map-mode-help");
  if (mapHelp) mapHelp.hidden = true;
  const assetChip = document.querySelector("#selected-asset-chip");
  if (assetChip) assetChip.hidden = true;
  const selectionHud = document.querySelector("#selection-hud");
  if (selectionHud) selectionHud.hidden = true;
})();
