"use strict";

(() => {
  const prototypeCreateRun = createRun;
  const prototypePrepare = prepare;
  const prototypeFinishRun = finishRun;
  const controls = document.querySelector(".controls");
  const stage = document.querySelector(".stage");
  const reelShell = document.querySelector(".reel-shell");
  const meterShell = document.querySelector(".meter");

  const style = document.createElement("style");
  style.textContent = `
    .production-fields{display:flex;align-items:center;gap:7px;flex-wrap:wrap}
    .production-fields label{display:flex;align-items:center;gap:5px;color:#a9c4d5;font-size:10px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}
    .production-fields input,.production-fields select{width:76px;min-height:38px;padding:0 8px;border:1px solid rgba(145,220,255,.32);background:rgba(12,45,73,.82);color:#eaf8ff;border-radius:9px}
    .production-fields input[data-seed]{width:142px;font-family:ui-monospace,SFMono-Regular,Consolas,monospace}
    .production-loading{opacity:.7;pointer-events:none}
    .presentation{display:none;position:absolute;inset:0;width:min(94vw,1120px);max-height:calc(100vh - 150px);margin:auto;padding:18px;overflow:auto;border:1px solid rgba(125,211,255,.3);border-radius:16px;background:rgba(1,13,27,.94);box-shadow:0 20px 70px rgba(0,0,0,.55);z-index:60}
    .presentation.visible{display:block}
    .presentation-head{display:grid;grid-template-columns:minmax(220px,.8fr) minmax(300px,1.2fr);gap:16px;margin-bottom:14px}
    .presentation-card,.presentation-diagnosis{padding:15px;border:1px solid rgba(145,220,255,.2);border-radius:13px;background:rgba(8,35,57,.72)}
    .presentation-card h2{margin:0 0 4px;font-size:27px;letter-spacing:.03em}.presentation-card .rarity-line{font-weight:850;letter-spacing:.1em}.presentation-card dl{display:grid;grid-template-columns:max-content 1fr;gap:5px 12px;margin:14px 0 0;font-size:12px}.presentation-card dt{color:#8eafc5}.presentation-card dd{margin:0;overflow-wrap:anywhere}
    .presentation-diagnosis h3{margin:0 0 8px;font-size:14px;letter-spacing:.1em;text-transform:uppercase}.presentation-diagnosis p{margin:6px 0;color:#cbe5f4;font-size:13px;line-height:1.45}.presentation-diagnosis code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;color:#8de1ff}
    .weight-table-wrap{overflow:auto;border:1px solid rgba(145,220,255,.18);border-radius:12px}.weight-table{width:100%;border-collapse:collapse;font-size:11px;white-space:nowrap}.weight-table th{position:sticky;top:0;z-index:2;padding:8px;background:#082b46;color:#addcf4;text-align:right;text-transform:uppercase;letter-spacing:.07em}.weight-table th:nth-child(-n+3){text-align:left}.weight-table td{padding:7px 8px;border-top:1px solid rgba(145,220,255,.1);text-align:right}.weight-table td:nth-child(-n+3){text-align:left}.weight-table tr.selected{background:rgba(71,190,255,.16);box-shadow:inset 3px 0 #75d7ff}.weight-table tr.zero{opacity:.55}.weight-table .reason{font-family:ui-monospace,SFMono-Regular,Consolas,monospace}
    .weapon-result-name{display:grid;place-items:center;width:100%;height:100%;padding:10px;text-align:center;font-size:21px;font-weight:900;letter-spacing:.04em;text-shadow:0 3px 15px rgba(0,0,0,.8)}
    .bridge-error{display:grid;place-items:center;min-height:260px;padding:30px;text-align:center}.bridge-error strong{display:block;font-size:22px;margin-bottom:10px}.bridge-error code{display:block;margin-top:10px;color:#8de1ff}
    @media(max-width:900px){.presentation-head{grid-template-columns:1fr}.presentation{inset:60px 8px 80px;width:auto;max-height:none}.production-fields label span{display:none}}
  `;
  document.head.appendChild(style);

  const presentation = document.createElement("section");
  presentation.id = "strongboxPresentation";
  presentation.className = "presentation";
  stage.appendChild(presentation);

  force.hidden = true;
  force.setAttribute("aria-hidden", "true");

  const modeDivider = controls.querySelector(".divider");
  const presentationButton = document.createElement("button");
  presentationButton.type = "button";
  presentationButton.className = "mode";
  presentationButton.dataset.mode = "presentation";
  presentationButton.textContent = "Presentation / debug";
  controls.insertBefore(presentationButton, modeDivider);

  const productionFields = document.createElement("div");
  productionFields.className = "production-fields";
  productionFields.innerHTML = `
    <label><span>Level</span><input id="previewPlayerLevel" type="number" min="0" step="1" value="1"></label>
    <label><span>Box</span><select id="previewTier">${Array.from({ length: 11 }, (_, index) => `<option value="${index + 1}">Tier ${index + 1}</option>`).join("")}</select></label>
    <label><span>Seed</span><input id="previewSeed" data-seed inputmode="numeric" value=""></label>
    <button id="newPreviewSeed" type="button">New seed</button>`;
  modeDivider.insertAdjacentElement("afterend", productionFields);

  const playerLevelInput = document.getElementById("previewPlayerLevel");
  const tierInput = document.getElementById("previewTier");
  const seedInput = document.getElementById("previewSeed");
  const newSeedButton = document.getElementById("newPreviewSeed");

  function freshSeed() {
    const words = new Uint32Array(2);
    crypto.getRandomValues(words);
    return ((BigInt(words[0]) << 32n) | BigInt(words[1])).toString();
  }
  seedInput.value = freshSeed();

  function escapeText(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");
  }
  function formatNumber(value, digits = 6) {
    const number = Number(value || 0);
    if (number === 0) return "0";
    if (Math.abs(number) < 0.0001) return number.toExponential(3);
    return number.toLocaleString(undefined, { maximumFractionDigits: digits });
  }
  function rarityView(id) {
    return rarityById(id || "common");
  }
  function setLoading(loading, message = "RESOLVING IN UNITY") {
    controls.classList.toggle("production-loading", loading);
    openBtn.disabled = loading;
    replayBtn.disabled = loading || !lastRun;
    statusEl.textContent = loading ? message : statusEl.textContent;
  }
  function showReel() {
    presentation.classList.remove("visible");
    reelShell.style.display = "";
    meterShell.style.display = "";
  }
  function showPresentation(run) {
    reelShell.style.display = "none";
    meterShell.style.display = "none";
    presentation.classList.add("visible");
    const reward = run?.reward;
    if (!reward) {
      presentation.innerHTML = `<div class="bridge-error"><div><strong>No production result prepared</strong>Choose a level, tier, and seed, then roll again.</div></div>`;
      return;
    }

    const candidates = [...(reward.candidates || [])].sort((left, right) =>
      Number(right.finalWeight) - Number(left.finalWeight)
      || String(left.definitionId).localeCompare(String(right.definitionId))
    );
    const positive = candidates.filter(candidate => Number(candidate.finalWeight) > 0);
    const selected = candidates.find(candidate => candidate.selected);
    const rarity = rarityView(reward.selectedRarityVisualId);
    let diagnosis;
    if (positive.length === 1) {
      diagnosis = `<strong>${escapeText(positive[0].displayName)}</strong> is the only weapon with positive weight, so every seed must select it.`;
    } else if (selected && Number(selected.chancePercent) >= 90) {
      diagnosis = `<strong>${escapeText(selected.displayName)}</strong> owns ${formatNumber(selected.chancePercent, 3)}% of the current weighted pool.`;
    } else {
      diagnosis = `${positive.length} weapons have positive weight. The selected weapon owned ${formatNumber(selected?.chancePercent, 3)}% of the pool.`;
    }

    presentation.innerHTML = `
      <div class="presentation-head">
        <article class="presentation-card" style="--winner-color:${rarity.color};--winner-glow:${rarity.glow}">
          <h2 style="color:${rarity.glow}">${escapeText(reward.selectedName)}</h2>
          <div class="rarity-line" style="color:${rarity.color}">${escapeText(reward.selectedRarityId)}</div>
          <dl>
            <dt>Definition</dt><dd>${escapeText(reward.selectedDefinitionId)}</dd>
            <dt>Player / target</dt><dd>${reward.playerLevel} → ${reward.targetLevel}</dd>
            <dt>Target delta</dt><dd>${reward.minimumTargetDelta} / ${reward.mostLikelyTargetDelta} / ${reward.maximumTargetDelta}</dd>
            <dt>Item level</dt><dd>${reward.itemLevel}</dd>
            <dt>Quality</dt><dd>${escapeText(reward.qualityId)}</dd>
            <dt>Augments</dt><dd>${reward.augmentSlots ? `${reward.augmentLevel}/${reward.augmentSlots}` : "none"}</dd>
            <dt>Tier</dt><dd>${reward.tierNumber} · ${escapeText(reward.tierId)}</dd>
            <dt>Seed</dt><dd>${escapeText(reward.seed)}</dd>
          </dl>
        </article>
        <article class="presentation-diagnosis">
          <h3>Why this weapon?</h3>
          <p>${diagnosis}</p>
          <p>Target level <strong>${reward.targetLevel}</strong> was rolled from player level ${reward.playerLevel}. Weapons outside the tier policy's level window receive zero weight.</p>
          <p>Production catalogue: <code>${escapeText(reward.catalogSource || "unknown")}</code></p>
          <p>Total eligible weight: <code>${formatNumber(reward.totalWeight)}</code></p>
        </article>
      </div>
      <div class="weight-table-wrap">
        <table class="weight-table">
          <thead><tr><th>Weapon</th><th>Rarity</th><th>Reason</th><th>First</th><th>Peak</th><th>Distance</th><th>Base</th><th>Level affinity</th><th>Rarity ×</th><th>Final weight</th><th>Chance</th></tr></thead>
          <tbody>${candidates.map(candidate => `
            <tr class="${candidate.selected ? "selected" : ""} ${Number(candidate.finalWeight) <= 0 ? "zero" : ""}">
              <td>${candidate.selected ? "▶ " : ""}${escapeText(candidate.displayName)}<br><small>${escapeText(candidate.definitionId)}</small></td>
              <td>${escapeText(candidate.rarityId)}</td>
              <td class="reason">${escapeText(candidate.reason)}</td>
              <td>${candidate.firstAppearanceLevel}</td>
              <td>${candidate.peakLevel}</td>
              <td>${candidate.distance}</td>
              <td>${formatNumber(candidate.baseWeight)}</td>
              <td>${formatNumber(candidate.levelAffinity)}</td>
              <td>${formatNumber(candidate.rarityMultiplier)}</td>
              <td>${formatNumber(candidate.finalWeight)}</td>
              <td>${formatNumber(candidate.chancePercent, 4)}%</td>
            </tr>`).join("")}</tbody>
        </table>
      </div>`;
  }
  function showError(error) {
    reelShell.style.display = "none";
    meterShell.style.display = "none";
    presentation.classList.add("visible");
    presentation.innerHTML = `<div class="bridge-error"><div><strong>Strongbox preview unavailable</strong>${escapeText(error.message || error)}<code>Open Unity, let scripts compile, and keep the Item Maker server running.</code></div></div>`;
    winnerEl.textContent = "Unity bridge unavailable";
    rarityEl.textContent = "No fallback raffle was used";
  }
  function decorateWinner(run) {
    if (!run?.reward) return;
    const card = reel.children[run.winnerIndex];
    if (!card) return;
    const swatch = card.querySelector(".swatch");
    const label = card.querySelector(".label");
    if (swatch) swatch.innerHTML = `<div class="weapon-result-name">${escapeText(run.reward.selectedName)}</div>`;
    if (label) label.textContent = `${rarityView(run.reward.selectedRarityVisualId).label} · LV ${run.reward.itemLevel}`;
  }

  prepare = function productionPrepare(run) {
    prototypePrepare(run);
    decorateWinner(run);
  };
  finishRun = function productionFinish(run) {
    prototypeFinishRun(run);
    if (!run?.reward) return;
    const rarity = rarityView(run.reward.selectedRarityVisualId);
    winnerEl.textContent = run.reward.selectedName;
    rarityEl.textContent = `${rarity.label} · ITEM LEVEL ${run.reward.itemLevel} · ${run.reward.augmentSlots ? `${run.reward.augmentLevel}/${run.reward.augmentSlots}` : "NO AUGMENTS"}`;
    winnerEl.style.color = rarity.glow;
    rarityEl.style.color = rarity.color;
  };

  function createResolvedRun(reward) {
    const run = prototypeCreateRun(reward.selectedRarityVisualId || "common");
    run.reward = reward;
    run.items[run.winnerIndex] = reward.selectedRarityVisualId || "common";
    return run;
  }
  async function resolveProductionReward() {
    const response = await fetch("/api/strongbox-preview", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        playerLevel: Number(playerLevelInput.value),
        tierNumber: Number(tierInput.value),
        seed: seedInput.value.trim()
      })
    });
    const payload = await response.json();
    if (!response.ok || !payload.ok) throw new Error(payload.error || "Strongbox preview failed.");
    return payload;
  }
  async function productionPrepareNextRun(useFreshSeed = false) {
    if (running) return;
    if (useFreshSeed) seedInput.value = freshSeed();
    setLoading(true);
    try {
      const reward = await resolveProductionReward();
      pendingRun = createResolvedRun(reward);
      prepare(pendingRun);
      screenState = "ready";
      openBtn.textContent = selectedMode === "presentation" ? "Roll again" : "Open";
      replayBtn.disabled = false;
      winnerEl.textContent = reward.selectedName;
      rarityEl.textContent = `Prepared by production solver · target ${reward.targetLevel}`;
      if (selectedMode === "presentation") {
        lastRun = pendingRun;
        screenState = "complete";
        showPresentation(pendingRun);
      } else {
        showReel();
      }
    } catch (error) {
      pendingRun = null;
      showError(error);
    } finally {
      setLoading(false);
    }
  }
  async function productionOpenOrPrepare() {
    if (running) return;
    if (selectedMode === "presentation") {
      await productionPrepareNextRun(true);
      return;
    }
    if (!pendingRun?.reward || screenState === "complete") {
      await productionPrepareNextRun(true);
      return;
    }
    showReel();
    lastRun = pendingRun;
    play(lastRun, selectedMode);
  }

  prepareNextRun = productionPrepareNextRun;
  openOrPrepare = productionOpenOrPrepare;
  openBtn.onclick = productionOpenOrPrepare;
  replayBtn.onclick = () => {
    if (!lastRun) return;
    if (selectedMode === "presentation") showPresentation(lastRun);
    else {
      showReel();
      play(lastRun, selectedMode);
    }
  };

  document.querySelectorAll('.mode:not([data-mode="presentation"])').forEach(button => {
    button.addEventListener("click", () => {
      if (running) return;
      showReel();
      if (pendingRun?.reward) prepare(pendingRun);
    });
  });
  presentationButton.onclick = async () => {
    if (running) return;
    selectedMode = "presentation";
    document.querySelectorAll(".mode").forEach(button => button.classList.toggle("active", button === presentationButton));
    if (pendingRun?.reward) {
      lastRun = pendingRun;
      screenState = "complete";
      openBtn.textContent = "Roll again";
      showPresentation(pendingRun);
    } else await productionPrepareNextRun(false);
  };

  newSeedButton.onclick = async () => {
    seedInput.value = freshSeed();
    await productionPrepareNextRun(false);
  };
  [playerLevelInput, tierInput].forEach(input => input.addEventListener("change", () => productionPrepareNextRun(false)));
  seedInput.addEventListener("change", () => productionPrepareNextRun(false));

  const rarityHeading = document.querySelector("#rarityEditor h3");
  if (rarityHeading) rarityHeading.textContent = "Visual filler rarity weights and colors";
  const visualNote = document.createElement("p");
  visualNote.className = "help";
  visualNote.textContent = "These settings affect filler cards only. The winner always comes from Unity's production Strongbox solver.";
  document.getElementById("rarityEditor")?.appendChild(visualNote);

  winnerEl.textContent = "Resolving production Strongbox";
  rarityEl.textContent = "Unity decides the reward before presentation";
  productionPrepareNextRun(false);
})();
