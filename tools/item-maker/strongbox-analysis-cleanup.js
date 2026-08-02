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

  const presentation = document.getElementById("strongboxPresentation");
  if (!presentation) return;

  const productionPrepare = prepare;
  const epicAnalysisDefinitions = new Set();
  let scheduled = false;

  function visualRarityId(rarityId) {
    const value = String(rarityId || "").toLowerCase();
    if (value.includes("artifact") || value.includes("mythic")) return "mythic";
    if (value.includes("legendary")) return "legendary";
    if (value.includes("epic")) return "epic";
    if (value.includes("rare") && !value.includes("uncommon")) return "rare";
    return "common";
  }

  function seedHash(value) {
    let hash = 2166136261;
    for (const character of String(value || "")) {
      hash ^= character.charCodeAt(0);
      hash = Math.imul(hash, 16777619);
    }
    return hash >>> 0;
  }

  function deterministicRandom(seed) {
    let state = seedHash(seed);
    return () => {
      state = (state + 0x6D2B79F5) >>> 0;
      let value = state;
      value = Math.imul(value ^ (value >>> 15), value | 1);
      value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
      return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
    };
  }

  function sampleCandidate(candidates, totalWeight, random) {
    let cursor = random() * totalWeight;
    for (const candidate of candidates) {
      cursor -= Number(candidate.finalWeight);
      if (cursor <= 0) return candidate;
    }
    return candidates[candidates.length - 1];
  }

  function buildCandidateReel(run) {
    if (!run?.reward || !Array.isArray(run.items)) return;
    if (run.previewWeapons?.length === run.items.length) return;

    const candidates = (run.reward.candidates || []).filter(
      candidate => Number(candidate.finalWeight) > 0
    );
    const totalWeight = candidates.reduce(
      (sum, candidate) => sum + Number(candidate.finalWeight),
      0
    );
    if (!candidates.length || totalWeight <= 0) return;

    const random = deterministicRandom([
      "strongbox-candidate-reel-v1",
      run.reward.seed,
      run.reward.targetLevel,
      run.reward.selectedDefinitionId
    ].join(":"));

    run.previewWeapons = run.items.map(() => {
      const candidate = sampleCandidate(candidates, totalWeight, random);
      return {
        definitionId: String(candidate.definitionId || ""),
        displayName: String(candidate.displayName || candidate.definitionId || "Unknown weapon"),
        rarityVisualId: visualRarityId(candidate.rarityId),
        peakLevel: candidate.peakLevel,
        chancePercent: candidate.chancePercent,
        winner: false
      };
    });

    const winnerIndex = Number(run.winnerIndex);
    const selected = candidates.find(candidate => candidate.selected)
      || candidates.find(candidate => candidate.definitionId === run.reward.selectedDefinitionId);
    run.previewWeapons[winnerIndex] = {
      definitionId: String(run.reward.selectedDefinitionId || ""),
      displayName: String(run.reward.selectedName || run.reward.selectedDefinitionId || "Unknown weapon"),
      rarityVisualId: run.reward.selectedRarityVisualId || visualRarityId(run.reward.selectedRarityId),
      peakLevel: selected?.peakLevel ?? run.reward.targetLevel,
      chancePercent: selected?.chancePercent ?? 0,
      winner: true
    };

    run.previewWeapons.forEach((weapon, index) => {
      run.items[index] = weapon.rarityVisualId;
    });
  }

  function decorateCandidateReel(run) {
    if (!run?.previewWeapons) return;

    run.previewWeapons.forEach((weapon, index) => {
      const card = reel.children[index];
      if (!card) return;

      const rarity = rarityById(weapon.rarityVisualId);
      const swatch = card.querySelector(".swatch");
      const label = card.querySelector(".label");
      if (swatch) {
        swatch.replaceChildren();
        const name = document.createElement("div");
        name.className = "weapon-result-name";
        name.textContent = weapon.displayName;
        if (weapon.displayName.length > 20) name.style.fontSize = "15px";
        else if (weapon.displayName.length > 15) name.style.fontSize = "18px";
        swatch.appendChild(name);
      }
      if (label) {
        label.textContent = weapon.winner
          ? `${rarity.label} · LV ${run.reward.itemLevel}`
          : `${rarity.label} · PEAK LV ${weapon.peakLevel}`;
      }
      card.title = `${weapon.displayName}\n${weapon.definitionId}\n${Number(weapon.chancePercent || 0).toFixed(3)}% of this target-level pool`;
    });
  }

  prepare = function candidateReelPrepare(run) {
    buildCandidateReel(run);
    productionPrepare(run);
    decorateCandidateReel(run);
  };

  if (pendingRun) prepare(pendingRun);

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

  function applyAnalysisEpicPalette() {
    presentation.querySelectorAll("[data-analysis-weapon]").forEach(button => {
      const chip = button.querySelector(".analysis-rarity-chip");
      if (chip?.textContent?.trim().toLowerCase() !== "epic") return;
      const definitionId = button.dataset.analysisWeapon || "";
      if (definitionId) epicAnalysisDefinitions.add(definitionId);
      button.style.color = epicGlow;
      button.style.textShadow = `0 0 12px ${epicColor}55`;
      chip.style.color = epicColor;
      const distributionFill = button.closest("td")?.querySelector(".distribution-bar i");
      if (distributionFill) distributionFill.style.background = epicColor;
    });

    const selectedDefinitionId = presentation.querySelector("#analysisWeaponFilter")?.value || "";
    if (!epicAnalysisDefinitions.has(selectedDefinitionId)) return;
    const heading = presentation.querySelector(".analysis-weapon-head h2");
    if (!heading) return;
    heading.style.color = epicGlow;
    heading.style.textShadow = `0 0 16px ${epicColor}66`;
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
    applyAnalysisEpicPalette();

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