"use strict";

(() => {
  const presentation = document.getElementById("strongboxPresentation");
  const analysisButton = document.querySelector('.mode[data-mode="analysis"]');
  if (!presentation || !analysisButton) return;

  const palette = {
    common: { label: "Common", color: "#858B94", glow: "#C5CBD3" },
    uncommon: { label: "Uncommon", color: "#39B98A", glow: "#82E8C5" },
    rare: { label: "Rare", color: "#2F7DF6", glow: "#72B2FF" },
    epic: { label: "Epic", color: "#8B5CF6", glow: "#C4B5FD" },
    legendary: { label: "Legendary", color: "#F0C419", glow: "#FFE67A" },
    mythic: { label: "Mythic", color: "#E53D43", glow: "#FF7A7F" },
    artifact: { label: "Artifact", color: "#E53D43", glow: "#FF7A7F" }
  };
  const order = ["common", "uncommon", "rare", "epic", "legendary", "mythic", "artifact"];
  const probePromises = new Map();
  let renderRevision = 0;

  const style = document.createElement("style");
  style.textContent = `
    .coverage-panel{margin:0 0 15px;padding:15px;border:1px solid rgba(145,220,255,.18);border-radius:12px;background:rgba(8,35,57,.55)}
    .coverage-head{display:flex;align-items:end;justify-content:space-between;gap:14px;margin-bottom:13px}.coverage-head h3{margin:0;font-size:14px;letter-spacing:.1em;text-transform:uppercase}.coverage-head p{margin:0;color:#9fc3d7;font-size:11px;text-align:right}
    .coverage-chart{--coverage-label:104px;min-width:760px}.coverage-scroll{overflow:auto;padding-bottom:4px}.coverage-axis,.coverage-row{display:grid;grid-template-columns:var(--coverage-label) minmax(620px,1fr);gap:10px;align-items:center}.coverage-axis{margin-bottom:7px}.coverage-axis-label{color:#789bb0;font-size:9px;text-transform:uppercase;letter-spacing:.08em}.coverage-track{position:relative;height:42px;border-radius:8px;background:repeating-linear-gradient(90deg,rgba(145,220,255,.045) 0,rgba(145,220,255,.045) 1px,transparent 1px,transparent 9.09%),rgba(1,18,31,.7);border:1px solid rgba(145,220,255,.12);overflow:hidden}.coverage-axis .coverage-track{height:24px;background:none;border:0;overflow:visible}.coverage-tick{position:absolute;top:0;transform:translateX(-50%);color:#789bb0;font-size:9px}.coverage-tick:after{content:"";position:absolute;left:50%;top:13px;height:7px;border-left:1px solid rgba(145,220,255,.2)}
    .coverage-row{margin:6px 0}.coverage-rarity{font-size:10px;font-weight:900;letter-spacing:.08em;text-transform:uppercase;text-align:right}.coverage-rarity small{font-size:8px;opacity:.72}.coverage-window{position:absolute;top:0;bottom:0;background:rgba(87,207,255,.08);border-left:1px solid rgba(87,207,255,.3);border-right:1px solid rgba(87,207,255,.3)}.coverage-player,.coverage-likely{position:absolute;top:0;bottom:0;width:2px;transform:translateX(-1px);z-index:2}.coverage-player{background:#eefaff;box-shadow:0 0 8px #8de1ff}.coverage-likely{background:#57cfff;opacity:.75}.coverage-marker{position:absolute;top:50%;width:12px;height:12px;transform:translate(-50%,-50%);padding:0;min-height:0;border-radius:50%;border:2px solid var(--marker-glow);background:var(--marker-color);box-shadow:0 0 10px var(--marker-color);z-index:4}.coverage-marker:hover,.coverage-marker:focus{width:16px;height:16px;z-index:8}.coverage-marker.multi:after{content:attr(data-count);position:absolute;left:7px;top:-9px;min-width:14px;padding:1px 3px;border-radius:8px;background:#061a2c;color:#eefaff;font-size:8px;line-height:12px;text-align:center;border:1px solid currentColor}.coverage-summary{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:8px;margin-top:13px}.coverage-stat{padding:10px;border:1px solid rgba(145,220,255,.12);border-radius:9px;background:rgba(1,18,31,.55)}.coverage-stat span{display:block;color:#8eafc5;font-size:9px;font-weight:800;letter-spacing:.07em;text-transform:uppercase}.coverage-stat strong{display:block;margin-top:4px;font-size:13px}.coverage-stat small{color:#8eafc5}.coverage-note{margin:10px 0 0;color:#8eafc5;font-size:10px;line-height:1.4}.coverage-loading{min-height:110px;display:grid;place-items:center;color:#9fc3d7;font-size:12px}
    @media(max-width:900px){.coverage-head{align-items:start;flex-direction:column}.coverage-head p{text-align:left}}
  `;
  document.head.appendChild(style);

  function escapeText(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");
  }

  function rarityKey(value) {
    const text = String(value || "").toLowerCase();
    for (const key of [...order].reverse()) {
      if (text.includes(key)) return key;
    }
    return "common";
  }

  function displayedReportContext() {
    const seedText = presentation.querySelector(".analysis-report-head p")?.textContent || "";
    const seed = seedText.replace(/^Seed\s+/i, "").trim();
    const tierMetric = [...presentation.querySelectorAll(".analysis-metric")].find(metric =>
      metric.querySelector("span")?.textContent?.trim() === "Tier / player"
    );
    const values = tierMetric?.querySelector("strong")?.textContent?.split("/") || [];
    const tierNumber = Number(String(values[0] || "").replace(/,/g, "").trim());
    const playerLevel = Number(String(values[1] || "").replace(/,/g, "").trim());
    if (!seed || !Number.isInteger(tierNumber) || !Number.isInteger(playerLevel)) return null;
    return {
      seed,
      tierNumber,
      playerLevel,
      signature: `${playerLevel}:${tierNumber}:${seed}`
    };
  }

  function loadProbe(context) {
    if (probePromises.has(context.signature)) return probePromises.get(context.signature);
    const promise = fetch("/api/strongbox-preview", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        mode: "single",
        playerLevel: context.playerLevel,
        tierNumber: context.tierNumber,
        seed: context.seed,
        sampleCount: 1
      })
    }).then(async response => {
      const payload = await response.json();
      if (!response.ok || !payload.ok) throw new Error(payload.error || "Catalogue probe failed.");
      return payload;
    }).catch(error => {
      probePromises.delete(context.signature);
      throw error;
    });
    probePromises.set(context.signature, promise);
    return promise;
  }

  function definitionsFrom(probe) {
    const seen = new Set();
    return (probe.candidates || [])
      .filter(candidate => candidate.reason !== "not-live")
      .filter(candidate => {
        const id = String(candidate.definitionId || "");
        if (!id || seen.has(id)) return false;
        seen.add(id);
        return true;
      })
      .map(candidate => ({
        definitionId: String(candidate.definitionId),
        displayName: String(candidate.displayName || candidate.definitionId),
        rarity: rarityKey(candidate.rarityId),
        firstLevel: Number(candidate.firstAppearanceLevel || 0),
        peakLevel: Number(candidate.peakLevel || 0)
      }))
      .filter(value => value.peakLevel > 0);
  }

  function percent(level, maximum) {
    return Math.max(0, Math.min(100, (Number(level) - 1) / Math.max(1, maximum - 1) * 100));
  }

  function markerPercent(level, maximum) {
    return Math.max(0.8, Math.min(99.2, percent(level, maximum)));
  }

  function largestGap(values) {
    const levels = [...new Set(values.map(value => value.peakLevel))].sort((a, b) => a - b);
    if (levels.length < 2) return { size: 0, from: levels[0] || 0, to: levels[0] || 0 };
    let result = { size: 0, from: levels[0], to: levels[1] };
    for (let index = 1; index < levels.length; index++) {
      const size = levels[index] - levels[index - 1];
      if (size > result.size) result = { size, from: levels[index - 1], to: levels[index] };
    }
    return result;
  }

  function coverageHtml(probe, signature) {
    const definitions = definitionsFrom(probe);
    if (!definitions.length) {
      return `<section class="coverage-panel" data-coverage-signature="${escapeText(signature)}"><h3>Catalogue level coverage</h3><p>No live definitions were returned by the catalogue probe.</p></section>`;
    }

    const player = Number(probe.playerLevel || 1);
    const targetMinimum = Math.max(1, player + Number(probe.minimumTargetDelta || 0));
    const targetLikely = Math.max(1, player + Number(probe.mostLikelyTargetDelta || 0));
    const targetMaximum = Math.max(targetMinimum, player + Number(probe.maximumTargetDelta || 0));
    const maximum = Math.max(110, player, targetMaximum, ...definitions.map(value => value.peakLevel));
    const rarities = order.filter(key => definitions.some(value => value.rarity === key));
    const ticks = [];
    for (let level = 1; level <= maximum; level += 10) ticks.push(level);
    if (ticks[ticks.length - 1] !== maximum) ticks.push(maximum);

    const rows = rarities.map(key => {
      const view = palette[key] || palette.common;
      const values = definitions.filter(value => value.rarity === key);
      const grouped = new Map();
      values.forEach(value => {
        const bucket = grouped.get(value.peakLevel) || [];
        bucket.push(value);
        grouped.set(value.peakLevel, bucket);
      });
      const markers = [...grouped.entries()]
        .sort((left, right) => left[0] - right[0])
        .map(([level, entries]) => {
          const title = entries
            .map(value => `${value.displayName} — ${value.definitionId} · first ${value.firstLevel}`)
            .join("\n");
          const ids = encodeURIComponent(JSON.stringify(entries.map(value => value.definitionId)));
          return `<button type="button" class="coverage-marker ${entries.length > 1 ? "multi" : ""}" data-count="${entries.length}" data-coverage-definitions="${ids}" title="${escapeText(`${view.label} · peak level ${level}\n${title}`)}" style="left:${markerPercent(level, maximum)}%;--marker-color:${view.color};--marker-glow:${view.glow}" aria-label="${escapeText(`${view.label} peak level ${level}: ${entries.map(value => value.displayName).join(", ")}`)}"></button>`;
        })
        .join("");
      return `<div class="coverage-row"><div class="coverage-rarity" style="color:${view.glow}">${view.label} <small>(${values.length})</small></div><div class="coverage-track"><span class="coverage-window" style="left:${percent(targetMinimum, maximum)}%;width:${Math.max(.4, percent(targetMaximum, maximum) - percent(targetMinimum, maximum))}%" title="Box target range ${targetMinimum}–${targetMaximum}"></span><span class="coverage-player" style="left:${percent(player, maximum)}%" title="Player level ${player}"></span><span class="coverage-likely" style="left:${percent(targetLikely, maximum)}%" title="Most likely target ${targetLikely}"></span>${markers}</div></div>`;
    }).join("");

    const stats = rarities.map(key => {
      const view = palette[key] || palette.common;
      const values = definitions.filter(value => value.rarity === key);
      const gap = largestGap(values);
      const near = values.filter(value =>
        value.peakLevel >= targetMinimum && value.peakLevel <= targetMaximum
      ).length;
      return `<div class="coverage-stat"><span style="color:${view.color}">${view.label}</span><strong>${near} in target band</strong><small>largest gap ${gap.size} levels${gap.size ? ` · ${gap.from}→${gap.to}` : ""}</small></div>`;
    }).join("");

    return `<section class="coverage-panel" data-coverage-signature="${escapeText(signature)}"><div class="coverage-head"><div><h3>Catalogue level coverage</h3></div><p>${definitions.length} live definitions · player ${player} · target ${targetMinimum}–${targetMaximum} · likely ${targetLikely}</p></div><div class="coverage-scroll"><div class="coverage-chart"><div class="coverage-axis"><div class="coverage-axis-label">Peak level</div><div class="coverage-track">${ticks.map(level => `<span class="coverage-tick" style="left:${percent(level, maximum)}%">${level}</span>`).join("")}</div></div>${rows}</div></div><div class="coverage-summary">${stats}</div><p class="coverage-note">Dots are authored weapon peak levels. White line: player level. Blue line: most likely box target. Shaded band: full target range. Click a dot to open a matching weapon in the Weapons tab when one appeared in the simulation.</p></section>`;
  }

  function injectCoverage() {
    if (!analysisButton.classList.contains("active")) return;
    const content = presentation.querySelector("#analysisTabContent");
    const levelsActive = presentation.querySelector('.analysis-tab.active[data-analysis-tab="levels"]');
    if (!content || !levelsActive || content.querySelector(".coverage-panel")) return;

    const context = displayedReportContext();
    if (!context) return;
    const revision = ++renderRevision;
    content.insertAdjacentHTML(
      "afterbegin",
      `<section class="coverage-panel coverage-loading" data-coverage-signature="${escapeText(context.signature)}">Loading live catalogue coverage…</section>`
    );

    loadProbe(context).then(probe => {
      if (revision !== renderRevision) return;
      const currentContent = presentation.querySelector("#analysisTabContent");
      const currentContext = displayedReportContext();
      const currentLevels = presentation.querySelector('.analysis-tab.active[data-analysis-tab="levels"]');
      const placeholder = currentContent?.querySelector(".coverage-loading");
      if (!currentContent || !currentLevels || !currentContext || currentContext.signature !== context.signature) return;
      if (!placeholder || placeholder.dataset.coverageSignature !== context.signature) return;
      placeholder.outerHTML = coverageHtml(probe, context.signature);
    }).catch(error => {
      if (revision !== renderRevision) return;
      const currentContent = presentation.querySelector("#analysisTabContent");
      const currentContext = displayedReportContext();
      const placeholder = currentContent?.querySelector(".coverage-loading");
      if (!currentContent || !currentContext || currentContext.signature !== context.signature || !placeholder) return;
      placeholder.outerHTML = `<section class="coverage-panel" data-coverage-signature="${escapeText(context.signature)}"><h3>Catalogue level coverage</h3><p>${escapeText(error.message || error)}</p></section>`;
    });
  }

  presentation.addEventListener("click", event => {
    const marker = event.target instanceof Element
      ? event.target.closest("[data-coverage-definitions]")
      : null;
    if (!marker) return;

    let definitionIds = [];
    try {
      definitionIds = JSON.parse(decodeURIComponent(marker.dataset.coverageDefinitions || ""));
    } catch (_) {
      return;
    }
    presentation.querySelector('[data-analysis-tab="weapons"]')?.click();
    requestAnimationFrame(() => {
      const buttons = [...presentation.querySelectorAll("[data-analysis-weapon]")];
      const weaponButton = buttons.find(button =>
        definitionIds.includes(button.dataset.analysisWeapon)
      );
      weaponButton?.click();
    });
  });

  new MutationObserver(injectCoverage).observe(presentation, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ["class"]
  });

  injectCoverage();
})();
