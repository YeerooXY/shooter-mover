"use strict";

const fs = require("fs");
const path = require("path");

const IMAGE_EXTENSIONS = new Set([".png", ".jpg", ".jpeg", ".webp", ".gif"]);
const IMAGE_CONTENT_TYPES = {
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
  ".gif": "image/gif",
};
const PREVIEW_KEY = /(sprite|icon|image|preview|thumbnail|texture|art|visual|portrait)/i;

function walk(folder, visit) {
  if (!fs.existsSync(folder)) return;
  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    const fullPath = path.join(folder, entry.name);
    if (entry.isDirectory()) walk(fullPath, visit);
    else visit(fullPath);
  }
}

function slash(value) {
  return String(value || "").replace(/\\/g, "/");
}

function compact(value) {
  return String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, "");
}

function assertInside(file, root, message) {
  const relative = path.relative(path.resolve(root), path.resolve(file));
  if (relative.startsWith("..") || path.isAbsolute(relative)) throw new Error(message);
}

function assetIds(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return [];
  return [value.id, value.object, value.definition_id, value.runtime_object]
    .filter(id => typeof id === "string" && /^(enemy|prop|tile|door|decor|presentation)\./.test(id));
}

function collectStrings(value, output, depth = 0) {
  if (depth > 5 || value == null) return;
  if (typeof value === "string") {
    output.add(value);
    return;
  }
  if (Array.isArray(value)) {
    value.forEach(item => collectStrings(item, output, depth + 1));
    return;
  }
  if (typeof value !== "object") return;
  Object.values(value).forEach(item => collectStrings(item, output, depth + 1));
}

