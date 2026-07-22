import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { deepLinkService } from "@bankers-seat/ui";
import { useNavigate, useParams } from "react-router-dom";
import { useUiSessionStore } from "../session/ui-session-store.ts";
import { useJoinSessionMutation } from "../session/use-session-mutations.ts";
import { QRScanner } from "./qr-scanner.tsx";

const normalizeRoomCode = (value: string): string => {
  return value.replace(/[^a-z0-9]/gi, "").toUpperCase().slice(0, 8);
};

export function JoinRoomView() {
  const { roomCode: routeRoomCode } = useParams<{ roomCode?: string }>();
  const navigate = useNavigate();
  const roomCodeDraft = useUiSessionStore((state) => state.roomCodeDraft);
  const setRoomCodeDraft = useUiSessionStore((state) => state.setRoomCodeDraft);
  const joinDisplayName = useUiSessionStore((state) => state.joinDisplayName);
  const setJoinDisplayName = useUiSessionStore((state) => state.setJoinDisplayName);
  const setActiveSession = useUiSessionStore((state) => state.setActiveSession);
  const joinSessionMutation = useJoinSessionMutation();
  const [tabIndex, setTabIndex] = useState(0);

  useEffect(() => {
    if (routeRoomCode) {
      setRoomCodeDraft(normalizeRoomCode(routeRoomCode));
    }
  }, [routeRoomCode, setRoomCodeDraft]);

  const handleScanComplete = (scannedCode: string) => {
    const normalized =
      deepLinkService.parseJoinLink(scannedCode)?.roomCode ??
      normalizeRoomCode(scannedCode);
    setRoomCodeDraft(normalized);
    setTabIndex(0);
  };

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
            <Box sx={{ borderBottom: 1, borderColor: "divider" }}>
              <Tabs
                value={tabIndex}
                onChange={(_, value) => setTabIndex(value)}
                aria-label="join method"
              >
                <Tab label="Manual Entry" />
                <Tab label="Scan QR Code" />
              </Tabs>
            </Box>

            {tabIndex === 0 && (
              <Stack spacing={2}>
                <Alert severity="info">
                  Join and reconnect will bind to `/api/v1/sessions/join` and `/reconnect`.
                </Alert>
                <TextField
                  label="Room code"
                  value={roomCodeDraft}
                  onChange={(event) =>
                    setRoomCodeDraft(normalizeRoomCode(event.target.value))
                  }
                  placeholder="ABCD12"
                  fullWidth
                />
              </Stack>
            )}

            {tabIndex === 1 && (
              <QRScanner
                onScanned={handleScanComplete}
                onClose={() => setTabIndex(0)}
              />
            )}

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
