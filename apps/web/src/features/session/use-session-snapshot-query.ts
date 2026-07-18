import { useQuery } from "@tanstack/react-query";
import { useAppSettingsStore } from "../../app/app-settings-store.ts";
import { getSessionSnapshot } from "./session-api-service.ts";

export const useSessionSnapshotQuery = (input: {
  sessionId: string | null;
  participantId: string | null;
  reconnectCredential: string | null;
}) => {
  const snapshotRefreshMs = useAppSettingsStore((state) => state.snapshotRefreshMs);

  return useQuery({
    queryKey: ["session-snapshot", input.sessionId, input.participantId],
    queryFn: () =>
      getSessionSnapshot({
        sessionId: input.sessionId!,
        participantId: input.participantId!,
        reconnectCredential: input.reconnectCredential!,
      }),
    enabled:
      input.sessionId !== null &&
      input.participantId !== null &&
      input.reconnectCredential !== null,
    refetchInterval: snapshotRefreshMs,
  });
};
