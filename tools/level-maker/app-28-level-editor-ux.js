"use strict";

{
  const previousNormalize = normalize;
  const previousRenderHeaderFields = renderHeaderFields;
  const previousRenderAssets = renderAssets;
  const previousSetViewMode = setViewMode;
  const previousSetTool = setTool;

  const ASSET_TYPES = [
    ["all", "All"],
    ["enemy", "Enemies"],
    ["prop", "Props"],
    ["floor", "Floors"],
    ["door", "Doors"],
    ["decor", "Decor"],
  ];

  const stylesheet = document.createElement("style");
  stylesheet.textContent = `
    #map-tools,
    #asset-group-switch {
      display: none !important;
    }

    body.room-focus #mode-switch {
      display: flex !important;
    }

    #persistent-editor-tools {
      position: absolute;
      top: 10px;
      right: 10px;
      z-index: 38;
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 5px;
      white-space: nowrap;
    }
    #persistent-editor-tools button {
      padding: 6px 10px;
    }
    #persistent-editor-tools .persistent-divider {
      width: 1px;
      height: 23px;
      margin: 0 2px;
      background: var(--line);
    }
    #persistent-editor-tools .persistent-label {
      margin: 0 2px 0 4px;
      color: var(--muted);
      font-size: 9px;
    }
    #persistent-editor-tools select {
      width: auto;
      min-width: 68px;
      padding: 5px 7px;
    }

    #view-hud,
    body.room-focus #view-hud {
      top: 58px !important;
      right: 10px !important;
    }
    #viewMenu {
      top: 58px !important;
      right: 10px !important;
      bottom: auto !important;
    }

    body.room-focus #room-focus-tools {
      left: 14px !important;
      top: 14px !important;
      right: auto !important;
      transform: none !important;
      max-width: min(420px, calc(100% - 28px)) !important;
    }
    #room-focus-tools #backToGraph,
    #room-focus-tools .divider,
    #room-focus-tools .grid-label,
    #room-focus-tools #selected-asset-chip {
      display: none !important;
    }

    #asset-palette.unified-asset-palette {
      display: none;
      top: 58px !important;
      left: 14px !important;
      right: auto !important;
      bottom: auto !important;
      width: min(320px, calc(100% - 28px)) !important;
      height: auto !important;
      min-height: 0 !important;
      max-height: none !important;
      padding: 10px !important;
      overflow: hidden !important;
      z-index: 37;
    }
    body.room-focus #asset-palette.unified-asset-palette {
      display: block;
    }
    #asset-palette.unified-asset-palette .palette-title-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      margin-bottom: 8px;
    }
    #asset-palette.unified-asset-palette .palette-title {
      color: #dbe7f6;
      font-size: 11px;
      font-weight: 850;
      letter-spacing: .08em;
      text-transform: uppercase;
    }
    #asset-palette.unified-asset-palette .palette-count {
      color: var(--muted);
      font-size: 9px;
    }
    .room-asset-type-switch {
      display: flex;
      flex-wrap: wrap;
      gap: 5px;
      margin-bottom: 9px;
    }
    .room-asset-type-switch button {
      padding: 5px 8px;
      font-size: 10px;
    }
    .room-asset-type-switch button.active {
      outline: 2px solid var(--accent);
      border-color: transparent;
    }
    #asset-palette.unified-asset-palette .palette-grid {
      display: grid;
      gap: 7px;
      max-height: min(460px, calc(100vh - 205px));
      padding-right: 5px;
      overflow-x: hidden;
      overflow-y: auto;
      scrollbar-gutter: stable;
      scrollbar-width: thin;
      scrollbar-color: #607089 #151b24;
    }
    #asset-palette.unified-asset-palette .palette-grid::-webkit-scrollbar {
      width: 10px;
    }
    #asset-palette.unified-asset-palette .palette-grid::-webkit-scrollbar-track {
      background: #151b24;
      border-radius: 8px;
    }
    #asset-palette.unified-asset-palette .palette-grid::-webkit-scrollbar-thumb {
      background: #607089;
      border: 2px solid #151b24;
      border-radius: 8px;
    }
    #asset-palette.unified-asset-palette .palette-asset {
      display: grid;
      grid-template-columns: 30px minmax(0, 1fr);
      align-items: center;
      gap: 8px;
      width: 100%;
      min-height: 48px;
      padding: 7px 9px;
      text-align: left;
    }
    #asset-palette.unified-asset-palette .palette-asset > span {
      display: grid;
      place-items: center;
      width: 30px;
      height: 30px;
      border-radius: 6px;
      background: #293342;
      font-size: 16px;
    }
    #asset-palette.unified-asset-palette .palette-asset > b {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-size: 11px;
    }
    #asset-palette.unified-asset-palette .palette-asset.selected {
      border-color: var(--accent);
      background: #263d52;
      box-shadow: 0 0 0 1px var(--accent) inset;
    }
    #asset-palette.unified-asset-palette .palette-asset[disabled] {
      opacity: .42;
      cursor: not-allowed;
    }
    #asset-palette.unified-asset-palette .palette-empty {
      margin: 0;
    }

    @media (max-width: 1050px) {
      #persistent-editor-tools {
        top: 58px;
      }
      #view-hud,
      body.room-focus #view-hud {
        top: 106px !important;
      }
      #viewMenu {
        top: 106px !important;
      }
    }

    @media (max-width: 760px) {
      body.room-focus #room-focus-tools {
        left: 8px !important;
        right: auto !important;
      }
      #persistent-editor-tools {
        right: 8px;
        max-width: calc(100% - 16px);
        overflow-x: auto;
      }
      #asset-palette.unified-asset-palette {
        left: 8px !important;
        width: min(310px, calc(100% - 16px)) !important;
      }
    }
  `;
  document.head.appendChild(stylesheet);

  function placedInstanceIds() {
    const ids = new Set();
    for (const room of state.level.rooms || []) {
      for (const entity of room.entities || []) ids.add(entity.id);
      for (const door of room.doors || []) ids.add(door.id);
    }
    return ids;
  }

  function placeableAssets() {
    const ids = placedInstanceIds();
    return (state.assets || []).filter(asset =>
      LevelMakerUiPolicy.isPlaceableAsset(asset, ids)
    );
  }

  function withPlaceableAssets(action) {
    const original = state.assets;
    state.assets = placeableAssets();
    try {
      return action();
    } finally {
      state.assets = original;
    }
  }

  function ensureSelectedAssetIsPlaceable() {
    const assets = placeableAssets();
    const selectedAsset = (state.assets || []).find(asset =>
      asset.id === state.editor.selectedAssetId
    );
    if (!selectedAsset || assets.some(asset => asset.id === selectedAsset.id)) return;

    const replacement = assets.find(asset => asset.type === selectedAsset.type)
      || assets.find(asset => asset.id === "prop.wall-1x1")
      || assets[0]
      || null;
    state.editor.selectedAssetId = replacement?.id || "";
  }

  function assetTool(asset) {
    if (asset.type === "enemy") return "enemy";
    if (asset.type === "floor") return "tile";
    if (asset.type === "door") return "door";
    if (asset.type === "prop" && String(asset.id).startsWith("prop.wall-")) return "wall";
    return "prop";
  }

  function activeAssetFilter() {
    const filter = String(state.editor.assetTypeFilter || "all");
    return ASSET_TYPES.some(([value]) => value === filter) ? filter : "all";
  }

  function filteredAssets() {
    const filter = activeAssetFilter();
    return placeableAssets().filter(asset => filter === "all" || asset.type === filter);
  }

  function specialTools(filter) {
    const entries = [
      { tool: "player", type: "all", label: "Player spawn", icon: "●", disabled: currentRoom()?.id !== state.level.startRoomId },
      { tool: "tile-erase", type: "floor", label: "Erase floor", icon: "⌫", disabled: false },
      { tool: "teleporter", type: "decor", label: "Teleporter", icon: "◎", disabled: false },
    ];
    return entries.filter(entry => filter === "all" || entry.type === filter);
  }

  function removeLegacyAssetUi() {
    document.querySelector("#asset-group-switch")?.remove();
  }

  function renderUnifiedPalette() {
    removeLegacyAssetUi();
    const palette = document.querySelector("#asset-palette");
    if (!palette) return;

    palette.classList.remove("authoring-group-palette");
    palette.classList.add("unified-asset-palette");
    const visible = state.editor.viewMode === "room" && state.editor.focusRoom !== false;
    palette.style.display = visible ? "block" : "none";
    if (!visible) return;

    const filter = activeAssetFilter();
    state.editor.assetTypeFilter = filter;
    const assets = filteredAssets();
    const specials = specialTools(filter);
    const lockedEnemyRoom = currentRoom()?.id === state.level.startRoomId;

    palette.innerHTML = `
      <div class="palette-title-row">
        <div class="palette-title">Assets</div>
        <span class="palette-count">${assets.length} available</span>
      </div>
      <div class="room-asset-type-switch" aria-label="Asset type">
        ${ASSET_TYPES.map(([value, label]) => `
          <button type="button" data-room-asset-type="${value}" class="${filter === value ? "active" : ""}">${label}</button>
        `).join("")}
      </div>
      <div class="palette-grid">
        ${specials.map(entry => `
          <button class="palette-asset" type="button" data-special-tool="${entry.tool}" ${entry.disabled ? "disabled" : ""}>
            <span>${entry.icon}</span><b>${entry.label}</b>
          </button>
        `).join("")}
        ${assets.map(asset => {
          const disabled = asset.type === "enemy" && lockedEnemyRoom;
          return `<button class="palette-asset ${asset.id === state.editor.selectedAssetId ? "selected" : ""}"
            type="button" data-palette-asset="${esc(asset.id)}" ${disabled ? "disabled" : ""} title="${esc(asset.id)}">
            <span>${iconFor(asset.type)}</span><b>${esc(asset.label || asset.id)}</b>
          </button>`;
        }).join("")}
        ${!assets.length && !specials.length
          ? `<div class="notice palette-empty">No ${ASSET_TYPES.find(([value]) => value === filter)?.[1].toLowerCase() || "assets"} are available.</div>`
          : ""}
      </div>`;

    palette.querySelectorAll("[data-room-asset-type]").forEach(button => {
      button.addEventListener("click", () => {
        state.editor.assetTypeFilter = button.dataset.roomAssetType;
        renderUnifiedPalette();
        scheduleRecoverySave();
      });
    });

    palette.querySelectorAll("[data-special-tool]").forEach(button => {
      button.addEventListener("click", () => {
        setTool(button.dataset.specialTool);
        renderAll();
      });
    });

    palette.querySelectorAll("[data-palette-asset]").forEach(button => {
      button.addEventListener("click", () => {
        const asset = state.assets.find(value => value.id === button.dataset.paletteAsset);
        if (!asset) return;
        state.editor.selectedAssetId = asset.id;
        setTool(assetTool(asset));
        renderAll();
        scheduleRecoverySave();
      });
    });
  }

  function installPersistentControls() {
    let controls = document.querySelector("#persistent-editor-tools");
    if (!controls) {
      controls = document.createElement("div");
      controls.id = "persistent-editor-tools";
      controls.className = "floating";
      controls.innerHTML = `<span class="persistent-label">Place</span>`;
      document.querySelector("#stage-wrap")?.appendChild(controls);
    }

    const single = document.querySelector('[data-placement-mode="single"]');
    const paint = document.querySelector('[data-placement-mode="paint"]');
    const viewButton = document.querySelector("#toggleViewMenu");
    const snap = document.querySelector("#snapSelect");

    [single, paint].forEach(button => {
      if (button && button.parentElement !== controls) controls.appendChild(button);
    });

    if (!controls.querySelector(".persistent-divider")) {
      const divider = document.createElement("span");
      divider.className = "persistent-divider";
      controls.appendChild(divider);
    }

    if (viewButton && viewButton.parentElement !== controls) controls.appendChild(viewButton);

    let gridLabel = controls.querySelector('[data-persistent-label="grid"]');
    if (!gridLabel) {
      gridLabel = document.createElement("span");
      gridLabel.className = "persistent-label";
      gridLabel.dataset.persistentLabel = "grid";
      gridLabel.textContent = "Grid";
      controls.appendChild(gridLabel);
    }
    if (snap && snap.parentElement !== controls) controls.appendChild(snap);
  }

  function tidyRoomToolbar() {
    const assets = document.querySelector("#toggleAssetsDrawer");
    const inspector = document.querySelector("#toggleInspectorDrawer");
    if (assets) {
      assets.textContent = "Asset search";
      assets.title = "Open the searchable project asset catalogue";
    }
    if (inspector) {
      inspector.textContent = "Inspector";
      inspector.title = "Open room and selection settings";
    }
  }

  function useModelessMap() {
    if (state.editor.viewMode !== "map") return;
    if (state.editor.mapMode === "open") state.editor.mapMode = "arrange";
    const doorMode = state.editor.mapMode === "connect";
    canvas.style.cursor = doorMode ? "crosshair" : "move";
    const hud = document.querySelector("#room-hud");
    if (hud) {
      hud.innerHTML = doorMode
        ? "<b>DOORS</b> · click a room edge or door · A returns to arranging"
        : "<b>LEVEL MAP</b> · drag rooms · double-click to open · D places/connects doors";
    }
  }

  function syncUxChrome() {
    removeLegacyAssetUi();
    installPersistentControls();
    tidyRoomToolbar();
    renderUnifiedPalette();
    useModelessMap();
  }

  normalize = function normalizeLevelEditorUx() {
    previousNormalize();
    state.editor.assetTypeFilter ||= "all";
    if (state.editor.mapMode === "open") state.editor.mapMode = "arrange";
    ensureSelectedAssetIsPlaceable();
  };

  renderHeaderFields = function renderLevelEditorUxHeader() {
    ensureSelectedAssetIsPlaceable();
    withPlaceableAssets(previousRenderHeaderFields);
    syncUxChrome();
  };

  renderAssets = function renderOnlyPlaceableAssets() {
    ensureSelectedAssetIsPlaceable();
    withPlaceableAssets(previousRenderAssets);
    syncUxChrome();
  };

  setViewMode = function setModelessLevelView(mode, options) {
    previousSetViewMode(mode, options);
    if (mode === "map" && state.editor.mapMode === "open") setMapMode("arrange");
    syncUxChrome();
  };

  setTool = function setToolWithoutLegacyPalette(tool) {
    previousSetTool(tool);
    syncUxChrome();
  };

  document.addEventListener("keydown", event => {
    if (event.key !== "Escape" || state.editor.viewMode !== "map" || state.editor.mapMode !== "connect") return;
    state.editor.connectSourceDoorId = null;
    setMapMode("arrange");
    renderAll();
    setStatus("Arrange map: drag rooms, or press D to work with doors.", "good");
  }, true);

  normalize();
  syncUxChrome();
  renderAll();
}
