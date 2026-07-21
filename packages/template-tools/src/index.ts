#!/usr/bin/env node

import { resolve } from "node:path";

type CommandConfig = {
  command: string;
  templatesRoot: string;
  schemaPath: string;
  args: string[];
};

const parseArgs = (): CommandConfig => {
  const args = process.argv.slice(2);
  const command = args[0] || "help";

  const templatesRoot = resolve(
    process.cwd(),
    "..",
    "..",
    "templates"
  );
  const schemaPath = resolve(
    templatesRoot,
    "schema",
    "game-template.schema.json"
  );

  return {
    command,
    templatesRoot,
    schemaPath,
    args: args.slice(1),
  };
};

const showHelp = (): void => {
  console.log(`
Banker's Seat Template Tools CLI

Usage: pnpm templates <command> [options]

Commands:
  validate [path]           Validate template.json files
  scaffold <templateId>     Create a new template scaffold
  package <path>            Package a template folder into ZIP
  dev-server [port]         Run local template preview server
  help                      Show this help message

Examples:
  pnpm templates validate
  pnpm templates scaffold my-game
  pnpm templates package ./my-game
  pnpm templates dev-server 3000

For detailed documentation, see docs/25-phase5-template-ecosystem.md
  `);
};

const main = async (): Promise<void> => {
  const config = parseArgs();

  try {
    switch (config.command) {
      case "validate":
        const { runValidate } = await import("./commands/validate.js");
        await runValidate(config.templatesRoot, config.schemaPath);
        break;

      case "scaffold":
        const { runScaffold } = await import("./commands/scaffold.js");
        if (config.args.length === 0) {
          console.error("Error: templateId is required");
          console.error("Usage: pnpm templates scaffold <templateId>");
          process.exitCode = 1;
          return;
        }
        await runScaffold(config.args[0], process.cwd());
        break;

      case "package":
        const { runPackage } = await import("./commands/package.js");
        if (config.args.length === 0) {
          console.error("Error: path is required");
          console.error("Usage: pnpm templates package <path>");
          process.exitCode = 1;
          return;
        }
        await runPackage(resolve(process.cwd(), config.args[0]));
        break;

      case "dev-server":
        const { runDevServer } = await import("./commands/dev-server.js");
        const port = config.args[0] ? parseInt(config.args[0], 10) : 3000;
        await runDevServer(port, config.templatesRoot);
        break;

      case "help":
        showHelp();
        break;

      default:
        console.error(`Unknown command: ${config.command}`);
        showHelp();
        process.exitCode = 1;
    }
  } catch (error) {
    console.error("Error:", error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
};

void main();

