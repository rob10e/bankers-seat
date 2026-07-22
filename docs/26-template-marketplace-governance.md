# Template Marketplace Governance & Licensing

**Status**: Phase 5 Complete  
**Last Updated**: 2026-07-22

## Overview

This document outlines the governance framework for Banker's Seat templates, including:

- **Private sharing** — Authors can share templates with specific users before public release
- **Licensing** — Templates are published with SPDX licenses or proprietary declarations
- **Moderation** — Admin review ensures quality and policy compliance
- **Marketplace** — Public catalog displays approved, published templates

## Component 6: Private Template Sharing

### Purpose

Enable template authors to share their work with specific users for feedback or collaboration before public release.

### User Flows

#### Sharing a Template

```
Author → /api/v1/templates/{templateId}/share
├─ POST with recipient emails
├─ Returns share IDs and any errors
└─ Each recipient can now import/preview the template
```

#### Accepting a Share

```
Recipient → /api/v1/templates/shared-with-me
├─ Lists all templates shared with their email
├─ Displays sharer name and grant date
└─ Can then import shared template
```

#### Revoking Access

```
Author → /api/v1/templates/{templateId}/share/{shareId}
├─ DELETE request
├─ Marks share as revoked
└─ Recipient loses access (soft delete preserves audit trail)
```

### Database Schema

**template_shares** table:

| Column | Type | Notes |
|--------|------|-------|
| id | GUID | Primary key |
| template_id | string(100) | FK to templates |
| shared_by_user_id | GUID | FK to user_accounts |
| shared_with_email | string(256) | Recipient email (normalized) |
| granted_at_utc | DateTime | When share was granted |
| revoked_at_utc | DateTime? | When share was revoked (null if active) |

**Unique constraint**: `(template_id, shared_with_email, revoked_at_utc IS NULL)` ensures only one active share per user.

### Access Control

- **Owner**: Can export the template, view all shares, revoke shares
- **Shared User**: Can preview and import (if public) but cannot export or share further
- **Non-shared User**: Cannot access template (404 if not also owner)

**Implementation**:

```csharp
// Check if user has access
bool hasAccess = await shareService.HasAccessAsync(userEmail, templateId, ct);

// Returns true if:
// 1. User owns the template (metadata.ownerUserId matches), OR
// 2. User has active share (revoked_at_utc IS NULL)
```

### API Contracts

#### POST /api/v1/templates/{templateId}/share
Share a template with one or more recipients.

**Request**:
```json
{
  "recipientEmails": ["alice@example.com", "bob@example.com"]
}
```

**Response (200 OK)**:
```json
{
  "shareIds": ["550e8400-e29b-41d4-a716-446655440000"],
  "successCount": 1,
  "errors": ["bob@example.com: This template is already shared with this user"]
}
```

#### DELETE /api/v1/templates/{templateId}/share/{shareId}
Revoke a share.

**Response**: 204 No Content

#### GET /api/v1/templates/shared-with-me
List templates shared with the current user.

**Response (200 OK)**:
```json
{
  "templates": [
    {
      "templateId": "my-game",
      "editionId": "2026-v1",
      "sharedByName": "Alice Smith",
      "grantedAtUtc": "2026-07-22T10:00:00Z"
    }
  ]
}
```

---

## Component 7: Marketplace Governance & Licensing

### Purpose

Provide a governance framework that:

1. **Attributes authorship** and licensing clearly
2. **Enables moderation** to maintain quality and policy compliance
3. **Protects IP** by requiring explicit license declarations
4. **Surfaces curated content** (e.g., featured templates) in search

### Licensing

#### SPDX Identifiers (Recommended)

Approved licenses for Banker's Seat templates:

| License | Abbreviation | Usage |
|---------|--------------|-------|
| MIT License | `MIT` | Permissive, most flexible |
| Apache License 2.0 | `Apache-2.0` | Permissive with patent clause |
| GPL v3 | `GPL-3.0` | Copyleft (derivative works must use same license) |
| GPL v2 | `GPL-2.0` | Copyleft, legacy |
| BSD 3-Clause | `BSD-3-Clause` | Permissive with attribution |
| BSD 2-Clause | `BSD-2-Clause` | Permissive, simpler |
| Creative Commons Zero | `CC0-1.0` | Public domain equivalent |
| Creative Commons BY | `CC-BY-4.0` | Requires attribution only |
| Creative Commons BY-SA | `CC-BY-SA-4.0` | Requires attribution + share-alike |
| ISC License | `ISC` | Permissive, minimal |
| Unlicense | `Unlicense` | Public domain |
| Proprietary | `Proprietary` | Custom license or closed source |

