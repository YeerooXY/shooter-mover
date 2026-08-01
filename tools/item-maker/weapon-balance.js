"use strict";

(function setupWeaponBalance() {
  const summaryColumn = document.querySelector(".weapon-summary");
  const panel = document.createElement("section");
  panel.className = "panel";
  panel.id = "weaponBalancePanel";
  panel.innerHTML = `
    <div class="panel-head">Damage balance</div>
    <div class="panel-body">
      <div id="weaponDpsSummary" class="weapon-dps-summary"></div>
      <button id="toggleDpsTargetsButton" type="button" class="small" style="margin-top:10px">Edit balance settings</button>
      <details id="dpsTargetEditor" class="dps-target-editor">
        <summary>Raw weapon curve and build layers</summary>
        <div class="dps-target-body">
          <div class="help">The curve is an authoring suggestion, not a runtime multiplier. It starts at 4 raw DPS and reaches 200 raw DPS at level 110, then continues automatically for future levels.</div>

          <div class="dps-config-group">
            <strong>Raw weapon curve</strong>
            <div class="dps-config-grid">
              <div class="field"><label>Starting level</label><input type="number" min="1" step="1" data-balance-path="rawWeaponCurve.startLevel"></div>
              <div class="field"><label>Starting raw DPS</label><input type="number" min="0.0001" step="any" data-balance-path="rawWeaponCurve.startDps"></div>
              <div class="field"><label>Reference level</label><input type="number" min="2" step="1" data-balance-path="rawWeaponCurve.referenceLevel"></div>
              <div class="field"><label>Reference raw DPS</label><input type="number" min="0.0001" step="any" data-balance-path="rawWeaponCurve.referenceDps"></div>
              <div class="field"><label>Current authored max level</label><input type="number" min="1" step="1" data-balance-path="rawWeaponCurve.maxAuthoredLevel"></div>
            </div>
          </div>

          <div class="dps-config-group">
            <strong>Rarity suggestions</strong>
            <div class="help">These guide weapon authoring only. They are not applied again during gameplay.</div>
            <div class="dps-config-grid compact">
              <div class="field"><label>Common</label><input type="number" min="0.0001" step="any" data-balance-path="rarityMultipliers.common"></div>
              <div class="field"><label>Rare</label><input type="number" min="0.0001" step="any" data-balance-path="rarityMultipliers.rare"></div>
              <div class="field"><label>Epic</label><input type="number" min="0.0001" step="any" data-balance-path="rarityMultipliers.epic"></div>
              <div class="field"><label>Legendary</label><input type="number" min="0.0001" step="any" data-balance-path="rarityMultipliers.legendary"></div>
              <div class="field"><label>Artifact</label><input type="number" min="0.0001" step="any" data-balance-path="rarityMultipliers.artifact"></div>
            </div>
          </div>

          <div class="dps-config-group">
            <strong>Build-layer estimates</strong>
            <div class="help">Each normal layer multiplies the previous one. Optimized total is a separate full-build ceiling measured from raw weapon DPS.</div>
            <div class="dps-config-grid compact">
              <div class="field"><label>Weapon upgrades</label><input type="number" min="0.0001" step="any" data-balance-path="buildMultipliers.weaponUpgrades"></div>
              <div class="field"><label>Gear</label><input type="number" min="0.0001" step="any" data-balance-path="buildMultipliers.gear"></div>
              <div class="field"><label>Skills</label><input type="number" min="0.0001" step="any" data-balance-path="buildMultipliers.skills"></div>
              <div class="field"><label>Collections / mastery</label><input type="number" min="0.0001" step="any" data-balance-path="buildMultipliers.accountProgression"></div>
              <div class="field"><label>Optimized total</label><input type="number" min="0.0001" step="any" data-balance-path="buildMultipliers.optimizedTotal"></div>
            </div>
            <div id="normalBuildMultiplier" class="dps-config-result"></div>
          </div>

          <div class="dps-target-actions">
            <button id="saveDpsTargetsButton" type="button" class="primary">Save balance settings</button>
            <span id="dpsTargetStatus" class="help">Not loaded</span>
          </div>
        </div>
      </details>
    </div>`;
  summaryColumn.insertBefore(panel, summaryColumn.children[1] || null);

  const summary = panel.querySelector("#weaponDpsSummary");
  const targetEditor = panel.querySelector("#dpsTargetEditor");
  const targetStatus = panel.querySelector("#dpsTargetStatus");
  const normalBuildMultiplier = panel.querySelector("#normalBuildMultiplier");
  let balance = WeaponDps.emptyTargets();
  let balanceDirty = false;
  let renderQueued = false;

  function formatNumber(value, digits = 2) {
    if (value === null || value === undefined || !Number.isFinite(value)) return "—";
    return Number(value.toFixed(digits)).toString();
  }

  function mergeObjects(shared, mark) {
    const result = {};
    Object.entries(shared || {}).forEach(([key, value]) => {
      result[key] = value && typeof value === "object" && !Array.isArray(value)
        ? mergeObjects(value, {})
        : value;
    });
    Object.entries(mark || {}).forEach(([key, value]) => {
      result[key] = value && typeof value === "object" && !Array.isArray(value) && result[key] && typeof result[key] === "object" && !Array.isArray(result[key])
        ? mergeObjects(result[key], value)
        : value;
    });
    return result;
  }

  function currentDefinitions() {
    const result = parseFiles();
    if (result.errors.length) return { errors: result.errors, definitions: [] };
    const shared = result.parsed["weapon.json"];
    const definitions = [1, 2, 3].map(mark => ({
      ...mergeObjects(shared, result.parsed[`mk${mark}.json`]),
      mark
    }));
    return { errors: [], definitions };
  }

  function differenceClass(percent) {
    if (percent === null) return "";
    if (Math.abs(percent) <= 15) return "on-target";
    return percent > 0 ? "over-target" : "under-target";
  }

  function applySuggestedDamage(mark, value) {
    const control = typeof gameplayControl === "function" ? gameplayControl(`mark.${mark}.damage`) : null;
    if (control && Number.isFinite(value)) {
      control.value = value;
      gameplayApply();
      queueRender();
      return;
    }

    const result = parseFiles();
    if (result.errors.length || !Number.isFinite(value)) return;
    result.parsed[`mk${mark}.json`].damage = value;
    files[`mk${mark}.json`] = format(result.parsed[`mk${mark}.json`]);
    elements.jsonEditor.value = files[activeFile];
    setDirty();
    localChecks();
    queueRender();
  }

  function buildRows(estimates) {
    if (!estimates) return "";
    return `
      <div class="dps-build-rows">
        <div class="dps-row"><span>With weapon upgrades</span><strong>${formatNumber(estimates.developedWeapon)}</strong></div>
        <div class="dps-row"><span>With gear</span><strong>${formatNumber(estimates.withGear)}</strong></div>
        <div class="dps-row"><span>With skills</span><strong>${formatNumber(estimates.withSkills)}</strong></div>
        <div class="dps-row"><span>Completed build</span><strong>${formatNumber(estimates.completeBuild)}</strong></div>
        <div class="dps-row"><span>Optimized ceiling</span><strong>${formatNumber(estimates.optimizedBuild)}</strong></div>
      </div>`;
  }

  function renderSummary() {
    const configErrors = WeaponDps.validateTargets(balance);
    if (configErrors.length) {
      summary.innerHTML = `<div class="issue error">Fix the balance settings: ${escapeHtml(configErrors[0])}</div>`;
      return;
    }

    const current = currentDefinitions();
    if (current.errors.length) {
      summary.innerHTML = `<div class="issue error">Fix the weapon data to calculate DPS.</div>`;
      return;
    }

    summary.innerHTML = current.definitions.map(definition => {
      const level = Number(definition.peakLevel);
      const rarity = String(definition.rarity || "common").toLowerCase();
      const rawTarget = WeaponDps.targetAtLevel(balance, level);
      const raritySuggestion = WeaponDps.rarityTargetAtLevel(balance, level, rarity);
      const result = WeaponDps.calculate(definition, raritySuggestion);
      const estimates = WeaponDps.buildEstimates(result.totalDps, balance);
      const difference = result.differencePercent === null
        ? "No suggestion available"
        : `${result.differencePercent >= 0 ? "+" : ""}${formatNumber(result.differencePercent)}%`;
      const utility = [
        definition.impact && definition.impact.pierce ? `Pierce ${definition.impact.pierce}` : null,
        definition.explosion ? `Explosion radius ${formatNumber(definition.explosion.radius)}` : null,
        definition.impact && definition.impact.knockback ? `Knockback ${formatNumber(definition.impact.knockback)}` : null
      ].filter(Boolean).join(" · ");
      const aboveAuthoredRange = Number.isInteger(level) && level > balance.rawWeaponCurve.maxAuthoredLevel;
      return `<article class="dps-mark-card ${differenceClass(result.differencePercent)}">
        <div class="dps-mark-head"><strong>MK${definition.mark}</strong><span>Level ${escapeHtml(level)} · ${escapeHtml(rarity)}</span></div>
        <div class="dps-main-row"><span>Current raw DPS</span><strong>${formatNumber(result.totalDps)}</strong></div>
        <div class="dps-row"><span>Level raw suggestion</span><strong>${formatNumber(rawTarget)}</strong></div>
        <div class="dps-row"><span>${escapeHtml(rarity)} suggestion</span><strong>${formatNumber(raritySuggestion)}</strong></div>
        <div class="dps-row"><span>Difference</span><strong>${escapeHtml(difference)}</strong></div>
        <div class="dps-breakdown">Direct ${formatNumber(result.directDps)}${result.dotDps ? ` + stacking damage ${formatNumber(result.dotDps)}` : ""}</div>
        <div class="dps-breakdown">${formatNumber(result.attacksPerSecond)} attacks/sec × ${result.projectiles} projectile${result.projectiles === 1 ? "" : "s"}</div>
        ${buildRows(estimates)}
        ${utility ? `<div class="dps-utility">${escapeHtml(utility)} — utility is shown separately from raw single-target DPS</div>` : ""}
        ${aboveAuthoredRange ? `<div class="dps-utility">Above the current authored level range; the curve is being extrapolated.</div>` : ""}
        ${result.suggestedDamage !== null ? `<button type="button" class="small" data-suggested-mark="${definition.mark}" data-suggested-damage="${result.suggestedDamage}">Use suggested damage ${formatNumber(result.suggestedDamage, 4)}</button>` : ""}
      </article>`;
    }).join("");

    summary.querySelectorAll("[data-suggested-mark]").forEach(button => button.addEventListener("click", () => {
      applySuggestedDamage(Number(button.dataset.suggestedMark), Number(button.dataset.suggestedDamage));
    }));
  }

  function getPath(path) {
    return path.split(".").reduce((value, part) => value && value[part], balance);
  }

  function setPath(path, value) {
    const parts = path.split(".");
    const key = parts.pop();
    const parent = parts.reduce((current, part) => current[part], balance);
    parent[key] = value;
  }

  function renderConfigControls() {
    panel.querySelectorAll("[data-balance-path]").forEach(input => {
      input.value = getPath(input.dataset.balancePath);
    });
    const build = balance.buildMultipliers;
    const normalTotal = build.weaponUpgrades * build.gear * build.skills * build.accountProgression;
    normalBuildMultiplier.textContent = `Normal completed-build multiplier: ${formatNumber(normalTotal, 3)}× raw weapon DPS`;
  }

  async function saveTargets() {
    const errors = WeaponDps.validateTargets(balance);
    if (errors.length) {
      targetStatus.textContent = errors[0];
      return;
    }
    try {
      const result = await api("/api/weapon-dps-targets", {
        method: "PUT",
        body: JSON.stringify({ targets: balance })
      });
      balanceDirty = false;
      targetStatus.textContent = `Saved ${result.saved}`;
    } catch (error) {
      targetStatus.textContent = error.message;
    }
  }

  async function loadTargets() {
    try {
      const result = await api("/api/weapon-dps-targets");
      balance = WeaponDps.normalizeTargets(result.targets);
      balanceDirty = false;
      targetStatus.textContent = "Balance settings loaded";
    } catch (error) {
      balance = WeaponDps.emptyTargets();
      targetStatus.textContent = "Using defaults until the local helper is running";
    }
    renderConfigControls();
    renderSummary();
  }

  function queueRender() {
    if (renderQueued) return;
    renderQueued = true;
    queueMicrotask(() => {
      renderQueued = false;
      renderSummary();
    });
  }

  panel.querySelector("#toggleDpsTargetsButton").addEventListener("click", () => {
    targetEditor.open = !targetEditor.open;
  });
  panel.querySelector("#saveDpsTargetsButton").addEventListener("click", saveTargets);
  panel.querySelectorAll("[data-balance-path]").forEach(input => input.addEventListener("input", () => {
    const value = Number(input.value);
    if (!Number.isFinite(value)) {
      targetStatus.textContent = "Enter a number for every balance setting.";
      return;
    }
    setPath(input.dataset.balancePath, value);
    balanceDirty = true;
    targetStatus.textContent = "Unsaved balance changes";
    renderConfigControls();
    renderSummary();
  }));

  document.addEventListener("input", event => {
    if (event.target.closest("#dpsTargetEditor")) return;
    if (event.target.closest("#gameplayEditor, #jsonWorkspace") || event.target === elements.categoryInput || event.target === elements.folderInput) queueRender();
  });
  new MutationObserver(queueRender).observe(document.querySelector("#gameplayEditor"), { childList: true, subtree: true });
  window.addEventListener("beforeunload", event => {
    if (!balanceDirty) return;
    event.preventDefault();
    event.returnValue = "";
  });

  loadTargets();
})();
