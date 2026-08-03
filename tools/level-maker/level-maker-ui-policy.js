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

  function isReusableRuntimeAssetId(value) {
    const id = String(value || "").trim();
    if (!/^(?:enemy|prop|tile|door|decor|presentation)\.[a-z0-9][a-z0-9._-]*$/i.test(id)) {
      return false;
    }
    const suffix = id.slice(id.indexOf(".") + 1);
    return /[a-z]/i.test(suffix) && !isOpaqueInstanceId(id);
  }

  function containsId(ids, id) {
    if (!ids) return false;
    if (typeof ids.has === "function") return ids.has(id);
    return Array.isArray(ids) && ids.includes(id);
  }

  function isPlaceableAsset(asset, placedInstanceIds) {
    const id = String(asset?.id || "").trim();
    if (!id || isOpaqueInstanceId(id)) return false;

    const placed = containsId(placedInstanceIds, id);
    const source = normalizeSource(asset?.source);

    // A manually entered ID that is already used as a placement is an instance,
    // not another catalogue card. Keep filtering it even if its spelling resembles
    // a runtime ID.
    if (placed && source === "manual") return false;

    // Runtime object IDs describe reusable catalogue entries, not individual room
    // placements. Imported rooms sometimes reuse an object ID as the placement ID,
    // so canonical runtime objects must survive that collision.
    if (isReusableRuntimeAssetId(id)) return true;

    if (placed) return false;
    return !isGeneratedPlacementSource(source);
  }

  return Object.freeze({
    isGeneratedPlacementSource,
    isOpaqueInstanceId,
    isReusableRuntimeAssetId,
    isPlaceableAsset,
  });
});
