import { access, mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import assert from "node:assert/strict";
import type { ValidateFunction } from "ajv";
import {
  discoverTemplateFiles,
  loadSchemaValidator,
  validateTemplateFile,
  validateTemplateObject,
} from "./validation.js";

const schemaPathCandidates = [
  resolve(process.cwd(), "..", "..", "templates", "schema", "game-template.schema.json"),
  resolve(process.cwd(), "templates", "schema", "game-template.schema.json"),
];

const resolveSchemaPath = async (): Promise<string> => {
  for (const candidate of schemaPathCandidates) {
    try {
      await access(candidate);
      return candidate;
    } catch {
      // Continue to next candidate.
    }
  }

  throw new Error("Could not locate templates/schema/game-template.schema.json");
};

const createValidTemplate = (): Record<string, unknown> => {
  return {
    schemaVersion: 1,
    templateId: "test-template",
    templateVersion: "1.0.0",
    name: "Test Template",
    edition: {
      id: "standard",
      name: "Standard",
    },
    playerCount: {
      minimum: 2,
      maximum: 6,
    },
    currency: {
      code: "TEST_COIN",
      symbol: "$",
      name: "coin",
      baseUnitName: "coin",
      fractionDigits: 0,
      position: "before",
    },
    bank: {
      startingPlayerBalance: 1000,
      bankMode: "unlimited",
      allowPlayerOverdraft: false,
    },
    denominations: [
      {
        value: 1,
        label: "1",
      },
      {
        value: 5,
        label: "5",
      },
    ],
    playerFields: [
      {
        id: "score",
        label: "Score",
        type: "counter",
        default: 0,
        minimum: 0,
        maximum: 999,
        step: 1,
        visibility: "all",
        editableBy: "host-and-owner",
      },
    ],
    actions: [
      {
        id: "collect",
        label: "Collect",
        category: "income",
        scope: "single-player",
        operation: {
          type: "bank-to-player",
          amount: 5,
        },
        confirmation: "never",
      },
    ],
  };
};

let schemaValidator: ValidateFunction<unknown>;

test("setup schema validator", async () => {
  schemaValidator = await loadSchemaValidator(await resolveSchemaPath());
  assert.ok(schemaValidator);
});

test("validates sample templates as valid", async () => {
  const schemaPath = await resolveSchemaPath();
  const templatesRoot = resolve(dirname(schemaPath), "..");
  const files = await discoverTemplateFiles(templatesRoot);
  assert.ok(files.length >= 2, "Expected at least two sample templates.");

  const invalidFiles: string[] = [];
  for (const templatePath of files) {
    const result = await validateTemplateFile(templatePath, schemaValidator);
    if (!result.valid) {
      invalidFiles.push(templatePath);
    }
  }

  assert.equal(invalidFiles.length, 0, `Unexpected invalid templates:\n${invalidFiles.join("\n")}`);
});

test("discovers template files recursively and returns sorted paths", async () => {
  const tempRoot = await mkdtemp(join(tmpdir(), "template-discovery-"));
  try {
    const alphaDir = join(tempRoot, "alpha");
    const betaDir = join(tempRoot, "nested", "beta");
    await mkdir(alphaDir, { recursive: true });
    await mkdir(betaDir, { recursive: true });
    await writeFile(join(betaDir, "notes.json"), "{}");
    await writeFile(join(betaDir, "template.json"), "{}");
    await writeFile(join(alphaDir, "template.json"), "{}");

    const discovered = await discoverTemplateFiles(tempRoot);
    assert.deepEqual(discovered, [join(alphaDir, "template.json"), join(betaDir, "template.json")]);
  } finally {
    await rm(tempRoot, { recursive: true, force: true });
  }
});

test("returns invalid-json issue when template file cannot be parsed", async () => {
  const tempRoot = await mkdtemp(join(tmpdir(), "template-invalid-json-"));
  const templatePath = join(tempRoot, "template.json");
  try {
    await writeFile(templatePath, "{ not-valid-json");
    const result = await validateTemplateFile(templatePath, schemaValidator);
    assert.equal(result.valid, false);
    assert.equal(result.issues[0]?.code, "invalid-json");
    assert.equal(result.issues[0]?.path, "/");
  } finally {
    await rm(tempRoot, { recursive: true, force: true });
  }
});

test("returns schema issues for schema-invalid templates", () => {
  const template = createValidTemplate();
  (template.currency as { fractionDigits: number }).fractionDigits = 2;

  const result = validateTemplateObject(template, schemaValidator, process.cwd());
  assert.equal(result.valid, false);
  assert.ok(result.issues.some((issue) => issue.code.startsWith("schema-")));
  assert.ok(result.issues.some((issue) => issue.path === "/currency/fractionDigits"));
});

test("returns semantic issues for duplicate ids and invalid field references", () => {
  const template = createValidTemplate();
  (template.playerFields as Array<Record<string, unknown>>).push({
    id: "score",
    label: "Duplicate Score",
    type: "counter",
    default: 0,
    minimum: 0,
    maximum: 999,
    step: 1,
    visibility: "all",
    editableBy: "host-and-owner",
  });
  (template.actions as Array<Record<string, unknown>>).push({
    id: "collect",
    label: "Duplicate Collect",
    category: "income",
    scope: "single-player",
    operation: {
      type: "set-field",
      fieldId: "missing-field",
      value: true,
    },
    confirmation: "never",
  });

  const result = validateTemplateObject(template, schemaValidator, process.cwd());
  assert.equal(result.valid, false);
  assert.ok(result.issues.some((issue) => issue.code === "duplicate-player-field-id"));
  assert.ok(result.issues.some((issue) => issue.code === "duplicate-action-id"));
  assert.ok(result.issues.some((issue) => issue.code === "action-field-reference-missing"));
});

test("returns semantic issues for invalid enum defaults and numeric constraints", () => {
  const template = createValidTemplate();
  template.playerFields = [
    {
      id: "career",
      label: "Career",
      type: "enum",
      default: "missing",
      options: [{ value: "engineer", label: "Engineer" }],
      visibility: "all",
      editableBy: "host-and-owner",
    },
    {
      id: "status",
      label: "Status",
      type: "text",
      default: "new",
      visibility: "all",
      editableBy: "host-and-owner",
    },
  ];
  template.actions = [
    {
      id: "bad-increment",
      label: "Bad Increment",
      category: "status",
      scope: "single-player",
      operation: {
        type: "increment-field",
        fieldId: "status",
        amount: 1,
      },
      confirmation: "never",
    },
    {
      id: "bad-toggle",
      label: "Bad Toggle",
      category: "status",
      scope: "single-player",
      operation: {
        type: "toggle-field",
        fieldId: "career",
      },
      confirmation: "never",
    },
  ];

  const result = validateTemplateObject(template, schemaValidator, process.cwd());
  assert.equal(result.valid, false);
  assert.ok(result.issues.some((issue) => issue.code === "enum-default-not-found"));
  assert.ok(result.issues.some((issue) => issue.code === "increment-non-numeric-field"));
  assert.ok(result.issues.some((issue) => issue.code === "toggle-non-boolean-field"));
});

test("returns semantic issues for disallowed asset extensions and missing icon keys", () => {
  const template = createValidTemplate();
  template.assets = {
    logo: "assets/logo.gif",
    images: {
      badge: "assets/badge.png",
    },
  };
  (template.playerFields as Array<Record<string, unknown>>)[0].iconAssetKey = "missing";

  const result = validateTemplateObject(template, schemaValidator, process.cwd());
  assert.equal(result.valid, false);
  assert.ok(result.issues.some((issue) => issue.code === "asset-extension-not-allowed"));
  assert.ok(result.issues.some((issue) => issue.code === "icon-asset-key-not-found"));
});

test("sample template files are valid JSON", async () => {
  const schemaPath = await resolveSchemaPath();
  const templatesRoot = resolve(dirname(schemaPath), "..");
  const files = await discoverTemplateFiles(templatesRoot);

  for (const templatePath of files) {
    const content = await readFile(templatePath, "utf8");
    assert.doesNotThrow(() => JSON.parse(content));
  }
});
