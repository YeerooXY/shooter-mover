"use strict";

(function exposeLevelState(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.LevelState = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createLevelState() {
  function copy(value) {
    if (value == null) return value;
    if (typeof structuredClone === "function") return structuredClone(value);
    return JSON.parse(JSON.stringify(value));
  }

  function makeState(levelFile, editorFile, assets, defaultEditor) {
    const level = {
      ...copy(levelFile.level || {}),
      rooms: copy(levelFile.rooms || levelFile.level?.rooms || []),
      connections: copy(levelFile.connections || levelFile.level?.connections || []),
      logic: copy(levelFile.logic || levelFile.level?.logic || []),
    };

    const savedEditor = editorFile?.editor || levelFile.editor || {};
    const activeRoomId =
      editorFile?.activeRoomId ||
      levelFile.activeRoomId ||
      level.startRoomId ||
      level.rooms[0]?.id ||
      null;

    const state = {
      format: levelFile.format || "shooter-mover-web-level-project",
      schemaVersion: Number(levelFile.schemaVersion || 4),
      level,
      editor: {
        ...copy(defaultEditor || {}),
        ...copy(savedEditor),
        activeRoomId,
        customAssets: copy(editorFile?.customAssets || []),
      },
      assets: copy(assets || []),
    };

    fixEditor(state);
    return state;
  }

  function levelFile(state) {
    return {
      format: state.format || "shooter-mover-web-level-project",
      schemaVersion: Number(state.schemaVersion || 4),
      level: copyLevelInfo(state.level),
      rooms: copy(state.level.rooms || []),
      connections: copy(state.level.connections || []),
      logic: copy(state.level.logic || []),
    };
  }

  function editorFile(state) {
    return {
      version: 1,
      levelId: state.level.id,
      activeRoomId: state.editor.activeRoomId,
      editor: copyEditor(state.editor),
      customAssets: copy(state.editor.customAssets || []),
    };
  }

  function copyLevelInfo(level) {
    const result = copy(level || {});
    delete result.rooms;
    delete result.connections;
    delete result.logic;
    return result;
  }

  function copyEditor(editor) {
    const result = copy(editor || {});
    delete result.activeRoomId;
    delete result.customAssets;
    return result;
  }

  function levelSnapshot(state) {
    return JSON.stringify(copy(state.level));
  }

  function restoreLevel(state, text) {
    state.level = JSON.parse(text);
    fixEditor(state);
    return state;
  }

  function fixEditor(state) {
    const rooms = state.level.rooms || [];
    if (!rooms.some(room => room.id === state.editor.activeRoomId)) {
      const startRoomExists = rooms.some(room => room.id === state.level.startRoomId);
      state.editor.activeRoomId = startRoomExists
        ? state.level.startRoomId
        : rooms[0]?.id || null;
    }

    if (!state.editor.selectedId) return;
    const selectedExists = rooms.some(room =>
      (room.entities || []).some(entity => entity.id === state.editor.selectedId) ||
      (room.doors || []).some(door => door.id === state.editor.selectedId)
    );
    if (!selectedExists) state.editor.selectedId = null;
  }

  return {
    makeState,
    levelFile,
    editorFile,
    levelSnapshot,
    restoreLevel,
    fixEditor,
  };
});
