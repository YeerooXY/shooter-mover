"use strict";

(function exposeLevelAuthoring(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.LevelAuthoring = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createLevelAuthoring() {
  function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, value));
  }

  function normalizedGrid(value) {
    return [
      Math.round(Number(value?.[0]) || 0),
      Math.round(Number(value?.[1]) || 0),
    ];
  }

  function gridKey(value) {
    const grid = normalizedGrid(value);
    return `${grid[0]},${grid[1]}`;
  }

  function nearestFreeGrid(rooms, desiredGrid) {
    const desired = normalizedGrid(desiredGrid);
    const occupied = new Set((rooms || []).map(room => gridKey(room?.grid)));
    if (!occupied.has(gridKey(desired))) return desired;

    const maximumRadius = Math.max(8, (rooms || []).length + 2);
    for (let radius = 1; radius <= maximumRadius; radius++) {
      const candidates = [];
      for (let y = -radius; y <= radius; y++) {
        for (let x = -radius; x <= radius; x++) {
          if (Math.max(Math.abs(x), Math.abs(y)) !== radius) continue;
          candidates.push([desired[0] + x, desired[1] + y]);
        }
      }
      candidates.sort((left, right) => {
        const leftX = left[0] - desired[0];
        const leftY = left[1] - desired[1];
        const rightX = right[0] - desired[0];
        const rightY = right[1] - desired[1];
        return (
          leftX * leftX + leftY * leftY - (rightX * rightX + rightY * rightY)
          || Math.abs(leftY) - Math.abs(rightY)
          || (leftX < 0 ? 1 : 0) - (rightX < 0 ? 1 : 0)
          || (leftY < 0 ? 1 : 0) - (rightY < 0 ? 1 : 0)
          || leftX - rightX
          || leftY - rightY
        );
      });
      const available = candidates.find(candidate => !occupied.has(gridKey(candidate)));
      if (available) return available;
    }

    return [desired[0] + maximumRadius + 1, desired[1]];
  }

  function isStartRoom(level, room) {
    return Boolean(room?.id && level?.startRoomId === room.id);
  }

  function preferredStartRoom(level) {
    const rooms = Array.isArray(level?.rooms) ? level.rooms : [];
    const existing = rooms.find(room => room.id === level.startRoomId);
    if (existing) return existing;

    const spawnRooms = rooms.filter(room => room?.playerStart);
    if (spawnRooms.length === 1) return spawnRooms[0];

    return rooms.find(room => /^start room$/i.test(String(room?.displayName || "").trim()))
      || spawnRooms[0]
      || rooms[0]
      || null;
  }

  function renameDoorReferences(level, oldId, newId) {
    for (const connection of level.connections || []) {
      if (connection.fromDoorId === oldId) connection.fromDoorId = newId;
      if (connection.toDoorId === oldId) connection.toDoorId = newId;
    }
    for (const rule of level.logic || []) {
      if (rule.targetId === oldId) rule.targetId = newId;
    }
  }

  function repairLevelReferences(level, editor = null) {
    const changes = [];
    const rooms = Array.isArray(level?.rooms) ? level.rooms : [];
    if (!level || rooms.length === 0) return changes;

    let startRoom = rooms.find(room => room.id === level.startRoomId) || null;
    if (!startRoom) {
      startRoom = preferredStartRoom(level);
      if (startRoom) {
        const requestedId = String(level.startRoomId || "");
        const canRestoreFreshProjectId = rooms.length === 1
          && startRoom.id === "room.level-1-start"
          && /^room\.[a-z0-9][a-z0-9.-]*-start$/i.test(requestedId)
          && !rooms.some(room => room !== startRoom && room.id === requestedId);

        if (canRestoreFreshProjectId) {
          const oldId = startRoom.id;
          startRoom.id = requestedId;
          if (editor?.activeRoomId === oldId) editor.activeRoomId = requestedId;
          for (const rule of level.logic || []) {
            if (rule.targetId === oldId) rule.targetId = requestedId;
          }
          changes.push(`Restored starter room ID ${requestedId}.`);
        } else {
          level.startRoomId = startRoom.id;
          changes.push(`Recovered starter room ${startRoom.id}.`);
        }
      }
    }

    if (!rooms.some(room => room.id === level.startRoomId) && startRoom) {
      level.startRoomId = startRoom.id;
    }

    if (!rooms.some(room => room.id === level.finalRoomId)) {
      level.finalRoomId = level.startRoomId || rooms.at(-1).id;
      changes.push(`Recovered final room ${level.finalRoomId}.`);
    }

    const finalRoom = rooms.find(room => room.id === level.finalRoomId) || null;
    const allDoors = rooms.flatMap(room => Array.isArray(room.doors) ? room.doors : []);
    if (!allDoors.some(door => door.id === level.finalExitDoorId)) {
      const requestedDoorId = String(level.finalExitDoorId || "");
      const candidate = (finalRoom?.doors || []).find(door => /final[-.]?exit/i.test(String(door.id || "")))
        || finalRoom?.doors?.[0]
        || null;

      if (candidate) {
        const canRestoreFreshProjectDoorId = rooms.length === 1
          && candidate.id === "door.level-1-final-exit"
          && /^door\.[a-z0-9][a-z0-9.-]*-final-exit$/i.test(requestedDoorId)
          && !allDoors.some(door => door !== candidate && door.id === requestedDoorId);

        if (canRestoreFreshProjectDoorId) {
          const oldId = candidate.id;
          candidate.id = requestedDoorId;
          renameDoorReferences(level, oldId, requestedDoorId);
          level.finalExitDoorId = requestedDoorId;
          changes.push(`Restored final exit door ID ${requestedDoorId}.`);
        } else {
          level.finalExitDoorId = candidate.id;
          changes.push(`Recovered final exit door ${candidate.id}.`);
        }
      } else if (
        rooms.length === 1
        && finalRoom
        && /^door\.[a-z0-9][a-z0-9.-]*-final-exit$/i.test(requestedDoorId)
      ) {
        const width = Math.max(2, Number(finalRoom.bounds?.width) || 24);
        finalRoom.doors ||= [];
        finalRoom.doors.push({
          id: requestedDoorId,
          kind: "door",
          position: [width / 2, 0],
          rotation: 90,
          side: "East",
          placementMode: "Fixed",
          traversable: true,
          visibleOnMap: true,
          runtimeObject: "door.room-standard",
          openWhen: "room-complete",
        });
        level.finalExitDoorId = requestedDoorId;
        changes.push(`Recreated final exit door ${requestedDoorId}.`);
      }
    }

    return changes;
  }

  function mapDoorPlacement(room, roomCenter, mapPoint, roomHalf = [4.5, 3]) {
    const center = Array.isArray(roomCenter) ? roomCenter : [0, 0];
    const point = Array.isArray(mapPoint) ? mapPoint : center;
    const half = [
      Math.max(0.001, Number(roomHalf?.[0]) || 4.5),
      Math.max(0.001, Number(roomHalf?.[1]) || 3),
    ];
    const relative = [point[0] - center[0], point[1] - center[1]];
    const side = [
      ["East", Math.abs(half[0] - relative[0])],
      ["West", Math.abs(-half[0] - relative[0])],
      ["North", Math.abs(half[1] - relative[1])],
      ["South", Math.abs(-half[1] - relative[1])],
    ].sort((left, right) => left[1] - right[1])[0][0];

    const width = Math.max(2, Number(room?.bounds?.width) || 24);
    const height = Math.max(2, Number(room?.bounds?.height) || 14);
    if (side === "East" || side === "West") {
      const amount = clamp(relative[1] / (half[1] * 0.8), -0.85, 0.85);
      return {
        side,
        position: [side === "East" ? width / 2 : -width / 2, Number((amount * height / 2).toFixed(6))],
        rotation: 90,
      };
    }

    const amount = clamp(relative[0] / (half[0] * 0.8), -0.85, 0.85);
    return {
      side,
      position: [Number((amount * width / 2).toFixed(6)), side === "North" ? height / 2 : -height / 2],
      rotation: 0,
    };
  }

  return {
    nearestFreeGrid,
    isStartRoom,
    preferredStartRoom,
    repairLevelReferences,
    mapDoorPlacement,
  };
});
