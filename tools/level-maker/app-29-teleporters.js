"use strict";

{
  const previousNormalize = normalize;
  const previousPlaceAt = placeAt;
  const previousRenderInspector = renderInspector;
  const previousValidate = validate;
  const previousRuntimeRoomFiles = runtimeRoomFiles;

  function prepareTeleporter(entity) {
    if (!entity || entity.kind !== "teleporter") return;
    entity.enabled = entity.enabled !== false;
    entity.unlockWhen = "room-complete";
    delete entity.pairId;
  }

  normalize = function normalizeTeleporters() {
    previousNormalize();
    state.level.rooms.forEach(room =>
      (room.entities || []).forEach(prepareTeleporter)
    );
  };

  placeAt = function placePlayableEntity(tool, position) {
    previousPlaceAt(tool, position);
    if (tool !== "teleporter") return;
    prepareTeleporter(selected()?.entity);
  };

  renderInspector = function renderPlayableTeleporterInspector() {
    previousRenderInspector();
    const entity = selected()?.entity;
    if (!entity || entity.kind !== "teleporter") return;

    const panel = document.querySelector("#inspector .panel");
    if (!panel) return;
    panel.innerHTML = `<h2>Teleporter</h2>
      <label>Instance ID</label><input data-i="id" value="${esc(entity.id)}">
      ${commonTransformInspector(entity)}
      <div class="section">
        <div class="section-title">Travel</div>
        <label><input data-i="enabled" type="checkbox" ${entity.enabled !== false ? "checked" : ""}> Enabled</label>
        <div class="notice">The teleporter unlocks when this room is cleared. Open the map and click an unlocked teleporter to travel.</div>
      </div>
      <button class="danger" data-action="delete">Delete teleporter</button>`;
    wireInspectorEntity(entity, panel);
  };

  validate = function validatePlayableTeleporters() {
    const issues = previousValidate().filter(issue =>
      !String(issue.message || "").includes("preserved but not emitted") &&
      !String(issue.message || "").startsWith("Teleporter pair ")
    );
    state.level.rooms.forEach((room, roomIndex) => {
      (room.entities || [])
        .filter(entity => entity.kind === "teleporter")
        .forEach((entity, teleporterIndex) => {
          const path = `rooms[${roomIndex}].teleporters[${teleporterIndex}]`;
          if (!String(entity.id || "").trim()) {
            issues.push({ severity: "error", message: "Teleporter ID is required.", path });
          }
          if (!Array.isArray(entity.position) || entity.position.length !== 2 ||
              !entity.position.every(Number.isFinite)) {
            issues.push({ severity: "error", message: `${entity.id || "Teleporter"} has an invalid position.`, path });
          }
        });
    });
    return issues;
  };

  runtimeRoomFiles = function runtimeRoomFilesWithTeleporters(room) {
    const result = previousRuntimeRoomFiles(room);
    result.documents["room.json"].teleporters = (room.entities || [])
      .filter(entity => entity.kind === "teleporter")
      .map(entity => ({
        id: entity.id,
        position: entity.position.map(round),
        rotation: round(entity.rotation || 0),
        enabled: entity.enabled !== false,
        unlock_when: "room-complete",
      }));
    return result;
  };

  normalize();
}
