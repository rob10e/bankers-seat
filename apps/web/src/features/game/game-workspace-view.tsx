import {
  Alert,
  Card,
  CardContent,
  Chip,
  Grid,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useParams } from "react-router-dom";
import { useUiSessionStore } from "../session/ui-session-store.ts";
import { useSessionSnapshotQuery } from "../session/use-session-snapshot-query.ts";
import { formatMoney } from "../../shared/format-money.ts";
import { QRDisplay } from "../join/qr-display.tsx";

export function GameWorkspaceView() {
  const { sessionId = "demo" } = useParams();
  const activeParticipantId = useUiSessionStore((state) => state.activeParticipantId);
  const activeReconnectCredential = useUiSessionStore(
    (state) => state.activeReconnectCredential,
  );
  const activeSessionId = useUiSessionStore((state) => state.activeSessionId);
  const snapshotQuery = useSessionSnapshotQuery({
    sessionId: activeSessionId ?? sessionId,
    participantId: activeParticipantId,
    reconnectCredential: activeReconnectCredential,
  });

  if (activeParticipantId === null || activeReconnectCredential === null) {
    return (
      <Alert severity="warning">
        No active session credentials in this browser state. Start or join a room first.
      </Alert>
    );
  }

  const isHost = snapshotQuery.data?.hostParticipantId === activeParticipantId;
  const roomCode = snapshotQuery.data?.roomCode;

  return (
    <Stack spacing={2.5} className="app-page">
      <Stack spacing={0.5}>
        <Typography variant="h4">Game workspace</Typography>
        <Typography color="text.secondary">
          Session: <code>{activeSessionId ?? sessionId}</code>
        </Typography>
      </Stack>
      {snapshotQuery.isFetching ? <LinearProgress /> : null}
      {snapshotQuery.isError ? (
        <Alert severity="error">
          Could not refresh session snapshot. Check reconnect credentials and server availability.
        </Alert>
      ) : null}

      {isHost && roomCode && (
        <QRDisplay roomCode={roomCode} title="Share this QR code to invite players" />
      )}

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Stack spacing={1.5}>
                <Typography variant="h5">Players</Typography>
                {(snapshotQuery.data?.participants ?? []).map((player) => {
                  const account = snapshotQuery.data?.accounts.find(
                    (candidate) => candidate.ownerId === player.participantId,
                  );
                  return (
                  <Stack
                    key={player.participantId}
                    direction="row"
                    sx={{ justifyContent: "space-between", alignItems: "center" }}
                  >
                    <Stack spacing={0.2}>
                      <Typography>{player.displayName}</Typography>
                      <Typography variant="caption" color="text.secondary">
                        Balance: {formatMoney(account?.balance ?? 0)}
                      </Typography>
                    </Stack>
                    <Chip
                      label={player.role}
                      size="small"
                      color="success"
                      variant="outlined"
                    />
                  </Stack>
                  );
                })}
              </Stack>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Stack spacing={1.5}>
                <Typography variant="h5">Session snapshot</Typography>
                <Typography color="text.secondary">
                  Status: {snapshotQuery.data?.status ?? "unknown"}
                </Typography>
                <Typography color="text.secondary">
                  Version: {snapshotQuery.data?.sessionVersion ?? 0}
                </Typography>
                <Typography color="text.secondary">
                  Template: {snapshotQuery.data?.template.templateId ?? "-"} (
                  {snapshotQuery.data?.template.editionId ?? "-"})
                </Typography>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Stack>
  );
}