#### Validation

```csharp
// Valid at publish time
static readonly HashSet<string> ValidLicenses = new()
{
    "MIT", "Apache-2.0", "GPL-3.0", "GPL-2.0",
    "BSD-3-Clause", "BSD-2-Clause",
    "CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0",
    "ISC", "Unlicense", "Proprietary"
};

// Reject unknown licenses
if (!ValidLicenses.Contains(license))
    throw new ArgumentException("Invalid license");
```

### Author & Governance Metadata

#### Template Metadata Entity

**template_metadata** table:

| Column | Type | Notes |
|--------|------|-------|
| id | GUID | Primary key |
| template_id | string(100) | FK to templates |
| edition_id | string(100) | FK to editions |
| owner_user_id | GUID | The user who published |
| author | string(200) | Display name (may differ from owner) |
| author_email | string(256)? | Contact email |
| author_url | string(500)? | Website, GitHub, etc. |
| license | string(50) | SPDX or "Proprietary" |
| published_at_utc | DateTime | When first published |
| template_status | string(32) | Draft / Published / Featured / Archived |
| moderation_status | string(32) | Pending / Approved / Flagged / Rejected |
| download_count | int | Incremented each import |
| updated_at_utc | DateTime | Last moderation/flag update |
| flag_reasons | JSON[]? | Array of flag reasons (stored as JSON) |

**Unique constraint**: `(template_id, edition_id)`

### Moderation Workflow

#### States

```
[Draft] → [Published] → [Pending Review] → [Approved] ⇄ [Flagged]
                                          ↘
                                        [Rejected] (terminal)
```

**State Definitions**:

- **Draft**: Not yet published; author still editing
- **Published**: Author submitted to marketplace; awaiting moderation
- **Pending**: Initial state after publish
- **Approved**: Passed moderation; visible in public catalog
- **Flagged**: Reviewer marked for investigation; hidden from catalog until resolved
- **Rejected**: Moderation rejected; author can republish after fixing issues
- **Featured**: Approved templates elevated for discovery (optional curator flag)
- **Archived**: Author deprecated; hidden but searchable by direct ID

#### Admin Moderation Queue

**GET /api/v1/admin/templates/moderation-queue**

Returns templates with `moderation_status = "Pending"`.

**Response (200 OK)**:
```json
{
  "items": [
    {
      "templateId": "my-game",
      "editionId": "2026-v1",
      "author": "Jane Doe",
      "publishedAtUtc": "2026-07-22T10:00:00Z",
      "moderationStatus": "Pending",
      "flagReasons": null
    }
  ],
  "totalCount": 42,
  "pendingCount": 5
}
```

#### Approval

**POST /api/v1/admin/templates/{templateId}/{editionId}/approve**

- Sets `moderation_status = "Approved"`
- Clears `flag_reasons`
- Template becomes visible in public catalog

**Response**: 204 No Content

#### Rejection

**POST /api/v1/admin/templates/{templateId}/{editionId}/reject**

**Request**:
```json
{
  "templateId": "my-game",
  "editionId": "2026-v1",
  "reason": "Uses copyrighted artwork without license"
}
```

- Sets `moderation_status = "Rejected"`
- Stores reason in `flag_reasons`
- Template hidden from public catalog
- Author notified (future: email integration)

**Response**: 204 No Content

#### Flagging

**POST /api/v1/admin/templates/{templateId}/{editionId}/flag**

**Request**:
```json
{
  "templateId": "my-game",
  "editionId": "2026-v1",
  "reasons": ["Potential trademark violation", "Needs content review"]
}
```

- Sets `moderation_status = "Flagged"`
- Stores array of reasons as JSON
- Template hidden from public catalog
- Preserved for later investigation

**Response**: 204 No Content

### Publishing & Catalog Visibility

#### Publish Template

**POST /api/v1/templates/{templateId}/publish**

Author publishes their template for public consideration.

**Request**:
```json
{
  "templateId": "my-game",
  "editionId": "2026-v1",
  "author": "Jane Doe",
  "authorEmail": "jane@example.com",
  "authorUrl": "https://example.com",
  "license": "CC-BY-4.0"
}
```

**Validation**:
- Require valid SPDX license
- Author email (if provided) must be valid format
- Author URL (if provided) must be valid HTTP(S) URL

**Response (200 OK)**:
```json
{
  "success": true,
  "metadataId": "550e8400-e29b-41d4-a716-446655440000"
}
```

#### Public Catalog Query

**GET /api/v1/templates/public** (authenticated or anonymous)

