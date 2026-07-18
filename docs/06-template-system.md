# Template System

## Purpose

Templates define banker-related configuration and safe actions without requiring a new application build. A template may describe:

- Game and edition metadata.
- Player count.
- Currency label and denominations.
- Starting balances.
- Bank policy.
- Player custom fields.
- Common actions.
- Payday or periodic payments.
- Session options.
- Logos and supporting images.

Templates do not define executable code.

## Template identity

A template has three important identifiers:

- `templateId` — stable identity for the general template.
- `edition.id` — stable identity for a specific edition.
- `templateVersion` — semantic version of this template definition.

The unique catalog key is:

```text
templateId + edition.id + templateVersion
```

The catalog may show only the latest compatible version by default while retaining older versions for diagnostics and imports.

## Directory model

Recommended server directory structure:

```text
templates/
  built-in/
    template-folder/
      template.json
      assets/
  installed/
    template-folder/
      template.json
      assets/
  invalid/
```

The server scans configured roots at startup. In development and self-hosted deployments, a file watcher may trigger a debounced rescan. Production systems should allow rescanning through an administrator endpoint.

## Automatic detection

A browser cannot safely scan arbitrary directories on a user's device. Therefore, automatic detection occurs on the server:

1. Enumerate allowed template roots.
2. Find files named `template.json`.
3. Parse JSON with strict limits.
4. Validate against JSON Schema.
5. Perform semantic validation.
6. Validate referenced assets.
7. Calculate content and asset hashes.
8. Add valid entries to the catalog.
9. Record invalid entries with actionable diagnostics.

A static client-only demo may use Vite's `import.meta.glob`, but the multiplayer product should use the server catalog.

## Validation layers

### JSON syntax

Reject malformed JSON and report line/column when available.

### JSON Schema

Validate required fields, types, formats, ranges, and known operation shapes.

### Semantic validation

Examples:

- Duplicate denomination values.
- Duplicate field or action IDs.
- Action references a missing field.
- Enum default not present in options.
- Minimum players greater than maximum.
- Asset path escapes the template directory.
- Edition ID missing while edition name is present.
- Starting balance violates a configured non-negative rule.
- Batch action expands beyond safety limits.

### Asset validation

- Paths must be relative.
- Reject `..`, rooted paths, device paths, and symbolic-link escapes.
- Allow only configured file types such as PNG, JPEG, WebP, and SVG after safe SVG handling.
- Enforce file-size and dimension limits.
- Serve assets from a dedicated route with safe headers.
- Do not execute embedded scripts.
- Remote assets are disabled by default.

## Catalog behavior

Each catalog entry contains:

- Template identity.
- Display name.
- Edition name and release label.
- Description.
- Player range.
- Thumbnail/logo URL.
- Template version.
- Validation status.
- Source type: built-in, installed, imported, or administrative.
- Content hash.
- Last discovered time.

Invalid templates must not be selectable, but administrators need access to diagnostics.

## Session snapshot

When a session is created, persist:

- Entire validated template JSON.
- Template identity.
- Schema version.
- Content hash.
- Resolved session settings.
- Asset package identity or immutable asset references.

The session must never reread live template data during gameplay.

## Template compatibility

### Schema version

`schemaVersion` controls the structural contract. The application supports a declared range of schema versions.

### Template version

`templateVersion` describes content changes by template authors.

Suggested semantics:

- Patch: labels, descriptions, or graphics without behavioral changes.
- Minor: additive fields or actions with compatible defaults.
- Major: breaking behavior or identity changes.

### Migration

Do not automatically migrate active sessions. Catalog templates may be migrated by an explicit administrative tool that produces a new file and report.

## Import/export package

A future package format can be a ZIP with:

```text
template.json
assets/
manifest.json
signature.json
```

Package extraction must defend against zip-slip, file bombs, excessive file counts, and unsupported types.

## Template authoring tools

Planned tools:

- CLI validator.
- Schema-aware editor support.
- Human-readable diagnostics.
- Preview catalog card.
- Preview player fields and action buttons.
- Template diff.
- Package builder.
- Optional visual editor after the schema stabilizes.

## Intellectual property policy

The repository should contain only original, licensed, or clearly permitted content. Generic templates may demonstrate property trading, life journeys, paydays, and similar mechanics without copying proprietary names, logos, board layouts, card text, or rulebooks.
