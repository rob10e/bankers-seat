import type { TemplateCatalogEntry } from "../../domain/template-catalog.ts";

export const sampleTemplateCatalog: readonly TemplateCatalogEntry[] = [
  {
    templateId: "generic-property-trading",
    editionId: "standard-edition",
    templateVersion: "1.0.0",
    name: "Property Trading Banker",
    editionName: "Standard Edition",
    description: "An original generic template for property-trading board games.",
    minimumPlayers: 2,
    maximumPlayers: 8,
    tags: ["property", "trading", "money", "generic"],
    validationStatus: "valid",
    sourceType: "built-in",
  },
  {
    templateId: "generic-life-journey",
    editionId: "family-edition",
    templateVersion: "1.0.0",
    name: "Life Journey Banker",
    editionName: "Family Edition",
    description:
      "An original generic template for life-event and career-journey board games.",
    minimumPlayers: 2,
    maximumPlayers: 10,
    tags: ["life", "career", "family", "generic"],
    validationStatus: "valid",
    sourceType: "built-in",
  },
];
