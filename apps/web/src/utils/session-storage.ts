/**
 * Session storage utilities for client-side session management.
 * Persists a user session ID for the duration of the browser session.
 */

const SESSION_USER_ID_KEY = "bankers-seat:session-user-id";

/**
 * Get or create a persistent session user ID for this browser session.
 * Used as a temporary user identifier until real authentication is implemented.
 */
export function getSessionUserId(): string {
  let sessionUserId = sessionStorage.getItem(SESSION_USER_ID_KEY);

  if (!sessionUserId) {
    // Generate a new UUID-like string (not cryptographically secure, but fine for this use case)
    sessionUserId = `${Date.now()}-${Math.random().toString(36).slice(2, 11)}`;
    sessionStorage.setItem(SESSION_USER_ID_KEY, sessionUserId);
  }

  return sessionUserId;
}

/**
 * Clear the session user ID (e.g., on logout).
 */
export function clearSessionUserId(): void {
  sessionStorage.removeItem(SESSION_USER_ID_KEY);
}
