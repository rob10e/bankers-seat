/**
 * Session storage utilities for client-side session management.
 * Persists a user session ID for the duration of the browser session.
 */

const SESSION_USER_ID_KEY = "bankers-seat:session-user-id";

/**
 * Generate a simple UUID v4-like Guid.
 */
function generateGuid(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

/**
 * Get or create a persistent session user ID (Guid) for this browser session.
 * Used as a temporary user identifier until real authentication is implemented.
 */
export function getSessionUserId(): string {
  let sessionUserId = sessionStorage.getItem(SESSION_USER_ID_KEY);

  if (!sessionUserId) {
    sessionUserId = generateGuid();
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
