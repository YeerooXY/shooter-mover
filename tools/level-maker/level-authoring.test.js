"use strict";

const assert = require("node:assert/strict");
const Authoring = require("./level-authoring.js");

{
  const rooms = [{ grid: [0, 0] }, { grid: [1, 0] }];
  assert.deepEqual(Authoring.nearestFreeGrid(rooms, [0, 0]), [-1, 0]);
  assert.deepEqual(Authoring.nearestFreeGrid(rooms, [4, 3]), [4, 3]);
}

{
  const room = { bounds: { width: 24, height: 14 } };
  assert.deepEqual(
    Authoring.mapDoorPlacement(room, [0, 0], [4.4, 1.2]),
    { side: "East", position: [12, 3.5], rotation: 90 }
  );
  assert.deepEqual(
    Authoring.mapDoorPlacement(room, [0, 0], [-1.8, 2.9]),
    { side: "North", position: [-6, 7], rotation: 0 }
  );
}

{
  const level = {
    startRoomId: "room.test-level-start",
    finalRoomId: "room.test-level-start",
    finalExitDoorId: "door.test-level-final-exit",
    rooms: [{
      id: "room.level-1-start",
      displayName: "START ROOM",
      playerStart: { position: [0, 0], rotation: 0 },
      doors: [{ id: "door.level-1-final-exit" }],
    }],
    connections: [],
    logic: [],
  };
  const editor = { activeRoomId: "room.level-1-start" };
  const changes = Authoring.repairLevelReferences(level, editor);
  assert.ok(changes.length >= 2);
  assert.equal(level.rooms[0].id, "room.test-level-start");
  assert.equal(level.startRoomId, "room.test-level-start");
  assert.equal(level.finalRoomId, "room.test-level-start");
  assert.equal(level.rooms[0].doors[0].id, "door.test-level-final-exit");
  assert.equal(level.finalExitDoorId, "door.test-level-final-exit");
  assert.equal(editor.activeRoomId, "room.test-level-start");
}

{
  const level = {
    startRoomId: "room.missing",
    finalRoomId: "room.b",
    finalExitDoorId: "door.b-exit",
    rooms: [
      { id: "room.a", displayName: "START ROOM", playerStart: { position: [0, 0] }, doors: [] },
      { id: "room.b", doors: [{ id: "door.b-exit" }] },
    ],
    connections: [],
    logic: [],
  };
  Authoring.repairLevelReferences(level, {});
  assert.equal(level.startRoomId, "room.a");
  assert.equal(level.rooms[0].id, "room.a");
  assert.equal(Authoring.isStartRoom(level, level.rooms[0]), true);
  assert.equal(Authoring.isStartRoom(level, level.rooms[1]), false);
}

{
  const level = {
    startRoomId: "room.empty-start",
    finalRoomId: "room.empty-start",
    finalExitDoorId: "door.empty-final-exit",
    rooms: [{
      id: "room.level-1-start",
      displayName: "START ROOM",
      bounds: { width: 24, height: 14 },
      playerStart: { position: [0, 0] },
      doors: [],
    }],
    connections: [],
    logic: [],
  };
  Authoring.repairLevelReferences(level, {});
  assert.equal(level.rooms[0].id, "room.empty-start");
  assert.equal(level.rooms[0].doors.length, 1);
  assert.equal(level.rooms[0].doors[0].id, "door.empty-final-exit");
  assert.deepEqual(level.rooms[0].doors[0].position, [12, 0]);
}

console.log("level-authoring tests passed");
