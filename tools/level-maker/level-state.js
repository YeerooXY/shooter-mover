"use strict";

(function exposeLevelState(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.LevelState = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createLevelState() {
  function copy(value) {
    return value == null ? value : JSON.parse(JSON.stringify(value));
  }

  function addOldNames(state) {
    state.level ||= {};
    state.editor ||= {};

    const oldRooms = Array.isArray(state.rooms) ? copy(state.rooms) : null;
    const oldConnections = Array.isArray(state.connections) ? copy(state.connections) : null;
    const oldLogic = Array.isArray(state.logic) ? copy(state.logic) : null;
    const oldAssets = Array.isArray(state.catalog) ? copy(state.catalog) : null;
    const oldActiveRoomId = state.activeRoomId;

    state.level.rooms ||= oldRooms || [];
    state.level.connections ||= oldConnections || [];
    state.level.logic ||= oldLogic || [];
    state.assets ||= oldAssets || [];
    state.editor.activeRoomId ||= oldActiveRoomId || state.level.startRoomId || state.level.rooms[0]?.id || null;
    state.editor.customAssets ||= [];

    const names = {
      rooms: {
        get: () => state.level.rooms,
        set: value => { state.level.rooms = value; },
      },
      connections: {
        get: () => state.level.connections,
        set: value => { state.level.connections = value; },
      },
      logic: {
        get: () => state.level.logic,
        set: value => { state.level.logic = value; },
      },
      catalog: {
        get: () => state.assets,
        set: value => { state.assets = value; },
      },
      activeRoomId: {
        get: () => state.editor.activeRoomId,
        set: value => { state.editor.activeRoomId = value; },
      },
    };

    for (const [name, access] of Object.entries(names)) {
      Object.defineProperty(state, name, {
        configurable: true,
        enumerable: false,
        get: access.get,
        set: access.set,
      });
    }

    return state;
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

    return addOldNames(state);
  }

  function levelFile(state) {
    addOldNames(state);
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
    addOldNames(state);
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
    addOldNames(state);
    return JSON.stringify(copy(state.level));
  }

  function restoreLevel(state, text) {
    state.level = JSON.parse(text);
    fixEditor(state);
    return addOldNames(state);
  }

  function fixEditor(state) {
    const rooms = state.level.rooms || [];
    if (!rooms.some(room => room.id === state.editor.activeRoomId)) {
      state.editor.activeRoomId = state.level.startRoomId || rooms[0]?.id || null;
    }

    if (!state.editor.selectedId) return;
    const selectedExists = rooms.some(room =>
      (room.entities || []).some(entity => entity.id === state.editor.selectedId) ||
      (room.doors || []).some(door => door.id === state.editor.selectedId)
    );
    if (!selectedExists) state.editor.selectedId = null;
  }

  return {
    addOldNames,
    makeState,
    levelFile,
    editorFile,
    levelSnapshot,
    restoreLevel,
    fixEditor,
  };
});
