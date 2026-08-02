"use strict";

(function exposeLevelSave(root, factory) {
  const floorData = typeof module === "object" && module.exports
    ? require("./floor-data")
    : root.FloorData;
  const levelState = typeof module === "object" && module.exports
    ? require("./level-state")
    : root.LevelState;
  const api = factory(floorData, levelState);
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.LevelSave = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createLevelSave(FloorData, LevelState) {
  const LEVEL_FORMAT = "shooter-mover-web-level-project";
  const LEVEL_VERSION = 4;
  const EDITOR_VERSION = 1;

  function copy(value) {
    return value == null ? value : JSON.parse(JSON.stringify(value));
  }

  function manualAssets(assets) {
    const list = Array.isArray(assets) ? assets : [];
    return list.filter(asset => asset?.source === "manual").map(copy);
  }

  function checkLevelFile(levelFile) {
    if (!levelFile || levelFile.format !== LEVEL_FORMAT) {
      throw new Error("Not a Shooter Mover level project.");
    }

    const rooms = levelFile.rooms || levelFile.level?.rooms;
    if (!levelFile.level || !Array.isArray(rooms)) {
      throw new Error("The level project is missing its level or rooms.");
    }
  }

  function makeLevelFile(state) {
    if (!state?.level) throw new Error("The Level Maker has no level to save.");

    const levelFile = LevelState.levelFile(LevelState.addOldNames(state));
    levelFile.format = LEVEL_FORMAT;
    levelFile.schemaVersion = LEVEL_VERSION;
    levelFile.rooms = levelFile.rooms.map(FloorData.saveRoom);
    return levelFile;
  }

  function makeEditorFile(state) {
    if (!state?.level) throw new Error("The Level Maker has no editor state to save.");

    LevelState.addOldNames(state);
    const saved = LevelState.editorFile(state);
    const customAssets = [
      ...manualAssets(state.editor?.customAssets),
      ...manualAssets(state.assets || state.catalog),
    ];
    const byId = new Map(customAssets.map(asset => [asset.id, asset]));

    return {
      version: EDITOR_VERSION,
      levelId: state.level.id,
      activeRoomId:
        state.editor?.activeRoomId || state.level.startRoomId || state.rooms?.[0]?.id || null,
      editor: saved.editor,
      customAssets: [...byId.values()].sort((a, b) => a.id.localeCompare(b.id)),
    };
  }

  function openLevelFile(levelFile, editorFile, defaultEditor) {
    checkLevelFile(levelFile);

    const savedRooms = levelFile.rooms || levelFile.level.rooms || [];
    const matchingEditor = editorFile?.levelId === levelFile.level.id ? editorFile : null;
    const oldEditorFile = {
      version: EDITOR_VERSION,
      levelId: levelFile.level.id,
      activeRoomId: levelFile.activeRoomId,
      editor: copy(levelFile.editor || {}),
      customAssets: manualAssets(levelFile.catalog),
    };
    const chosenEditor = matchingEditor || oldEditorFile;
    const openFile = {
      format: LEVEL_FORMAT,
      schemaVersion: Number(levelFile.schemaVersion || 2),
      level: copy(levelFile.level),
      rooms: savedRooms.map(FloorData.openRoom),
      connections: copy(levelFile.connections || levelFile.level.connections || []),
      logic: copy(levelFile.logic || levelFile.level.logic || []),
    };

    const state = LevelState.makeState(openFile, chosenEditor, [], defaultEditor);
    state.editor.customAssets = manualAssets(chosenEditor.customAssets);
    return state;
  }

  function editorStorageKey(levelId) {
    return `shooter-mover.level-maker.editor.v1:${String(levelId || "unknown")}`;
  }

  function levelRecoveryKey() {
    return "shooter-mover.level-maker.level-recovery.v3";
  }

  return {
    LEVEL_FORMAT,
    LEVEL_VERSION,
    EDITOR_VERSION,
    checkLevelFile,
    makeLevelFile,
    makeEditorFile,
    openLevelFile,
    editorStorageKey,
    levelRecoveryKey,
  };
});
