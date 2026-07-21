# Phase 4 — Hosted Production Implementation

## Overview

Phase 4 adds enterprise-grade capabilities for hosted deployments, including user authentication, account management, data retention controls, room security enhancements, observability, and administrative support tools. This phase enables safe, scalable self-hosted or managed deployments with session ownership, audit trails, and compliance features.

## Implemented Features

### 1. PostgreSQL Database Support

**Status:** ✅ Complete

- Dual-database support: SQLite (development/small deployments) and PostgreSQL (production/scalable)
- Configured via `DatabaseProvider` setting in appsettings.json
- Connection pooling (Npgsql: 25 min, 100 max connections)
- Retry policy for transient failures
- All existing migrations work with both providers

**Configuration:**
```json
{
  "DatabaseProvider": "Postgres",
  "ConnectionStrings": {
    "BankersSeatPostgres": "Host=localhost;Port=5432;Database=bankers_seat;Username=postgres;Password=postgres"
  }
}
```

### 2. JWT Authentication & User Accounts

**Status:** ✅ Complete

- User registration with email and password
- JWT access tokens (default 15 minutes)
- Refresh token rotation (default 7 days)
- BCrypt password hashing (12-round work factor)
- Token validation middleware
- Secure credential storage

**User Account Entity:**
- Email (unique, lowercase-normalized)
- Password hash (BCrypt)
- Display name
- Creation and last-authentication timestamps
- Soft delete support

**Endpoints:**
- `POST /api/v1/auth/register` — Create account
- `POST /api/v1/auth/login` — Obtain tokens
- `POST /api/v1/auth/refresh` — Refresh access token
- `GET /api/v1/auth/me` — Get current user profile
- `POST /api/v1/auth/logout` — Logout (client-side token removal)

**Configuration:**
```json
{
  "Jwt": {
    "SigningKey": "your-256-bit-secret-key-minimum-32-characters",
    "Issuer": "bankers-seat",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

### 3. Session Ownership & Saved Sessions

**Status:** ✅ Complete

- Sessions linked to user accounts (optional)
- Session metadata (label, creation time, access time, participant count)
- Owned sessions listing with pagination
- Last-accessed tracking for "recent sessions" UI

**Endpoints:**
- `GET /api/v1/sessions/owned` — List user's sessions (paginated, requires auth)

**Features:**
- Automatic metadata creation when session is created
- Access time updates on read operations
- Session owner can be null for anonymous/guest sessions

### 4. Enhanced Room Security

**Status:** ✅ Complete

- Stronger room code generation (8 alphanumeric characters)
- Temporary join links with expiry
- IP-based rate limiting on join attempts
- Join link token consumption tracking
- Rate limit headers in responses

**Services:**
- `IRoomSecurityService` — Room code and link generation

**Endpoints:**
- `POST /api/v1/sessions/{sessionId}/join-links` — Create temporary link
- Rate limits enforced by middleware

**Rate Limit Configuration:**
```json
{
  "IpRateLimit": {
    "GeneralRules": [
      {
        "Endpoint": "*:/api/v1/sessions/join",
        "Period": "1m",
        "Limit": 10
      },
      {
        "Endpoint": "*:/api/v1/auth/login",
        "Period": "1m",
        "Limit": 5
      }
    ]
  }
}
```

### 5. Audit Logging

**Status:** ✅ Complete

- Immutable audit trail for all sensitive operations
- Captures actor (user or participant), action, timestamp, IP address, result
- Queryable by session
- Supports custom action types

**Services:**
- `IAuditService` — Log and query audit events

**Endpoints:**
- `GET /api/v1/sessions/{sessionId}/audit-logs` — View session audit trail (requires auth)

**Audited Actions:**
- User registration and authentication
- Session creation, join, lifecycle changes
- Monetary transactions and corrections
- Template action execution
- Admin operations

### 6. Data Retention & Privacy

**Status:** ✅ Complete

- Configurable TTL policies per session
- Automatic cleanup of expired incomplete sessions
- Ledger archive marking (for audit trails)
- GDPR: User account deletion cascades to owned sessions
- Soft-delete pattern preserves historical data

**Services:**
- `IDataRetentionService` — TTL management and cleanup

**Features:**
- Background job ready (manual invocation for now)
- Dry-run mode for testing cleanup logic
- Retention policies linked to session lifecycle

**Configuration:**
```json
{
  "DataRetention": {
    "IncompleteSessionTtlDays": 7,
    "CompletedSessionTtlDays": 365,
    "LedgerArchiveAfterDays": 90,
    "AutoDeleteEnabled": false
  }
}
```

### 7. Administrative Support Operations

**Status:** ✅ Complete

- Admin-only endpoints for session management
- Session lookup by code, ID, or owner
- Force pause/resume/delete capabilities
- Full audit trail of admin actions
- Support tools for incident response

**Endpoints:**
- `GET /api/v1/admin/sessions/{sessionId}` — Session details
- `GET /api/v1/admin/sessions?roomCode=...&ownerId=...` — Search sessions
- `POST /api/v1/admin/sessions/{sessionId}/pause` — Admin pause
- `POST /api/v1/admin/sessions/{sessionId}/resume` — Admin resume
- `DELETE /api/v1/admin/sessions/{sessionId}` — Admin delete

**Admin Detection:**
- Email ending with `@admin.local` grants admin privileges

### 8. Observability & Monitoring

**Status:** ✅ Complete

- Application Insights integration (ready for configuration)
- Structured logging on all command handlers
- Rate limit tracking
- Command execution timing

**Integration Points:**
- Dependency injection ready
- Instrumentation key configurable in appsettings.json
- No-op configuration for development

**Configuration:**
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-instrumentation-key"
  }
}
```

