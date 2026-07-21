# Phase 5 — Template Ecosystem

**Status**: In Progress  
**Started**: 2026-07-21  
**Target Completion**: 2026-08-14

## Overview

Phase 5 enables template authors to create, package, share, and distribute custom templates with visual tools and marketplace governance. This phase extends Banker's Seat from a single-template host system to a flexible ecosystem where users can:

- **Export** templates as redistributable ZIP packages
- **Import** templates from ZIP packages
- **Create** templates visually without editing JSON
- **Preview** templates before use
- **Share** templates privately with other users
- **Diff** template versions to understand changes
- **Publish** templates with licensing information

All Phase 5 features are **backward compatible** with Phase 1–4. Existing sessions, users, and built-in templates are unaffected.

---

## Component 1: Template Export/Import System

### Status: ✅ Complete

### Purpose

Enable users to export any template as a ZIP package containing:
- `template.json` — The canonical template definition
- `metadata.json` — Export metadata (timestamp, version, author info)
- `assets/` — All referenced images and media files

Users can then:
1. **Export** a template via HTTP endpoint
2. **Share** the ZIP file offline
3. **Import** it on another Banker's Seat instance
4. **Validate** it matches the schema during import

### API Contracts

#### Export Template
```
POST /api/v1/templates/export
Content-Type: application/json

{
  "templateId": "monopoly-deluxe",
  "editionId": "2026-edition",
  "templateVersion": "2.1.0"
}

Response: 200 OK
Content-Type: application/zip
Content-Disposition: attachment; filename="monopoly-deluxe-2.1.0.zip"

[ZIP file containing template.json, metadata.json, assets/]
```

**Use Case**: Host wants to create a backup of a custom template or share with another deployment.

#### Import Template
```
POST /api/v1/templates/import
Content-Type: multipart/form-data
Authorization: Bearer <token>

[Form: file=<template.zip>]

Response: 200 OK
{
  "success": true,
  "templateId": "my-template",
  "editionId": "v1",
  "templateVersion": "1.0.0",
  "destinationPath": "/templates/installed/my-template/v1"
}

Response: 400 Bad Request (validation error)
{
  "success": false,
  "error": "Package exceeds maximum size of 100 MB"
}
```

**Use Case**: User uploads a downloaded template ZIP to make it available in the catalog.

#### Get Template Preview
```
GET /api/v1/templates/{templateId}/preview?editionId={editionId}&version={version}

Response: 200 OK
{
  "templateId": "monopoly-deluxe",
  "templateName": "Monopoly Deluxe",
  "editionId": "2026-edition",
  "editionName": "2026 Anniversary Edition",
  "templateVersion": "2.1.0",
  "description": "Official Monopoly game companion app",
  "schemaVersion": 2
}
```

**Use Case**: User clicks a template name to see a quick preview before starting a session.

### Implementation Details

**Files Created**:
- `apps/server/Application/Templates/TemplatePackageService.cs` (443 lines)
  - `ExportTemplateAsync()` — Reads template from catalog, packages as ZIP
  - `ImportTemplateAsync()` — Validates ZIP structure, extracts to installed directory
  - Validates asset extensions (PNG, JPG, WebP, SVG, GIF, WebM, MP4)
  - Prevents path traversal attacks (rejects `..`, absolute paths)
  - Enforces size limits (100 MB max package, 500 max assets)

- `apps/server/Api/V1/TemplatePackageController.cs` (195 lines)
  - `ExportTemplate(request)` — Returns ZIP file download
  - `ImportTemplate()` — Accepts multipart file upload
  - `GetTemplatePreview()` — Returns template summary

- `apps/server/Api/V1/Contracts/Phase5Contracts.cs` (266 lines)
  - `ExportTemplateRequest/Response`
  - `ImportTemplateResponse`
  - `TemplatePreviewResponse` and supporting types
  - Plus contracts for future Phase 5 components (stubs for Components 2–7)

**Program.cs Changes**:
- Registered `ITemplatePackageService` in DI container (line 112)

### Security Considerations

