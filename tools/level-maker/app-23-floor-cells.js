"use strict";

{
  const oldNewRoom = newRoom;
  const oldNormalize = normalize;

  newRoom = function makeRoomWithFloor(index = 0) {
    return FloorData.prepareRoom(oldNewRoom(index));
  };

  normalize = function normalizeFloorCells() {
    oldNormalize();
    FloorData.prepareRooms(state.level.rooms);
  };

  setRoomTile = function setFloorCell(room, cell, object) {
    if (!cell) return false;
    return FloorData.setFloorTile(room, cell.x, cell.y, object);
  };

  fillRoomTiles = function fillRoomFloor(room, object) {
    FloorData.fillFloor(room, object);
  };

  selectedFloorObject = function selectedFloorTile() {
    const asset = state.assets.find(
      item => item.id === state.editor.selectedAssetId && item.type === "floor"
    );
    return asset?.id || FloorData.defaultFloorTile(currentRoom());
  };

  drawRoomTiles = function drawFloorCells(room) {
    FloorData.prepareRoom(room);
    const floor = room.floor;
    const topLeft = worldToScreen([-room.bounds.width / 2, room.bounds.height / 2]);
    const bottomRight = worldToScreen([room.bounds.width / 2, -room.bounds.height / 2]);
    const left = devicePixel(Math.min(topLeft[0], bottomRight[0]));
    const top = devicePixel(Math.min(topLeft[1], bottomRight[1]));
    const right = devicePixel(Math.max(topLeft[0], bottomRight[0]));
    const bottom = devicePixel(Math.max(topLeft[1], bottomRight[1]));
    const fullFloor = FloorData.isFullFloor(room);

    ctx.fillStyle = tileColor(FloorData.defaultFloorTile(room));
    ctx.globalAlpha = fullFloor ? 0.34 : 0.14;
    ctx.fillRect(left, top, right - left, bottom - top);
    ctx.globalAlpha = 1;

    if (!fullFloor) {
      for (let y = 0; y < floor.height; y++) {
        for (let x = 0; x < floor.width; x++) {
          const object = FloorData.getFloorTile(room, x, y);
          if (!object) continue;

          const rect = tileCellWorldRect(room, x, y);
          const a = worldToScreen([rect.x, rect.y + 1]);
          const b = worldToScreen([rect.x + 1, rect.y]);
          const screenX = devicePixel(Math.min(a[0], b[0]));
          const screenY = devicePixel(Math.min(a[1], b[1]));
          const width = devicePixel(Math.abs(b[0] - a[0]));
          const height = devicePixel(Math.abs(b[1] - a[1]));

          ctx.fillStyle = tileColor(object);
          ctx.fillRect(screenX + 2, screenY + 2, Math.max(0, width - 4), Math.max(0, height - 4));
          ctx.strokeStyle = "rgba(239,247,255,.48)";
          ctx.lineWidth = Math.max(1, 1 / dpr);
          ctx.strokeRect(screenX + 1.5, screenY + 1.5, Math.max(0, width - 3), Math.max(0, height - 3));

          if (viewScale() >= 34) {
            ctx.fillStyle = "rgba(255,255,255,.86)";
            ctx.font = "8px system-ui";
            ctx.textAlign = "center";
            ctx.fillText(
              String(object).split(".").pop().slice(0, 8),
              screenX + width / 2,
              screenY + height / 2 + 3
            );
          }
        }
      }
    }

    const minor = Math.max(1, 1 / dpr);
    const major = Math.max(2, 2 / dpr);
    for (let x = 0; x <= floor.width; x++) {
      const isMajor = x % 4 === 0 || x === floor.width;
      const a = worldToScreen([-room.bounds.width / 2 + x, -room.bounds.height / 2]);
      const b = worldToScreen([-room.bounds.width / 2 + x, room.bounds.height / 2]);
      const screenX = devicePixel(a[0]);
      const lineWidth = isMajor ? major : minor;
      ctx.fillStyle = isMajor ? "rgba(213,233,251,.96)" : "rgba(157,192,224,.76)";
      ctx.fillRect(
        devicePixel(screenX - lineWidth / 2),
        devicePixel(Math.min(a[1], b[1])),
        lineWidth,
        devicePixel(Math.abs(b[1] - a[1]))
      );
      if (isMajor && viewScale() >= 20 && x < floor.width) {
        ctx.fillStyle = "rgba(231,243,253,.94)";
        ctx.font = "9px system-ui";
        ctx.textAlign = "left";
        ctx.fillText(String(x), screenX + 4, top + 12);
      }
    }

    for (let y = 0; y <= floor.height; y++) {
      const isMajor = y % 4 === 0 || y === floor.height;
      const a = worldToScreen([-room.bounds.width / 2, -room.bounds.height / 2 + y]);
      const b = worldToScreen([room.bounds.width / 2, -room.bounds.height / 2 + y]);
      const screenY = devicePixel(a[1]);
      const lineWidth = isMajor ? major : minor;
      ctx.fillStyle = isMajor ? "rgba(213,233,251,.96)" : "rgba(157,192,224,.76)";
      ctx.fillRect(
        devicePixel(Math.min(a[0], b[0])),
        devicePixel(screenY - lineWidth / 2),
        devicePixel(Math.abs(b[0] - a[0])),
        lineWidth
      );
      if (isMajor && viewScale() >= 20 && y < floor.height) {
        ctx.fillStyle = "rgba(231,243,253,.94)";
        ctx.font = "9px system-ui";
        ctx.textAlign = "left";
        ctx.fillText(String(y), left + 4, screenY - 4);
      }
    }

    if (viewScale() >= 14) {
      ctx.fillStyle = "rgba(226,240,252,.5)";
      const radius = viewScale() >= 28 ? 1.5 : 1;
      for (let y = 0; y < floor.height; y++) {
        for (let x = 0; x < floor.width; x++) {
          const center = worldToScreen([
            -room.bounds.width / 2 + x + 0.5,
            -room.bounds.height / 2 + y + 0.5,
          ]);
          ctx.beginPath();
          ctx.arc(devicePixel(center[0]), devicePixel(center[1]), radius, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    }
  };

  FloorData.prepareRooms(state.level.rooms);
  normalize();
}
