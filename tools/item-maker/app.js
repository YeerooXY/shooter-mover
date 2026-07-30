"use strict";

elements.itemName.addEventListener("input", () => { state.name = elements.itemName.value; if (idTracksName) state.id = slugify(state.name); setDirty(); renderIdentityOutput(); });
elements.itemId.addEventListener("input", () => { idTracksName = false; state.id = slugify(elements.itemId.value); elements.itemId.value = state.id; setDirty(); renderIdentityOutput(); });
elements.intendedUse.addEventListener("input", () => { state.intendedUse = elements.intendedUse.value; setDirty(); refreshOutput(); });
elements.useNameForId.addEventListener("click", () => { idTracksName = true; state.id = slugify(state.name); setDirty(); render(); });
elements.gunKindButton.addEventListener("click", () => switchKind("gun-family"));
elements.gearKindButton.addEventListener("click", () => switchKind("gear-set"));
elements.newGunButton.addEventListener("click", () => newItem("gun-family"));
elements.newGearButton.addEventListener("click", () => newItem("gear-set"));
elements.importButton.addEventListener("click", () => elements.importInput.click());
elements.importInput.addEventListener("change", async () => { const file = elements.importInput.files?.[0]; if (!file) return; try { await importFile(file); } catch (error) { alert("Could not import: " + error.message); } elements.importInput.value = ""; });
elements.exportButton.addEventListener("click", exportPackage);
elements.copyJsonButton.addEventListener("click", async () => {
  const text = JSON.stringify(cleanPackage(), null, 2);
  try { await navigator.clipboard.writeText(text); } catch (_) {
    const area = Object.assign(document.createElement("textarea"), { value: text }); area.style.cssText = "position:fixed;opacity:0";
    document.body.appendChild(area); area.select(); document.execCommand("copy"); area.remove();
  }
  elements.copyJsonButton.textContent = "Copied"; setTimeout(() => { elements.copyJsonButton.textContent = "Copy"; }, 1000);
});
document.querySelectorAll("[data-mark]").forEach(button => button.addEventListener("click", () => { activeMark = button.dataset.mark; render(); }));
elements.copyPreviousButton.addEventListener("click", () => {
  const index = Number(activeMark.substring(2)) - 1, mark = state.marks[index].mark;
  state.marks[index] = clone(state.marks[index - 1]); state.marks[index].mark = mark; setDirty(); render();
});
elements.copyFirstToAllButton.addEventListener("click", () => {
  if (!confirm("Replace MK2 and MK3 with MK1 values?")) return;
  [1, 2].forEach(index => { state.marks[index] = clone(state.marks[0]); state.marks[index].mark = index + 1; });
  setDirty(); render();
});

function renderIdentityOutput() {
  elements.packageFile.textContent = packageFileName();
  elements.idExample.textContent = state.kind === "gun-family" ? `${state.id || "item"}.mk1` : `equipment.gear-${state.id || "set"}-headpiece-mk1`;
  elements.activeMarkId.textContent = `${state.id || "item"}.${activeMark}`; refreshOutput();
}
function switchKind(kind) { if (state.kind === kind) return; if (dirty && !confirm("Replace this package?")) return; newState(kind); }
function newItem(kind) { if (dirty && !confirm("Discard this package?")) return; newState(kind); elements.itemName.focus(); }
function newState(kind) { state = makeItem(kind); idTracksName = true; activeMark = "mk1"; Object.keys(previews).forEach(key => delete previews[key]); setDirty(); render(); }

let mutationToken = "";
async function api(path, options = {}) {
  const response = await fetch(path, { ...options, headers: { "Content-Type": "application/json", ...(options.method && options.method !== "GET" ? { "X-Item-Maker-Token": mutationToken } : {}), ...(options.headers || {}) } });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error || `${response.status} ${response.statusText}`);
  return body;
}
async function refreshRepository() {
  if (!repositoryConnected) return;
  try {
    const [status, list] = await Promise.all([api("/api/status"), api("/api/packages")]);
    mutationToken = status.mutationToken;
    elements.repoStatus.textContent = `${status.branch} · ${status.clean ? "clean" : `${status.changed} changed`} · ${status.behind} behind`;
    elements.repoStatus.className = `repo-state ${status.clean && !status.behind ? "good" : "warn"}`;
    elements.packageList.innerHTML = list.packages.length ? list.packages.map(item => `<button class="package-entry" data-kind="${item.kind}" data-id="${item.id}">${escapeHtml(item.name)} <small>${escapeHtml(item.kind)}</small></button>`).join("") : `<div class="help">No authored packages yet.</div>`;
    elements.packageList.querySelectorAll(".package-entry").forEach(button => button.addEventListener("click", async () => {
      if (dirty && !confirm("Discard unsaved changes?")) return;
      try { state = normalizeImportedItem((await api(`/api/package?kind=${encodeURIComponent(button.dataset.kind)}&id=${encodeURIComponent(button.dataset.id)}`)).package); idTracksName = false; activeMark = "mk1"; setDirty(false); render(); }
      catch (error) { alert(error.message); }
    }));
  } catch (_) { repositoryConnected = false; setRepoAvailability(); }
}
function setRepoAvailability() {
  elements.repoStatus.textContent = repositoryConnected ? "Connected" : "Offline export mode";
  [elements.saveRepoButton, elements.fetchButton, elements.pullButton].forEach(button => { button.disabled = !repositoryConnected; });
}
elements.fetchButton.addEventListener("click", async () => { try { await api("/api/fetch", { method: "POST", body: "{}" }); await refreshRepository(); } catch (error) { alert(error.message); } });
elements.pullButton.addEventListener("click", async () => { if (!confirm("Fast-forward this clean branch from its upstream?")) return; try { await api("/api/pull", { method: "POST", body: "{}" }); await refreshRepository(); } catch (error) { alert(error.message); } });
elements.saveRepoButton.addEventListener("click", async () => {
  if (validateItem().errors.length) { alert("Fix blocking problems before saving."); return; }
  try { await api("/api/package", { method: "PUT", body: JSON.stringify({ package: cleanPackage() }) }); setDirty(false); await refreshRepository(); renderChecks(); } catch (error) { alert(error.message); }
});
window.addEventListener("beforeunload", event => { if (!dirty) return; event.preventDefault(); event.returnValue = ""; });
(async function start() { try { const status = await api("/api/status"); mutationToken = status.mutationToken; repositoryConnected = true; } catch (_) { repositoryConnected = false; } setRepoAvailability(); render(); await refreshRepository(); })();
