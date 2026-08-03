"use strict";

(function exposeLevelMakerUiPolicy(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  else root.LevelMakerUiPolicy = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createLevelMakerUiPolicy() {
  function normalizeSource(value) {
    return String(value || "").replace(/\\/g, "/").toLowerCase();
  }

  function isGeneratedPlacementSource(value) {
    const source = normalizeSource(value);
    if (!source) return false;
    if (source === "level-reference") return true;
    if (source.includes("content/definitions/missions/rooms/levels/")) return true;
    if (source.includes("content/levels/") && source.endsWith(".level.json")) return true;
    return false;
  }

  function isOpaqueInstanceId(value) {
    const id = String(value || "").trim();
    return /^(?:[a-z]+\.)?[0-9a-f]{8}$/i.test(id)
      || /^(?:enemy|prop|door|room|entity)\.[0-9a-f]{8}(?:-[0-9a-f]{4})*$/i.test(id);
  }

  function containsId(ids, id) {
    if (!ids) return false;
    if (typeof ids.has === "function") return ids.has(id);
    return Array.isArray(ids) && ids.includes(id);
  }

  function isPlaceableAsset(asset, placedInstanceIds) {
    const id = String(asset?.id || "").trim();
    if (!id) return false;
    if (containsId(placedInstanceIds, id)) return false;
    if (isOpaqueInstanceId(id)) return false;
    return !isGeneratedPlacementSource(asset?.source);
  }

  return Object.freeze({
    isGeneratedPlacementSource,
    isOpaqueInstanceId,
    isPlaceableAsset,
  });
});