function directPreviewReferences(value) {
  const output = new Set();
  if (!value || typeof value !== "object" || Array.isArray(value)) return output;
  for (const [key, item] of Object.entries(value)) {
    if (typeof item === "string") {
      const extension = path.extname(item.split(/[?#]/, 1)[0]).toLowerCase();
      if (PREVIEW_KEY.test(key) || IMAGE_EXTENSIONS.has(extension)) output.add(item);
      continue;
    }
    if (PREVIEW_KEY.test(key)) collectStrings(item, output);
  }
  return output;
}

function collectHints(value, hints) {
  if (Array.isArray(value)) {
    value.forEach(item => collectHints(item, hints));
    return;
  }
  if (!value || typeof value !== "object") return;
  const ids = assetIds(value);
  if (ids.length) {
    const references = directPreviewReferences(value);
    if (references.size) {
      ids.forEach(id => {
        const list = hints.get(id) || new Set();
        references.forEach(reference => list.add(reference));
        hints.set(id, list);
      });
    }
  }
  Object.values(value).forEach(item => collectHints(item, hints));
}

function createAssetPreviewService(repo) {
  const assetsRoot = path.join(repo, "Assets");
  let cache = null;
  const parsedSources = new Map();

  function imageIndex() {
    const images = [];
    walk(assetsRoot, file => {
      const extension = path.extname(file).toLowerCase();
      if (!IMAGE_EXTENSIONS.has(extension)) return;
      const relative = slash(path.relative(repo, file));
      const withoutExtension = relative.slice(0, -extension.length);
      images.push({
        file,
        relative,
        extension,
        pathKey: compact(withoutExtension),
        stemKey: compact(path.basename(withoutExtension)),
        directoryKey: compact(path.dirname(withoutExtension)),
      });
    });
    return images;
  }

  function hintsForSource(source, ids) {
    const normalized = slash(source);
    if (!normalized || !normalized.endsWith(".json")) return new Map();
    if (!parsedSources.has(normalized)) {
      const hints = new Map();
      try {
        const file = path.resolve(repo, normalized);
        assertInside(file, assetsRoot, "Asset definition escaped the Assets folder.");
        if (fs.existsSync(file) && fs.statSync(file).size <= 4_000_000) {
          collectHints(JSON.parse(fs.readFileSync(file, "utf8")), hints);
        }
      } catch {
        // A malformed or unrelated file simply has no preview hints.
      }
      parsedSources.set(normalized, hints);
    }
    const all = parsedSources.get(normalized);
    const selected = new Map();
    ids.forEach(id => {
      if (all.has(id)) selected.set(id, all.get(id));
    });
    return selected;
  }

  function resolveReference(reference, source, images) {
    const raw = slash(reference).trim().replace(/^file:\/\//i, "").split(/[?#]/, 1)[0];
    if (!raw || raw.length > 500) return null;
    const sourceFolder = path.dirname(source || "");
    const candidates = new Set([raw]);
    if (!raw.startsWith("Assets/")) {
      candidates.add(slash(path.join(sourceFolder, raw)));
      candidates.add(`Assets/${raw.replace(/^\/+/, "")}`);
      candidates.add(`Assets/ShooterMover/Resources/${raw.replace(/^Resources\//i, "")}`);
    }

    for (const candidate of candidates) {
      const normalized = candidate.replace(/^\.\//, "");
      const extension = path.extname(normalized).toLowerCase();
      const forms = extension ? [normalized] : [...IMAGE_EXTENSIONS].map(ext => `${normalized}${ext}`);
      for (const form of forms) {
        const absolute = path.resolve(repo, form);
        try {
          assertInside(absolute, assetsRoot, "Preview image escaped the Assets folder.");
        } catch {
          continue;
        }
        if (fs.existsSync(absolute) && fs.statSync(absolute).isFile()
            && IMAGE_EXTENSIONS.has(path.extname(absolute).toLowerCase())) {
          return slash(path.relative(repo, absolute));
        }
      }
    }

    const key = compact(raw.replace(/\.[^.]+$/, ""));
    if (!key) return null;
    const suffixMatches = images.filter(image => image.pathKey.endsWith(key));
    if (suffixMatches.length === 1) return suffixMatches[0].relative;
    const stemMatches = images.filter(image => image.stemKey === compact(path.basename(raw, path.extname(raw))));
    return stemMatches.length === 1 ? stemMatches[0].relative : null;
  }

  function fallbackPreview(asset, images) {
    const idTail = compact(String(asset.id || "").split(".").pop());
    const label = compact(asset.label || "");
    if (!idTail && !label) return null;
    const sourceDirectory = compact(path.dirname(asset.source || ""));
    let best = null;
    let bestScore = 0;
    let tied = false;

    for (const image of images) {
      let score = 0;
      if (idTail && image.stemKey === idTail) score += 130;
      if (label && image.stemKey === label) score += 120;
      if (idTail.length >= 5 && image.stemKey.includes(idTail)) score += 82;
      if (label.length >= 5 && image.stemKey.includes(label)) score += 72;
      if (idTail.length >= 5 && image.pathKey.endsWith(idTail)) score += 92;
      if (sourceDirectory && image.directoryKey.includes(sourceDirectory.slice(-40))) score += 16;
      if (asset.type && image.pathKey.includes(compact(asset.type))) score += 4;
      if (score > bestScore) {
        best = image;
        bestScore = score;
        tied = false;
      } else if (score === bestScore && score > 0) {
        tied = true;
      }
    }

    return best && bestScore >= 82 && !tied ? best.relative : null;
  }

  function build(assets) {
    const images = imageIndex();
    const previews = {};
    const bySource = new Map();
    for (const asset of assets || []) {
      const source = slash(asset.source);
      if (!bySource.has(source)) bySource.set(source, []);
      bySource.get(source).push(asset.id);
    }

    const hints = new Map();
    for (const [source, ids] of bySource) {
      for (const [id, values] of hintsForSource(source, ids)) hints.set(id, values);
    }

    for (const asset of assets || []) {
      let relative = null;
      for (const reference of hints.get(asset.id) || []) {
        relative = resolveReference(reference, asset.source, images);
        if (relative) break;
      }
      relative ||= fallbackPreview(asset, images);
      if (relative) {
        previews[asset.id] = `/api/asset-image?path=${encodeURIComponent(relative)}`;
      }
    }
    return { previews, imageCount: images.length };
  }

  function previews(assets) {
    cache ||= build(assets);
    return cache;
  }

  function sendImage(response, relativePath) {
    const relative = slash(relativePath).replace(/^\/+/, "");
    const file = path.resolve(repo, relative);
    assertInside(file, assetsRoot, "Preview image escaped the Assets folder.");
    const extension = path.extname(file).toLowerCase();
    if (!IMAGE_EXTENSIONS.has(extension) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
      throw new Error("Preview image was not found.");
    }
    const value = fs.readFileSync(file);
    response.writeHead(200, {
      "Content-Type": IMAGE_CONTENT_TYPES[extension] || "application/octet-stream",
      "Content-Length": value.length,
      "Cache-Control": "no-cache",
    });
    response.end(value);
  }

  return { previews, sendImage };
}

module.exports = { createAssetPreviewService };
