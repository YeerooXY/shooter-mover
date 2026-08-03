"use strict";

{
  const oldWriteRecoveryDraft = writeRecoveryDraft;
  const oldScheduleRecoverySave = scheduleRecoverySave;
  const LEVEL_SAVE_DELAY_MS = 500;
  const EDITOR_SAVE_DELAY_MS = 300;
  let levelSaveTimer = null;
  let editorSaveTimer = null;

  function readLocalJson(key) {
    try {
      const raw = localStorage.getItem(key);
      return raw ? JSON.parse(raw) : null;
    } catch (error) {
      console.warn(`Level Maker could not read ${key}.`, error);
      return null;
    }
  }

  function readEditorFile(levelId) {
    const saved = readLocalJson(LevelSave.editorStorageKey(levelId));
    return saved?.editor || null;
  }

  function readLevelRecovery() {
    const saved = readLocalJson(LevelSave.levelRecoveryKey());
    if (!saved?.level) return null;
    try {
      LevelSave.checkLevelFile(saved.level);
      return saved;
    } catch (error) {
      console.warn("Level Maker level recovery could not be restored.", error);
      return null;
    }
  }

  function legacyEditorFile(project) {
    if (!project?.editor && !project?.activeRoomId && !project?.catalog) return null;
    const customAssets = Array.isArray(project.catalog)
      ? project.catalog.filter(asset => asset?.source === "manual").map(clone)
      : [];
    return {
      version: LevelSave.EDITOR_VERSION,
      levelId: project.level.id,
      activeRoomId:
        project.activeRoomId || project.level.startRoomId || project.rooms?.[0]?.id || null,
      editor: clone(project.editor || {}),
      customAssets,
    };
  }

  function saveEditorNow() {
    if (editorSaveTimer) {
      clearTimeout(editorSaveTimer);
      editorSaveTimer = null;
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
      console.warn("Level Maker editor save failed.", error);
    }
  }

  function saveLevelRecoveryNow() {
    if (levelSaveTimer) {
      clearTimeout(levelSaveTimer);
      levelSaveTimer = null;
    }
    try {
      const savedAt = new Date().toISOString();
      localStorage.setItem(
        LevelSave.levelRecoveryKey(),
        JSON.stringify({ version: 2, savedAt, level: LevelSave.makeLevelFile(state) })
      );
      recoveryWriteError = "";
    } catch (error) {
      recoveryWriteError = error?.message || "Browser storage is unavailable.";
      console.warn("Level Maker level recovery failed.", error);
    }
  }

  function saveLocalStateNow() {
    saveLevelRecoveryNow();
    saveEditorNow();
  }

  function scheduleLocalStateSave() {
    if (levelSaveTimer) clearTimeout(levelSaveTimer);
    if (editorSaveTimer) clearTimeout(editorSaveTimer);
    levelSaveTimer = setTimeout(saveLevelRecoveryNow, LEVEL_SAVE_DELAY_MS);
    editorSaveTimer = setTimeout(saveEditorNow, EDITOR_SAVE_DELAY_MS);
  }

  function mergeKnownAssets(previousAssets) {
    const merged = new Map(defaultCatalog.map(asset => [asset.id, clone(asset)]));
    (previousAssets || [])
      .filter(asset => asset?.source !== "manual")
      .forEach(asset => merged.set(asset.id, clone(asset)));
    (state.assets || []).forEach(asset => merged.set(asset.id, clone(asset)));
    state.assets = [...merged.values()].sort(
      (left, right) => left.type.localeCompare(right.type) || left.id.localeCompare(right.id)
    );
  }

  function openSavedLevel(project) {
    LevelSave.checkLevelFile(project);
    const previousAssets = clone(state.assets || []);
    const editor = legacyEditorFile(project) || readEditorFile(project.level.id);

    state = LevelSave.openLevelFile(project, editor, initialState().editor);
    mergeKnownAssets(previousAssets);
    normalize();
    saveEditorNow();
  }

  const cleanRecovery = readLevelRecovery();
  if (cleanRecovery) {
    const previousEditor = readEditorFile(cleanRecovery.level.level.id);
    state = LevelSave.openLevelFile(
      cleanRecovery.level,
      previousEditor,
      initialState().editor
    );
    recoveryRestoredAt = String(cleanRecovery.savedAt || "");
  } else {
    const currentLevel = LevelSave.makeLevelFile(state);
    const currentEditor = LevelSave.makeEditorFile(state);
    const savedEditor = readEditorFile(currentLevel.level.id);
    state = LevelSave.openLevelFile(
      currentLevel,
      savedEditor || currentEditor,
      initialState().editor
    );
  }
  normalize();

  if (recoverySaveTimer) {
    clearTimeout(recoverySaveTimer);
    recoverySaveTimer = null;
  }
  document.removeEventListener("pointerup", oldScheduleRecoverySave);
  document.removeEventListener("change", oldScheduleRecoverySave);
  document.removeEventListener("keyup", oldScheduleRecoverySave);
  canvas.removeEventListener("wheel", oldScheduleRecoverySave);
  window.removeEventListener("pagehide", oldWriteRecoveryDraft);
  window.removeEventListener("beforeunload", oldWriteRecoveryDraft);

  writeRecoveryDraft = saveLocalStateNow;
  scheduleRecoverySave = scheduleLocalStateSave;

  publishProject = async function publishCleanLevel() {
    const errors = validate().filter(item => item.severity === "error");
    if (errors.length) {
      showValidation();
      setStatus("Publish blocked by validation errors.", "bad");
      return;
    }

    try {
      saveLocalStateNow();
      const project = LevelSave.makeLevelFile(state);
      const result = await helper("/api/level", {
        method: "PUT",
        body: JSON.stringify({ project, files: buildExportFiles() }),
      });
      setStatus(
        `Saved ${result.projectPath} and published ${result.fileCount} Unity source files.`,
        "good"
      );
    } catch (error) {
      setStatus(error.message, "bad");
    }
  };

  saveProject = function saveCleanLevel() {
    return publishProject();
  };

  downloadProject = function downloadCleanLevel() {
    const project = LevelSave.makeLevelFile(state);
    download(
      new Blob([pretty(project)], { type: "application/json" }),
      `${cleanSlug(state.level.targetFolder)}.level.json`
    );
    setStatus("Portable level project exported without editor state.", "good");
  };

  openProjectFile = async function openCleanProjectFile(file) {
    try {
      const parsed = JSON.parse(await file.text());
      pushHistory(snapshot());
      openSavedLevel(parsed);
      fitRoom();
      renderAll();
      setStatus(`Opened ${file.name}.`, "good");
    } catch (error) {
      setStatus(error.message, "bad");
      alert(error.message);
    }
  };

  openRepositoryLevel = async function openCleanRepositoryLevel() {
    try {
      const list = await helper("/api/levels");
      if (!list.levels.length) throw new Error("No project levels were found.");
      const choices = list.levels
        .map((item, index) => `${index + 1}. ${item.name} (${item.target})`)
        .join("\n");
      const answer = prompt(`Choose a project level:\n${choices}`, "1");
      if (answer === null) return;
      const index = Number(answer) - 1;
      if (!Number.isInteger(index) || index < 0 || index >= list.levels.length) {
        throw new Error("That level selection is invalid.");
      }
      const result = await helper(
        `/api/level?target=${encodeURIComponent(list.levels[index].target)}`
      );
      pushHistory(snapshot());
      openSavedLevel(result.project);
      fitRoom();
      renderAll();
      setStatus(`Opened ${list.levels[index].name} from the repository.`, "good");
    } catch (error) {
      setStatus(error.message, "bad");
    }
  };

  $("#openRepoBtn").onclick = openRepositoryLevel;
  $("#openInput").onchange = event =>
    event.target.files[0] && openProjectFile(event.target.files[0]);
  $("#saveBtn").onclick = saveProject;
  $("#downloadBtn").onclick = downloadProject;
  $("#exportBtn").onclick = publishProject;

  document.addEventListener("pointerup", scheduleRecoverySave);
  document.addEventListener("change", scheduleRecoverySave);
  document.addEventListener("keyup", scheduleRecoverySave);
  canvas.addEventListener("wheel", scheduleRecoverySave, { passive: true });
  window.addEventListener("pagehide", writeRecoveryDraft);
}
