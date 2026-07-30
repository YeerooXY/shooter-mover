const levelsContainer = document.getElementById("levelsContainer");
const levelTemplate = document.getElementById("levelTemplate");
const emptyLevels = document.getElementById("emptyLevels");
const previewDialog = document.getElementById("previewDialog");
const previewOutput = document.getElementById("previewOutput");
const statusMessage = document.getElementById("statusMessage");

function valueOf(card, selector) {
  return card.querySelector(selector).value;
}

function numberOf(card, selector) {
  const parsed = Number(valueOf(card, selector));
  return Number.isFinite(parsed) ? parsed : 0;
}

function getLevelCards() {
  return [...levelsContainer.querySelectorAll(".level-card")];
}

function updateEmptyState() {
  emptyLevels.hidden = getLevelCards().length > 0;
}

function updateLevelHeading(card) {
  const levelNumber = numberOf(card, ".level-number");
  const hp = numberOf(card, ".max-hp");
  const shootingType = valueOf(card, ".shooting-type");

  card.querySelector(".level-heading").textContent = `Level ${levelNumber || "?"}`;
  card.querySelector(".level-summary").textContent = `${hp} HP · ${shootingType}`;
}

function wireLevelCard(card) {
  card.querySelectorAll("input, select, textarea").forEach((element) => {
    element.addEventListener("input", () => updateLevelHeading(card));
    element.addEventListener("change", () => updateLevelHeading(card));
  });

  card.querySelector(".remove-level").addEventListener("click", () => {
    card.remove();
    updateEmptyState();
  });

  card.querySelector(".duplicate-level").addEventListener("click", () => {
    const data = collectLevelData(card);
    data.level += 1;
    addLevel(data);
  });

  card.querySelector(".collapse-level").addEventListener("click", (event) => {
    card.classList.toggle("collapsed");
    event.currentTarget.textContent =
      card.classList.contains("collapsed") ? "Expand" : "Collapse";
  });

  updateLevelHeading(card);
}

function setValue(card, selector, value) {
  const element = card.querySelector(selector);
  if (value !== undefined && value !== null) {
    element.value = value;
  }
}

function addLevel(initialData = {}) {
  const card = levelTemplate.content.firstElementChild.cloneNode(true);
  const suggestedLevel = getLevelCards().length + 1;

  setValue(card, ".level-number", initialData.level ?? suggestedLevel);
  setValue(card, ".max-hp", initialData.maxHp);
  setValue(card, ".contact-damage", initialData.contactDamage);
  setValue(card, ".movement-speed", initialData.movementSpeed);
  setValue(card, ".detection-range", initialData.detectionRange);
  setValue(card, ".attack-range", initialData.attackRange);
  setValue(card, ".drop-chance", initialData.dropChancePercent);
  setValue(card, ".loot-table", initialData.lootTableReference);

  const shooting = initialData.shooting ?? {};
  setValue(card, ".shooting-type", shooting.type);
  setValue(card, ".projectile-reference", shooting.projectileReference);
  setValue(card, ".projectile-damage", shooting.projectileDamage);
  setValue(card, ".rate-of-fire", shooting.rateOfFire);
  setValue(card, ".projectile-speed", shooting.projectileSpeed);
  setValue(card, ".projectile-count", shooting.projectileCount);
  setValue(card, ".burst-size", shooting.burstSize);
  setValue(card, ".burst-delay", shooting.timeBetweenBursts);
  setValue(card, ".horizontal-spread", shooting.horizontalSpreadDegrees);
  setValue(card, ".vertical-spread", shooting.verticalSpreadDegrees);
  setValue(card, ".spread-distribution", shooting.spreadDistribution);
  setValue(card, ".level-notes", initialData.notes);

  wireLevelCard(card);
  levelsContainer.appendChild(card);
  updateEmptyState();
  card.scrollIntoView({ behavior: "smooth", block: "nearest" });
}

