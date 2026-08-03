"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const AuthoringUx = require("./authoring-ux.js");

{
  const level = {
    startRoomId: "room.start",
    rooms: [
      {
        id: "room.start",
        bounds: { width: 24, height: 14 },
        playerStart: { position: [3, -2], rotation: 45 },
      },
      {
        id: "room.other",
        bounds: { width: 24, height: 14 },
        playerStart: { position: [0, 0], rotation: 0 },
      },
    ],
  };
  const changes = AuthoringUx.normalizeSingleSpawn(level);
  assert.ok(changes.some(change => change.includes("duplicate")));
  assert.deepEqual(level.rooms[0].playerStart, { position: [3, -2], rotation: 45 });
  assert.equal(level.rooms[1].playerStart, null);
  assert.equal(level.rooms.filter(room => room.playerStart).length, 1);
}

{
  const level = {
    startRoomId: "room.start",
    rooms: [{ id: "room.start", bounds: { width: 10, height: 8 }, playerStart: null }],
  };
  AuthoringUx.normalizeSingleSpawn(level);
  assert.deepEqual(level.rooms[0].playerStart, { position: [0, 0], rotation: 0 });
}

{
  const room = { bounds: { width: 24, height: 14 } };
  assert.deepEqual(
    AuthoringUx.centerDoorPlacement(room, "North"),
    { side: "North", position: [0, 7], rotation: 0 }
  );
  assert.deepEqual(
    AuthoringUx.centerDoorPlacement(room, "West"),
    { side: "West", position: [-12, 0], rotation: 90 }
  );
}

{
  assert.equal(AuthoringUx.assetGroup({ id: "enemy.gunner", type: "enemy" }), "interactive");
  assert.equal(AuthoringUx.assetGroup({ id: "prop.red-key", type: "prop", label: "Red Key" }), "interactive");
  assert.equal(AuthoringUx.assetGroup({ id: "prop.crate", type: "prop", label: "Breakable Crate" }), "static");
  assert.equal(AuthoringUx.assetGroup({ id: "door.standard", type: "door" }), "static");
}

{
  const source = fs.readFileSync(path.join(__dirname, "app-26-authoring-ux.js"), "utf8");
  assert.match(source, /Interactive items/);
  assert.match(source, /Static items/);
  assert.match(source, /key === "a"/);
  assert.match(source, /event\.ctrlKey/);
  assert.match(source, /exactly one player spawn/);
  assert.match(source, /Hold Ctrl to snap it to the center of a side/);
}

console.log("authoring UX tests passed");
