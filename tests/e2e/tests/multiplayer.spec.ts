import { test, expect, Page, Browser, BrowserContext } from '@playwright/test';
import {
  createSession,
  joinSession,
  getSnapshot,
  startSession,
  bankPayment,
  transfer,
  completeSession,
  correctTransaction,
  getLedger,
  SessionSnapshot,
} from '../helpers/session-api';

test.describe('Multiplayer Game Sessions', () => {
  const TEMPLATE_ID = 'generic-property-trading';
  const EDITION_ID = 'standard-edition';
  const TEMPLATE_VERSION = '1.0.0';

  test('Host creates a session, two players join, host starts game, and all see matching state', async ({
    page,
    browser,
  }) => {
    // Step 1: Host creates a session
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    expect(hostSession.snapshot.status).toBe('lobby');
    expect(hostSession.snapshot.participants).toHaveLength(1);
    expect(hostSession.snapshot.participants[0].role).toBe('host');

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    // Step 2: Player 1 joins
    const player1Page = await browser.newPage();
    const player1Session = await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    expect(player1Session.sessionId).toBe(sessionId);
    expect(player1Session.snapshot.participants).toHaveLength(2);

    // Step 3: Player 2 joins
    const player2Page = await browser.newPage();
    const player2Session = await joinSession(player2Page, roomCode, 'Player 2', 'red');

    expect(player2Session.snapshot.participants).toHaveLength(3);

    // Verify all three clients see the same participant list
    const hostSnapshot = await getSnapshot(page, sessionId);
    const player1Snapshot = await getSnapshot(player1Page, sessionId);
    const player2Snapshot = await getSnapshot(player2Page, sessionId);

    expect(hostSnapshot.sessionVersion).toBe(player1Snapshot.sessionVersion);
    expect(hostSnapshot.sessionVersion).toBe(player2Snapshot.sessionVersion);
    expect(hostSnapshot.participants).toHaveLength(3);

    // Step 4: Host starts the game
    const startedSnapshot = await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    expect(startedSnapshot.status).toBe('active');
    expect(startedSnapshot.sessionVersion).toBeGreaterThan(hostSnapshot.sessionVersion);

    // Verify all clients see the updated state
    const hostAfterStart = await getSnapshot(page, sessionId);
    const player1AfterStart = await getSnapshot(player1Page, sessionId);
    const player2AfterStart = await getSnapshot(player2Page, sessionId);

    expect(hostAfterStart.status).toBe('active');
    expect(player1AfterStart.status).toBe('active');
    expect(player2AfterStart.status).toBe('active');

    await player1Page.close();
    await player2Page.close();
  });

  test('Bank payment updates balances consistently across devices', async ({ page, browser }) => {
    // Setup: Host creates session, two players join
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    const player1Page = await browser.newPage();
    const player1Session = await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    const hostSnapshot = await getSnapshot(page, sessionId);
    const startedSnapshot = await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    const player1Id = player1Session.participantId;
    const initialBalance = startedSnapshot.accounts.find(
      (a) => a.participantId === player1Id
    )?.balance;

    // Host makes a bank payment to Player 1
    const payment = 50000; // $500 in cents
    const afterPayment = await bankPayment(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      startedSnapshot.sessionVersion,
      player1Id,
      payment,
      `payment-${Date.now()}`
    );

    // Verify host sees updated balance
    const hostBalance = afterPayment.accounts.find((a) => a.participantId === player1Id)?.balance;
    expect(hostBalance).toBe((initialBalance || 0) + payment);

    // Verify player 1 sees the same updated balance
    const player1Snapshot = await getSnapshot(player1Page, sessionId);
    const player1Balance = player1Snapshot.accounts.find((a) => a.participantId === player1Id)
      ?.balance;
    expect(player1Balance).toBe(hostBalance);

    await player1Page.close();
  });

  test('Transfer between players updates both balances atomically', async ({ page, browser }) => {
    // Setup: Host creates session, two players join and start
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    const player1Page = await browser.newPage();
    const player1Session = await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    const player2Page = await browser.newPage();
    const player2Session = await joinSession(player2Page, roomCode, 'Player 2', 'red');

    const hostSnapshot = await getSnapshot(page, sessionId);
    const startedSnapshot = await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    const player1Id = player1Session.participantId;
    const player2Id = player2Session.participantId;
    const player1InitialBalance = startedSnapshot.accounts.find(
      (a) => a.participantId === player1Id
    )?.balance;
    const player2InitialBalance = startedSnapshot.accounts.find(
      (a) => a.participantId === player2Id
    )?.balance;

    // Host transfers 100 credits from Player 1 to Player 2
    const transferAmount = 10000; // $100
    const afterTransfer = await transfer(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      startedSnapshot.sessionVersion,
      player1Id,
      player2Id,
      transferAmount,
      `transfer-${Date.now()}`
    );

    // Verify the transfer is atomic
    const player1Balance = afterTransfer.accounts.find((a) => a.participantId === player1Id)
      ?.balance;
    const player2Balance = afterTransfer.accounts.find((a) => a.participantId === player2Id)
      ?.balance;

    expect(player1Balance).toBe((player1InitialBalance || 0) - transferAmount);
    expect(player2Balance).toBe((player2InitialBalance || 0) + transferAmount);

    // Verify all clients see the same result
    const hostAfter = await getSnapshot(page, sessionId);
    const player1After = await getSnapshot(player1Page, sessionId);
    const player2After = await getSnapshot(player2Page, sessionId);

    const p1HostView = hostAfter.accounts.find((a) => a.participantId === player1Id)?.balance;
    const p1Player1View = player1After.accounts.find((a) => a.participantId === player1Id)
      ?.balance;
    const p1Player2View = player2After.accounts.find((a) => a.participantId === player1Id)
      ?.balance;

    expect(p1HostView).toBe(p1Player1View);
    expect(p1HostView).toBe(p1Player2View);

    await player1Page.close();
    await player2Page.close();
  });

  test('Duplicate transaction submission is idempotent', async ({ page }) => {
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    const sessionId = hostSession.sessionId;

    const hostSnapshot = await getSnapshot(page, sessionId);
    const startedSnapshot = await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    const participants = startedSnapshot.participants.filter((p) => p.role === 'player');
    if (participants.length === 0) {
      test.skip();
      return;
    }

    const targetId = participants[0].participantId;
    const idempotencyKey = `payment-idempotent-${Date.now()}`;

    // First payment
    const payment1 = await bankPayment(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      startedSnapshot.sessionVersion,
      targetId,
      5000,
      idempotencyKey
    );

    const balance1 = payment1.accounts.find((a) => a.participantId === targetId)?.balance;

    // Replay with same idempotency key - should not double-charge
    const payment2 = await bankPayment(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      payment1.sessionVersion,
      targetId,
      5000,
      idempotencyKey
    );

    const balance2 = payment2.accounts.find((a) => a.participantId === targetId)?.balance;

    expect(balance1).toBe(balance2);
  });

  test('Host correction creates compensating ledger entry', async ({ page, browser }) => {
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    const player1Page = await browser.newPage();
    await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    const hostSnapshot = await getSnapshot(page, sessionId);
    const startedSnapshot = await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    const participants = startedSnapshot.participants.filter((p) => p.role === 'player');
    const targetId = participants[0].participantId;

    // Make a payment
    const paymentSnapshot = await bankPayment(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      startedSnapshot.sessionVersion,
      targetId,
      10000,
      `payment-to-correct-${Date.now()}`
    );

    // Get ledger to find transaction ID
    const ledger = await getLedger(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      10
    );

    expect(ledger.entries.length).toBeGreaterThan(0);
    const transactionId = ledger.entries[0].transactionId;

    // Correct the transaction
    const correctionSnapshot = await correctTransaction(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      paymentSnapshot.sessionVersion,
      transactionId,
      'Accidental duplicate payment',
      `correction-${Date.now()}`
    );

    // Verify correction is in ledger
    const correctedLedger = await getLedger(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      10
    );

    expect(correctedLedger.entries.length).toBeGreaterThan(ledger.entries.length);

    await player1Page.close();
  });

  test('Refresh during active game preserves participant identity', async ({ page, browser }) => {
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    const player1Page = await browser.newPage();
    const player1Session = await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    const hostSnapshot = await getSnapshot(page, sessionId);
    await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    const player1Id = player1Session.participantId;
    const player1Balance = player1Session.snapshot.accounts.find(
      (a) => a.participantId === player1Id
    )?.balance;

    // Simulate refresh by reloading player1 page
    await player1Page.reload();
    await player1Page.waitForLoadState('networkidle');

    // Get new snapshot after reload
    const afterReload = await getSnapshot(player1Page, sessionId);

    // Verify identity is preserved
    const participant = afterReload.participants.find((p) => p.displayName === 'Player 1');
    expect(participant).toBeDefined();
    expect(participant?.participantId).toBe(player1Id);

    // Verify balance is correct
    const reloadedBalance = afterReload.accounts.find((a) => a.participantId === player1Id)
      ?.balance;
    expect(reloadedBalance).toBe(player1Balance);

    await player1Page.close();
  });

  test('Insufficient funds rejection when overdraft is disabled', async ({ page, browser }) => {
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {
        'allow-overdraft': false,
      },
    });

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    const player1Page = await browser.newPage();
    const player1Session = await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    const hostSnapshot = await getSnapshot(page, sessionId);
    const startedSnapshot = await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    const player1Id = player1Session.participantId;
    const player1Balance = startedSnapshot.accounts.find(
      (a) => a.participantId === player1Id
    )?.balance;

    // Attempt to collect more than the player's balance
    const excessAmount = (player1Balance || 0) + 10000; // More than available

    const response = await page.request.post(`http://localhost:5266/api/v1/sessions/${sessionId}/bank-collections`, {
      data: {
        fromParticipantId: player1Id,
        amountInCents: excessAmount,
        expectedSessionVersion: startedSnapshot.sessionVersion,
      },
      headers: {
        'X-Actor-Id': hostSession.participantId,
        'X-Actor-Credential': hostSession.reconnectCredential,
        'Idempotency-Key': `collection-excess-${Date.now()}`,
      },
    });

    expect(response.status()).toBe(400);

    await player1Page.close();
  });

  test('Session completion and export preserves full audit trail', async ({ page, browser }) => {
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    const player1Page = await browser.newPage();
    const player1Session = await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    const hostSnapshot = await getSnapshot(page, sessionId);
    const startedSnapshot = await startSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      hostSnapshot.sessionVersion,
      `start-${Date.now()}`
    );

    const player1Id = player1Session.participantId;

    // Make a transaction
    await bankPayment(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      startedSnapshot.sessionVersion,
      player1Id,
      5000,
      `payment-before-completion-${Date.now()}`
    );

    const beforeCompletion = await getSnapshot(page, sessionId);

    // Complete the session
    const completedSnapshot = await completeSession(
      page,
      sessionId,
      hostSession.participantId,
      hostSession.reconnectCredential,
      beforeCompletion.sessionVersion,
      `complete-${Date.now()}`
    );

    expect(completedSnapshot.status).toBe('completed');

    // Export the session
    const exportResponse = await page.request.get(`http://localhost:5266/api/v1/sessions/${sessionId}/export`, {
      headers: {
        'X-Actor-Id': hostSession.participantId,
        'X-Actor-Credential': hostSession.reconnectCredential,
      },
    });

    expect(exportResponse.status()).toBe(200);
    const exportData = await exportResponse.json();

    // Verify export contains full audit trail
    expect(exportData.snapshot).toBeDefined();
    expect(exportData.ledger).toBeDefined();
    expect(exportData.ledger.length).toBeGreaterThan(0);

    await player1Page.close();
  });

  test('Invalid room code is rejected on join', async ({ page }) => {
    const response = await page.request.post('http://localhost:5266/api/v1/sessions/join', {
      data: {
        roomCode: 'INVALID',
        displayName: 'Hacker',
      },
    });

    expect(response.status()).toBe(404);
  });

  test('Stale session version is rejected with conflict', async ({ page, browser }) => {
    const hostSession = await createSession(page, {
      templateId: TEMPLATE_ID,
      editionId: EDITION_ID,
      templateVersion: TEMPLATE_VERSION,
      hostDisplayName: 'Host',
      sessionOptions: {},
    });

    const sessionId = hostSession.sessionId;
    const roomCode = hostSession.roomCode;

    const player1Page = await browser.newPage();
    const player1Session = await joinSession(player1Page, roomCode, 'Player 1', 'blue');

    const hostSnapshot1 = await getSnapshot(page, sessionId);

    // Simulate out-of-sync client by using stale version
    const staleVersion = 1;

    const response = await page.request.post(`http://localhost:5266/api/v1/sessions/${sessionId}/start`, {
      data: {
        expectedSessionVersion: staleVersion,
      },
      headers: {
        'X-Actor-Id': hostSession.participantId,
        'X-Actor-Credential': hostSession.reconnectCredential,
        'Idempotency-Key': `start-stale-${Date.now()}`,
      },
    });

    expect(response.status()).toBe(409);

    await player1Page.close();
  });
});
