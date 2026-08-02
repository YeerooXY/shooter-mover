"use strict";

const fs = require("fs");
const path = require("path");
const { URL } = require("url");

function createEditorFileService(repo, port) {
  const editorRoot = path.join(
    repo,
    "Library",
    "ShooterMover",
    "LevelMaker"
  );

  function slug(value) {
    const result = String(value || "").trim().toLowerCase();
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(result)) {
      throw new Error("Level target must use lowercase words separated by hyphens.");
    }
    return result;
  }

  function editorPath(target) {
    return path.join(editorRoot, `${slug(target)}.editor.json`);
  }

  function atomicWrite(file, text) {
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const temporary = `${file}.${process.pid}.tmp`;
    fs.writeFileSync(temporary, text, "utf8");
    fs.renameSync(temporary, file);
  }

  function send(response, status, value) {
    const body = JSON.stringify(value);
    response.writeHead(status, {
      "Content-Type": "application/json; charset=utf-8",
      "Content-Length": Buffer.byteLength(body),
      "Cache-Control": "no-store",
    });
    response.end(body);
  }

  function readBody(request) {
    return new Promise((resolve, reject) => {
      let value = "";
      request.on("data", chunk => {
        value += chunk;
        if (value.length > 200_000) reject(new Error("Editor state is too large."));
      });
      request.on("end", () => {
        try {
          resolve(JSON.parse(value || "{}"));
        } catch (error) {
          reject(error);
        }
      });
      request.on("error", reject);
    });
  }

  function isLocalEditor(request) {
    const origin = String(request.headers.origin || "");
    const referer = String(request.headers.referer || "");
    const allowed = [
      `http://127.0.0.1:${port}`,
      `http://localhost:${port}`,
    ];
    return allowed.some(value => origin === value || referer.startsWith(`${value}/`));
  }

  function checkEditor(editor) {
    if (!editor || Number(editor.version) !== 1) {
      throw new Error("Editor state version is invalid.");
    }
    if (!/^level\.[a-z0-9]+(?:-[a-z0-9]+)*$/.test(String(editor.levelId || ""))) {
      throw new Error("Editor state level ID is invalid.");
    }
    if (!editor.editor || typeof editor.editor !== "object") {
      throw new Error("Editor state is missing its editor settings.");
    }
  }

  async function handle(request, response) {
    const url = new URL(
      request.url,
      `http://${request.headers.host || `127.0.0.1:${port}`}`
    );
    if (url.pathname !== "/api/level-editor") return false;

    if (request.method === "GET") {
      const file = editorPath(url.searchParams.get("target"));
      if (!fs.existsSync(file)) {
        send(response, 200, { savedAt: "", editor: null });
        return true;
      }
      const saved = JSON.parse(fs.readFileSync(file, "utf8"));
      send(response, 200, saved);
      return true;
    }

    if (request.method !== "PUT" && request.method !== "POST") {
      send(response, 405, { error: "Method not allowed." });
      return true;
    }

    if (!isLocalEditor(request)) {
      throw new Error("Editor state writes are only accepted from the local Level Maker.");
    }

    const value = await readBody(request);
    const target = slug(value.target);
    checkEditor(value.editor);
    const savedAt = String(value.savedAt || new Date().toISOString());
    const file = editorPath(target);
    atomicWrite(
      file,
      `${JSON.stringify({ savedAt, editor: value.editor }, null, 2)}\n`
    );
    send(response, 200, {
      editorPath: path.relative(repo, file).replace(/\\/g, "/"),
      savedAt,
    });
    return true;
  }

  return { handle };
}

module.exports = { createEditorFileService };
