"use strict";

(() => {
  const epicColor = "#8B5CF6";
  const epicGlow = "#C4B5FD";

  function applyEpicPalette(rarities) {
    const epic = rarities?.find(rarity => rarity.id === "epic");
    if (!epic) return;
    epic.color = epicColor;
    epic.glow = epicGlow;
  }

  applyEpicPalette(DEFAULT_CONFIG.rarities);
  applyEpicPalette(config.rarities);

  const prototypeResetVisualState = resetVisualState;
  resetVisualState = function purpleEpicResetVisualState(run, rerender) {
    prototypeResetVisualState(run, rerender);
    if (rarityEl.textContent === "Grey · Blue · Green · Yellow · Red") {
      rarityEl.textContent = "Grey · Blue · Purple · Yellow · Red";
    }
  };

  const epicIndex = config.rarities.findIndex(rarity => rarity.id === "epic");
  const epicInput = document.querySelector(
    `[data-rarity="${epicIndex}"][data-field="color"]`
  );
  if (epicInput) epicInput.value = epicColor;
  if (pendingRun) prepare(pendingRun);

  const presentation = document.getElementById("strongboxPresentation");
  if (!presentation) return;

  let scheduled = false;

  function sectionTitle(section) {
    return section.querySelector("h3")?.textContent?.trim() || "";
  }

  function removeSections(titles) {
    presentation.querySelectorAll(".analysis-section").forEach(section => {
      if (titles.has(sectionTitle(section))) section.remove();
    });
  }

  function parseCount(value) {
    const digits = String(value || "").replace(/[^0-9]/g, "");
    return digits ? Number(digits) : 0;
  }

  function showNoAugmentBucket() {
    const summary = presentation.querySelector(".weapon-analysis-summary")?.textContent || "";
    const totalMatch = summary.match(/^(.+?)\s+drops\b/);
    const matrix = presentation.querySelector(".augment-matrix");
    const zeroSlotsLevelOne = matrix?.querySelector("tbody tr:first-child td:first-of-type");
    if (!totalMatch || !matrix || !zeroSlotsLevelOne) return;

    const total = parseCount(totalMatch[1]);
    let represented = 0;
    matrix.querySelectorAll("tbody td").forEach(cell => {
      if (cell === zeroSlotsLevelOne) return;
      represented += parseCount(cell.querySelector("strong")?.textContent);
    });

    const count = Math.max(0, total - represented);
    const percentage = total > 0 ? 100 * count / total : 0;
    const content = count > 0
      ? `<strong>${count.toLocaleString()}</strong><small>${percentage.toLocaleString(undefined, { maximumFractionDigits: 2 })}%</small>`
      : "—";

    if (zeroSlotsLevelOne.innerHTML !== content) {
      zeroSlotsLevelOne.innerHTML = content;
    }
    zeroSlotsLevelOne.classList.toggle("empty", count === 0);
    zeroSlotsLevelOne.title = "No augment slots. Shared augment level is 0; represented in the 0 slots / level 1 bucket.";
  }

  function cleanReport() {
    scheduled = false;

    const activeTab = presentation.querySelector(".analysis-tab.active")?.dataset.analysisTab || "";
    const rarityTab = presentation.querySelector('[data-analysis-tab="rarity"]');
    if (rarityTab && rarityTab.textContent !== "Rarity") rarityTab.textContent = "Rarity";

    presentation.querySelectorAll(".analysis-metric span").forEach(label => {
      if (label.textContent === "Target level") label.textContent = "Loot target";
      if (label.textContent === "Item level") label.textContent = "Generated level";
    });

    if (activeTab === "weapons") {
      removeSections(new Set(["Item levels", "Target levels", "Quality"]));
      presentation.querySelector(".weapon-analysis-picker")?.remove();
      presentation.querySelector(".analysis-weapon-head")?.remove();
      return;
    }

    if (activeTab === "rarity") {
      removeSections(new Set(["Quality"]));
      return;
    }

    if (activeTab === "augments") {
      removeSections(new Set([
        "Augment signatures (level/slots)",
        "Augment slots",
        "Augment levels"
      ]));
      showNoAugmentBucket();
      return;
    }

    if (activeTab === "levels") {
      presentation.querySelectorAll(".analysis-section h3").forEach(heading => {
        if (heading.textContent === "Item levels") heading.textContent = "Generated item levels";
        if (heading.textContent === "Target levels") heading.textContent = "Loot target levels";
      });
    }
  }

  function scheduleCleanup() {
    if (scheduled) return;
    scheduled = true;
    requestAnimationFrame(cleanReport);
  }

  presentation.addEventListener("click", event => {
    const weapon = event.target instanceof Element
      ? event.target.closest("[data-analysis-weapon]")
      : null;
    const activeTab = presentation.querySelector(".analysis-tab.active")?.dataset.analysisTab;
    if (!weapon || activeTab !== "weapons") return;

    // Let the existing handler retain the selected weapon, then open its only
    // remaining detail view: the augment slot/level matrix.
    queueMicrotask(() => {
      presentation.querySelector('[data-analysis-tab="augments"]')?.click();
    });
  }, true);

  new MutationObserver(scheduleCleanup).observe(presentation, {
    childList: true,
    subtree: true,
    characterData: true
  });

  scheduleCleanup();
})();
