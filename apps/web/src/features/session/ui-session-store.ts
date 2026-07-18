import { create } from "zustand";

interface UiSessionState {
  readonly selectedTemplateKey: string | null;
  readonly hostDisplayName: string;
  readonly joinDisplayName: string;
  readonly roomCodeDraft: string;
  readonly activeSessionId: string | null;
  readonly activeParticipantId: string | null;
  readonly activeReconnectCredential: string | null;
  readonly activeRoomCode: string | null;
  setSelectedTemplateKey: (value: string | null) => void;
  setHostDisplayName: (value: string) => void;
  setJoinDisplayName: (value: string) => void;
  setRoomCodeDraft: (value: string) => void;
  setActiveSession: (value: {
    sessionId: string;
    participantId: string;
    reconnectCredential: string;
    roomCode: string;
  }) => void;
  clearActiveSession: () => void;
}

export const useUiSessionStore = create<UiSessionState>((set) => ({
  selectedTemplateKey: null,
  hostDisplayName: "Host",
  joinDisplayName: "Player",
  roomCodeDraft: "",
  activeSessionId: null,
  activeParticipantId: null,
  activeReconnectCredential: null,
  activeRoomCode: null,
  setSelectedTemplateKey: (value) => set({ selectedTemplateKey: value }),
  setHostDisplayName: (value) => set({ hostDisplayName: value }),
  setJoinDisplayName: (value) => set({ joinDisplayName: value }),
  setRoomCodeDraft: (value) => set({ roomCodeDraft: value }),
  setActiveSession: (value) =>
    set({
      activeSessionId: value.sessionId,
      activeParticipantId: value.participantId,
      activeReconnectCredential: value.reconnectCredential,
      activeRoomCode: value.roomCode,
    }),
  clearActiveSession: () =>
    set({
      activeSessionId: null,
      activeParticipantId: null,
      activeReconnectCredential: null,
      activeRoomCode: null,
    }),
}));
