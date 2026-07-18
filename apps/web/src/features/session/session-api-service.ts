import type {
  CreateSessionRequest,
  CreateSessionResponse,
  JoinSessionRequest,
  JoinSessionResponse,
  SessionSnapshot,
} from "../../domain/session.ts";

interface UnknownRecord {
  [key: string]: unknown;
}

const isRecord = (value: unknown): value is UnknownRecord => {
  return typeof value === "object" && value !== null;
};

const asString = (value: unknown): string | null => {
  return typeof value === "string" ? value : null;
};

const asNumber = (value: unknown): number | null => {
  return typeof value === "number" ? value : null;
};

const parseSessionSnapshot = (value: unknown): SessionSnapshot | null => {
  if (!isRecord(value)) {
    return null;
  }

  const {
    sessionId,
    roomCode,
    status,
    sessionVersion,
    createdAtUtc,
    hostParticipantId,
    template,
    participants,
    accounts,
    serverTimeUtc,
  } = value;
  const parsedSessionId = asString(sessionId);
  const parsedRoomCode = asString(roomCode);
  const parsedStatus = asString(status);
  const parsedSessionVersion = asNumber(sessionVersion);
  const parsedCreatedAtUtc = asString(createdAtUtc);
  const parsedHostParticipantId = asString(hostParticipantId);
  const parsedServerTimeUtc = asString(serverTimeUtc);

  if (
    !parsedSessionId ||
    !parsedRoomCode ||
    !parsedStatus ||
    parsedSessionVersion === null ||
    !parsedCreatedAtUtc ||
    !parsedHostParticipantId ||
    !isRecord(template) ||
    !Array.isArray(participants) ||
    !Array.isArray(accounts) ||
    !parsedServerTimeUtc
  ) {
    return null;
  }

  const parsedTemplate = {
    snapshotId: asString(template.snapshotId),
    templateId: asString(template.templateId),
    editionId: asString(template.editionId),
    templateVersion: asString(template.templateVersion),
    schemaVersion: asNumber(template.schemaVersion),
    contentHash: asString(template.contentHash),
  };

  if (
    !parsedTemplate.snapshotId ||
    !parsedTemplate.templateId ||
    !parsedTemplate.editionId ||
    !parsedTemplate.templateVersion ||
    parsedTemplate.schemaVersion === null ||
    !parsedTemplate.contentHash
  ) {
    return null;
  }

  const parsedParticipants = participants
    .map((participant) => {
      if (!isRecord(participant)) {
        return null;
      }

      const participantId = asString(participant.participantId);
      const displayName = asString(participant.displayName);
      const role = asString(participant.role);
      const identityKey = asString(participant.identityKey);
      const joinOrder = asNumber(participant.joinOrder);
      if (
        !participantId ||
        !displayName ||
        !role ||
        !identityKey ||
        joinOrder === null
      ) {
        return null;
      }

      return { participantId, displayName, role, identityKey, joinOrder };
    })
    .filter((participant): participant is NonNullable<typeof participant> => participant !== null);

  if (parsedParticipants.length !== participants.length) {
    return null;
  }

  const parsedAccounts = accounts
    .map((account) => {
      if (!isRecord(account)) {
        return null;
      }

      const accountId = asString(account.accountId);
      const ownerId = asString(account.ownerId);
      const ownerType = asString(account.ownerType);
      const balance = asNumber(account.balance);
      if (!accountId || !ownerId || !ownerType || balance === null) {
        return null;
      }

      return { accountId, ownerId, ownerType, balance };
    })
    .filter((account): account is NonNullable<typeof account> => account !== null);

  if (parsedAccounts.length !== accounts.length) {
    return null;
  }

  return {
    sessionId: parsedSessionId,
    roomCode: parsedRoomCode,
    status: parsedStatus,
    sessionVersion: parsedSessionVersion,
    createdAtUtc: parsedCreatedAtUtc,
    hostParticipantId: parsedHostParticipantId,
    template: {
      snapshotId: parsedTemplate.snapshotId,
      templateId: parsedTemplate.templateId,
      editionId: parsedTemplate.editionId,
      templateVersion: parsedTemplate.templateVersion,
      schemaVersion: parsedTemplate.schemaVersion,
      contentHash: parsedTemplate.contentHash,
    },
    participants: parsedParticipants,
    accounts: parsedAccounts,
    serverTimeUtc: parsedServerTimeUtc,
  };
};

