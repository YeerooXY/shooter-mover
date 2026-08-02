"use strict";

(() => {
  // The balance panel can render before the async startup request finishes.
  // Seed the textarea immediately so an early parse cannot replace the default
  // weapon file with an empty string.
  if (typeof elements !== "undefined"
    && typeof files !== "undefined"
    && elements.jsonEditor
    && !elements.jsonEditor.value) {
    elements.jsonEditor.value = files[activeFile];
  }

  const has = (value, key) => Object.prototype.hasOwnProperty.call(value || {}, key);
  const objectValue = value => value && typeof value === "object" && !Array.isArray(value);
  const positiveValue = value => typeof value === "number" && Number.isFinite(value) && value > 0;
  const nonNegativeValue = value => typeof value === "number" && Number.isFinite(value) && value >= 0;

  function addError(result, message) {
    if (!result.errors.includes(message)) result.errors.push(message);
  }

  function requireTextValue(result, value, label) {
    if (typeof value !== "string" || !value.trim()) addError(result, `${label} is required.`);
  }

  function validateOwnership(result, shared, marks, block, required) {
    const sharedHasBlock = has(shared, block);
    const markCount = marks.filter(mark => has(mark, block)).length;
    if (sharedHasBlock && markCount) {
      addError(result, `${block}: choose shared values or Mark-specific values, not both.`);
    }
    if (!sharedHasBlock && markCount !== 0 && markCount !== 3) {
      addError(result, `${block}: all three Marks must provide the complete values.`);
    }
    if (required && !sharedHasBlock && markCount !== 3) {
      addError(result, `${block}: gameplay values are required.`);
    }
  }

  function validateEffectiveFire(result, value, label) {
    if (!objectValue(value) || !positiveValue(value.rate)) {
      addError(result, `${label} cycles per second must be positive.`);
      return;
    }
    if (!["automatic", "semi-automatic", "burst"].includes(value.mode)) {
      addError(result, `${label} trigger mode is unsupported.`);
      return;
    }
    if (value.mode !== "burst") return;
    if (!Number.isInteger(value.shotsPerBurst) || value.shotsPerBurst < 2) {
      addError(result, `${label} burst requires at least two shots.`);
    }
    if (!positiveValue(value.secondsBetweenShots)) {
      addError(result, `${label} burst shot gap must be positive.`);
      return;
    }
    if (Number.isInteger(value.shotsPerBurst)
      && (1 / value.rate) <= (value.shotsPerBurst - 1) * value.secondsBetweenShots) {
      addError(result, `${label} burst timing leaves no recovery time after the final shot.`);
    }
  }

  function validateDotNumbers(result, value, label) {
    if (!objectValue(value)
      || !positiveValue(value.damagePerSecond)
      || !positiveValue(value.duration)
      || !positiveValue(value.ticksPerSecond)
      || !Number.isInteger(value.maxStacks)
      || value.maxStacks < 1) {
      addError(result, `${label} damage-over-time values must be positive, with whole-number stacks.`);
    }
  }

  function validateDotOwnership(result, shared, marks) {
    const sharedDot = has(shared, "dot") ? shared.dot : null;
    const markCount = marks.filter(mark => has(mark, "dot")).length;
    const sharedHasNumbers = objectValue(sharedDot)
      && ["damagePerSecond", "duration", "ticksPerSecond", "maxStacks"].some(key => has(sharedDot, key));

    if (!sharedDot && markCount !== 0 && markCount !== 3) {
      addError(result, "dot: all three Marks must provide damage-over-time values.");
      return { sharedHasNumbers: false, sharedDot: null };
    }
    if (sharedHasNumbers && markCount) {
      addError(result, "dot: shared numerical values cannot also be defined by Marks.");
    }
    if (sharedDot && !sharedHasNumbers && markCount !== 3) {
      addError(result, "dot: shared refresh behaviour requires damage-over-time values in all three Marks.");
    }

    if (sharedHasNumbers) {
      validateDotNumbers(result, sharedDot, "Shared");
      if (typeof sharedDot.refreshDuration !== "boolean") {
        addError(result, "Shared damage-over-time refresh behaviour must be true or false.");
      }
    } else if (sharedDot) {
      if (typeof sharedDot.refreshDuration !== "boolean") {
        addError(result, "Shared damage-over-time refresh behaviour must be true or false.");
      }
      marks.forEach((mark, index) => {
        if (!has(mark, "dot")) return;
        validateDotNumbers(result, mark.dot, `MK${index + 1}`);
        if (has(mark.dot, "refreshDuration")) {
          addError(result, `MK${index + 1} refresh behaviour must be defined once in shared weapon data.`);
        }
      });
    } else {
      marks.forEach((mark, index) => {
        if (!has(mark, "dot")) return;
        validateDotNumbers(result, mark.dot, `MK${index + 1}`);
        if (typeof mark.dot.refreshDuration !== "boolean") {
          addError(result, `MK${index + 1} damage-over-time refresh behaviour must be true or false.`);
        }
      });
    }

    return { sharedHasNumbers, sharedDot };
  }

  // Replace the lightweight first-pass checks with checks based on each Mark's
  // effective shared + Mark-specific data. This mirrors the validator's
  // ownership model and does not reject older valid authored families.
  validateBrowserData = function validateEffectiveWeaponData(result) {
    if (result.errors.length) return;
    const shared = result.parsed["weapon.json"];
    const marks = [1, 2, 3].map(mark => result.parsed[`mk${mark}.json`]);

    requireTextValue(result, shared.name, "Weapon name");
    requireTextValue(result, shared.category, "Weapon category");
    requireTextValue(result, shared.rarity, "Rarity");
    requireTextValue(result, shared.projectileType, "Projectile type");
    requireTextValue(result, shared.damageType, "Damage type");
    if (shared.category !== elements.categoryInput.value.trim()) {
      addError(result, "The selected weapon type and saved category folder do not match.");
    }
    if (shared.category === "special") {
      addError(result, "Special weapons are not implemented yet. Choose a supported weapon type.");
    }

    validateOwnership(result, shared, marks, "fire", true);
    validateOwnership(result, shared, marks, "shot", true);
    validateOwnership(result, shared, marks, "impact", true);
    validateOwnership(result, shared, marks, "homing", false);
    if (shared.projectileType !== "beam") validateOwnership(result, shared, marks, "projectile", true);
    validateDotOwnership(result, shared, marks);

    if (!objectValue(shared.art)) {
      addError(result, "Shared projectile art settings are required.");
    } else {
      requireTextValue(result, shared.art.delivery, "Projectile / beam art");
      requireTextValue(result, shared.art.trail, "Trail art");
      requireTextValue(result, shared.art.impact, "Impact art");
      if (has(shared.art, "mounted")) requireTextValue(result, shared.art.mounted, "Shared mounted art");
    }
    const sharedMounted = objectValue(shared.art)
      && typeof shared.art.mounted === "string"
      && !!shared.art.mounted.trim();
    const markMountedCount = marks.filter(mark => objectValue(mark.art) && has(mark.art, "mounted")).length;
    if (sharedMounted && markMountedCount) {
      addError(result, "Mounted art must be shared or Mark-specific, not both.");
    }
    if (!sharedMounted && markMountedCount !== 3) {
      addError(result, "All three Marks must provide mounted art when it is not shared.");
    }

    const anyExplosion = marks.some(mark => objectValue(mark.explosion));
    if (anyExplosion && shared.projectileType !== "rocket") {
      addError(result, "Explosion values are only supported for rocket weapons.");
    }
    if (anyExplosion && !marks.every(mark => objectValue(mark.explosion))) {
      addError(result, "Explosion values must be defined for all three Marks.");
    }

    marks.forEach((mark, index) => {
      const label = `MK${index + 1}`;
      if (!Number.isInteger(mark.peakLevel) || mark.peakLevel < 1) {
        addError(result, `${label} drop level must be a positive whole number.`);
      }
      if (!positiveValue(mark.damage)) addError(result, `${label} damage must be positive.`);
      if (!objectValue(mark.art)) {
        addError(result, `${label} art settings are required.`);
      } else {
        requireTextValue(result, mark.art.side, `${label} side art`);
        if (!sharedMounted) requireTextValue(result, mark.art.mounted, `${label} mounted art`);
      }

      validateEffectiveFire(result, shared.fire || mark.fire, label);

      const shot = shared.shot || mark.shot;
      if (!objectValue(shot)
        || !Number.isInteger(shot.projectiles)
        || shot.projectiles < 1
        || !nonNegativeValue(shot.spread)) {
        addError(result, `${label} shot count must be a positive whole number and spread cannot be negative.`);
      }

      if (shared.projectileType === "beam") {
        const beam = shared.beam;
        if (!objectValue(beam) || !positiveValue(beam.range) || !positiveValue(beam.width)) {
          addError(result, `${label} beam range and width must be positive.`);
        }
      } else {
        const projectile = shared.projectile || mark.projectile;
        if (!objectValue(projectile)
          || !positiveValue(projectile.speed)
          || !positiveValue(projectile.radius)
          || !positiveValue(projectile.range)) {
          addError(result, `${label} projectile speed, radius, and range must be positive.`);
        }
      }

      const impact = shared.impact || mark.impact;
      if (!objectValue(impact)
        || !Number.isInteger(impact.pierce)
        || impact.pierce < 0
        || !nonNegativeValue(impact.ricochet)
        || !nonNegativeValue(impact.knockback)) {
        addError(result, `${label} pierce must be a non-negative whole number; ricochet and knockback cannot be negative.`);
      }

      const homing = shared.homing || mark.homing;
      if (homing && (!objectValue(homing)
        || !positiveValue(homing.acquisitionRange)
        || !positiveValue(homing.turnRate)
        || !nonNegativeValue(homing.activationDelay))) {
        addError(result, `${label} homing range and turn speed must be positive, and activation delay cannot be negative.`);
      }

      if (objectValue(mark.explosion)
        && (!positiveValue(mark.explosion.radius)
          || !nonNegativeValue(mark.explosion.edgeDamageMultiplier)
          || mark.explosion.edgeDamageMultiplier > 1)) {
        addError(result, `${label} explosion radius must be positive and outer-edge damage must be between 0 and 1.`);
      }
    });
  };

  const labelReplacements = new Map([
    ["Cycles / second", "Cycles / second"],
    ["Burst shot gap", "Burst shot gap (sec)"],
    ["Speed", "Speed (world units/sec)"],
    ["Radius", "Radius (world units)"],
    ["Range", "Range (world units)"],
    ["Width", "Width (world units)"],
    ["Explosion radius", "Explosion radius (world units)"],
    ["Search range", "Search range (world units)"],
    ["Turn speed", "Turn speed (degrees/sec)"],
    ["Activation delay", "Activation delay (sec)"],
    ["Duration", "Duration (sec)"],
    ["Ticks / second", "Ticks / second"]
  ]);

  function improveGameplayForm() {
    document.querySelectorAll("#weaponGlobalEditor label, #gameplayEditor .field > label").forEach(label => {
      const replacement = labelReplacements.get(label.textContent.trim());
      if (replacement) label.textContent = replacement;
    });

    const typeSelect = document.querySelector('[data-g-key="settings.weaponType"]');
    const special = typeSelect?.querySelector('option[value="special"]');
    if (special) {
      special.disabled = true;
      special.textContent = "Special — not implemented";
      special.title = "Dedicated special-weapon delivery is not implemented yet.";
    }
  }

  const editorRoot = document.querySelector(".weapon-workspace");
  if (editorRoot) {
    new MutationObserver(improveGameplayForm).observe(editorRoot, {
      childList: true,
      subtree: true
    });
  }

  document.addEventListener("weapon-maker-change", improveGameplayForm);
  improveGameplayForm();
})();
