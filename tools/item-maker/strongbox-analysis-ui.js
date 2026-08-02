"use strict";

(() => {
  const controls = document.querySelector(".controls");
  const presentation = document.getElementById("strongboxPresentation");
  const analysisButton = document.querySelector('.mode[data-mode="analysis"]');
  const openButton = document.getElementById("open");
  const replayButton = document.getElementById("replay");
  const sampleInput = document.getElementById("previewSamples");
  const playerLevelInput = document.getElementById("previewPlayerLevel");
  const tierInput = document.getElementById("previewTier");
  const seedInput = document.getElementById("previewSeed");
  const newSeedButton = document.getElementById("newPreviewSeed");
  const winner = document.getElementById("winner");
  const rarity = document.getElementById("rarity");

  if (!controls || !presentation || !analysisButton || !openButton || !sampleInput
      || !playerLevelInput || !tierInput || !seedInput || !newSeedButton) {
    return;
  }

  const style = document.createElement("style");
  style.textContent = `
    body.analysis-workspace .app{grid-template-columns:1fr;padding:0}
    body.analysis-workspace .app>.side{display:none}
    body.analysis-workspace .stage{width:100%;max-width:none;height:100vh;min-height:0;margin:0}
    body.analysis-workspace .presentation{position:fixed;inset:18px 18px 96px;width:auto;max-height:none;margin:0;padding:16px 18px;overflow:auto}
    body.analysis-workspace .controls{width:min(96vw,1400px)}
    .analysis-report-head{display:flex;align-items:end;justify-content:space-between;gap:18px;margin-bottom:12px}
    .analysis-report-head h2{margin:0;font-size:22px;letter-spacing:.08em;text-transform:uppercase}
    .analysis-report-head p{margin:0;color:#8eafc5;font-size:12px}
    .analysis-tabs{position:sticky;top:-16px;z-index:20;display:flex;gap:7px;margin:0 -2px 14px;padding:10px 2px;border-bottom:1px solid rgba(145,220,255,.16);background:rgba(1,13,27,.97);overflow-x:auto}
    .analysis-tab{flex:0 0 auto;min-height:34px;padding:0 13px;border-color:rgba(145,220,255,.22);background:rgba(8,35,57,.82)}
    .analysis-tab.active{color:#04233d;background:#daf5ff;border-color:#fff;box-shadow:0 0 14px rgba(120,220,255,.25)}
    .analysis-tab-content{min-height:280px}
    .analysis-tab-content>.analysis-grid{align-items:start}
    .analysis-weapon-name{display:inline-flex;align-items:center;gap:7px}
    .analysis-rarity-chip{padding:2px 6px;border:1px solid currentColor;border-radius:999px;font-size:9px;font-weight:900;letter-spacing:.08em;text-transform:uppercase;opacity:.82}
    .analysis-weapon-head{display:flex;align-items:end;justify-content:space-between;gap:12px;margin:15px 0 10px}
    .analysis-weapon-head h2{margin:0;font-size:22px}
    .analysis-weapon-head p{margin:0;color:#9fc3d7;font-size:12px}
    .analysis-diagnostics{display:grid;grid-template-columns:max-content minmax(0,1fr);gap:8px 14px;margin:0;padding:16px;border:1px solid rgba(145,220,255,.18);border-radius:12px;background:rgba(8,35,57,.55);font-size:12px}
    .analysis-diagnostics dt{color:#8eafc5}.analysis-diagnostics dd{margin:0;overflow-wrap:anywhere}.analysis-diagnostics code{color:#8de1ff}
    .analysis-empty{min-height:220px}
    .analysis-compact-error{display:grid;place-items:center;min-height:220px;padding:24px;text-align:center;color:#d8edf8}
    .presentation-card h2{text-shadow:0 0 18px currentColor}
    @media(max-width:900px){
      body.analysis-workspace .presentation{inset:10px 8px 138px}
      .analysis-report-head{align-items:start;flex-direction:column;gap:4px}
    }
  `;
  document.head.appendChild(style);

  const palette = {
    common: { label: "Common", color: "#858b94", glow: "#c5cbd3" },
    rare: { label: "Rare", color: "#2f7df6", glow: "#72b2ff" },
    epic: { label: "Epic", color: "#24b85a", glow: "#70f09a" },
    legendary: { label: "Legendary", color: "#f0c419", glow: "#ffe67a" },
    mythic: { label: "Mythic", color: "#e53d43", glow: "#ff7a7f" }
  };

  let busy = false;
  let lastReport = null;
  let activeTab = "weapons";
  let selectedWeaponId = "";
  const weaponRarities = new Map();

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

  function freshSeed() {
    const words = new Uint32Array(2);
    crypto.getRandomValues(words);
    return ((BigInt(words[0]) << 32n) | BigInt(words[1])).toString();
  }

  function visualRarityId(rarityId) {
    const value = String(rarityId || "").toLowerCase();
    if (value.includes("artifact") || value.includes("mythic")) return "mythic";
    if (value.includes("legendary")) return "legendary";
    if (value.includes("epic")) return "epic";
    if (value.includes("rare") && !value.includes("uncommon")) return "rare";
    return "common";
  }

  function rarityFor(definitionId) {
    return palette[visualRarityId(weaponRarities.get(definitionId))] || palette.common;
  }

  function sampleCount() {
    const value = Number(sampleInput.value);
    return Number.isFinite(value) ? Math.max(1, Math.trunc(value)) : 1;
  }

  function updateActionLabel() {
    if (!analysisButton.classList.contains("active")) return;
    openButton.textContent = busy
      ? `Running ${formatNumber(sampleCount(), 0)} openings…`
      : `Run ${formatNumber(sampleCount(), 0)} openings`;
  }

  function setBusy(value) {
    busy = value;
    controls.classList.toggle("production-loading", value);
    openButton.disabled = value;
    newSeedButton.disabled = value;
    replayButton.disabled = true;
    updateActionLabel();
  }

  async function postPreview(body) {
    const response = await fetch("/api/strongbox-preview", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    const payload = await response.json();
    if (!response.ok || !payload.ok) {
      throw new Error(payload.error || "Strongbox preview failed.");
    }
    return payload;
  }

  async function loadWeaponRarities(request) {
    if (weaponRarities.size) return;
    try {
      const probe = await postPreview({ ...request, mode: "single", sampleCount: 1 });
      (probe.candidates || []).forEach(candidate => {
        weaponRarities.set(String(candidate.definitionId), String(candidate.rarityId || ""));
      });
    } catch (_) {
      // Rarity colouring is optional; the analysis itself should still render.
    }
  }

  function distributionTable(title, entries, options = {}) {
    const rows = (entries || []).slice(0, options.limit || 1000);
    if (!rows.length) {
      return `<section class="analysis-section ${options.full ? "full" : ""}"><h3>${escapeText(title)}</h3><p>No results.</p></section>`;
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

  function weaponTable(report) {
    const rows = report.weaponDistribution || [];
    const maximum = Math.max(...rows.map(entry => Number(entry.percentage) || 0), 0.0001);
    return `
      <section class="analysis-section full">
        <h3>Weapon results</h3>
        <table class="distribution">
          <thead><tr><th>Weapon</th><th>Count</th><th>Share</th></tr></thead>
          <tbody>${rows.map(entry => {
            const view = rarityFor(entry.key);
            return `
              <tr>
                <td>
                  <span class="distribution-bar"><i style="width:${Math.max(0, Math.min(100, Number(entry.percentage) / maximum * 100))}%;background:${view.color}"></i></span>
                  <button type="button" class="weapon-link analysis-weapon-name" data-analysis-weapon="${escapeText(entry.key)}" style="color:${view.glow};text-shadow:0 0 12px ${view.color}55">
                    <span>${escapeText(entry.label)}</span><small class="analysis-rarity-chip" style="color:${view.color}">${view.label}</small>
                  </button>
                </td>
                <td>${formatNumber(entry.count, 0)}</td>
                <td>${formatNumber(entry.percentage, 3)}%</td>
              </tr>`;
          }).join("")}</tbody>
        </table>
      </section>`;
  }

  function augmentMatrixTable(detail) {
    const cells = new Map((detail?.augmentMatrix || []).map(cell => [`${cell.slots}:${cell.level}`, cell]));
    const levels = Array.from({ length: 12 }, (_, index) => index + 1);
    const slots = Array.from({ length: 5 }, (_, index) => index);
    return `
      <section class="augment-matrix-section">
        <h3>Augment slots × augment level</h3>
        <div class="augment-matrix-wrap">
          <table class="augment-matrix">
            <thead><tr><th>Slots \\ Level</th>${levels.map(level => `<th>${level}</th>`).join("")}</tr></thead>
            <tbody>${slots.map(slot => `
              <tr><th>${slot}</th>${levels.map(level => {
                const cell = cells.get(`${slot}:${level}`) || { count: 0, percentage: 0 };
                return `<td class="${Number(cell.count) ? "" : "empty"}">${Number(cell.count) ? `<strong>${formatNumber(cell.count, 0)}</strong><small>${formatNumber(cell.percentage, 2)}%</small>` : "—"}</td>`;
              }).join("")}</tr>`).join("")}</tbody>
          </table>
        </div>
      </section>`;
  }

  function selectedDetail(report) {
    const details = report.weaponBreakdowns || [];
    if (!selectedWeaponId || !details.some(detail => detail.definitionId === selectedWeaponId)) {
      selectedWeaponId = details[0]?.definitionId || "";
    }
    return details.find(detail => detail.definitionId === selectedWeaponId) || null;
  }

  function weaponPicker(report, detail) {
    const view = rarityFor(detail?.definitionId);
    return `
      <div class="weapon-analysis-picker">
        <label for="analysisWeaponFilter">Inspect weapon</label>
        <select id="analysisWeaponFilter">${(report.weaponBreakdowns || []).map(value => `<option value="${escapeText(value.definitionId)}" ${value.definitionId === detail?.definitionId ? "selected" : ""}>${escapeText(value.displayName)}</option>`).join("")}</select>
        <span class="weapon-analysis-summary">${detail ? `${formatNumber(detail.count, 0)} drops · ${formatNumber(detail.percentage, 3)}%` : ""}</span>
      </div>
      ${detail ? `<div class="analysis-weapon-head"><div><h2 style="color:${view.glow};text-shadow:0 0 16px ${view.color}66">${escapeText(detail.displayName)}</h2><p>${escapeText(detail.definitionId)}</p></div><p>${formatNumber(detail.count, 0)} of ${formatNumber(report.successfulOpenings, 0)} successful openings</p></div>` : ""}`;
  }

  function renderWeapons(report) {
    const detail = selectedDetail(report);
    return `
      <div class="analysis-grid">${weaponTable(report)}</div>
      ${weaponPicker(report, detail)}
      ${detail ? `<div class="analysis-grid">
        ${distributionTable("Item levels", detail.itemLevelDistribution)}
        ${distributionTable("Target levels", detail.targetLevelDistribution)}
        ${distributionTable("Quality", detail.qualityDistribution, { full: true })}
      </div>` : ""}`;
  }

  function renderLevels(report) {
    return `<div class="analysis-grid">
      ${distributionTable("Item levels", report.itemLevelDistribution)}
      ${distributionTable("Target levels", report.targetLevelDistribution)}
    </div>`;
  }

  function renderRarity(report) {
    return `<div class="analysis-grid">
      ${distributionTable("Rarity", report.rarityDistribution)}
      ${distributionTable("Quality", report.qualityDistribution)}
    </div>`;
  }

  function renderAugments(report) {
    const detail = selectedDetail(report);
    return `
      <div class="analysis-grid">
        ${distributionTable("Augment signatures (level/slots)", report.augmentSignatureDistribution)}
        ${distributionTable("Augment slots", report.augmentSlotDistribution)}
        ${distributionTable("Augment levels", report.augmentLevelDistribution, { full: true })}
      </div>
      ${weaponPicker(report, detail)}
      ${detail ? augmentMatrixTable(detail) : ""}`;
  }

  function renderDiagnostics(report) {
    const rejected = Number(report.rejectedOpenings || 0);
    return `
      <dl class="analysis-diagnostics">
        <dt>Catalogue</dt><dd><code>${escapeText(report.catalogAuthority)}</code></dd>
        <dt>Fingerprint</dt><dd><code>${escapeText(report.catalogFingerprint)}</code></dd>
        <dt>Definitions</dt><dd>${formatNumber(report.catalogDefinitionCount, 0)}</dd>
        <dt>Seed</dt><dd><code>${escapeText(report.seed)}</code></dd>
        <dt>Tier</dt><dd>${formatNumber(report.tierNumber, 0)} · <code>${escapeText(report.tierId)}</code></dd>
        <dt>Player level</dt><dd>${formatNumber(report.playerLevel, 0)}</dd>
        <dt>Target delta</dt><dd>${report.minimumTargetDelta} / ${report.mostLikelyTargetDelta} / ${report.maximumTargetDelta}</dd>
      </dl>
      ${rejected ? `<div class="analysis-grid" style="margin-top:14px">${distributionTable("Rejected openings", report.rejectionDistribution, { full: true })}</div>` : ""}`;
  }

  const tabs = [
    ["weapons", "Weapons"],
    ["levels", "Levels"],
    ["rarity", "Rarity & quality"],
    ["augments", "Augments"],
    ["diagnostics", "Diagnostics"]
  ];

  function renderTab(report) {
    const content = presentation.querySelector("#analysisTabContent");
    if (!content) return;
    if (activeTab === "levels") content.innerHTML = renderLevels(report);
    else if (activeTab === "rarity") content.innerHTML = renderRarity(report);
    else if (activeTab === "augments") content.innerHTML = renderAugments(report);
    else if (activeTab === "diagnostics") content.innerHTML = renderDiagnostics(report);
    else content.innerHTML = renderWeapons(report);

    presentation.querySelectorAll(".analysis-tab").forEach(button => {
      button.classList.toggle("active", button.dataset.analysisTab === activeTab);
    });
    presentation.querySelectorAll("[data-analysis-weapon]").forEach(button => {
      button.addEventListener("click", () => {
        selectedWeaponId = button.dataset.analysisWeapon || "";
        renderTab(report);
      });
    });
    presentation.querySelector("#analysisWeaponFilter")?.addEventListener("change", event => {
      selectedWeaponId = event.target.value;
      renderTab(report);
    });
  }

  function renderAnalysis(report) {
    const rejected = Number(report.rejectedOpenings || 0);
    presentation.innerHTML = `
      <div class="analysis-report-head">
        <h2>Strongbox analysis</h2>
        <p>Seed ${escapeText(report.seed)}</p>
      </div>
      <div class="analysis-metrics">
        <div class="analysis-metric"><span>Openings</span><strong>${formatNumber(report.sampleCount, 0)}</strong></div>
        <div class="analysis-metric"><span>Successful</span><strong>${formatNumber(report.successfulOpenings, 0)}</strong></div>
        <div class="analysis-metric"><span>Rejected</span><strong>${formatNumber(rejected, 0)}</strong></div>
        <div class="analysis-metric"><span>Target level</span><strong>${formatNumber(report.averageTargetLevel, 2)}</strong><small>${report.minimumTargetLevel}–${report.maximumTargetLevel}</small></div>
        <div class="analysis-metric"><span>Item level</span><strong>${formatNumber(report.averageItemLevel, 2)}</strong><small>${report.minimumItemLevel}–${report.maximumItemLevel}</small></div>
        <div class="analysis-metric"><span>Tier / player</span><strong>${report.tierNumber} / ${report.playerLevel}</strong></div>
      </div>
      <nav class="analysis-tabs" aria-label="Analysis views">
        ${tabs.map(([id, label]) => `<button type="button" class="analysis-tab ${id === activeTab ? "active" : ""}" data-analysis-tab="${id}">${label}</button>`).join("")}
      </nav>
      <div id="analysisTabContent" class="analysis-tab-content"></div>`;

    presentation.querySelectorAll(".analysis-tab").forEach(button => {
      button.addEventListener("click", () => {
        activeTab = button.dataset.analysisTab || "weapons";
        renderTab(report);
        presentation.scrollTop = 0;
      });
    });
    renderTab(report);
  }

  function renderError(error) {
    presentation.innerHTML = `<div class="analysis-compact-error"><div><strong>Analysis failed</strong><br>${escapeText(error.message || error)}</div></div>`;
  }

  async function runAnalysis() {
    if (busy) return;
    const request = {
      mode: "analysis",
      playerLevel: Number(playerLevelInput.value),
      tierNumber: Number(tierInput.value),
      seed: seedInput.value.trim(),
      sampleCount: sampleCount()
    };

    setBusy(true);
    const rarityTask = loadWeaponRarities(request);
    try {
      const report = await postPreview(request);
      await rarityTask;
      lastReport = report;
      selectedWeaponId = report.weaponBreakdowns?.[0]?.definitionId || "";
      if (winner) winner.textContent = `${formatNumber(report.sampleCount, 0)} production openings`;
      if (rarity) rarity.textContent = `${formatNumber(report.successfulOpenings, 0)} successful`;
      renderAnalysis(report);
    } catch (error) {
      renderError(error);
    } finally {
      setBusy(false);
    }
  }

  function syncMode() {
    const active = analysisButton.classList.contains("active");
    document.body.classList.toggle("analysis-workspace", active);
    if (!active) return;
    replayButton.disabled = true;
    updateActionLabel();
    if (lastReport) renderAnalysis(lastReport);
    else presentation.innerHTML = `<div class="analysis-empty"></div>`;
  }

  new MutationObserver(syncMode).observe(analysisButton, {
    attributes: true,
    attributeFilter: ["class"]
  });

  sampleInput.addEventListener("input", updateActionLabel);
  sampleInput.addEventListener("change", updateActionLabel);

  document.addEventListener("click", event => {
    if (!analysisButton.classList.contains("active")) return;
    const target = event.target instanceof Element ? event.target : null;
    if (target?.closest("#open")) {
      event.preventDefault();
      event.stopImmediatePropagation();
      runAnalysis();
      return;
    }
    if (target?.closest("#newPreviewSeed")) {
      event.preventDefault();
      event.stopImmediatePropagation();
      seedInput.value = freshSeed();
    }
  }, true);

  syncMode();
})();
