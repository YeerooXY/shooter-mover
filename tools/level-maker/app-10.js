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

  function decorateToolButton(button, shortcut, icon, label) {
    if (!button) return;
    button.innerHTML = `<span class="tool-shortcut">${shortcut}</span><span class="tool-icon">${icon}</span><span>${label}</span>`;
    button.title = `${label} (${shortcut})`;
  }

  function actionButton(action, shortcut, icon, label) {
    const button = document.createElement("button");
    button.type = "button";
    button.dataset.editorAction = action;
    decorateToolButton(button, shortcut, icon, label);
    return button;
  }

  function toolCluster(label, name, buttons) {
    const cluster = document.createElement("div");
    cluster.className = "tool-cluster";
    cluster.dataset.cluster = name;
    cluster.innerHTML = `<span class="tool-cluster-label">${label}</span>`;
    buttons.filter(Boolean).forEach(button => cluster.appendChild(button));
    return cluster;
  }

  function beginRoomPlacement() {
    setViewMode("map", { focus: false });
    setMapMode("arrange");
    document.querySelector("#addRoom")?.click();
    fitMap();
    renderAll();
  }

  function beginDoorConnections() {
    state.editor.connectSourceDoorId = null;
    setViewMode("map", { focus: false });
    setMapMode("connect");
    fitMap();
    renderAll();
  }

  function installActivityToolbar() {
    const tools = document.querySelector("#tools");
    if (!tools) return;

    const select = tools.querySelector('[data-tool="select"]');
    const pan = tools.querySelector('[data-tool="pan"]');
    const enemy = tools.querySelector('[data-tool="enemy"]');
    const prop = tools.querySelector('[data-tool="prop"]');
    const floor = tools.querySelector('[data-tool="tile"]');
    const erase = tools.querySelector('[data-tool="tile-erase"]');
    const wall = tools.querySelector('[data-tool="wall"]');
    const door = tools.querySelector('[data-tool="door"]');
    const player = tools.querySelector('[data-tool="player"]');
    const teleporter = tools.querySelector('[data-tool="teleporter"]');

    decorateToolButton(select, "V", "↖", "Select");
    decorateToolButton(pan, "H", "✋", "Pan");
    decorateToolButton(erase, "X", "⌫", "Erase");
    decorateToolButton(enemy, "N", "👾", "Enemies");
    decorateToolButton(prop, "P", "▣", "Props");
    decorateToolButton(floor, "F", "▦", "Floors");
    decorateToolButton(wall, "W", "▬", "Walls");
    decorateToolButton(door, "D", "▥", "Doors");
    decorateToolButton(player, "S", "●", "Spawn");
    decorateToolButton(teleporter, "T", "◎", "Teleport");

    const newRoom = actionButton("new-room", "R", "⊞", "New room");
    const connect = actionButton("connect-doors", "C", "⇄", "Connect");
    newRoom.addEventListener("click", beginRoomPlacement);
    connect.addEventListener("click", beginDoorConnections);

    tools.replaceChildren(
      toolCluster("Edit", "edit", [select, pan, erase]),
      toolCluster("Level", "level", [newRoom, connect, player, teleporter]),
      toolCluster("Place assets", "assets", [enemy, prop, floor, wall, door])
    );
  }

  function handleActivityShortcut(event) {
    if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;

    const key = event.key.toLowerCase();
    const action = {
      r: beginRoomPlacement,
      c: beginDoorConnections,
      n: () => setTool("enemy"),
      s: () => setTool("player")
    }[key];
    if (!action) return;

    event.preventDefault();
    event.stopImmediatePropagation();
    action();
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

    const connectButton = document.querySelector('[data-editor-action="connect-doors"]');
    connectButton?.classList.toggle("active", state.editor.viewMode === "map" && state.editor.mapMode === "connect");
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

  installActivityToolbar();
  document.addEventListener("keydown", handleActivityShortcut, true);

  const mapHelp = document.querySelector("#map-mode-help");
  if (mapHelp) mapHelp.hidden = true;
  const assetChip = document.querySelector("#selected-asset-chip");
  if (assetChip) assetChip.hidden = true;
  const selectionHud = document.querySelector("#selection-hud");
  if (selectionHud) selectionHud.hidden = true;
})();
