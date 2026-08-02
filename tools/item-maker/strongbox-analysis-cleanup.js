"use strict";

(() => {
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
