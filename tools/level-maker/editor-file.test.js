"use strict";

const assert = require("assert");
const fs = require("fs");
const http = require("http");
const os = require("os");
const path = require("path");
const { createEditorFileService } = require("./editor-file");

async function main() {
  const repo = fs.mkdtempSync(path.join(os.tmpdir(), "level-maker-editor-"));
  makeUnityLevel(repo);

  const service = createEditorFileService(repo, 0);
  const server = http.createServer(async (request, response) => {
    try {
      if (await service.handle(request, response)) return;
      response.writeHead(404);
      response.end();
    } catch (error) {
      const body = JSON.stringify({ error: error.message });
      response.writeHead(400, {
        "Content-Type": "application/json",
        "Content-Length": Buffer.byteLength(body),
      });
      response.end(body);
    }
  });

  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const port = server.address().port;

  const editor = {
    version: 1,
    levelId: "level.test",
    activeRoomId: "room.test",
    editor: { tool: "tile", zoom: 48, pan: [2, 3] },
    customAssets: [],
  };
  const save = await request(port, "/api/level-editor", "POST", {
    target: "test",
    savedAt: "2026-08-03T00:00:00.000Z",
    editor,
  }, {
    Origin: "http://127.0.0.1:0",
  });
  assert.strictEqual(save.status, 200);
  assert.strictEqual(
    save.value.editorPath,
    "Library/ShooterMover/LevelMaker/test.editor.json"
  );
  assert.ok(fs.existsSync(path.join(
    repo,
    "Library",
    "ShooterMover",
    "LevelMaker",
    "test.editor.json"
  )));

  const loaded = await request(port, "/api/level-editor?target=test");
  assert.strictEqual(loaded.status, 200);
  assert.deepStrictEqual(loaded.value.editor, editor);

  const floors = await request(port, "/api/level-floors?target=test");
  assert.strictEqual(floors.status, 200);
  assert.deepStrictEqual(floors.value.rooms, [
    {
      roomId: "room.test",
      tiles: [
        {
          object: "tile.floor-industrial",
          fill: { from: [-2, -2], to: [0, 2] },
        },
        {
          object: "tile.floor-metal",
          fill: { from: [0, -2], to: [2, 2] },
        },
      ],
    },
  ]);

  const blocked = await request(port, "/api/level-editor", "POST", {
    target: "test",
    editor,
  }, {
    Origin: "https://example.com",
  });
  assert.strictEqual(blocked.status, 400);

  await new Promise(resolve => server.close(resolve));
  fs.rmSync(repo, { recursive: true, force: true });
  console.log("Level Maker editor file tests passed.");
}

function makeUnityLevel(repo) {
  const levelFolder = path.join(
    repo,
    "Assets",
    "ShooterMover",
    "Content",
    "Definitions",
    "Missions",
    "Rooms",
    "Levels",
    "Test"
  );
  const roomFolder = path.join(levelFolder, "Rooms", "Room_0_0_01");
  fs.mkdirSync(roomFolder, { recursive: true });
  fs.writeFileSync(path.join(levelFolder, "level.json"), JSON.stringify({
    level_id: "level.test",
    rooms: [{ folder: "Room_0_0_01" }],
  }));
  fs.writeFileSync(path.join(roomFolder, "room.json"), JSON.stringify({
    room_id: "room.test",
  }));
  fs.writeFileSync(path.join(roomFolder, "floor.json"), JSON.stringify({
    tiles: [
      {
        object: "tile.floor-industrial",
        fill: { from: [-2, -2], to: [0, 2] },
      },
      {
        object: "tile.floor-metal",
        fill: { from: [0, -2], to: [2, 2] },
      },
    ],
  }));
}

function request(port, requestPath, method = "GET", value = null, extraHeaders = {}) {
  return new Promise((resolve, reject) => {
    const body = value === null ? "" : JSON.stringify(value);
    const headers = { ...extraHeaders };
    if (body) {
      headers["Content-Type"] = "application/json";
      headers["Content-Length"] = Buffer.byteLength(body);
    }

    const req = http.request({
      hostname: "127.0.0.1",
      port,
      path: requestPath,
      method,
      headers,
    }, response => {
      let text = "";
      response.setEncoding("utf8");
      response.on("data", chunk => { text += chunk; });
      response.on("end", () => {
        resolve({
          status: response.statusCode,
          value: text ? JSON.parse(text) : null,
        });
      });
    });
    req.on("error", reject);
    if (body) req.write(body);
    req.end();
  });
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