const parseCreateSessionResponse = (value: unknown): CreateSessionResponse | null => {
  if (!isRecord(value)) {
    return null;
  }

  const sessionId = asString(value.sessionId);
  const roomCode = asString(value.roomCode);
  const hostParticipantId = asString(value.hostParticipantId);
  const reconnectCredential = asString(value.reconnectCredential);
  const initialSnapshot = parseSessionSnapshot(value.initialSnapshot);
  const connection = isRecord(value.connection)
    ? {
        hubPath: asString(value.connection.hubPath),
        protocolVersion: asNumber(value.connection.protocolVersion),
      }
    : null;

  if (
    !sessionId ||
    !roomCode ||
    !hostParticipantId ||
    !reconnectCredential ||
    !initialSnapshot ||
    !connection ||
    !connection.hubPath ||
    connection.protocolVersion === null
  ) {
    return null;
  }

  return {
    sessionId,
    roomCode,
    hostParticipantId,
    reconnectCredential,
    initialSnapshot,
    connection: {
      hubPath: connection.hubPath,
      protocolVersion: connection.protocolVersion,
    },
  };
};

const parseJoinSessionResponse = (value: unknown): JoinSessionResponse | null => {
  if (!isRecord(value)) {
    return null;
  }

  const sessionId = asString(value.sessionId);
  const participantId = asString(value.participantId);
  const reconnectCredential = asString(value.reconnectCredential);
  const snapshot = parseSessionSnapshot(value.snapshot);
  const connection = isRecord(value.connection)
    ? {
        hubPath: asString(value.connection.hubPath),
        protocolVersion: asNumber(value.connection.protocolVersion),
      }
    : null;

  if (
    !sessionId ||
    !participantId ||
    !reconnectCredential ||
    !snapshot ||
    !connection ||
    !connection.hubPath ||
    connection.protocolVersion === null
  ) {
    return null;
  }

  return {
    sessionId,
    participantId,
    reconnectCredential,
    snapshot,
    connection: {
      hubPath: connection.hubPath,
      protocolVersion: connection.protocolVersion,
    },
  };
};

const throwApiError = async (response: Response): Promise<never> => {
  const message = await response.text();
  throw new Error(`api-request-failed:${response.status}:${message}`);
};

export const createSession = async (
  request: CreateSessionRequest,
): Promise<CreateSessionResponse> => {
  const response = await fetch("/api/v1/sessions", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    return throwApiError(response);
  }

  const json = (await response.json()) as unknown;
  const parsed = parseCreateSessionResponse(json);
  if (!parsed) {
    throw new Error("create-session-invalid-response");
  }

  return parsed;
};

export const joinSession = async (
  request: JoinSessionRequest,
): Promise<JoinSessionResponse> => {
  const response = await fetch("/api/v1/sessions/join", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    return throwApiError(response);
  }

  const json = (await response.json()) as unknown;
  const parsed = parseJoinSessionResponse(json);
  if (!parsed) {
    throw new Error("join-session-invalid-response");
  }

  return parsed;
};

export const getSessionSnapshot = async (input: {
  sessionId: string;
  participantId: string;
  reconnectCredential: string;
}): Promise<SessionSnapshot> => {
  const response = await fetch(`/api/v1/sessions/${input.sessionId}/snapshot`, {
    method: "GET",
    headers: {
      "x-participant-id": input.participantId,
      "x-reconnect-credential": input.reconnectCredential,
    },
  });

  if (!response.ok) {
    return throwApiError(response);
  }

  const json = (await response.json()) as unknown;
  const parsed = parseSessionSnapshot(json);
  if (!parsed) {
    throw new Error("session-snapshot-invalid-response");
  }

  return parsed;
};
