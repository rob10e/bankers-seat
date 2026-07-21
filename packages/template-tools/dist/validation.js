import { readdir, readFile } from "node:fs/promises";
import { dirname, extname, isAbsolute, join, relative, resolve, sep } from "node:path";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
const allowedAssetExtensions = new Set([".png", ".jpg", ".jpeg", ".webp", ".svg"]);
const ajv = new Ajv2020({
    allErrors: true,
    strict: true,
    strictRequired: false,
    allowUnionTypes: true,
});
addFormats(ajv);
const addIssue = (issues, code, path, message) => {
    issues.push({ code, path, message });
};
const normalizeJsonPath = (path) => {
    return path.length > 0 ? path : "/";
};
const toIssueFromAjv = (error) => {
    return {
        code: `schema-${error.keyword}`,
        path: normalizeJsonPath(error.instancePath),
        message: error.message ?? "Schema validation failed.",
    };
};
const collectDistinctDuplicates = (values) => {
    const indexMap = new Map();
    values.forEach((value, index) => {
        const existing = indexMap.get(value);
        if (existing) {
            existing.push(index);
            return;
        }
        indexMap.set(value, [index]);
    });
    return [...indexMap.entries()]
        .filter(([, indexes]) => indexes.length > 1)
        .map(([value, indexes]) => ({ value, indexes }));
};
const extractFieldOperations = (operation, path) => {
    if (operation.type === "composite" && Array.isArray(operation.steps)) {
        return operation.steps.flatMap((step, stepIndex) => extractFieldOperations(step, `${path}/steps/${stepIndex}`));
    }
    if (operation.type === "set-field"
        || operation.type === "toggle-field"
        || operation.type === "increment-field") {
        return [{ operation, path }];
    }
    return [];
};
const validateAssetPathValue = (rawPath, templateDir, issuePath, issues) => {
    const candidate = rawPath.trim();
    const normalizedCandidate = candidate.replace(/\\/g, "/");
    const extension = extname(normalizedCandidate).toLowerCase();
    if (candidate.length === 0) {
        addIssue(issues, "asset-empty-path", issuePath, "Asset path must not be empty.");
        return;
    }
    if (isAbsolute(candidate) ||
        normalizedCandidate.startsWith("/") ||
        /^[A-Za-z]:/.test(candidate)) {
        addIssue(issues, "asset-absolute-path", issuePath, "Asset path must be relative to the template directory.");
        return;
    }
    if (normalizedCandidate.split("/").includes("..")) {
        addIssue(issues, "asset-parent-traversal", issuePath, "Asset path must not include parent-directory traversal segments.");
        return;
    }
    const absoluteTemplateDir = resolve(templateDir);
    const absoluteAssetPath = resolve(absoluteTemplateDir, candidate);
    const containment = relative(absoluteTemplateDir, absoluteAssetPath);
    const isOutside = containment === ".." || containment.startsWith(`..${sep}`) || isAbsolute(containment);
    if (isOutside) {
        addIssue(issues, "asset-outside-template", issuePath, "Asset path resolves outside the template directory.");
    }
    if (!allowedAssetExtensions.has(extension)) {
        addIssue(issues, "asset-extension-not-allowed", issuePath, "Asset extension must be one of: .png, .jpg, .jpeg, .webp, .svg.");
    }
};
const semanticValidateTemplate = (template, templateDir) => {
    const issues = [];
    if (template.playerCount.minimum > template.playerCount.maximum) {
        addIssue(issues, "player-count-range-invalid", "/playerCount", "playerCount.minimum must be less than or equal to playerCount.maximum.");
    }
    if (template.currency.fractionDigits !== 0) {
        addIssue(issues, "currency-fraction-digits-invalid", "/currency/fractionDigits", "fractionDigits must be 0 because money is stored as integer base units.");
    }
    if (!template.bank.allowPlayerOverdraft && template.bank.startingPlayerBalance < 0) {
        addIssue(issues, "starting-balance-overdraft-conflict", "/bank/startingPlayerBalance", "startingPlayerBalance cannot be negative when allowPlayerOverdraft is false.");
    }
    const denominationValues = (template.denominations ?? []).map((entry) => entry.value);
    collectDistinctDuplicates(denominationValues).forEach(({ value, indexes }) => {
        indexes.forEach((index) => {
            addIssue(issues, "duplicate-denomination-value", `/denominations/${index}/value`, `Duplicate denomination value '${String(value)}'.`);
        });
    });
    const fieldIds = (template.playerFields ?? []).map((field) => field.id);
    collectDistinctDuplicates(fieldIds).forEach(({ value, indexes }) => {
        indexes.forEach((index) => {
            addIssue(issues, "duplicate-player-field-id", `/playerFields/${index}/id`, `Duplicate player field id '${value}'.`);
        });
    });
    const actionIds = (template.actions ?? []).map((action) => action.id);
    collectDistinctDuplicates(actionIds).forEach(({ value, indexes }) => {
        indexes.forEach((index) => {
            addIssue(issues, "duplicate-action-id", `/actions/${index}/id`, `Duplicate action id '${value}'.`);
        });
    });
    const definedFieldMap = new Map((template.playerFields ?? []).map((field) => [field.id, field]));
    (template.playerFields ?? []).forEach((field, fieldIndex) => {
        if (field.type !== "enum" || !Array.isArray(field.options)) {
            return;
        }
        const optionValues = field.options.map((option) => option.value);
        if (!optionValues.includes(String(field.default))) {
            addIssue(issues, "enum-default-not-found", `/playerFields/${fieldIndex}/default`, `Enum default '${String(field.default)}' is not present in options.`);
        }
    });
    (template.actions ?? []).forEach((action, actionIndex) => {
        const fieldOps = extractFieldOperations(action.operation, `/actions/${actionIndex}/operation`);
        fieldOps.forEach(({ operation, path }) => {
            const fieldId = String(operation.fieldId ?? "");
            const field = definedFieldMap.get(fieldId);
            if (!field) {
                addIssue(issues, "action-field-reference-missing", `${path}/fieldId`, `Operation references missing field '${fieldId}'.`);
                return;
            }
            if (operation.type === "increment-field") {
                if (!["integer", "counter", "currency"].includes(field.type)) {
                    addIssue(issues, "increment-non-numeric-field", path, `increment-field requires numeric target field. Field '${fieldId}' has type '${field.type}'.`);
                }
            }
            if (operation.type === "toggle-field" && field.type !== "boolean") {
                addIssue(issues, "toggle-non-boolean-field", path, `toggle-field requires boolean target field. Field '${fieldId}' has type '${field.type}'.`);
            }
        });
    });
    const imageAssets = Object.entries(template.assets?.images ?? {});
    const assetsToValidate = [
        ...(template.assets?.logo ? [{ path: template.assets.logo, issuePath: "/assets/logo" }] : []),
        ...(template.assets?.thumbnail
            ? [{ path: template.assets.thumbnail, issuePath: "/assets/thumbnail" }]
            : []),
        ...(template.assets?.background
            ? [{ path: template.assets.background, issuePath: "/assets/background" }]
            : []),
        ...imageAssets.map(([key, value]) => ({
            path: value,
            issuePath: `/assets/images/${key}`,
        })),
    ];
    (template.denominations ?? []).forEach((entry, index) => {
        const candidate = entry.asset;
        if (!candidate) {
            return;
        }
        assetsToValidate.push({
            path: candidate,
            issuePath: `/denominations/${index}/asset`,
        });
    });
    assetsToValidate.forEach(({ path, issuePath }) => {
        validateAssetPathValue(path, templateDir, issuePath, issues);
    });
    const assetKeys = new Set(Object.keys(template.assets?.images ?? {}));
    (template.playerFields ?? []).forEach((field, index) => {
        if (!field.iconAssetKey) {
            return;
        }
        if (!assetKeys.has(field.iconAssetKey)) {
            addIssue(issues, "icon-asset-key-not-found", `/playerFields/${index}/iconAssetKey`, `iconAssetKey '${field.iconAssetKey}' is not defined in assets.images.`);
        }
    });
    return issues;
};
const readJsonFile = async (filePath) => {
    const raw = await readFile(filePath, "utf8");
    return JSON.parse(raw);
};
export const discoverTemplateFiles = async (rootDir) => {
    const results = [];
    const absoluteRoot = resolve(rootDir);
    const stack = [absoluteRoot];
    while (stack.length > 0) {
        const current = stack.pop();
        if (!current) {
            continue;
        }
        const entries = await readdir(current, { withFileTypes: true });
        entries.forEach((entry) => {
            const absolutePath = join(current, entry.name);
            if (entry.isDirectory()) {
                stack.push(absolutePath);
                return;
            }
            if (entry.isFile() && entry.name === "template.json") {
                results.push(absolutePath);
            }
        });
    }
    return results.sort((a, b) => a.localeCompare(b));
};
export const loadSchemaValidator = async (schemaPath) => {
    const schema = await readJsonFile(schemaPath);
    return ajv.compile(schema);
};
export const validateTemplateObject = (template, schemaValidator, templateDir) => {
    const schemaValid = schemaValidator(template);
    const schemaIssues = schemaValid
        ? []
        : (schemaValidator.errors ?? []).map((error) => toIssueFromAjv(error));
    if (!schemaValid) {
        return { valid: false, issues: schemaIssues };
    }
    const semanticIssues = semanticValidateTemplate(template, templateDir);
    const issues = [...schemaIssues, ...semanticIssues];
    return { valid: issues.length === 0, issues };
};
export const validateTemplateFile = async (templatePath, schemaValidator) => {
    const templateDir = dirname(resolve(templatePath));
    try {
        const parsed = await readJsonFile(templatePath);
        return validateTemplateObject(parsed, schemaValidator, templateDir);
    }
    catch (error) {
        const message = error instanceof Error ? error.message : "Failed to parse template JSON file.";
        return {
            valid: false,
            issues: [
                {
                    code: "invalid-json",
                    path: "/",
                    message,
                },
            ],
        };
    }
};
