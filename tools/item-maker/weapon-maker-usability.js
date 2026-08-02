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
