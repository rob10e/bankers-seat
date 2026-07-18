export interface ParticipantView {
  readonly participantId: string;
  readonly displayName: string;
  readonly role: string;
  readonly identityKey: string;
  readonly joinOrder: number;
}

export interface AccountView {
  readonly accountId: string;
  readonly ownerId: string;
  readonly ownerType: string;
  readonly balance: number;
}

export interface TemplateSnapshotView {
  readonly snapshotId: string;
  readonly templateId: string;
  readonly editionId: string;
  readonly templateVersion: string;
  readonly schemaVersion: number;
  readonly contentHash: string;
}

export interface SessionSnapshot {
  readonly sessionId: string;
  readonly roomCode: string;
  readonly status: string;
  readonly sessionVersion: number;
  readonly createdAtUtc: string;
  readonly hostParticipantId: string;
  readonly template: TemplateSnapshotView;
  readonly participants: readonly ParticipantView[];
  readonly accounts: readonly AccountView[];
  readonly serverTimeUtc: string;
}

export interface SessionConnectionInfo {
  readonly hubPath: string;
  readonly protocolVersion: number;
}

export interface CreateSessionRequest {
  readonly templateId: string;
  readonly editionId: string;
  readonly templateVersion: string;
  readonly hostDisplayName: string;
  readonly sessionOptions: Record<string, unknown>;
}

export interface JoinSessionRequest {
  readonly roomCode: string;
  readonly displayName: string;
  readonly identityKey: string;
}

export interface CreateSessionResponse {
  readonly sessionId: string;
  readonly roomCode: string;
  readonly hostParticipantId: string;
  readonly reconnectCredential: string;
  readonly initialSnapshot: SessionSnapshot;
  readonly connection: SessionConnectionInfo;
}

export interface JoinSessionResponse {
  readonly sessionId: string;
  readonly participantId: string;
  readonly reconnectCredential: string;
  readonly snapshot: SessionSnapshot;
  readonly connection: SessionConnectionInfo;
}
