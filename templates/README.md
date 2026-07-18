# Templates

## Layout

Each template lives in its own directory:

```text
template-folder/
  template.json
  assets/
```

The server detects files named `template.json`, validates them against `schema/game-template.schema.json`, performs semantic and asset validation, and adds valid templates to the catalog.

## Samples

- `samples/generic-property-trading`
- `samples/generic-life-journey`

The samples are original and intentionally generic. Their placeholder asset paths are omitted unless an actual asset file is included.

## Identity

Do not reuse the same combination of:

- `templateId`
- `edition.id`
- `templateVersion`

IDs use lowercase kebab case and remain stable even when labels change.