function collectLevelData(card) {
  return {
    level: numberOf(card, ".level-number"),
    maxHp: numberOf(card, ".max-hp"),
    contactDamage: numberOf(card, ".contact-damage"),
    movementSpeed: numberOf(card, ".movement-speed"),
    detectionRange: numberOf(card, ".detection-range"),
    attackRange: numberOf(card, ".attack-range"),
    dropChancePercent: numberOf(card, ".drop-chance"),
    lootTableReference: valueOf(card, ".loot-table").trim(),
    shooting: {
      type: valueOf(card, ".shooting-type"),
      projectileReference: valueOf(card, ".projectile-reference").trim(),
      projectileDamage: numberOf(card, ".projectile-damage"),
      rateOfFire: numberOf(card, ".rate-of-fire"),
      projectileSpeed: numberOf(card, ".projectile-speed"),
      projectileCount: numberOf(card, ".projectile-count"),
      burstSize: numberOf(card, ".burst-size"),
      timeBetweenBursts: numberOf(card, ".burst-delay"),
      horizontalSpreadDegrees: numberOf(card, ".horizontal-spread"),
      verticalSpreadDegrees: numberOf(card, ".vertical-spread"),
      spreadDistribution: valueOf(card, ".spread-distribution")
    },
    notes: valueOf(card, ".level-notes").trim()
  };
}

function collectEnemyData() {
  return {
    enemyId: document.getElementById("enemyId").value.trim(),
    displayName: document.getElementById("displayName").value.trim(),
    enemyType: document.getElementById("enemyType").value,
    assetReference: document.getElementById("prefabReference").value.trim(),
    tags: document
      .getElementById("tags")
      .value.split(",")
      .map((tag) => tag.trim())
      .filter(Boolean),
    description: document.getElementById("description").value.trim(),
    notes: document.getElementById("globalNotes").value.trim(),
    levels: getLevelCards().map(collectLevelData)
  };
}

/*
  Replace this function later if the project needs YAML, XML, CSV,
  a custom binary format, an API request, or another serializer.
*/
function serializeEnemyData(data) {
  return JSON.stringify(data, null, 2);
}

function createExport() {
  const data = collectEnemyData();
  return serializeEnemyData(data);
}

function safeFilename() {
  const enemyId = document.getElementById("enemyId").value.trim();
  const base = enemyId || "enemy-definition";
  return `${base.replace(/[^a-z0-9_-]+/gi, "-").toLowerCase()}.json`;
}

function showStatus(message) {
  statusMessage.textContent = message;
  statusMessage.style.display = "block";
  window.clearTimeout(showStatus.timeoutId);
  showStatus.timeoutId = window.setTimeout(() => {
    statusMessage.style.display = "none";
  }, 2600);
}

async function copyExport() {
  const output = createExport();
  try {
    await navigator.clipboard.writeText(output);
    showStatus("Enemy data copied to the clipboard.");
  } catch {
    previewOutput.textContent = output;
    previewDialog.showModal();
    showStatus("Clipboard access was blocked. The export is open for manual copying.");
  }
}

function downloadExport() {
  const output = createExport();
  const blob = new Blob([output], { type: "application/json;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");

  link.href = url;
  link.download = safeFilename();
  document.body.appendChild(link);
  link.click();
  link.remove();

  URL.revokeObjectURL(url);
  showStatus(`Exported ${safeFilename()}`);
}

document.getElementById("addLevelButton").addEventListener("click", () => addLevel());

document.getElementById("previewButton").addEventListener("click", () => {
  previewOutput.textContent = createExport();
  previewDialog.showModal();
});

document.getElementById("closePreviewButton").addEventListener("click", () => {
  previewDialog.close();
});

document.getElementById("copyButton").addEventListener("click", copyExport);
document.getElementById("copyPreviewButton").addEventListener("click", copyExport);
document.getElementById("downloadButton").addEventListener("click", downloadExport);
document.getElementById("downloadPreviewButton").addEventListener("click", downloadExport);

document.getElementById("resetButton").addEventListener("click", () => {
  const shouldReset = window.confirm(
    "Clear all enemy fields and remove every level?"
  );

  if (!shouldReset) return;

  document.getElementById("enemyForm").reset();
  levelsContainer.innerHTML = "";
  addLevel();
  showStatus("Enemy definition reset.");
});

previewDialog.addEventListener("click", (event) => {
  if (event.target === previewDialog) {
    previewDialog.close();
  }
});

addLevel();
