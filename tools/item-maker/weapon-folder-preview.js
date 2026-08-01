"use strict";

const compiledPanel = document.createElement("section");
compiledPanel.className = "panel";
compiledPanel.innerHTML = `
  <div class="panel-head">Compiled Preview</div>
  <div class="panel-body">
    <div class="help" style="margin-bottom:8px">Read-only merged definitions. Repository save still runs the full folder validator.</div>
    <div id="compiledPreview" class="json-preview" style="max-height:420px;overflow:auto"></div>
  </div>`;

document.querySelector(".weapon-summary").insertBefore(
  compiledPanel,
  document.querySelector(".weapon-summary").children[1]);

const compiledPreview = compiledPanel.querySelector("#compiledPreview");
compiledPreview.textContent = "Waiting for editor data…";

function previewObject(value) {
  return value && typeof value === "object" && !Array.isArray(value);
}

function mergePreviewObjects(shared, mark) {
  const result = {};
  for (const [key, value] of Object.entries(shared)) {
    result[key] = previewObject(value) ? mergePreviewObjects(value, {}) : value;
  }
  for (const [key, value] of Object.entries(mark)) {
    result[key] = previewObject(value) && previewObject(result[key])
      ? mergePreviewObjects(result[key], value)
      : value;
  }
  return result;
}

function renderCompiledPreview() {
  const result = parseFiles();
  const folder = elements.folderInput.value.trim();
  const category = elements.categoryInput.value.trim();
  if (result.errors.length
      || !/^[a-z0-9]+(?:[-_][a-z0-9]+)*$/.test(category)
      || !/^[a-z0-9]+(?:_[a-z0-9]+)*$/.test(folder)) {
    compiledPreview.textContent = "Fix the local JSON and folder checks to see compiled definitions.";
    return;
  }

  const shared = result.parsed["weapon.json"];
  const definitions = [1, 2, 3].map(mark => ({
    ...mergePreviewObjects(shared, result.parsed[`mk${mark}.json`]),
    definitionId: `gun_${folder}_mk${mark}_01`,
    familyId: folder,
    mark,
    variant: 1
  }));

  compiledPreview.textContent = JSON.stringify({
    familyId: folder,
    categoryFolder: category,
    sourceFiles: fileNames,
    definitions
  }, null, 2);
}

[elements.jsonEditor, elements.categoryInput, elements.folderInput].forEach(control =>
  control.addEventListener("input", renderCompiledPreview));

new MutationObserver(renderCompiledPreview).observe(
  elements.fileTabs,
  { childList: true });
