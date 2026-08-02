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
    return LevelState.levelSnapshot(state);
  };

  undo = function undoLevelChange() {
    if (!history.length) return;
    future.push(snapshot());
    LevelState.restoreLevel(state, history.pop());
    normalize();
    renderAll();
  };

  redo = function redoLevelChange() {
    if (!future.length) return;
    history.push(snapshot());
    LevelState.restoreLevel(state, future.pop());
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
