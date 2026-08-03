"use strict";

{
  const previousNormalize = normalize;
  const previousRenderHeaderFields = renderHeaderFields;
  const previousRenderAssets = renderAssets;
  const previousSetViewMode = setViewMode;

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
    #map-tools {
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

    body.room-focus #asset-group-switch {
      top: 58px !important;
      left: 14px !important;
    }
    body.room-focus #asset-palette.authoring-group-palette {
      top: 102px !important;
      left: 14px !important;
      max-height: min(520px, calc(100% - 132px));
    }

    .room-asset-type-switch {
      display: flex;
      flex-wrap: wrap;
      gap: 5px;
      margin: 8px 0;
      padding: 7px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: rgba(12, 17, 24, .48);
    }
    .room-asset-type-switch button {
      padding: 5px 8px;
      font-size: 10px;
    }
    .room-asset-type-switch button.active {
      outline: 2px solid var(--accent);
      border-color: transparent;
    }
    .room-asset-empty {
      margin: 8px 0 0;
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

  function preferredGroupForType(type) {
    if (type === "all") return state.editor.assetGroup;
    const matching = placeableAssets().filter(asset => asset.type === type);
    if (!matching.length) return state.editor.assetGroup;
    const counts = matching.reduce((result, asset) => {
      const group = AuthoringUx.assetGroup(asset);
      result[group] = (result[group] || 0) + 1;
      return result;
    }, {});
    return (counts.interactive || 0) > (counts.static || 0)
      ? "interactive"
      : "static";
  }

  function renderAssetTypeSwitch() {
    const palette = document.querySelector("#asset-palette.authoring-group-palette");
    if (!palette || state.editor.viewMode !== "room") return;

    let filter = String(state.editor.assetTypeFilter || "all");
    if (!ASSET_TYPES.some(([value]) => value === filter)) filter = "all";
    state.editor.assetTypeFilter = filter;

    palette.querySelector(".room-asset-type-switch")?.remove();
    const title = palette.querySelector(".palette-title");
    const switcher = document.createElement("div");
    switcher.className = "room-asset-type-switch";
    switcher.setAttribute("aria-label", "Asset type");
    switcher.innerHTML = ASSET_TYPES.map(([value, label]) => `
      <button type="button" data-room-asset-type="${value}" class="${filter === value ? "active" : ""}">${label}</button>`
    ).join("");
    (title || palette.firstElementChild)?.after(switcher);

    switcher.querySelectorAll("[data-room-asset-type]").forEach(button => {
      button.addEventListener("click", () => {
        const type = button.dataset.roomAssetType;
        state.editor.assetTypeFilter = type;
        state.editor.assetGroup = preferredGroupForType(type);
        renderHeaderFields();
        renderAssets();
        renderCanvas();
        renderFooter();
        scheduleRecoverySave();
      });
    });

    let visibleCount = 0;
    palette.querySelectorAll("[data-group-asset]").forEach(button => {
      button.dataset.paletteAsset = button.dataset.groupAsset;
      const asset = state.assets.find(value => value.id === button.dataset.groupAsset);
      const visible = filter === "all" || asset?.type === filter;
      button.hidden = !visible;
      if (visible) visibleCount += 1;
    });
    palette.querySelectorAll("[data-special-tool]").forEach(button => {
      button.hidden = filter !== "all";
    });

    palette.querySelector(".room-asset-empty")?.remove();
    if (filter !== "all" && visibleCount === 0) {
      const empty = document.createElement("div");
      empty.className = "notice room-asset-empty";
      empty.textContent = `No ${ASSET_TYPES.find(([value]) => value === filter)?.[1].toLowerCase() || "assets"} are available in this group.`;
      palette.querySelector(".palette-grid")?.after(empty);
    }
  }

  function installGroupReset() {
    const switcher = document.querySelector("#asset-group-switch");
    if (!switcher || switcher.dataset.typeResetInstalled) return;
    switcher.dataset.typeResetInstalled = "true";
    switcher.addEventListener("click", event => {
      if (!event.target.closest("[data-asset-group]")) return;
      state.editor.assetTypeFilter = "all";
      queueMicrotask(renderAssetTypeSwitch);
    }, true);
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

    if (viewButton && viewButton.parentElement !== controls) {
      controls.appendChild(viewButton);
    }

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
    installPersistentControls();
    tidyRoomToolbar();
    installGroupReset();
    renderAssetTypeSwitch();
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
