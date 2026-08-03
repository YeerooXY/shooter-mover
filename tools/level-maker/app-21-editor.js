"use strict";

{
  const EDITOR_SAVE_DELAY_MS = 350;
  let editorServerTimer = null;

  function localEditorSave() {
    try {
      const raw = localStorage.getItem(LevelSave.editorStorageKey(state.level.id));
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }

  function editorSaveBody() {
    return {
      target: cleanSlug(state.level.targetFolder).toLowerCase(),
      savedAt: new Date().toISOString(),
      editor: LevelSave.makeEditorFile(state),
    };
  }

  function rememberEditor(body) {
    localStorage.setItem(
      LevelSave.editorStorageKey(body.editor.levelId),
      JSON.stringify({ savedAt: body.savedAt, editor: body.editor })
    );
  }

  function useEditorFile(editorFile) {
    if (!editorFile || editorFile.levelId !== state.level.id) return;

    const repositoryAssets = (state.assets || []).filter(asset => asset?.source !== "manual");
    const customAssets = Array.isArray(editorFile.customAssets)
      ? editorFile.customAssets.filter(asset => asset?.source === "manual")
      : [];
    const assets = new Map(repositoryAssets.map(asset => [asset.id, asset]));
    customAssets.forEach(asset => assets.set(asset.id, asset));

    state.editor = {
      ...state.editor,
      ...(editorFile.editor || {}),
      activeRoomId:
        editorFile.activeRoomId || state.level.startRoomId || state.level.rooms[0]?.id || null,
      customAssets,
    };
    state.assets = [...assets.values()].sort(
      (left, right) => left.type.localeCompare(right.type) || left.id.localeCompare(right.id)
    );
    LevelState.fixEditor(state);
    normalize();
  }

  async function saveEditorToServer() {
    if (editorServerTimer) {
      clearTimeout(editorServerTimer);
      editorServerTimer = null;
    }

    const body = editorSaveBody();
    rememberEditor(body);
    try {
      await helper("/api/level-editor", {
        method: "PUT",
        body: JSON.stringify(body),
      });
    } catch (error) {
      console.warn("Level Maker editor file could not be saved.", error);
    }
  }

  function queueEditorServerSave() {
    if (editorServerTimer) clearTimeout(editorServerTimer);
    editorServerTimer = setTimeout(saveEditorToServer, EDITOR_SAVE_DELAY_MS);
  }

  async function loadEditorFromServer() {
    const target = cleanSlug(state.level.targetFolder).toLowerCase();
    const serverSave = await helper(
      `/api/level-editor?target=${encodeURIComponent(target)}`
    );
    const localSave = localEditorSave();
    const serverTime = Date.parse(serverSave?.savedAt || "") || 0;
    const localTime = Date.parse(localSave?.savedAt || "") || 0;

    if (serverSave?.editor && serverTime > localTime) {
      useEditorFile(serverSave.editor);
      rememberEditor({ savedAt: serverSave.savedAt, editor: serverSave.editor });
      renderAll();
      return;
    }

    if (localSave?.editor && localTime >= serverTime) {
      useEditorFile(localSave.editor);
      renderAll();
      await saveEditorToServer();
    }
  }

  async function rebuildFloorsFromUnity() {
    if (Number(state.schemaVersion || 0) >= LevelSave.LEVEL_VERSION) return;

    const target = cleanSlug(state.level.targetFolder).toLowerCase();
    const result = await helper(
      `/api/level-floors?target=${encodeURIComponent(target)}`
    );
    const floors = new Map(
      (result.rooms || []).map(entry => [entry.roomId, entry.tiles || []])
    );

    state.level.rooms = state.level.rooms.map(room => {
      if (!floors.has(room.id)) return room;
      return FloorData.openUnityTiles(room, floors.get(room.id));
    });
    state.schemaVersion = LevelSave.LEVEL_VERSION;
    normalize();
  }

  function sendEditorBeforeExit() {
    const body = editorSaveBody();
    try {
      rememberEditor(body);
    } catch {
      // The normal recovery path already reports browser storage failures.
    }

    if (!navigator.sendBeacon) return;
    const data = new Blob([JSON.stringify(body)], { type: "application/json" });
    navigator.sendBeacon("/api/level-editor", data);
  }

  const connectLevelHelper = connectHelper;
  connectHelper = async function connectLevelHelperAndEditor() {
    await connectLevelHelper();
    try {
      await loadEditorFromServer();
    } catch (error) {
      console.warn("Level Maker editor file could not be loaded.", error);
    }
  };

  const openLevelFromRepository = openRepositoryLevel;
  openRepositoryLevel = async function openRepositoryLevelWithLocalState() {
    await openLevelFromRepository();
    try {
      await rebuildFloorsFromUnity();
      await loadEditorFromServer();
      renderAll();
    } catch (error) {
      console.warn("Level Maker local room state could not be restored.", error);
    }
  };

  const publishLevel = publishProject;
  publishProject = async function publishLevelAndEditor() {
    await saveEditorToServer();
    return publishLevel();
  };
  saveProject = publishProject;

  $("#openRepoBtn").onclick = openRepositoryLevel;
  $("#saveBtn").onclick = saveProject;
  $("#exportBtn").onclick = publishProject;

  document.addEventListener("pointerup", queueEditorServerSave);
  document.addEventListener("change", queueEditorServerSave);
  document.addEventListener("keyup", queueEditorServerSave);
  canvas.addEventListener("wheel", queueEditorServerSave, { passive: true });
  window.addEventListener("pagehide", sendEditorBeforeExit);
}