- **Path Traversal**: ZIP entries are validated; rejected if containing `..` or rooted paths
- **File Extensions**: Whitelist enforced (PNG, JPG, WebP, SVG, GIF, WebM, MP4 only)
- **Size Limits**: Package capped at 100 MB; max 500 assets per package
- **Authentication**: Import requires Bearer token (authorized users only)
- **Validation**: Schema validation on import (enforces template.json structure)

### Testing

Manual test procedure:
```bash
# Export a template
curl -X POST http://localhost:5000/api/v1/templates/export \
  -H "Content-Type: application/json" \
  -d '{"templateId":"monopoly-deluxe","editionId":"classic","templateVersion":"1.0.0"}' \
  --output template.zip

# Import the template (requires authentication)
curl -X POST http://localhost:5000/api/v1/templates/import \
  -H "Authorization: Bearer <token>" \
  -F "file=@template.zip"

# Preview a template
curl http://localhost:5000/api/v1/templates/monopoly-deluxe/preview?editionId=classic&version=1.0.0
```

### Known Limitations

1. **Asset Reference Tracking**: Currently, assets referenced in template.json must exist in the ZIP. In a future iteration, we could:
   - Auto-detect missing assets and warn during import
   - Support remote asset URLs with integrity checking
   - Provide asset validation report

2. **Incremental Export**: Large templates with many assets may be slow. Future optimization:
   - Stream ZIP creation for large packages
   - Cache asset hashes to detect changes

3. **Conditional Asset Inclusion**: Currently exports all assets. Future enhancement:
   - Allow selective asset export (e.g., exclude unused images)
   - Optimize PNG/JPG compression

---

## Component 2: Template Author CLI Tools

**Status**: Planned (starts after Component 1 delivery)

### Purpose

Enable template authors to work offline, validate locally, and package templates without UI. CLI commands for authors:

- `pnpm templates:scaffold <templateId>` — Create starter template
- `pnpm templates:validate` — Check template.json validity
- `pnpm templates:package <path>` — Create ZIP for import
- `pnpm templates:dev-server` — Local testing without SignalR
- `pnpm templates:diff <v1> <v2>` — See what changed

### Expected Implementation

Files will be added to `packages/template-tools/src/cli/`:
- `scaffold.ts` — Starter template generation
- `package.ts` — ZIP creation logic
- `dev-server.ts` — Lightweight local preview server

---

## Component 3: Visual Template Preview

**Status**: Planned

### Purpose

Render template metadata in React without creating a session. Used as:
- Template catalog detail page
- Template import confirmation screen
- Visual diff viewer

### Expected Implementation

- `apps/web/src/components/templates/TemplatePreview.tsx`
- React hook: `useTemplatePreview.ts`

---

## Component 4: Visual Template Editor

**Status**: Planned

### Purpose

Form-based editor for non-technical authors to create templates without JSON.

### Expected Implementation

- `apps/web/src/screens/TemplateEditorScreen.tsx`
- Dynamic form builder from schema
- Live preview pane
- Draft persistence to server
- Asset upload management

---

## Component 5: Template Diff/Migration Helper

**Status**: Planned

### Purpose

Show what changed between template versions and advise on migration steps.

### Expected Implementation

- Diff algorithm in `packages/template-tools`
- Breaking change detection
- Changelog generation
- API endpoint: `GET /api/v1/templates/{id}/diff?from={v1}&to={v2}`

---

## Component 6: Private Template Sharing

**Status**: Planned

### Purpose

Authors can share templates with specific users before public release.

### Expected Implementation

- Share model: `TemplateShare` entity
- Share endpoints: grant, revoke, list
- Access control in catalog queries
- Database migration for sharing table

---

## Component 7: Marketplace Governance & Licensing

**Status**: Planned

### Purpose

Establish framework for sustainable template distribution with:
- Licensing metadata (SPDX)
- Quality checklist for featured templates
- Moderation queue for new submissions
- Copyright conflict detection
- Terms of use

### Expected Implementation

- License selector in editor
- Moderation UI for admins
- Governance policy documentation
- Template metadata entity with author, license, contact

