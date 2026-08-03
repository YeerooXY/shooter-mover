"use strict";

{
  const oldInitialState = initialState;

  initialState = function makeCurrentInitialState() {
    const next = oldInitialState();
    next.schemaVersion = LevelSave.LEVEL_VERSION;
    return next;
  };

  state.schemaVersion = LevelSave.LEVEL_VERSION;
  state.editor.customAssets = [
    ...(state.editor.customAssets || []),
    ...(state.assets || []).filter(asset => asset?.source === "manual"),
  ];

  snapshot = function saveLevelUndoPoint() {
    return JSON.stringify(LevelSave.makeLevelFile(state));
  };

  function restoreLevelUndoPoint(text) {
    const savedLevel = JSON.parse(text);
    const opened = LevelSave.openLevelFile(
      savedLevel,
      LevelSave.makeEditorFile(state),
      state.editor
    );
    state.level = opened.level;
    LevelState.fixEditor(state);
    FloorData.prepareRooms(state.level.rooms);
  }

  undo = function undoLevelChange() {
    if (!history.length) return;
    future.push(snapshot());
    restoreLevelUndoPoint(history.pop());
    normalize();
    renderAll();
  };

  redo = function redoLevelChange() {
    if (!future.length) return;
    history.push(snapshot());
    restoreLevelUndoPoint(future.pop());
    normalize();
    renderAll();
  };

  compressedFloorTiles = function buildRoomFloorAreas(room) {
    return FloorData.buildUnityTiles(room);
  };

  $("#undoBtn").onclick = undo;
  $("#redoBtn").onclick = redo;

  normalize();
}
