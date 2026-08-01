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
      <button id="toggleDpsTargetsButton" type="button" class="small" style="margin-top:10px">Edit level DPS targets</button>
      <details id="dpsTargetEditor" class="dps-target-editor">
        <summary>Level 1–110 target curve</summary>
        <div class="dps-target-body">
          <div class="help">One shared sustained-DPS target for every game level. Blank levels are not compared.</div>
          <div class="dps-curve-controls">
            <div class="field"><label>Level 1 DPS</label><input id="curveStartDps" type="number" min="0.0001" step="any"></div>
            <div class="field"><label>Level 110 DPS</label><input id="curveEndDps" type="number" min="0.0001" step="any"></div>
            <div class="field"><label>Curve</label><select id="curveMode"><option value="linear">Linear</option><option value="exponential">Exponential</option></select></div>
            <button id="generateDpsCurveButton" type="button">Generate all levels</button>
          </div>
          <div class="field" style="margin-top:10px">
            <label>Paste 110 DPS values</label>
            <textarea id="pasteDpsTargets" rows="3" placeholder="Comma, space, or line-separated values"></textarea>
          </div>
          <button id="applyPastedTargetsButton" type="button" class="small">Apply pasted values</button>
          <div id="dpsTargetRows" class="dps-target-rows"></div>
          <div class="dps-target-actions">
            <button id="saveDpsTargetsButton" type="button" class="primary">Save DPS targets</button>
            <span id="dpsTargetStatus" class="help">Not loaded</span>
          </div>
        </div>
      </details>
    </div>`;
  summaryColumn.insertBefore(panel, summaryColumn.children[1] || null);

  const summary = panel.querySelector("#weaponDpsSummary");
  const targetEditor = panel.querySelector("#dpsTargetEditor");
  const targetRows = panel.querySelector("#dpsTargetRows");
  const targetStatus = panel.querySelector("#dpsTargetStatus");
  let targets = WeaponDps.emptyTargets();
  let targetsDirty = false;
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
    if (Math.abs(percent) <= 10) return "on-target";
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

  function renderSummary() {
    const current = currentDefinitions();
    if (current.errors.length) {
      summary.innerHTML = `<div class="issue error">Fix the weapon data to calculate DPS.</div>`;
      return;
    }

    summary.innerHTML = current.definitions.map(definition => {
      const level = Number(definition.peakLevel);
      const target = WeaponDps.targetAtLevel(targets, level);
      const result = WeaponDps.calculate(definition, target);
      const difference = result.differencePercent === null
        ? "Set this level's DPS target"
        : `${result.differencePercent >= 0 ? "+" : ""}${formatNumber(result.differencePercent)}%`;
      const utility = [
        definition.impact && definition.impact.pierce ? `Pierce ${definition.impact.pierce}` : null,
        definition.explosion ? `Explosion radius ${formatNumber(definition.explosion.radius)}` : null,
        definition.impact && definition.impact.knockback ? `Knockback ${formatNumber(definition.impact.knockback)}` : null
      ].filter(Boolean).join(" · ");
      return `<article class="dps-mark-card ${differenceClass(result.differencePercent)}">
        <div class="dps-mark-head"><strong>MK${definition.mark}</strong><span>Level ${escapeHtml(level)}</span></div>
        <div class="dps-main-row"><span>Sustained DPS</span><strong>${formatNumber(result.totalDps)}</strong></div>
        <div class="dps-row"><span>Target DPS</span><strong>${formatNumber(result.targetDps)}</strong></div>
        <div class="dps-row"><span>Difference</span><strong>${escapeHtml(difference)}</strong></div>
        <div class="dps-breakdown">Direct ${formatNumber(result.directDps)}${result.dotDps ? ` + stacking damage ${formatNumber(result.dotDps)}` : ""}</div>
        <div class="dps-breakdown">${formatNumber(result.attacksPerSecond)} attacks/sec × ${result.projectiles} projectile${result.projectiles === 1 ? "" : "s"}</div>
        ${utility ? `<div class="dps-utility">${escapeHtml(utility)} — not added to single-target DPS</div>` : ""}
        ${result.suggestedDamage !== null ? `<button type="button" class="small" data-suggested-mark="${definition.mark}" data-suggested-damage="${result.suggestedDamage}">Set damage to ${formatNumber(result.suggestedDamage, 4)}</button>` : ""}
      </article>`;
    }).join("");

    summary.querySelectorAll("[data-suggested-mark]").forEach(button => button.addEventListener("click", () => {
      applySuggestedDamage(Number(button.dataset.suggestedMark), Number(button.dataset.suggestedDamage));
    }));
  }

  function renderTargetRows() {
    targetRows.innerHTML = Array.from({ length: targets.maxLevel }, (_, index) => {
      const level = index + 1;
      const value = targets.targets[String(level)];
      return `<label class="dps-target-row"><span>Level ${level}</span><input type="number" min="0.0001" step="any" data-target-level="${level}" value="${value ?? ""}" placeholder="Not set"></label>`;
    }).join("");

    targetRows.querySelectorAll("[data-target-level]").forEach(input => input.addEventListener("input", () => {
      const level = input.dataset.targetLevel;
      const value = input.value.trim() === "" ? null : Number(input.value);
      targets.targets[level] = Number.isFinite(value) && value > 0 ? value : null;
      targetsDirty = true;
      targetStatus.textContent = "Unsaved target changes";
      renderSummary();
    }));
  }

  function applyCurve() {
    try {
      const start = Number(panel.querySelector("#curveStartDps").value);
      const end = Number(panel.querySelector("#curveEndDps").value);
      const mode = panel.querySelector("#curveMode").value;
      targets = WeaponDps.generateCurve(start, end, targets.maxLevel, mode);
      targetsDirty = true;
      targetStatus.textContent = "Generated curve — not saved";
      renderTargetRows();
      renderSummary();
    } catch (error) {
      targetStatus.textContent = error.message;
    }
  }

  function applyPastedTargets() {
    const values = panel.querySelector("#pasteDpsTargets").value
      .split(/[\s,;]+/)
      .map(value => value.trim())
      .filter(Boolean)
      .map(Number);
    if (values.length !== targets.maxLevel || values.some(value => !Number.isFinite(value) || value <= 0)) {
      targetStatus.textContent = `Paste exactly ${targets.maxLevel} positive DPS values.`;
      return;
    }
    values.forEach((value, index) => { targets.targets[String(index + 1)] = value; });
    targetsDirty = true;
    targetStatus.textContent = "Pasted targets — not saved";
    renderTargetRows();
    renderSummary();
  }

  async function saveTargets() {
    const errors = WeaponDps.validateTargets(targets);
    if (errors.length) {
      targetStatus.textContent = errors[0];
      return;
    }
    try {
      const result = await api("/api/weapon-dps-targets", {
        method: "PUT",
        body: JSON.stringify({ targets })
      });
      targetsDirty = false;
      targetStatus.textContent = `Saved ${result.saved}`;
    } catch (error) {
      targetStatus.textContent = error.message;
    }
  }

  async function loadTargets() {
    try {
      const result = await api("/api/weapon-dps-targets");
      targets = WeaponDps.normalizeTargets(result.targets, result.targets.maxLevel || WeaponDps.DEFAULT_MAX_LEVEL);
      targetsDirty = false;
      targetStatus.textContent = "Targets loaded";
    } catch (error) {
      targets = WeaponDps.emptyTargets();
      targetStatus.textContent = "Targets unavailable until the local helper is running";
    }
    renderTargetRows();
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
  panel.querySelector("#generateDpsCurveButton").addEventListener("click", applyCurve);
  panel.querySelector("#applyPastedTargetsButton").addEventListener("click", applyPastedTargets);
  panel.querySelector("#saveDpsTargetsButton").addEventListener("click", saveTargets);

  document.addEventListener("input", event => {
    if (event.target.closest("#dpsTargetEditor")) return;
    if (event.target.closest("#gameplayEditor, #jsonWorkspace") || event.target === elements.categoryInput || event.target === elements.folderInput) queueRender();
  });
  new MutationObserver(queueRender).observe(document.querySelector("#gameplayEditor"), { childList: true, subtree: true });
  window.addEventListener("beforeunload", event => {
    if (!targetsDirty) return;
    event.preventDefault();
    event.returnValue = "";
  });

  loadTargets();
})();
