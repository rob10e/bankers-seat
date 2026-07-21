import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
const STARTER_TEMPLATE = {
    schemaVersion: 2,
    templateId: "",
    templateVersion: "1.0.0",
    name: "",
    edition: {
        id: "classic",
        name: "Classic Edition",
        releaseLabel: "First release",
        year: 2026,
    },
    description: "A custom game template",
    playerCount: {
        minimum: 2,
        maximum: 6,
    },
    currency: {
        code: "CURRENCY",
        symbol: "¤",
        name: "currency",
        baseUnitName: "unit",
        fractionDigits: 0,
        position: "before",
    },
    bank: {
        startingPlayerBalance: 1000,
        bankMode: "unlimited",
        allowPlayerOverdraft: false,
    },
    denominations: [
        { value: 1, label: "1" },
        { value: 5, label: "5" },
        { value: 10, label: "10" },
    ],
    assets: {
        logo: "assets/logo.webp",
        thumbnail: "assets/thumbnail.webp",
        background: "assets/background.webp",
    },
    playerFields: [
        {
            id: "properties",
            label: "Properties",
            type: "counter",
            default: 0,
        },
    ],
    actions: [
        {
            id: "collect-rent",
            label: "Collect Rent",
            operation: {
                type: "bank-to-player",
                amount: 50,
            },
        },
    ],
    sessionOptions: {
        allowPlayerNames: true,
        allowCustomFields: false,
    },
    tags: ["custom"],
};
export const runScaffold = async (templateId, outputDir) => {
    if (!templateId || !/^[a-z0-9-]+$/.test(templateId)) {
        console.error("Error: templateId must be lowercase alphanumeric with hyphens");
        console.error("Example: pnpm templates scaffold my-game");
        process.exitCode = 1;
        return;
    }
    const templateDir = join(outputDir, templateId);
    try {
        // Create directory structure
        await mkdir(join(templateDir, "assets"), { recursive: true });
        // Write template.json
        const template = {
            ...STARTER_TEMPLATE,
            templateId,
            name: templateId
                .split("-")
                .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
                .join(" "),
        };
        await writeFile(join(templateDir, "template.json"), JSON.stringify(template, null, 2) + "\n");
        // Write README
        const readme = `# ${template.name}

A custom Banker's Seat template.

## Getting Started

1. Edit \`template.json\` to customize your template
2. Run validation: \`pnpm templates validate\`
3. Test with: \`pnpm templates dev-server\`
4. Package for distribution: \`pnpm templates package ${templateId}\`

## Template Structure

- \`template.json\` - Main template definition
- \`assets/\` - Images and media files

## Validation

Validate your template:
\`\`\`bash
pnpm templates validate
\`\`\`

## Preview Locally

Run a local dev server to test your template:
\`\`\`bash
pnpm templates dev-server 3000
\`\`\`

Visit http://localhost:3000 to see your template in action.

## Export for Distribution

Package your template as a ZIP file:
\`\`\`bash
pnpm templates package ${templateId}
\`\`\`

This creates a \`${templateId}.zip\` that can be imported into Banker's Seat.

## Template Fields

- **templateId**: Unique identifier (lowercase kebab-case)
- **templateVersion**: Semantic version (e.g., 1.0.0)
- **edition**: Game edition metadata
- **playerCount**: Min/max players
- **currency**: Currency symbol and formatting
- **bank**: Starting balance and overdraft rules
- **denominations**: Physical money denominations (optional)
- **playerFields**: Custom tracked state per player
- **actions**: Quick action buttons

## More Information

See the [Phase 5 Template Ecosystem documentation](../../docs/25-phase5-template-ecosystem.md)
for complete details on template structure and capabilities.
`;
        await writeFile(join(templateDir, "README.md"), readme);
        console.log(`✔ Template scaffold created: ${templateDir}`);
        console.log(`\nNext steps:`);
        console.log(`  1. Edit template.json to customize`);
        console.log(`  2. Run: pnpm templates validate`);
        console.log(`  3. Test: pnpm templates dev-server 3000`);
    }
    catch (error) {
        console.error(`Error creating scaffold: ${error instanceof Error ? error.message : String(error)}`);
        process.exitCode = 1;
    }
};
