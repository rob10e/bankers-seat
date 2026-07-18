# Security, Privacy, and Intellectual Property

## Security model

The application is not a real financial system, but users still expect game-state integrity. Treat balances and tracked state as protected session data.

## Threats

- Guessing room codes.
- Impersonating a player after learning a display name.
- Sending forged balance commands.
- Duplicate command replay.
- Tampering with client state.
- Unauthorized host/banker action.
- Malicious template JSON.
- Asset path traversal or script execution.
- Denial of service through large templates or command spam.
- Sensitive data in logs or exports.
- Supply-chain vulnerabilities.
- Unlicensed template assets.

## Room access

Room codes are short and convenient but are not sufficient as long-term authorization.

Recommended flow:

- Join with room code.
- Server creates participant identity.
- Server returns a cryptographically strong participant reconnect secret.
- Store only a protected representation server-side where possible.
- Use short-lived access credentials for hub/API operations.
- Rotate credentials after suspicious activity or host removal.

Optional later controls:

- Host-approved join.
- Room PIN.
- Per-player PIN.
- Account login.
- Private invite links.

## Authorization

Every mutating command checks:

- Session membership.
- Participant status.
- Role.
- Ownership.
- Session lifecycle state.
- Template policy.
- Command-specific permission.

The UI hiding a button is not authorization.

## Template security

- Parse with input-size limits.
- Validate JSON Schema.
- Perform semantic validation.
- Reject unknown operations.
- Reject arbitrary expressions and code.
- Restrict asset paths.
- Prevent symlink escape.
- Disable remote assets by default.
- Sanitize SVG or convert it to a safe raster format.
- Serve assets from a separate route with `nosniff` and safe content types.
- Apply content security policy.

## Web security

- HTTPS in hosted production.
- Secure headers.
- Strict content security policy.
- CSRF protection for cookie-authenticated endpoints.
- CORS restricted to known origins.
- Rate limits.
- Request body limits.
- output encoding.
- dependency updates.
- no secrets in browser bundles.

## Privacy

MVP should require minimal data:

- Display name.
- Session identity.
- Gameplay state.
- Technical connection information.

Avoid collecting:

- Legal name.
- Email.
- location.
- contacts.
- advertising identifiers.
- unnecessary analytics.

Provide configurable retention and explicit session deletion for hosted deployments.

## Logging

Useful structured fields:

- Correlation ID.
- Session ID.
- participant ID.
- command type.
- result code.
- duration.
- server version.
- template identity/hash.

Do not log:

- Reconnect secrets.
- raw authorization tokens.
- full imported templates by default.
- private field values.
- free-form notes without a clear operational need.

## Exports

Exports may contain player names and gameplay history. Require authorization, mark exports with creation time, and avoid public predictable URLs.

## Intellectual property

Board game names, logos, artwork, board designs, card text, and rulebook text may be protected.

Repository policy:

- Ship original generic examples.
- Require template authors to assert they have rights to submitted assets.
- Keep official/licensed templates separate.
- Provide a removal/reporting process before operating a public marketplace.
- Do not market generic templates in a way that implies official endorsement.
- Do not copy proprietary rules when a neutral mechanic description is sufficient.

This is product planning, not legal advice. A commercial release should receive appropriate legal review.

## Security response

Before public hosting, define:

- Vulnerability reporting contact.
- Patch severity process.
- dependency update process.
- incident logging and containment.
- user notification criteria.
- backup and restoration steps.
