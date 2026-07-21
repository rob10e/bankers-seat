import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { join } from "node:path";
import { loadSchemaValidator, validateTemplateFile, } from "../validation.js";
const DEFAULT_SCHEMA_PATH = join(process.cwd(), "..", "..", "templates", "schema", "game-template.schema.json");
const getMimeType = (filePath) => {
    const ext = filePath.substring(filePath.lastIndexOf(".")).toLowerCase();
    const mimeTypes = {
        ".json": "application/json",
        ".html": "text/html",
        ".css": "text/css",
        ".js": "text/javascript",
        ".mjs": "text/javascript",
        ".png": "image/png",
        ".jpg": "image/jpeg",
        ".jpeg": "image/jpeg",
        ".webp": "image/webp",
        ".svg": "image/svg+xml",
        ".gif": "image/gif",
        ".webm": "video/webm",
        ".mp4": "video/mp4",
    };
    return mimeTypes[ext] || "application/octet-stream";
};
const sendJson = (res, data, status = 200) => {
    res.writeHead(status, { "Content-Type": "application/json" });
    res.end(JSON.stringify(data, null, 2));
};
const sendFile = async (res, filePath) => {
    try {
        const content = await readFile(filePath);
        res.writeHead(200, {
            "Content-Type": getMimeType(filePath),
            "Content-Length": content.length,
        });
        res.end(content);
    }
    catch (error) {
        res.writeHead(404, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ error: "Not found" }));
    }
};
const sendHtml = (res, html) => {
    res.writeHead(200, {
        "Content-Type": "text/html",
        "Content-Length": html.length,
    });
    res.end(html);
};
const HTML_PAGE = `
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Banker's Seat Template Preview</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
    }
    .container {
      background: white;
      border-radius: 12px;
      box-shadow: 0 20px 60px rgba(0,0,0,0.3);
      max-width: 800px;
      width: 100%;
      padding: 40px;
    }
    h1 {
      color: #333;
      margin-bottom: 10px;
    }
    .subtitle {
      color: #666;
      margin-bottom: 30px;
      font-size: 14px;
    }
    .template-info {
      background: #f5f5f5;
      padding: 20px;
      border-radius: 8px;
      margin-bottom: 30px;
    }
    .template-info p {
      margin: 8px 0;
      color: #555;
    }
    .template-info strong {
      color: #333;
    }
    .section {
      margin-bottom: 30px;
    }
    .section h2 {
      font-size: 18px;
      color: #333;
      margin-bottom: 15px;
      border-bottom: 2px solid #667eea;
      padding-bottom: 10px;
    }
    .list-item {
      padding: 12px;
      background: #f9f9f9;
      margin-bottom: 8px;
      border-radius: 4px;
      border-left: 4px solid #667eea;
    }
    .status {
      display: inline-block;
      padding: 6px 12px;
      border-radius: 4px;
      font-size: 12px;
      font-weight: bold;
    }
    .status.valid {
      background: #d4edda;
      color: #155724;
    }
    .status.invalid {
      background: #f8d7da;
      color: #721c24;
    }
    .issues {
      background: #fff3cd;
      padding: 15px;
      border-radius: 4px;
      color: #856404;
      margin-top: 10px;
    }
    .issues ul {
      margin-left: 20px;
      margin-top: 10px;
    }
    .issues li {
      margin-bottom: 5px;
      font-size: 13px;
    }
    .footer {
      text-align: center;
      margin-top: 30px;
      padding-top: 20px;
      border-top: 1px solid #eee;
      color: #999;
      font-size: 13px;
    }
    .help {
      background: #e7f3ff;
      padding: 15px;
      border-radius: 4px;
      color: #004085;
      margin-top: 20px;
      font-size: 13px;
    }
  </style>
</head>
<body>
  <div class="container">
    <h1>🏦 Banker's Seat Template Preview</h1>
    <p class="subtitle">Local development server for template testing</p>
    
    <div class="section">
      <h2>API Endpoints</h2>
      <div class="list-item">
        <strong>GET /api/template</strong> — Current template JSON
      </div>
      <div class="list-item">
        <strong>GET /api/validate</strong> — Validation results
      </div>
      <div class="list-item">
        <strong>GET /</strong> — This page
      </div>
    </div>

    <div class="section">
      <h2>How to Use</h2>
      <p style="color: #555; margin-bottom: 10px;">This development server allows you to:</p>
      <ul style="margin-left: 20px; color: #666;">
        <li>Preview your template structure</li>
        <li>Validate template.json syntax</li>
        <li>Test API responses locally</li>
        <li>See validation errors in real-time</li>
      </ul>
    </div>

    <div class="section">
      <h2>Test the API</h2>
      <p style="color: #666; margin-bottom: 15px;">Try these commands:</p>
      <div class="list-item" style="font-family: monospace; font-size: 12px; overflow-x: auto;">
        curl http://localhost:PORT/api/template
      </div>
      <div class="list-item" style="font-family: monospace; font-size: 12px; overflow-x: auto;">
        curl http://localhost:PORT/api/validate
      </div>
    </div>

    <div class="help">
      <strong>📝 Next Steps:</strong>
      <ul style="margin-left: 20px; margin-top: 8px;">
        <li>Edit template.json in your template folder</li>
        <li>Refresh this page to see updates</li>
        <li>Check /api/validate for any errors</li>
        <li>When ready, run: pnpm templates package &lt;template-id&gt;</li>
      </ul>
    </div>

    <div class="footer">
      Banker's Seat Template Tools · Hot-reload enabled
    </div>
  </div>
</body>
</html>
`;
export const runDevServer = async (port, templatesRoot) => {
    const templatePath = join(templatesRoot, "template.json");
    const schemaValidator = await loadSchemaValidator(DEFAULT_SCHEMA_PATH);
    const server = createServer(async (req, res) => {
        const url = req.url || "/";
        // CORS headers
        res.setHeader("Access-Control-Allow-Origin", "*");
        res.setHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
        res.setHeader("Access-Control-Allow-Headers", "Content-Type");
        if (req.method === "OPTIONS") {
            res.writeHead(200);
            res.end();
            return;
        }
        try {
            if (url === "/") {
                sendHtml(res, HTML_PAGE.replace(/PORT/g, port.toString()));
            }
            else if (url === "/api/template") {
                try {
                    const templateJson = await readFile(templatePath, "utf-8");
                    const template = JSON.parse(templateJson);
                    sendJson(res, template);
                }
                catch (error) {
                    sendJson(res, { error: "template.json not found or invalid" }, 404);
                }
            }
            else if (url === "/api/validate") {
                try {
                    const result = await validateTemplateFile(templatePath, schemaValidator);
                    sendJson(res, {
                        valid: result.valid,
                        issues: result.issues,
                    });
                }
                catch (error) {
                    sendJson(res, { error: "Failed to validate template" }, 500);
                }
            }
            else if (url.startsWith("/assets/")) {
                const assetPath = join(templatesRoot, url.substring(1));
                await sendFile(res, assetPath);
            }
            else {
                res.writeHead(404, { "Content-Type": "application/json" });
                res.end(JSON.stringify({ error: "Not found" }));
            }
        }
        catch (error) {
            console.error("Server error:", error);
            sendJson(res, { error: "Internal server error" }, 500);
        }
    });
    server.listen(port, () => {
        console.log(`\n✔ Template preview server started`);
        console.log(`\n  URL: http://localhost:${port}`);
        console.log(`  Template: ${templatePath}`);
        console.log(`\n  API endpoints:`);
        console.log(`    GET http://localhost:${port}/api/template`);
        console.log(`    GET http://localhost:${port}/api/validate`);
        console.log(`\n  Press Ctrl+C to stop the server\n`);
    });
    server.on("error", (error) => {
        if (error.code === "EADDRINUSE") {
            console.error(`Error: Port ${port} is already in use`);
            console.error(`Try a different port: pnpm templates dev-server ${port + 1}`);
        }
        else {
            console.error(`Server error: ${error.message}`);
        }
        process.exitCode = 1;
    });
};