Returns templates visible to public:

```sql
WHERE template_status = 'Published'
  AND moderation_status = 'Approved'
ORDER BY download_count DESC, published_at_utc DESC
```

**Response (200 OK)**:
```json
[
  {
    "metadataId": "550e8400-e29b-41d4-a716-446655440000",
    "templateId": "my-game",
    "editionId": "2026-v1",
    "author": "Jane Doe",
    "authorEmail": "jane@example.com",
    "authorUrl": "https://example.com",
    "license": "CC-BY-4.0",
    "publishedAtUtc": "2026-07-22T10:00:00Z",
    "templateStatus": "Published",
    "moderationStatus": "Approved",
    "downloadCount": 42,
    "flagReasons": null
  }
]
```

### Template Schema Updates

The `template.json` schema now includes optional license and author metadata:

```json
{
  "schemaVersion": 1,
  "templateId": "my-game",
  "name": "My Game",
  "license": "CC-BY-4.0",
  "author": "Jane Doe",
  "authorEmail": "jane@example.com",
  "authorUrl": "https://example.com",
  ...
}
```

**Validation Rules**:

- `license`: Must be valid SPDX identifier or "Proprietary" (case-sensitive)
- `author`: String, 1–200 characters
- `authorEmail`: Valid email format
- `authorUrl`: Valid HTTP(S) URL

### Moderation Guideline

**Templates are approvable if**:

✅ No copyright violations (original artwork or properly licensed)  
✅ No trademark misuse without permission  
✅ No explicit malware or malicious code  
✅ License declaration is valid and clear  
✅ Author information is accurate and verifiable (for Proprietary)  
✅ No excessive offensive content inappropriate for family/educational use  

**Red flags requiring rejection or flagging**:

🚩 Uses game artwork/names without attribution or license  
🚩 Makes false claims (e.g., "official" when not licensed)  
🚩 Missing or unclear license  
🚩 License mismatch (e.g., claims CC-BY but includes proprietary art)  
🚩 Derivative of copyrighted work without compatible license  

### Access Control

**Public users** (unauthenticated):
- Can view public templates (`Approved`, `Published`)
- Cannot see drafts, flagged, or rejected

**Authenticated authors**:
- Can publish their own templates
- Cannot export others' templates (even if shared)
- Can see their own draft/rejected templates

**Admins**:
- Full access to moderation queue
- Can approve/reject/flag any template
- Can view all templates regardless of status
- Can search by owner, license, date range

### Testing Obligations

**Publishing**:
- ✅ Publish with valid license
- ✅ Reject invalid license
- ✅ Reject duplicate template + edition ID
- ✅ Verify metadata is persisted

**Moderation**:
- ✅ Moderation queue shows only pending items
- ✅ Approve moves status to "Approved" and clears flags
- ✅ Reject stores reason and hides template
- ✅ Flag stores multiple reasons and hides template
- ✅ Public catalog excludes non-approved templates

**Access Control**:
- ✅ Unauthenticated users see only public templates
- ✅ Authors cannot export other authors' templates
- ✅ Admins can see all templates
- ✅ Shared templates appear in user's catalog (but not public)

---

## Integration with Existing Components

### Phase 1–4 Compatibility

- Existing sessions, participants, and ledgers are **unaffected**
- Built-in templates do not require metadata (optional)
- User authentication from Phase 4 enables sharing & publishing

### Export/Import with Licensing

When exporting (Component 1):

```
template.zip/
├─ template.json (includes license, author fields)
├─ metadata.json (export timestamp, schema version)
└─ assets/
```

Importing respects the license field; admins may validate compliance.

### Session Starting with Shared/Public Templates

```
User → Import shared/public template
    → Create session with template snapshot
    → Template snapshot includes license/author at capture time
    → Audit log records who published/shared
```

---

## Future Enhancements (Phase 6+)

- **Email notifications** to admins for pending reviews
- **Featured curators** role for theme-based curation
- **Ratings/reviews** from users who played with templates
- **Search filters** by license, author, tag, rating
- **Author profiles** with all their published templates
- **Bulk import/export** for collections
- **License compatibility checker** for derived works
- **Takedown/DMCA process** for reported violations

---

## Glossary

| Term | Definition |
|------|-----------|
| SPDX | Software Package Data Exchange; standard for identifying licenses |
| Moderation | Admin review of published templates for policy compliance |
| Flagged | Marked for admin review due to potential issues |
| Proprietary | Custom or closed-source license (author-specified terms) |
| Curatorial | Admin action to elevate or feature templates |
| Derived Work | Template based on another template; must respect base license |
