import {
  Alert,
  Button,
  Card,
  CardContent,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useUiSessionStore } from "../session/ui-session-store.ts";
import { useJoinSessionMutation } from "../session/use-session-mutations.ts";

const normalizeRoomCode = (value: string): string => {
  return value.replace(/[^a-z0-9]/gi, "").toUpperCase().slice(0, 8);
};

export function JoinRoomView() {
  const navigate = useNavigate();
  const roomCodeDraft = useUiSessionStore((state) => state.roomCodeDraft);
  const setRoomCodeDraft = useUiSessionStore((state) => state.setRoomCodeDraft);
  const joinDisplayName = useUiSessionStore((state) => state.joinDisplayName);
  const setJoinDisplayName = useUiSessionStore((state) => state.setJoinDisplayName);
  const setActiveSession = useUiSessionStore((state) => state.setActiveSession);
  const joinSessionMutation = useJoinSessionMutation();

  return (
    <Stack spacing={2.5} className="app-page">
      <Typography variant="h4">Join room</Typography>
      {joinSessionMutation.isError ? (
        <Alert severity="error">
          Could not join session. Check the room code and ensure the backend is reachable.
        </Alert>
      ) : null}
      <Card>
        <CardContent>
          <Stack spacing={2}>
            <Alert severity="info">
              Join and reconnect will bind to `/api/v1/sessions/join` and `/reconnect`.
            </Alert>
            <TextField
              label="Room code"
              value={roomCodeDraft}
              onChange={(event) => setRoomCodeDraft(normalizeRoomCode(event.target.value))}
              placeholder="ABCD12"
              fullWidth
            />
            <TextField
            label="Display name"
            value={joinDisplayName}
            onChange={(event) => setJoinDisplayName(event.target.value)}
            placeholder="Player name"
            fullWidth
            />
            <Button
            variant="contained"
            disabled={
              joinSessionMutation.isPending ||
              roomCodeDraft.trim().length < 4 ||
              joinDisplayName.trim().length === 0
            }
            onClick={() => {
              void joinSessionMutation
                .mutateAsync({
                  roomCode: roomCodeDraft.trim(),
                  displayName: joinDisplayName.trim(),
                  identityKey: "player",
                })
                .then((response) => {
                  setActiveSession({
                    sessionId: response.sessionId,
                    participantId: response.participantId,
                    reconnectCredential: response.reconnectCredential,
                    roomCode: response.snapshot.roomCode,
                  });
                  navigate(`/game/${response.sessionId}`);
                });
            }}
            >
            {joinSessionMutation.isPending ? "Joining..." : "Join session"}
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  );
}
