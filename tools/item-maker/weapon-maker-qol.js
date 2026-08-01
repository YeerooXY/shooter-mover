"use strict";

(function setupWeaponMakerQol() {
  function findSection(title) {
    return Array.from(gameplayEditor.querySelectorAll(".gameplay-section"))
      .find(section => section.querySelector("h2")?.textContent.trim() === title);
  }

  function setControl(key, value) {
    const control = gameplayControl(key);
    if (!control) return;
    if (control.type === "checkbox") control.checked = Boolean(value);
    else control.value = value;
  }

  function copyMk1Combat(targetMarks) {
    const sourceControls = Array.from(gameplayEditor.querySelectorAll('[data-g-key^="mark.1."]'))
      .filter(control => !control.dataset.gKey.endsWith(".peakLevel") && !control.dataset.gKey.includes(".art."));

    targetMarks.forEach(mark => {
      sourceControls.forEach(source => {
        const targetKey = source.dataset.gKey.replace(/^mark\.1\./, `mark.${mark}.`);
        const target = gameplayControl(targetKey);
        if (!target) return;
        if (source.type === "checkbox") target.checked = source.checked;
        else target.value = source.value;
      });
    });

    gameplayUpdateVisibility();
    gameplayApply();
    renderQolNotice(`Copied MK1 combat stats to ${targetMarks.map(mark => `MK${mark}`).join(" and ")}.`);
  }

  function resetMkLevels() {
    [1, 25, 50].forEach((level, index) => setControl(`mark.${index + 1}.peakLevel`, level));
    gameplayApply();
    renderQolNotice("Reset peak drop levels to 1, 25, and 50.");
  }

  function fillMkArtIds() {
    const folder = elements.folderInput.value.trim();
    if (!/^[a-z0-9]+(?:_[a-z0-9]+)*$/.test(folder)) {
      renderQolNotice("Set a valid weapon key before filling art IDs.", true);
      return;
    }
    for (let mark = 1; mark <= 3; mark += 1) {
      setControl(`mark.${mark}.art.side`, `gun-art.${folder}.mk${mark}.side-v1`);
      const mounted = gameplayControl(`mark.${mark}.art.mounted`);
      if (mounted) mounted.value = `gun-art.${folder}.mk${mark}.mounted-top-v1`;
    }
    gameplayApply();
    renderQolNotice("Filled MK1–MK3 side and mounted art IDs.");
  }

  function renderQolNotice(message, error = false) {
    const notice = gameplayEditor.querySelector("#weaponQolNotice");
    if (!notice) return;
    notice.textContent = message;
    notice.className = `gameplay-note${error ? " error" : ""}`;
  }

  function installControls() {
    if (gameplayEditor.querySelector("#weaponQolControls")) return;
    const section = findSection("MK1–MK3");
    if (!section) return;
    const body = section.querySelector(".gameplay-section-body");
    if (!body) return;

    const controls = document.createElement("div");
    controls.id = "weaponQolControls";
    controls.className = "weapon-qol-controls";
    controls.innerHTML = `
      <div class="weapon-qol-title">Mark shortcuts</div>
      <div class="weapon-qol-buttons">
        <button type="button" data-copy-mk="2">Copy MK1 combat → MK2</button>
        <button type="button" data-copy-mk="3">Copy MK1 combat → MK3</button>
        <button type="button" data-copy-mk="2,3">Copy MK1 combat → both</button>
        <button type="button" id="resetMkLevelsButton">Reset levels 1 / 25 / 50</button>
        <button type="button" id="fillMkArtButton">Fill MK art IDs</button>
      </div>
      <div id="weaponQolNotice" class="gameplay-note">Combat copy leaves each Mark's level and art untouched.</div>`;
    body.insertBefore(controls, body.firstChild);

    controls.querySelectorAll("[data-copy-mk]").forEach(button => button.addEventListener("click", () => {
      copyMk1Combat(button.dataset.copyMk.split(",").map(Number));
    }));
    controls.querySelector("#resetMkLevelsButton").addEventListener("click", resetMkLevels);
    controls.querySelector("#fillMkArtButton").addEventListener("click", fillMkArtIds);
  }

  new MutationObserver(installControls).observe(gameplayEditor, { childList: true, subtree: true });
  installControls();
})();
