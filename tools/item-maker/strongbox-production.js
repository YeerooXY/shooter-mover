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
    .production-fields .sample-field{display:none}.production-fields.analysis .sample-field{display:flex}
    .production-loading{opacity:.7;pointer-events:none}
    .presentation{display:none;position:absolute;inset:0;width:min(94vw,1180px);max-height:calc(100vh - 150px);margin:auto;padding:18px;overflow:auto;border:1px solid rgba(125,211,255,.3);border-radius:16px;background:rgba(1,13,27,.96);box-shadow:0 20px 70px rgba(0,0,0,.55);z-index:60}
    .presentation.visible{display:block}
    .presentation-head{display:grid;grid-template-columns:minmax(220px,.8fr) minmax(300px,1.2fr);gap:16px;margin-bottom:14px}
    .presentation-card,.presentation-diagnosis{padding:15px;border:1px solid rgba(145,220,255,.2);border-radius:13px;background:rgba(8,35,57,.72)}
    .presentation-card h2{margin:0 0 4px;font-size:27px;letter-spacing:.03em}.presentation-card .rarity-line{font-weight:850;letter-spacing:.1em}
    .presentation-card dl{display:grid;grid-template-columns:max-content 1fr;gap:5px 12px;margin:14px 0 0;font-size:12px}.presentation-card dt{color:#8eafc5}.presentation-card dd{margin:0;overflow-wrap:anywhere}
    .presentation-diagnosis h3,.analysis-section h3{margin:0 0 8px;font-size:14px;letter-spacing:.1em;text-transform:uppercase}.presentation-diagnosis p{margin:6px 0;color:#cbe5f4;font-size:13px;line-height:1.45}.presentation-diagnosis code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;color:#8de1ff}
    .weight-table-wrap{overflow:auto;border:1px solid rgba(145,220,255,.18);border-radius:12px}.weight-table{width:100%;border-collapse:collapse;font-size:11px;white-space:nowrap}.weight-table th{position:sticky;top:0;z-index:2;padding:8px;background:#082b46;color:#addcf4;text-align:right;text-transform:uppercase;letter-spacing:.07em}.weight-table th:nth-child(-n+3){text-align:left}.weight-table td{padding:7px 8px;border-top:1px solid rgba(145,220,255,.1);text-align:right}.weight-table td:nth-child(-n+3){text-align:left}.weight-table tr.selected{background:rgba(71,190,255,.16);box-shadow:inset 3px 0 #75d7ff}.weight-table tr.zero{opacity:.55}.weight-table .reason{font-family:ui-monospace,SFMono-Regular,Consolas,monospace}
    .weapon-result-name{display:grid;place-items:center;width:100%;height:100%;padding:10px;text-align:center;font-size:21px;font-weight:900;letter-spacing:.04em;text-shadow:0 3px 15px rgba(0,0,0,.8)}
    .bridge-error{display:grid;place-items:center;min-height:260px;padding:30px;text-align:center}.bridge-error strong{display:block;font-size:22px;margin-bottom:10px}.bridge-error code{display:block;margin-top:10px;color:#8de1ff}
    .analysis-metrics{display:grid;grid-template-columns:repeat(6,minmax(110px,1fr));gap:9px;margin-bottom:14px}.analysis-metric{padding:12px;border:1px solid rgba(145,220,255,.18);border-radius:12px;background:rgba(8,35,57,.72)}.analysis-metric span{display:block;color:#8eafc5;font-size:10px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}.analysis-metric strong{display:block;margin-top:5px;font-size:20px}
    .analysis-grid{display:grid;grid-template-columns:1.35fr 1fr;gap:14px}.analysis-section{min-width:0;padding:14px;border:1px solid rgba(145,220,255,.18);border-radius:12px;background:rgba(8,35,57,.55)}.analysis-section.full{grid-column:1/-1}
    .distribution{width:100%;border-collapse:collapse;font-size:12px}.distribution th,.distribution td{padding:7px 6px;border-top:1px solid rgba(145,220,255,.1);text-align:right}.distribution th:first-child,.distribution td:first-child{text-align:left}.distribution tbody tr:first-child td{border-top:0}
    .distribution-bar{display:inline-block;width:90px;height:7px;margin-right:8px;border-radius:99px;background:rgba(145,220,255,.12);vertical-align:middle;overflow:hidden}.distribution-bar i{display:block;height:100%;background:#73d8ff}
    .analysis-meta{margin:0 0 14px;color:#b8d7e8;font-size:12px}.analysis-meta code{color:#8de1ff}
    .weapon-analysis-picker{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:0 0 14px;padding:12px 14px;border:1px solid rgba(145,220,255,.18);border-radius:12px;background:rgba(8,35,57,.72)}.weapon-analysis-picker label{color:#9fc3d7;font-size:11px;font-weight:850;letter-spacing:.08em;text-transform:uppercase}.weapon-analysis-picker select{min-width:240px;min-height:38px;padding:0 10px;border:1px solid rgba(145,220,255,.3);border-radius:9px;background:#092c47;color:#eefaff}.weapon-analysis-summary{margin-left:auto;color:#cbe5f4;font-size:12px}.distribution .weapon-link{padding:0;border:0;background:none;color:#8de1ff;font:inherit;font-weight:800;text-align:left;cursor:pointer}.distribution .weapon-link:hover,.distribution .weapon-link:focus{text-decoration:underline}.weapon-drilldown{margin-top:14px}.weapon-drilldown-head{display:flex;align-items:end;justify-content:space-between;gap:12px;margin-bottom:10px}.weapon-drilldown-head h2{margin:0;font-size:22px}.weapon-drilldown-head p{margin:0;color:#9fc3d7;font-size:12px}
    .augment-matrix-section{grid-column:1/-1;min-width:0;padding:14px;border:1px solid rgba(145,220,255,.18);border-radius:12px;background:rgba(8,35,57,.55)}.augment-matrix-section h3{margin:0 0 6px;font-size:14px;letter-spacing:.1em;text-transform:uppercase}.augment-matrix-note{margin:0 0 10px;color:#9fc3d7;font-size:11px}.augment-matrix-wrap{overflow:auto;border:1px solid rgba(145,220,255,.16);border-radius:10px}.augment-matrix{width:100%;min-width:900px;border-collapse:collapse;table-layout:fixed;font-size:11px}.augment-matrix th,.augment-matrix td{padding:7px 5px;border-left:1px solid rgba(145,220,255,.1);border-top:1px solid rgba(145,220,255,.1);text-align:center}.augment-matrix thead th{position:sticky;top:0;background:#082b46;color:#addcf4}.augment-matrix th:first-child{position:sticky;left:0;z-index:2;width:86px;background:#082b46;text-align:left}.augment-matrix tbody th{color:#addcf4}.augment-matrix td strong{display:block;font-size:12px}.augment-matrix td small{display:block;margin-top:2px;color:#8eafc5;font-size:9px}.augment-matrix td.empty{color:rgba(169,196,213,.36)}
    @media(max-width:1000px){.analysis-metrics{grid-template-columns:repeat(3,1fr)}.analysis-grid{grid-template-columns:1fr}.analysis-section.full{grid-column:auto}.augment-matrix-section{grid-column:auto}}
    @media(max-width:900px){.presentation-head{grid-template-columns:1fr}.presentation{inset:60px 8px 80px;width:auto;max-height:none}.production-fields label span{display:none}}
    @media(max-width:560px){.analysis-metrics{grid-template-columns:repeat(2,1fr)}}
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

  const analysisButton = document.createElement("button");
  analysisButton.type = "button";
  analysisButton.className = "mode";
  analysisButton.dataset.mode = "analysis";
  analysisButton.textContent = "Analysis";
  controls.insertBefore(analysisButton, modeDivider);

  const productionFields = document.createElement("div");
  productionFields.className = "production-fields";
  productionFields.innerHTML = `
    <label><span>Level</span><input id="previewPlayerLevel" type="number" min="0" step="1" value="1"></label>
    <label><span>Box</span><select id="previewTier">${Array.from({ length: 11 }, (_, index) => `<option value="${index + 1}">Tier ${index + 1}</option>`).join("")}</select></label>
    <label><span>Seed</span><input id="previewSeed" data-seed inputmode="numeric" value=""></label>
    <label class="sample-field"><span>Samples</span><input id="previewSamples" type="number" min="1" max="10000" step="100" value="1000"></label>
    <button id="newPreviewSeed" type="button">New seed</button>`;
  modeDivider.insertAdjacentElement("afterend", productionFields);

  const playerLevelInput = document.getElementById("previewPlayerLevel");
  const tierInput = document.getElementById("previewTier");
  const seedInput = document.getElementById("previewSeed");
  const sampleInput = document.getElementById("previewSamples");
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

  function showPanel() {
    reelShell.style.display = "none";
    meterShell.style.display = "none";
    presentation.classList.add("visible");
  }

  function showPresentation(run) {
    showPanel();
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
        <article class="presentation-card">
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
          <p>Live catalogue: <code>${escapeText(reward.catalogAuthority)}</code></p>
          <p>Definitions: <code>${reward.catalogDefinitionCount}</code></p>
          <p>Fingerprint: <code>${escapeText(reward.catalogFingerprint)}</code></p>
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

  function distributionTable(title, entries, options = {}) {
    const rows = (entries || []).slice(0, options.limit || 1000);
    if (!rows.length) {
      return `<section class="analysis-section"><h3>${escapeText(title)}</h3><p>No results.</p></section>`;
    }
    const maximum = Math.max(...rows.map(entry => Number(entry.percentage) || 0), 0.0001);
    return `
      <section class="analysis-section ${options.full ? "full" : ""}">
        <h3>${escapeText(title)}</h3>
        <table class="distribution">
          <thead><tr><th>Result</th><th>Count</th><th>Share</th></tr></thead>
          <tbody>${rows.map(entry => `
            <tr>
              <td><span class="distribution-bar"><i style="width:${Math.max(0, Math.min(100, Number(entry.percentage) / maximum * 100))}%"></i></span>${escapeText(entry.label)}</td>
              <td>${formatNumber(entry.count, 0)}</td>
              <td>${formatNumber(entry.percentage, 3)}%</td>
            </tr>`).join("")}</tbody>
        </table>
      </section>`;
  }

  function weaponDistributionTable(entries) {
    const rows = entries || [];
    if (!rows.length) {
      return `<section class="analysis-section full"><h3>Weapon results</h3><p>No results.</p></section>`;
    }
    const maximum = Math.max(...rows.map(entry => Number(entry.percentage) || 0), 0.0001);
    return `
      <section class="analysis-section full">
        <h3>Weapon results — click a weapon to inspect it</h3>
        <table class="distribution">
          <thead><tr><th>Weapon</th><th>Count</th><th>Share</th></tr></thead>
          <tbody>${rows.map(entry => `
            <tr>
              <td><span class="distribution-bar"><i style="width:${Math.max(0, Math.min(100, Number(entry.percentage) / maximum * 100))}%"></i></span><button type="button" class="weapon-link" data-weapon-key="${escapeText(entry.key)}">${escapeText(entry.label)}</button></td>
              <td>${formatNumber(entry.count, 0)}</td>
              <td>${formatNumber(entry.percentage, 3)}%</td>
            </tr>`).join("")}</tbody>
        </table>
      </section>`;
  }

  function augmentMatrixTable(detail) {
    const cells = new Map(
      (detail.augmentMatrix || []).map(cell => [`${cell.slots}:${cell.level}`, cell])
    );
    const levels = Array.from({ length: 12 }, (_, index) => index + 1);
    const slots = Array.from({ length: 5 }, (_, index) => index);
    return `
      <section class="augment-matrix-section">
        <h3>Augment slots × augment level</h3>
        <p class="augment-matrix-note">Rows are slot counts 0–4. Columns are shared augment levels 1–12. Each cell shows drops and share of this weapon's drops.</p>
        <div class="augment-matrix-wrap">
          <table class="augment-matrix">
            <thead><tr><th>Slots \\ Level</th>${levels.map(level => `<th>${level}</th>`).join("")}</tr></thead>
            <tbody>${slots.map(slot => `
              <tr>
                <th>${slot}</th>
                ${levels.map(level => {
                  const cell = cells.get(`${slot}:${level}`) || { count: 0, percentage: 0 };
                  return `<td class="${Number(cell.count) ? "" : "empty"}">${Number(cell.count) ? `<strong>${formatNumber(cell.count, 0)}</strong><small>${formatNumber(cell.percentage, 2)}%</small>` : "—"}</td>`;
                }).join("")}
              </tr>`).join("")}</tbody>
          </table>
        </div>
      </section>`;
  }

  function renderWeaponDrilldown(report, definitionId) {
    const detail = (report.weaponBreakdowns || []).find(value => value.definitionId === definitionId);
    const target = presentation.querySelector("#weaponDrilldown");
    const summary = presentation.querySelector("#weaponAnalysisSummary");
    if (!target) return;
    if (!detail) {
      target.innerHTML = `<div class="bridge-error"><div><strong>No weapon detail found</strong>The selected weapon was not present in this analysis.</div></div>`;
      if (summary) summary.textContent = "";
      return;
    }
    if (summary) {
      summary.textContent = `${formatNumber(detail.count, 0)} drops · ${formatNumber(detail.percentage, 3)}% of successful openings`;
    }
    target.innerHTML = `
      <div class="weapon-drilldown-head">
        <div><h2>${escapeText(detail.displayName)}</h2><p>${escapeText(detail.definitionId)}</p></div>
        <p>${formatNumber(detail.count, 0)} of ${formatNumber(report.successfulOpenings, 0)} successful openings</p>
      </div>
      <div class="analysis-grid">
        ${augmentMatrixTable(detail)}
        ${distributionTable("Item levels", detail.itemLevelDistribution)}
        ${distributionTable("Target levels", detail.targetLevelDistribution)}
        ${distributionTable("Quality", detail.qualityDistribution)}
      </div>`;
  }

  function showAnalysis(report) {
    showPanel();
    const rejection = Number(report.rejectedOpenings || 0);
    presentation.innerHTML = `
      <p class="analysis-meta">
        Every sample was opened through the production Strongbox resolver using
        <code>${escapeText(report.catalogAuthority)}</code>.
        Catalogue fingerprint: <code>${escapeText(report.catalogFingerprint)}</code>
        · ${report.catalogDefinitionCount} definitions
        · seed ${escapeText(report.seed)}
      </p>
      <div class="analysis-metrics">
        <div class="analysis-metric"><span>Openings</span><strong>${formatNumber(report.sampleCount, 0)}</strong></div>
        <div class="analysis-metric"><span>Successful</span><strong>${formatNumber(report.successfulOpenings, 0)}</strong></div>
        <div class="analysis-metric"><span>Rejected</span><strong>${formatNumber(rejection, 0)}</strong></div>
        <div class="analysis-metric"><span>Target level</span><strong>${formatNumber(report.averageTargetLevel, 2)}</strong><small>${report.minimumTargetLevel}–${report.maximumTargetLevel}</small></div>
        <div class="analysis-metric"><span>Item level</span><strong>${formatNumber(report.averageItemLevel, 2)}</strong><small>${report.minimumItemLevel}–${report.maximumItemLevel}</small></div>
        <div class="analysis-metric"><span>Tier / player</span><strong>${report.tierNumber} / ${report.playerLevel}</strong></div>
      </div>
      <div class="analysis-grid">
        ${weaponDistributionTable(report.weaponDistribution)}
        ${distributionTable("Rarity", report.rarityDistribution)}
        ${distributionTable("Augment signatures (level/slots)", report.augmentSignatureDistribution)}
        ${distributionTable("Augment slots", report.augmentSlotDistribution)}
        ${distributionTable("Augment levels", report.augmentLevelDistribution)}
        ${distributionTable("Item levels", report.itemLevelDistribution)}
        ${distributionTable("Target levels", report.targetLevelDistribution)}
        ${distributionTable("Quality", report.qualityDistribution)}
        ${rejection ? distributionTable("Rejected openings", report.rejectionDistribution, { full: true }) : ""}
      </div>
      <div class="weapon-drilldown">
        <div class="weapon-analysis-picker">
          <label for="analysisWeaponFilter">Inspect one weapon</label>
          <select id="analysisWeaponFilter">${(report.weaponBreakdowns || []).map(detail => `<option value="${escapeText(detail.definitionId)}">${escapeText(detail.displayName)}</option>`).join("")}</select>
          <span id="weaponAnalysisSummary" class="weapon-analysis-summary"></span>
        </div>
        <div id="weaponDrilldown"></div>
      </div>`;

    const weaponFilter = presentation.querySelector("#analysisWeaponFilter");
    const firstWeapon = weaponFilter?.value || report.weaponBreakdowns?.[0]?.definitionId;
    if (firstWeapon) renderWeaponDrilldown(report, firstWeapon);
    weaponFilter?.addEventListener("change", () => renderWeaponDrilldown(report, weaponFilter.value));
    presentation.querySelectorAll("[data-weapon-key]").forEach(button => {
      button.addEventListener("click", () => {
        const definitionId = button.dataset.weaponKey;
        if (weaponFilter) weaponFilter.value = definitionId;
        renderWeaponDrilldown(report, definitionId);
        presentation.querySelector("#weaponDrilldown")?.scrollIntoView({ behavior: "smooth", block: "start" });
      });
    });
  }

  function showError(error) {
    showPanel();
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
    if (swatch) {
      swatch.innerHTML = `<div class="weapon-result-name">${escapeText(run.reward.selectedName)}</div>`;
    }
    if (label) {
      label.textContent = `${rarityView(run.reward.selectedRarityVisualId).label} · LV ${run.reward.itemLevel}`;
    }
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

  async function resolveProduction(mode) {
    const response = await fetch("/api/strongbox-preview", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        mode,
        playerLevel: Number(playerLevelInput.value),
        tierNumber: Number(tierInput.value),
        seed: seedInput.value.trim(),
        sampleCount: Number(sampleInput.value)
      })
    });
    const payload = await response.json();
    if (!response.ok || !payload.ok) {
      throw new Error(payload.error || "Strongbox preview failed.");
    }
    return payload;
  }

  async function runAnalysis(useFreshSeed = false) {
    if (running) return;
    if (useFreshSeed) seedInput.value = freshSeed();
    setLoading(true, "RUNNING PRODUCTION ANALYSIS");
    try {
      const report = await resolveProduction("analysis");
      pendingRun = null;
      lastRun = null;
      screenState = "complete";
      winnerEl.textContent = `${formatNumber(report.sampleCount, 0)} production openings`;
      rarityEl.textContent = `${report.catalogDefinitionCount} live definitions · ${formatNumber(report.successfulOpenings, 0)} successful`;
      openBtn.textContent = "Run analysis";
      replayBtn.disabled = true;
      showAnalysis(report);
    } catch (error) {
      showError(error);
    } finally {
      setLoading(false);
    }
  }

  async function productionPrepareNextRun(useFreshSeed = false) {
    if (running) return;
    if (selectedMode === "analysis") {
      await runAnalysis(useFreshSeed);
      return;
    }
    if (useFreshSeed) seedInput.value = freshSeed();
    setLoading(true);
    try {
      const reward = await resolveProduction("single");
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
    if (selectedMode === "analysis") {
      await runAnalysis(true);
      return;
    }
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

  function activateMode(button, mode) {
    if (running) return;
    selectedMode = mode;
    document.querySelectorAll(".mode").forEach(value => value.classList.toggle("active", value === button));
    productionFields.classList.toggle("analysis", mode === "analysis");
    if (mode === "analysis") {
      openBtn.textContent = "Run analysis";
      replayBtn.disabled = true;
      showPanel();
      presentation.innerHTML = `<div class="bridge-error"><div><strong>Production Strongbox analysis</strong>Choose a level, tier, seed, and sample count, then run the analysis.</div></div>`;
      return;
    }
    if (mode === "presentation") {
      openBtn.textContent = "Roll again";
      if (pendingRun?.reward) {
        lastRun = pendingRun;
        screenState = "complete";
        showPresentation(pendingRun);
      } else {
        productionPrepareNextRun(false);
      }
      return;
    }
    showReel();
    openBtn.textContent = pendingRun?.reward ? "Open" : "Prepare";
    if (pendingRun?.reward) prepare(pendingRun);
  }

  prepareNextRun = productionPrepareNextRun;
  openOrPrepare = productionOpenOrPrepare;
  openBtn.onclick = productionOpenOrPrepare;
  replayBtn.onclick = () => {
    if (!lastRun) return;
    if (selectedMode === "presentation") {
      showPresentation(lastRun);
    } else if (selectedMode !== "analysis") {
      showReel();
      play(lastRun, selectedMode);
    }
  };

  document.querySelectorAll('.mode:not([data-mode="presentation"]):not([data-mode="analysis"])').forEach(button => {
    button.addEventListener("click", () => activateMode(button, button.dataset.mode));
  });
  presentationButton.onclick = () => activateMode(presentationButton, "presentation");
  analysisButton.onclick = () => activateMode(analysisButton, "analysis");

  newSeedButton.onclick = async () => {
    seedInput.value = freshSeed();
    if (selectedMode === "analysis") {
      await runAnalysis(false);
    } else {
      await productionPrepareNextRun(false);
    }
  };

  [playerLevelInput, tierInput, seedInput].forEach(input => input.addEventListener("change", () => {
    if (selectedMode === "analysis") return;
    productionPrepareNextRun(false);
  }));

  const rarityHeading = document.querySelector("#rarityEditor h3");
  if (rarityHeading) rarityHeading.textContent = "Visual filler rarity weights and colors";
  const visualNote = document.createElement("p");
  visualNote.className = "help";
  visualNote.textContent = "These settings affect filler cards only. Every winner and every analysis sample comes from Unity's current production Strongbox solver and live gun catalogue.";
  document.getElementById("rarityEditor")?.appendChild(visualNote);

  winnerEl.textContent = "Resolving production Strongbox";
  rarityEl.textContent = "Unity decides the reward before presentation";
  productionPrepareNextRun(false);
})();
