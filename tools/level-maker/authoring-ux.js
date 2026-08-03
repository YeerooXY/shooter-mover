"use strict";

(function exposeAuthoringUx(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.AuthoringUx = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createAuthoringUx() {
  function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, value));
  }

  function centerDoorPlacement(room, side) {
    const width = Math.max(2, Number(room?.bounds?.width) || 24);
    const height = Math.max(2, Number(room?.bounds?.height) || 14);
    if (side === "West") return { side, position: [-width / 2, 0], rotation: 90 };
    if (side === "North") return { side, position: [0, height / 2], rotation: 0 };
    if (side === "South") return { side, position: [0, -height / 2], rotation: 0 };
    return { side: "East", position: [width / 2, 0], rotation: 90 };
  }

  function normalizedSpawn(room, spawn) {
    const width = Math.max(2, Number(room?.bounds?.width) || 24);
    const height = Math.max(2, Number(room?.bounds?.height) || 14);
    const x = clamp(Number(spawn?.position?.[0]) || 0, -width / 2 + 0.5, width / 2 - 0.5);
    const y = clamp(Number(spawn?.position?.[1]) || 0, -height / 2 + 0.5, height / 2 - 0.5);
    return {
      position: [x, y],
      rotation: Number(spawn?.rotation) || 0,
    };
  }

  function normalizeSingleSpawn(level) {
    const changes = [];
    const rooms = Array.isArray(level?.rooms) ? level.rooms : [];
    if (!rooms.length) return changes;

    const startRoom = rooms.find(room => room.id === level.startRoomId) || rooms[0];
    if (level.startRoomId !== startRoom.id) {
      level.startRoomId = startRoom.id;
      changes.push(`Recovered starter room ${startRoom.id}.`);
    }

    if (!startRoom.playerStart) {
      startRoom.playerStart = normalizedSpawn(startRoom, null);
      changes.push(`Created the single player spawn in ${startRoom.id}.`);
    } else {
      startRoom.playerStart = normalizedSpawn(startRoom, startRoom.playerStart);
    }

    for (const room of rooms) {
      if (room === startRoom || !room.playerStart) continue;
      room.playerStart = null;
      changes.push(`Removed duplicate player spawn from ${room.id}.`);
    }
    return changes;
  }

  function assetGroup(asset) {
    const type = String(asset?.type || "").toLowerCase();
    if (["enemy", "key", "pickup", "collectible", "interactive", "interactable"].includes(type)) {
      return "interactive";
    }

    const text = `${asset?.id || ""} ${asset?.label || ""} ${(asset?.tags || []).join?.(" ") || ""}`.toLowerCase();
    if (type === "prop" && /\b(key|pickup|collectible|cash|scrap|switch|trigger|terminal|button)\b/.test(text)) {
      return "interactive";
    }
    return "static";
  }

  return {
    centerDoorPlacement,
    normalizeSingleSpawn,
    assetGroup,
  };
});
