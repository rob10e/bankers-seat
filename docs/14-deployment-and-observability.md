# Deployment and Observability

## Environments

- Development.
- Automated test.
- Staging.
- Production.
- Optional self-hosted local profile.

Each environment must have explicit configuration and must not depend on source-controlled secrets.

## Configuration

Suggested settings:

- Database provider and connection.
- Public base URL.
- allowed origins.
- template roots.
- asset limits.
- room-code length and expiration.
- reconnect credential lifetime.
- command rate limits.
- session retention.
- logging level.
- telemetry endpoint.
- feature flags.
- SignalR scaling/backplane.
- data protection key storage.

## Docker deployment

Recommended services:

```text
web/server application
database
reverse proxy
optional telemetry collector
```

The web application can be served by ASP.NET Core or as static assets behind the same reverse proxy.

Mounted paths:

- Installed templates.
- Content-addressed template assets.
- SQLite database for local mode.
- Data-protection keys.
- backups.

## Health endpoints

- `/health/live` — process is running.
- `/health/ready` — database and critical dependencies are available.
- `/health/templates` — catalog scan status and invalid-template count.
- `/health/version` — application and schema compatibility information.

Do not expose sensitive configuration in health responses.

## Structured logs

Log events such as:

- Session created/started/completed.
- Join success/failure category.
- command accepted/rejected.
- concurrency conflict.
- template discovered/invalidated.
- reconnect result.
- export generated.
- cleanup job result.

Use correlation IDs across HTTP, hub, application service, and persistence logs.

## Metrics

- Active sessions.
- Connected participants.
- command rate.
- command latency.
- rejection counts by code.
- reconnect success.
- SignalR connection failures.
- database transaction latency.
- catalog scan duration.
- valid/invalid template counts.
- export duration.
- unhandled exception count.

Avoid high-cardinality labels such as raw session IDs in metrics.

## Tracing

Trace critical command flow:

```text
receive command
authorize
idempotency lookup
domain validation
database transaction
event publication
client acknowledgement
```

## Error handling

- Return stable error codes.
- Show actionable client messages.
- Preserve technical details in server logs with correlation ID.
- Use global exception handling.
- Do not leak stack traces or database details.

## Release strategy

- Version server and web together initially.
- Publish release notes describing template/API compatibility.
- Run migration and backup checks.
- Use staging with representative concurrent sessions.
- Support rollback of application images.
- Avoid rolling back a database migration without a tested restore path.

## Self-hosted operations

Provide:

- Docker Compose example.
- environment-variable reference.
- template installation guide.
- backup and restore guide.
- upgrade guide.
- health check instructions.
- log collection instructions.
- default local-network security warning.

## Scale path

When one instance is no longer sufficient:

- PostgreSQL.
- shared data-protection keys.
- SignalR backplane/managed service.
- distributed cache for transient presence.
- object storage or shared immutable asset storage.
- distributed locks or database concurrency around a session aggregate.
- outbox publisher coordination.
