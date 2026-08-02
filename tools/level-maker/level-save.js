"use strict";

(function exposeLevelSave(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.LevelSave = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createLevelSave() {
  const LEVEL_FORMAT = "shooter-mover-web-level-project";
  const LEVEL_VERSION = 3;
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
    if (!levelFile.level || !Array.isArray(levelFile.rooms)) {
      throw new Error("The level project is missing its level or rooms.");
    }
  }

  function makeLevelFile(state) {
    if (!state?.level) throw new Error("The Level Maker has no level to save.");

    return {
      format: LEVEL_FORMAT,
      schemaVersion: LEVEL_VERSION,
      level: copy(state.level),
      rooms: copy(state.rooms || []),
      connections: copy(state.connections || []),
      logic: copy(state.logic || []),
    };
  }

  function makeEditorFile(state) {
    if (!state?.level) throw new Error("The Level Maker has no editor state to save.");

    return {
      version: EDITOR_VERSION,
      levelId: state.level.id,
      activeRoomId:
        state.activeRoomId || state.level.startRoomId || state.rooms?.[0]?.id || null,
      editor: copy(state.editor || {}),
      customAssets: manualAssets(state.catalog),
    };
  }

  function openLevelFile(levelFile, editorFile, defaultEditor) {
    checkLevelFile(levelFile);

    const matchingEditor =
      editorFile?.levelId === levelFile.level.id ? editorFile : null;
    const oldEditor = levelFile.editor || {};
    const savedEditor = matchingEditor?.editor || {};
    const editor = {
      ...copy(defaultEditor || {}),
      ...copy(oldEditor),
      ...copy(savedEditor),
    };

    const oldManualAssets = manualAssets(levelFile.catalog);
    const customAssets = matchingEditor
      ? manualAssets(matchingEditor.customAssets)
      : oldManualAssets;

    return {
      format: LEVEL_FORMAT,
      editorVersion: EDITOR_VERSION,
      schemaVersion: Number(levelFile.schemaVersion || 2),
      level: copy(levelFile.level),
      rooms: copy(levelFile.rooms),
      connections: copy(levelFile.connections || []),
      logic: copy(levelFile.logic || []),
      catalog: customAssets,
      activeRoomId:
        matchingEditor?.activeRoomId ||
        levelFile.activeRoomId ||
        levelFile.level.startRoomId ||
        levelFile.rooms[0]?.id ||
        null,
      editor,
    };
  }

  function editorStorageKey(levelId) {
    return `shooter-mover.level-maker.editor.v1:${String(levelId || "unknown")}`;
  }

  function levelRecoveryKey() {
    return "shooter-mover.level-maker.level-recovery.v2";
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
