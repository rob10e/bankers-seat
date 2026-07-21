import { Page, expect } from '@playwright/test';

export interface SessionCreate {
  templateId: string;
  editionId: string;
  templateVersion: string;
  hostDisplayName: string;
  sessionOptions: Record<string, unknown>;
}

export interface SessionJoin {
  roomCode: string;
  displayName: string;
  identityKey?: string;
}

export interface SessionSnapshot {
  sessionId: string;
  sessionVersion: number;
  roomCode: string;
  status: 'lobby' | 'active' | 'paused' | 'completed';
  template: {
    templateId: string;
    editionId: string;
    templateVersion: string;
  };
  participants: Array<{
    participantId: string;
    displayName: string;
    role: 'host' | 'player';
    identityKey?: string;
  }>;
  accounts: Array<{
    participantId: string;
    balance: number;
  }>;
  playerFields: Record<string, Record<string, unknown>>;
}

export interface ParticipantCredentials {
  sessionId: string;
  participantId: string;
  reconnectCredential: string;
}

const API_BASE = 'http://localhost:5266';

/**
 * Create a game session via API
 */
export async function createSession(
  page: Page,
  request: SessionCreate
): Promise<ParticipantCredentials & { snapshot: SessionSnapshot; roomCode: string }> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions`, {
    data: request,
  });

  expect(response.status()).toBe(200);
  const data = await response.json();

  return {
    sessionId: data.sessionId,
    participantId: data.hostParticipantId,
    reconnectCredential: data.reconnectCredential,
    roomCode: data.roomCode,
    snapshot: data.snapshot,
  };
}

/**
 * Join an existing session
 */
export async function joinSession(
  page: Page,
  roomCode: string,
  displayName: string,
  identityKey?: string
): Promise<ParticipantCredentials & { snapshot: SessionSnapshot }> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions/join`, {
    data: {
      roomCode,
      displayName,
      identityKey,
    },
  });

  expect(response.status()).toBe(200);
  const data = await response.json();

  return {
    sessionId: data.sessionId,
    participantId: data.participantId,
    reconnectCredential: data.reconnectCredential,
    snapshot: data.snapshot,
  };
}

/**
 * Get current session snapshot
 */
export async function getSnapshot(page: Page, sessionId: string): Promise<SessionSnapshot> {
  const response = await page.request.get(`${API_BASE}/api/v1/sessions/${sessionId}/snapshot`);
  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Start a session (host-authorized)
 */
export async function startSession(
  page: Page,
  sessionId: string,
  participantId: string,
  credential: string,
  expectedVersion: number,
  idempotencyKey: string
): Promise<SessionSnapshot> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions/${sessionId}/start`, {
    data: {
      expectedSessionVersion: expectedVersion,
    },
    headers: {
      'X-Actor-Id': participantId,
      'X-Actor-Credential': credential,
      'Idempotency-Key': idempotencyKey,
    },
  });

  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Complete a session (host-authorized)
 */
export async function completeSession(
  page: Page,
  sessionId: string,
  participantId: string,
  credential: string,
  expectedVersion: number,
  idempotencyKey: string
): Promise<SessionSnapshot> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions/${sessionId}/complete`, {
    data: {
      expectedSessionVersion: expectedVersion,
    },
    headers: {
      'X-Actor-Id': participantId,
      'X-Actor-Credential': credential,
      'Idempotency-Key': idempotencyKey,
    },
  });

  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Transfer between participants (host-authorized)
 */
export async function transfer(
  page: Page,
  sessionId: string,
  hostId: string,
  credential: string,
  expectedVersion: number,
  fromParticipantId: string,
  toParticipantId: string,
  amount: number,
  idempotencyKey: string
): Promise<SessionSnapshot> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions/${sessionId}/transfer`, {
    data: {
      fromParticipantId,
      toParticipantId,
      amountInCents: amount,
      expectedSessionVersion: expectedVersion,
    },
    headers: {
      'X-Actor-Id': hostId,
      'X-Actor-Credential': credential,
      'Idempotency-Key': idempotencyKey,
    },
  });

  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Bank payment to participant (host-authorized)
 */
export async function bankPayment(
  page: Page,
  sessionId: string,
  hostId: string,
  credential: string,
  expectedVersion: number,
  toParticipantId: string,
  amount: number,
  idempotencyKey: string
): Promise<SessionSnapshot> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions/${sessionId}/bank-payments`, {
    data: {
      toParticipantId,
      amountInCents: amount,
      expectedSessionVersion: expectedVersion,
    },
    headers: {
      'X-Actor-Id': hostId,
      'X-Actor-Credential': credential,
      'Idempotency-Key': idempotencyKey,
    },
  });

  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Bank collection from participant (host-authorized)
 */
export async function bankCollection(
  page: Page,
  sessionId: string,
  hostId: string,
  credential: string,
  expectedVersion: number,
  fromParticipantId: string,
  amount: number,
  idempotencyKey: string
): Promise<SessionSnapshot> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions/${sessionId}/bank-collections`, {
    data: {
      fromParticipantId,
      amountInCents: amount,
      expectedSessionVersion: expectedVersion,
    },
    headers: {
      'X-Actor-Id': hostId,
      'X-Actor-Credential': credential,
      'Idempotency-Key': idempotencyKey,
    },
  });

  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Execute a template action (host-authorized)
 */
export async function executeAction(
  page: Page,
  sessionId: string,
  hostId: string,
  credential: string,
  expectedVersion: number,
  actionId: string,
  scope: 'single-player' | 'two-players' | 'all-players',
  targetParticipants?: string[],
  idempotencyKey?: string
): Promise<SessionSnapshot> {
  const response = await page.request.post(
    `${API_BASE}/api/v1/sessions/${sessionId}/actions/${actionId}/execute`,
    {
      data: {
        scope,
        targetParticipants: targetParticipants || [],
        expectedSessionVersion: expectedVersion,
      },
      headers: {
        'X-Actor-Id': hostId,
        'X-Actor-Credential': credential,
        'Idempotency-Key': idempotencyKey || crypto.randomUUID(),
      },
    }
  );

  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Correct a transaction (host-authorized)
 */
export async function correctTransaction(
  page: Page,
  sessionId: string,
  hostId: string,
  credential: string,
  expectedVersion: number,
  transactionId: string,
  reason: string,
  idempotencyKey: string
): Promise<SessionSnapshot> {
  const response = await page.request.post(`${API_BASE}/api/v1/sessions/${sessionId}/corrections`, {
    data: {
      transactionId,
      reason,
      expectedSessionVersion: expectedVersion,
    },
    headers: {
      'X-Actor-Id': hostId,
      'X-Actor-Credential': credential,
      'Idempotency-Key': idempotencyKey,
    },
  });

  expect(response.status()).toBe(200);
  return response.json();
}

/**
 * Get ledger with pagination
 */
export async function getLedger(
  page: Page,
  sessionId: string,
  participantId: string,
  credential: string,
  limit?: number,
  cursor?: string
): Promise<{
  entries: Array<{
    transactionId: string;
    timestamp: string;
    postings: Array<{
      participantId: string;
      amount: number;
      accountKind: string;
    }>;
  }>;
  nextCursor?: string;
}> {
  const url = new URL(`/api/v1/sessions/${sessionId}/ledger`, API_BASE);
  if (limit) url.searchParams.set('limit', String(limit));
  if (cursor) url.searchParams.set('cursor', cursor);

  const response = await page.request.get(url.toString(), {
    headers: {
      'X-Actor-Id': participantId,
      'X-Actor-Credential': credential,
    },
  });

  expect(response.status()).toBe(200);
  return response.json();
}
