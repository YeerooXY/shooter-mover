"use strict";

{
  const oldInitialState = initialState;

  initialState = function makeBucketedInitialState() {
    const next = oldInitialState();
    next.schemaVersion = LevelSave.LEVEL_VERSION;
    return LevelState.addOldNames(next);
  };

  state = LevelState.addOldNames(state);
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
    LevelState.addOldNames(state);
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
