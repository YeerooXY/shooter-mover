"use strict";

{
  const LEVEL_RECOVERY_DELAY_MS = 500;
  const EDITOR_RECOVERY_DELAY_MS = 250;
  const previousSchedule = scheduleRecoverySave;
  const previousWrite = writeRecoveryDraft;
  const previousMutate = mutate;
  const previousUndo = undo;
  const previousRedo = redo;
  let levelRecoveryTimer = null;
  let editorRecoveryTimer = null;
  let lastSavedLevel = JSON.stringify(LevelSave.makeLevelFile(state));

  document.removeEventListener("pointerup", previousSchedule);
  document.removeEventListener("change", previousSchedule);
  document.removeEventListener("keyup", previousSchedule);
  canvas.removeEventListener("wheel", previousSchedule);
  window.removeEventListener("pagehide", previousWrite);

  function saveEditorLocally() {
    if (editorRecoveryTimer) {
      clearTimeout(editorRecoveryTimer);
      editorRecoveryTimer = null;
    }

    try {
      const editor = LevelSave.makeEditorFile(state);
      localStorage.setItem(
        LevelSave.editorStorageKey(editor.levelId),
        JSON.stringify({ savedAt: new Date().toISOString(), editor })
      );
      recoveryWriteError = "";
    } catch (error) {
      recoveryWriteError = error?.message || "Browser storage is unavailable.";
      console.warn("Level Maker editor recovery failed.", error);
    }
  }

  function saveLevelLocally(force = false) {
    if (levelRecoveryTimer) {
      clearTimeout(levelRecoveryTimer);
      levelRecoveryTimer = null;
    }

    try {
      const level = LevelSave.makeLevelFile(state);
      const text = JSON.stringify(level);
      if (!force && text === lastSavedLevel) return;
      const savedAt = new Date().toISOString();
      localStorage.setItem(
        LevelSave.levelRecoveryKey(),
        JSON.stringify({ version: 3, savedAt, level })
      );
      lastSavedLevel = text;
      recoveryWriteError = "";
    } catch (error) {
      recoveryWriteError = error?.message || "Browser storage is unavailable.";
      console.warn("Level Maker level recovery failed.", error);
    }
  }

  function saveLocalState() {
    saveLevelLocally(true);
    saveEditorLocally();
  }

  function queueEditorRecovery() {
    if (editorRecoveryTimer) clearTimeout(editorRecoveryTimer);
    editorRecoveryTimer = setTimeout(saveEditorLocally, EDITOR_RECOVERY_DELAY_MS);
  }

  function queueLevelRecovery() {
    if (levelRecoveryTimer) clearTimeout(levelRecoveryTimer);
    levelRecoveryTimer = setTimeout(saveLevelLocally, LEVEL_RECOVERY_DELAY_MS);
  }

  scheduleRecoverySave = queueEditorRecovery;
  writeRecoveryDraft = saveLocalState;

  mutate = function changeLevel(change, options) {
    previousMutate(change, options);
    queueLevelRecovery();
  };

  undo = function undoLevel() {
    previousUndo();
    queueLevelRecovery();
  };

  redo = function redoLevel() {
    previousRedo();
    queueLevelRecovery();
  };

  const previousOpenFile = openProjectFile;
  openProjectFile = async function openLevelFile(file) {
    await previousOpenFile(file);
    lastSavedLevel = "";
    queueLevelRecovery();
  };

  const previousOpenRepository = openRepositoryLevel;
  openRepositoryLevel = async function openSavedLevel() {
    await previousOpenRepository();
    lastSavedLevel = "";
    queueLevelRecovery();
  };

  const previousNew = $("#newBtn").onclick;
  $("#newBtn").onclick = event => {
    previousNew(event);
    lastSavedLevel = "";
    queueLevelRecovery();
  };

  $("#openRepoBtn").onclick = openRepositoryLevel;
  $("#openInput").onchange = event =>
    event.target.files[0] && openProjectFile(event.target.files[0]);
  $("#undoBtn").onclick = undo;
  $("#redoBtn").onclick = redo;

  document.addEventListener("pointerup", queueEditorRecovery);
  document.addEventListener("change", queueEditorRecovery);
  document.addEventListener("keyup", queueEditorRecovery);
  canvas.addEventListener("wheel", queueEditorRecovery, { passive: true });
  window.addEventListener("pagehide", saveLocalState);
}