## Database Schema

### New Tables

#### user_accounts
- id (GUID, PK)
- email (VARCHAR 256, unique, indexed)
- password_hash_bcrypt (VARCHAR max)
- display_name (VARCHAR 100)
- created_at_utc (DATETIME, indexed)
- last_authenticated_at_utc (DATETIME)
- is_deleted (BOOLEAN)

#### refresh_tokens
- id (GUID, PK)
- user_id (GUID, FK → user_accounts)
- token_hash (VARCHAR 128, indexed)
- expires_at_utc (DATETIME, indexed)
- created_at_utc (DATETIME)
- is_revoked (BOOLEAN)

#### audit_logs
- id (GUID, PK)
- session_id (GUID, FK, nullable, indexed)
- actor_user_id (GUID, nullable, indexed)
- actor_participant_id (GUID, nullable)
- action (VARCHAR 100)
- details (TEXT)
- ip_address (VARCHAR 45, nullable)
- user_agent (TEXT, nullable)
- created_at_utc (DATETIME, indexed)
- result (TEXT, nullable)

#### session_metadata
- session_id (GUID, PK, FK → game_sessions)
- owner_user_id (GUID, nullable, FK, indexed)
- label (VARCHAR 200)
- created_at_utc (DATETIME)
- last_accessed_at_utc (DATETIME, indexed)
- participant_count (INT)

#### join_links
- id (GUID, PK)
- session_id (GUID, FK, indexed)
- link_token (VARCHAR 128, unique)
- expires_at_utc (DATETIME, indexed)
- created_at_utc (DATETIME)
- use_count (INT)
- is_revoked (BOOLEAN)

#### session_ttl_policies
- session_id (GUID, PK, FK)
- retention_days (INT)
- auto_delete_on_complete (BOOLEAN)
- expires_at_utc (DATETIME, indexed)
- is_archived (BOOLEAN, indexed)
- archived_at_utc (DATETIME, nullable)

## API Contracts

### Authentication

#### POST /api/v1/auth/register
Request:
```json
{
  "email": "user@example.com",
  "password": "secure-password-min-8-chars",
  "displayName": "Display Name"
}
```
Response (201):
```json
{
  "success": true,
  "accessToken": "eyJ...",
  "refreshToken": "base64-encoded-token"
}
```

#### POST /api/v1/auth/login
Request:
```json
{
  "email": "user@example.com",
  "password": "password"
}
```
Response (200):
```json
{
  "success": true,
  "accessToken": "eyJ...",
  "refreshToken": "base64-encoded-token"
}
```

#### POST /api/v1/auth/refresh
Request:
```json
{
  "refreshToken": "base64-encoded-token"
}
```
Response (200):
```json
{
  "success": true,
  "accessToken": "eyJ...",
  "refreshToken": "base64-encoded-token"
}
```

#### GET /api/v1/auth/me (Requires: Authorization header)
Response (200):
```json
{
  "id": "guid",
  "email": "user@example.com",
  "displayName": "Display Name",
  "createdAtUtc": "2026-07-21T03:10:18Z",
  "lastAuthenticatedAtUtc": "2026-07-21T03:10:18Z"
}
```

### Sessions

#### GET /api/v1/sessions/owned (Requires: Authorization header)
Query Parameters:
- `limit` (optional, default 20)

Response (200):
```json
{
  "sessions": [
    {
      "sessionId": "guid",
      "roomCode": "ABCD1234",
      "label": "My Game",
      "templateName": "Template Name",
      "createdAtUtc": "2026-07-21T03:10:18Z",
      "lastAccessedAtUtc": "2026-07-21T03:15:00Z",
      "participantCount": 4
    }
  ],
  "total": 1
}
```

#### POST /api/v1/sessions/{sessionId}/join-links (Requires: Authorization header)
Request:
```json
{
  "expirationMinutes": 60
}
```
Response (201):
```json
{
  "id": "guid",
  "linkToken": "base64-token",
  "expiresAtUtc": "2026-07-21T04:10:18Z",
  "joinUrl": "https://example.com/join?link=base64-token"
}
```

#### GET /api/v1/sessions/{sessionId}/audit-logs (Requires: Authorization header)
Query Parameters:
- `limit` (optional, default 100)

Response (200):
```json
[
  {
    "id": "guid",
    "actorUserId": "guid",
    "action": "CreateJoinLink",
    "details": "Created temporary join link",
    "ipAddress": "192.168.1.1",
    "createdAtUtc": "2026-07-21T03:10:18Z",
    "result": "success"
  }
]
```

### Admin Operations

