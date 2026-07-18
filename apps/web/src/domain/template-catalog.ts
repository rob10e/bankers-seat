export type TemplateValidationStatus = "valid" | "invalid";
export type TemplateSourceType =
  | "built-in"
  | "installed"
  | "imported"
  | "administrative";

export interface TemplateCatalogEntry {
  readonly templateId: string;
  readonly editionId: string;
  readonly templateVersion: string;
  readonly name: string;
  readonly editionName: string;
  readonly description: string;
  readonly minimumPlayers: number;
  readonly maximumPlayers: number;
  readonly tags: readonly string[];
  readonly validationStatus: TemplateValidationStatus;
  readonly sourceType: TemplateSourceType;
}

export const toTemplateKey = (entry: TemplateCatalogEntry): string => {
  return `${entry.templateId}:${entry.editionId}:${entry.templateVersion}`;
};
