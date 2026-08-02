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

  buildEditors();
  if (pendingRun) prepare(pendingRun);
})();
