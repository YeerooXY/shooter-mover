elements.itemName.addEventListener("input", () => {
  state.name = elements.itemName.value;
  if (idTracksName) state.id = slugify(state.name);
  setDirty();
  elements.itemId.value = state.id;
  elements.packageFile.textContent = `${state.id || "item"}.item.json`;
  elements.idExample.textContent = `${state.id || "item"}.mk1`;
  elements.activeMarkId.textContent = `${state.id || "item"}.${activeMark}`;
  refreshOutput();
});
elements.itemId.addEventListener("input", () => {
  idTracksName = false;
  state.id = slugify(elements.itemId.value);
  elements.itemId.value = state.id;
  setDirty();
  elements.packageFile.textContent = `${state.id || "item"}.item.json`;
  elements.idExample.textContent = `${state.id || "item"}.mk1`;
  elements.activeMarkId.textContent = `${state.id || "item"}.${activeMark}`;
  refreshOutput();
});
elements.intendedUse.addEventListener("input", () => { state.intendedUse = elements.intendedUse.value; setDirty(); refreshOutput(); });
elements.useNameForId.addEventListener("click", () => { idTracksName = true; state.id = slugify(state.name); setDirty(); render(); });
elements.gunKindButton.addEventListener("click", () => switchKind("gun"));
elements.gearKindButton.addEventListener("click", () => switchKind("gear"));
elements.newGunButton.addEventListener("click", () => newItem("gun"));
elements.newGearButton.addEventListener("click", () => newItem("gear"));
elements.importButton.addEventListener("click", () => elements.importInput.click());
elements.importInput.addEventListener("change", async () => {
  const file = elements.importInput.files?.[0];
  if (!file) return;
  try { await importFile(file); } catch (error) { alert("Could not import package: " + error.message); }
  elements.importInput.value = "";
});
elements.exportButton.addEventListener("click", exportPackage);
elements.copyJsonButton.addEventListener("click", async () => {
  const text = JSON.stringify(cleanPackage(), null, 2);
  try {
    if (!navigator.clipboard) throw new Error("Clipboard API unavailable");
    await navigator.clipboard.writeText(text);
  } catch (_) {
    const area = document.createElement("textarea");
    area.value = text;
    area.style.position = "fixed";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    document.execCommand("copy");
    area.remove();
  }
  elements.copyJsonButton.textContent = "Copied";
  setTimeout(() => { elements.copyJsonButton.textContent = "Copy"; }, 1000);
});
document.querySelectorAll("[data-mark]").forEach(button => button.addEventListener("click", () => { activeMark = button.dataset.mark; render(); }));
elements.copyPreviousButton.addEventListener("click", () => {
  const previous = activeMark === "mk2" ? "mk1" : "mk2";
  state.marks[activeMark] = clone(state.marks[previous]);
  setDirty();
  render();
});
elements.copyFirstToAllButton.addEventListener("click", () => {
  if (!confirm("Replace MK2 and MK3 with copies of MK1?")) return;
  state.marks.mk2 = clone(state.marks.mk1);
  state.marks.mk3 = clone(state.marks.mk1);
  setDirty();
  render();
});

window.addEventListener("beforeunload", event => {
  if (!dirty) return;
  event.preventDefault();
  event.returnValue = "";
});

render();
