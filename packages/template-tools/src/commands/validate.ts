import {
  discoverTemplateFiles,
  loadSchemaValidator,
  validateTemplateFile,
  type TemplateValidationIssue,
} from "../validation.js";

const formatIssue = (issue: TemplateValidationIssue): string => {
  return `  - [${issue.code}] ${issue.path}: ${issue.message}`;
};

export const runValidate = async (
  templatesRoot: string,
  schemaPath: string
): Promise<void> => {
  const schemaValidator = await loadSchemaValidator(schemaPath);
  const templateFiles = await discoverTemplateFiles(templatesRoot);

  if (templateFiles.length === 0) {
    console.error(`No template.json files found under: ${templatesRoot}`);
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
