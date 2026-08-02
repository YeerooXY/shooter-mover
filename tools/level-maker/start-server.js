"use strict";

const http = require("http");
const path = require("path");
const { createEditorFileService } = require("./editor-file");

const args = process.argv.slice(2);
const repo = path.resolve(argument("--repo") || path.join(__dirname, "..", ".."));
const port = Number(argument("--port") || 4174);
const editorFiles = createEditorFileService(repo, port);
const createServer = http.createServer.bind(http);

function argument(name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : "";
}

http.createServer = function createLevelMakerServer(options, listener) {
  let serverOptions = options;
  let requestListener = listener;
  if (typeof options === "function") {
    requestListener = options;
    serverOptions = undefined;
  }

  const wrappedListener = async (request, response) => {
    try {
      if (await editorFiles.handle(request, response)) return;
      return requestListener(request, response);
    } catch (error) {
      const body = JSON.stringify({ error: error.message });
      response.writeHead(400, {
        "Content-Type": "application/json; charset=utf-8",
        "Content-Length": Buffer.byteLength(body),
        "Cache-Control": "no-store",
      });
      response.end(body);
    }
  };

  return serverOptions === undefined
    ? createServer(wrappedListener)
    : createServer(serverOptions, wrappedListener);
};

require("./server");