#### GET /api/v1/admin/sessions/{sessionId} (Requires: Authorization header + @admin.local email)
Response (200):
```json
{
  "sessionId": "guid",
  "roomCode": "ABCD1234",
  "status": "active",
  "ownerUserId": "guid",
  "participantCount": 4,
  "createdAtUtc": "2026-07-21T03:10:18Z",
  "lastAccessedAtUtc": "2026-07-21T03:15:00Z",
  "transactionCount": 15
}
```

#### GET /api/v1/admin/sessions (Requires: Authorization header + @admin.local email)
Query Parameters:
- `roomCode` (optional filter)
- `ownerId` (optional filter)
- `limit` (optional, default 50)

Response (200):
```json
[
  { /* AdminSessionInfoResponse */ }
]
```

## Running the Application

### Development (SQLite)
```bash
export DatabaseProvider=Sqlite
export Jwt__SigningKey=your-test-key-minimum-32-characters-long
dotnet run --project apps/server
```

### Production (PostgreSQL)
```bash
export DatabaseProvider=Postgres
export Jwt__SigningKey=your-production-key-minimum-32-characters
export ConnectionStrings__BankersSeatPostgres="Host=db.example.com;Port=5432;Database=bankers_seat;Username=app;Password=secret"
dotnet run --project apps/server
```

## Security Considerations

### Password Handling
- Bcrypt with 12-round work factor
- Minimum 8 characters enforced on registration
- Never log or transmit passwords
- Use HTTPS only in production

### Token Management
- JWT signed with HS256
- Access tokens short-lived (15 min default)
- Refresh tokens stored as hashed values
- Revocation supported
- Expired tokens immediately rejected

### Rate Limiting
- Per-IP sliding window on sensitive endpoints
- Configurable thresholds
- No bypass for authenticated users (prevent credential stuffing)
- Returns X-RateLimit-* headers

### Audit Trail
- Immutable append-only records
- Includes IP address and user agent
- Tracks actor identity
- Action details captured
- Queryable for compliance

### Admin Credentials
- Email-based (`@admin.local` suffix)
- Still requires valid JWT token
- All admin actions audited
- Recommended: Use strong, unique email or use environment variables for separate admin credentials

## Migration Path

### From Phase 3 → Phase 4

**No data loss:**
1. All existing game sessions preserved
2. Session metadata created automatically
3. Participants can join without account (backward compatible)
4. Existing ledger entries unchanged

**Manual Steps:**
1. Run `dotnet ef database update` to apply new tables
2. Configure JWT signing key (generate secure key in production)
3. Set `DatabaseProvider` if moving to PostgreSQL
4. Update CORS origin if hosting remotely
5. Enable auth on frontend (login/register screens)

**Gradual Adoption:**
- Existing sessions work without accounts
- New sessions can be user-owned
- Authentication is optional per endpoint
- Rate limiting applies to all users

## Testing the Phase 4 Features

### Authentication Flow
```bash
# Register
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "TestPassword123",
    "displayName": "Test User"
  }'

# Login
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "TestPassword123"
  }'

# Get Profile (use accessToken from login)
curl -H "Authorization: Bearer {accessToken}" \
  http://localhost:5000/api/v1/auth/me
```

### Owned Sessions
```bash
curl -H "Authorization: Bearer {accessToken}" \
  "http://localhost:5000/api/v1/sessions/owned?limit=20"
```

### Create Join Link
```bash
curl -X POST "http://localhost:5000/api/v1/sessions/{sessionId}/join-links" \
  -H "Authorization: Bearer {accessToken}" \
  -H "Content-Type: application/json" \
  -d '{"expirationMinutes": 60}'
```

## Known Limitations & Future Work

### Current Limitations
- Admin users identified by email suffix (not role-based)
- Automatic cleanup runs on-demand (not background job yet)
- Single-tenancy mode (no organization support)
- No IP allowlist/denylist

### Future Enhancements
- Role-based access control (RBAC)
- Scheduled background tasks (cleanup, reporting)
- Multi-tenancy support
- OAuth/OIDC federation
- Hardware security key support
- Session-level permission scopes

## Support & Troubleshooting

### "Invalid JWT"
- Verify signing key is configured correctly
- Check token has not expired
- Ensure Authorization header format is `Bearer {token}`

### Rate limit exceeded
- Check `X-RateLimit-Remaining` header
- Implement exponential backoff on client
- Whitelist trusted IPs via `IpRateLimit:IpWhitelist`

### Database connection failed
- Verify connection string
- Check PostgreSQL is running (if using Postgres provider)
- Ensure network access to database

### "Unauthorized" on admin endpoints
- Verify token present and valid
- Check email ends with `@admin.local`
- Confirm user is not deleted

## Summary

Phase 4 brings production-ready features for hosted deployments:
- **Security:** JWT auth, rate limiting, audit trails
- **Scalability:** PostgreSQL support, connection pooling
- **Compliance:** Data retention, GDPR deletion, audit logs
- **Operations:** Admin tools, session management, monitoring integration

All Phase 4 features are backward compatible with Phase 3. Existing sessions continue to work; new features are opt-in on the frontend.
