import { resolve } from "node:path";
import {
  discoverTemplateFiles,
  loadSchemaValidator,
  validateTemplateFile,
  type TemplateValidationIssue,
} from "./validation.js";

type CliConfig = {
  templatesRoot: string;
  schemaPath: string;
};

const parseArgs = (): CliConfig => {
  const args = process.argv.slice(2);
  const rootArg = args[0];
  const schemaArg = args[1];

  const templatesRoot = rootArg
    ? resolve(rootArg)
    : resolve(process.cwd(), "..", "..", "templates");
  const schemaPath = schemaArg
    ? resolve(schemaArg)
    : resolve(templatesRoot, "schema", "game-template.schema.json");

  return { templatesRoot, schemaPath };
};

const formatIssue = (issue: TemplateValidationIssue): string => {
  return `  - [${issue.code}] ${issue.path}: ${issue.message}`;
};

const run = async (): Promise<void> => {
  const config = parseArgs();
  const schemaValidator = await loadSchemaValidator(config.schemaPath);
  const templateFiles = await discoverTemplateFiles(config.templatesRoot);

  if (templateFiles.length === 0) {
    console.error(`No template.json files found under: ${config.templatesRoot}`);
    process.exitCode = 1;
    return;
  }

  let invalidCount = 0;

  for (const templatePath of templateFiles) {
    const result = await validateTemplateFile(templatePath, schemaValidator);
    if (result.valid) {
      console.log(`✔ ${templatePath}`);
      continue;
    }

    invalidCount += 1;
    console.error(`✖ ${templatePath}`);
    result.issues.forEach((issue) => {
      console.error(formatIssue(issue));
    });
  }

  const validCount = templateFiles.length - invalidCount;
  const summary = `Validated ${templateFiles.length} template(s): ${validCount} valid, ${invalidCount} invalid.`;

  if (invalidCount > 0) {
    console.error(summary);
    process.exitCode = 1;
    return;
  }

  console.log(summary);
};

void run();
