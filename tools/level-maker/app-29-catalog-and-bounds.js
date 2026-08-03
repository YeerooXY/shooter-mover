"use strict";

{
  const previousNormalize = normalize;
  const previousRenderHeaderFields = renderHeaderFields;
  const previousRenderAssets = renderAssets;

  const coreAssets = defaultCatalog.map(asset => clone(asset));
  const coreAssetIds = new Set(coreAssets.map(asset => asset.id));

  const stylesheet = document.createElement("style");
  stylesheet.textContent = `
    body.room-focus #room-focus-tools {
      position: absolute !important;
      top: 14px !important;
      left: 14px !important;
      right: auto !important;
      bottom: auto !important;
      width: max-content !important;
      height: auto !important;
      min-width: 0 !important;
      min-height: 0 !important;
      max-width: calc(100% - 28px) !important;
      max-height: max-content !important;
      display: inline-flex !important;
      align-items: center !important;
      align-content: flex-start !important;
      flex-wrap: nowrap !important;
      overflow: visible !important;
    }

    #asset-palette.unified-asset-palette {
      right: auto !important;
      bottom: auto !important;
      width: min(320px, calc(100% - 28px)) !important;
      height: auto !important;
      min-height: 0 !important;
      max-height: calc(100% - 72px) !important;
      overflow: hidden !important;
      contain: layout paint;
    }

    #asset-palette.unified-asset-palette .palette-grid {
      max-height: min(460px, calc(100vh - 210px)) !important;
      overscroll-behavior: contain;
    }

    @media (max-width: 760px) {
      body.room-focus #room-focus-tools {
        top: 8px !important;
        left: 8px !important;
        max-width: calc(100% - 16px) !important;
        overflow-x: auto !important;
        overflow-y: hidden !important;
      }

      #asset-palette.unified-asset-palette {
        left: 8px !important;
        width: min(320px, calc(100% - 16px)) !important;
        max-height: calc(100% - 66px) !important;
      }
    }
  `;
  document.head.appendChild(stylesheet);

  function mergeAsset(previous, next, preferNext = false) {
    if (!previous) return clone(next);
    if (!next) return clone(previous);
    return preferNext
      ? { ...clone(previous), ...clone(next) }
      : { ...clone(next), ...clone(previous) };
  }

  function restoreAuthoringCatalog() {
    const byId = new Map();

    for (const asset of state.assets || []) {
      if (!asset?.id) continue;
      byId.set(asset.id, mergeAsset(byId.get(asset.id), asset, true));
    }

    // These are editor primitives, not discoveries from a level instance. Their
    // canonical type/source must win even when the helper found the same runtime
    // object inside generated room documents.
    for (const asset of coreAssets) {
      byId.set(asset.id, mergeAsset(byId.get(asset.id), asset, true));
    }

    for (const asset of state.editor?.customAssets || []) {
      if (!asset?.id || coreAssetIds.has(asset.id)) continue;
      byId.set(asset.id, mergeAsset(byId.get(asset.id), asset, true));
    }

    state.assets = [...byId.values()].sort((left, right) =>
      String(left.type || "").localeCompare(String(right.type || ""))
      || String(left.label || left.id).localeCompare(String(right.label || right.id))
    );
  }

  function enforceFocusedRoomBounds() {
    const controls = document.querySelector("#room-focus-tools");
    if (controls) {
      controls.style.top = "14px";
      controls.style.left = "14px";
      controls.style.right = "auto";
      controls.style.bottom = "auto";
      controls.style.width = "max-content";
      controls.style.height = "auto";
      controls.style.minHeight = "0";
      controls.style.maxHeight = "max-content";
    }

    const palette = document.querySelector("#asset-palette.unified-asset-palette");
    if (palette) {
      palette.style.right = "auto";
      palette.style.bottom = "auto";
      palette.style.height = "auto";
      palette.style.minHeight = "0";
    }
  }

  normalize = function normalizeCatalogAndBounds() {
    previousNormalize();
    restoreAuthoringCatalog();
  };

  renderHeaderFields = function renderCatalogAndBoundsHeader() {
    restoreAuthoringCatalog();
    previousRenderHeaderFields();
    enforceFocusedRoomBounds();
  };

  renderAssets = function renderCatalogAndBoundsAssets() {
    restoreAuthoringCatalog();
    previousRenderAssets();
    enforceFocusedRoomBounds();
  };

  normalize();
  enforceFocusedRoomBounds();
  renderAll();
}