---

## Architecture & Design Decisions

### Backward Compatibility

- **No breaking changes** to Phase 1–4 APIs
- Guest sessions (pre-authentication) still work
- Built-in templates remain immutable
- Existing template catalog unaffected
- Import goes to separate `templates/installed/` directory

### Template Immutability (Active Sessions)

A critical invariant: **Active game sessions use a persisted template snapshot. Editing a template file only affects NEW sessions.**

**This means**:
- Exporting a template captures its current state
- Importing a template doesn't affect active games
- Sessions from v1.0.0 stay on v1.0.0 even if v1.1.0 is imported

### Security Model

**Import Authorization**:
- Authentication required (Bearer token)
- Authorization: Any authenticated user can import (future: role-based)
- Rate limiting: Import endpoint subject to rate limits

**Export Authorization**:
- No authentication required for built-in templates (public)
- Custom templates: owner-only (future in Phase 6)

**Validation**:
- Schema validation on import (rejects invalid JSON)
- Asset path validation (rejects traversal attacks)
- Asset type whitelist (rejects executable files)

### Configuration

Add to `appsettings.json`:
```json
{
  "TemplatesRoot": {
    "BuiltIn": "templates/built-in",
    "Installed": "templates/installed",
    "MaxPackageSize": 104857600,
    "MaxAssetCount": 500,
    "AllowedAssetExtensions": [".png", ".jpg", ".jpeg", ".webp", ".svg", ".gif", ".webm", ".mp4"]
  }
}
```

---

## Phase 5 Success Criteria

- [x] Templates can be exported as ZIP packages (Component 1)
- [ ] Templates can be imported from ZIP files (Component 1)
- [ ] CLI tools enable offline template authoring (Component 2)
- [ ] Visual preview shows template details (Component 3)
- [ ] Visual editor allows non-technical creation (Component 4)
- [ ] Template version diffs are visible (Component 5)
- [ ] Private template sharing with revokable access (Component 6)
- [ ] Marketplace governance framework established (Component 7)
- [ ] All Phase 1–4 features remain functional
- [ ] Comprehensive documentation for each component
- [ ] E2E tests for import/export workflow

---

## Testing Strategy

### Unit Tests

- Export: Template not found, encoding, asset list
- Import: Invalid ZIP, missing JSON, path traversal, size limit
- Preview: Snapshot parsing, missing fields, schema version

### Integration Tests

- Export-then-import round-trip
- Duplicate detection (re-import same template)
- Asset extraction with directory creation
- Malicious ZIPs (path traversal attempts, oversized files)

### E2E Tests (Playwright)

- User exports template, downloads ZIP
- User imports template, verifies in catalog
- User previews template before use
- Two sessions use different editions of same template

---

## Risk Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Path traversal in ZIP | High | Validate all paths; reject `..` and rooted paths |
| Oversized packages | Medium | 100 MB limit enforced on stream size check |
| Missing assets in ZIP | Medium | Log warning if referenced assets not present; don't fail import |
| Duplicate templates | Low | Overwrite with new version; log warning |
| Schema versioning | Medium | Preserve schema version; fail if unsupported |
| Performance with large templates | Low | Stream ZIP; lazy-load assets in preview |

---

## Next Steps

**Next Todo**: Component 2 (template-cli)
- Scaffold command to generate starter templates
- Package command to create ZIPs locally
- Integrate with existing `pnpm templates:validate`

**Estimated Timeline**:
- Component 2: 2–3 days
- Component 3: 2 days
- Component 4: 5–7 days
- Component 5: 2–3 days
- Component 6: 2–3 days
- Component 7: 1–2 days

---

## Summary

Phase 5 Component 1 (**Template Export/Import System**) is complete with:
- ✅ Export endpoint returning ZIP packages
- ✅ Import endpoint accepting and validating ZIP files
- ✅ Preview endpoint for quick template inspection
- ✅ Comprehensive security validation
- ✅ API contracts and service layer
- ✅ Full backward compatibility

The foundation is set for Components 2–7 to build atop this packaging and distribution infrastructure.
