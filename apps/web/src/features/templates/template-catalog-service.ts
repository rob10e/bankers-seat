import type { TemplateCatalogEntry } from "../../domain/template-catalog.ts";

interface UnknownRecord {
  [key: string]: unknown;
}

const isRecord = (value: unknown): value is UnknownRecord => {
  return typeof value === "object" && value !== null;
};

const isStringArray = (value: unknown): value is string[] => {
  return Array.isArray(value) && value.every((item) => typeof item === "string");
};

const parseCatalogEntry = (value: unknown): TemplateCatalogEntry | null => {
  if (!isRecord(value)) {
    return null;
  }

  const {
    templateId,
    editionId,
    templateVersion,
    name,
    editionName,
    description,
    minimumPlayers,
    maximumPlayers,
    tags,
    validationStatus,
    sourceType,
  } = value;

  if (
    typeof templateId !== "string" ||
    typeof editionId !== "string" ||
    typeof templateVersion !== "string" ||
    typeof name !== "string" ||
    typeof editionName !== "string" ||
    typeof description !== "string" ||
    typeof minimumPlayers !== "number" ||
    typeof maximumPlayers !== "number" ||
    !isStringArray(tags) ||
    (validationStatus !== "valid" && validationStatus !== "invalid")
  ) {
    return null;
  }

  if (
    sourceType !== "built-in" &&
    sourceType !== "installed" &&
    sourceType !== "imported" &&
    sourceType !== "administrative"
  ) {
    return null;
  }

  return {
    templateId,
    editionId,
    templateVersion,
    name,
    editionName,
    description,
    minimumPlayers,
    maximumPlayers,
    tags,
    validationStatus,
    sourceType,
  };
};

const parseCatalogResponse = (value: unknown): TemplateCatalogEntry[] | null => {
  if (!Array.isArray(value)) {
    return null;
  }

  const parsedEntries = value.map(parseCatalogEntry);
  if (parsedEntries.some((entry) => entry === null)) {
    return null;
  }

  return parsedEntries as TemplateCatalogEntry[];
};

export const getTemplateCatalog = async (): Promise<TemplateCatalogEntry[]> => {
  const response = await fetch("/api/v1/templates");
  if (!response.ok) {
    throw new Error(`template-catalog-request-failed:${response.status}`);
  }

  const json = (await response.json()) as unknown;
  const parsed = parseCatalogResponse(json);
  if (!parsed) {
    throw new Error("template-catalog-invalid-response");
  }

  return parsed.filter((entry) => entry.validationStatus === "valid");
};
