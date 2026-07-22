import { Alert, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import { QRCodeCanvas } from "qrcode.react";
import { useMemo, useRef, useState } from "react";
import { useHaptics, useShare } from "../../hooks/index.ts";

interface QRDisplayProps {
  roomCode: string;
  title?: string;
}

export function QRDisplay({ roomCode, title = "Room QR Code" }: QRDisplayProps) {
  const qrRef = useRef<HTMLDivElement>(null);
  const { share, copy, isSharing, error } = useShare();
  const { vibrate } = useHaptics();
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const joinUrl = useMemo(() => {
    const baseOrigin =
      typeof window !== "undefined" ? window.location.origin : "https://example.com";
    return new URL(`/join/${roomCode}`, baseOrigin).toString();
  }, [roomCode]);

  return (
    <Card>
      <CardContent>
        <Stack spacing={2} sx={{ alignItems: "center" }}>
          <Typography variant="h6">{title}</Typography>
          {statusMessage ? <Alert severity="success">{statusMessage}</Alert> : null}
          {error ? <Alert severity="warning">{error.message}</Alert> : null}
          <div ref={qrRef} style={{ display: "flex", justifyContent: "center" }}>
            <QRCodeCanvas
              value={joinUrl}
              size={256}
              level="H"
              includeMargin={true}
            />
          </div>
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: "center" }}>
            Room Code: <strong>{roomCode}</strong>
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ textAlign: "center" }}>
            Scan this QR code to open the join link on another device, or share the invite link.
          </Typography>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
            <Button
              variant="contained"
              disabled={isSharing}
              onClick={() => {
                void share(
                  "Join Banker's Seat",
                  `Join room ${roomCode}`,
                  joinUrl,
                )
                  .then(async () => {
                    setStatusMessage("Invite link shared.");
                    await vibrate("success");
                  })
                  .catch(async () => {
                    await copy(joinUrl);
                    setStatusMessage("Invite link copied to clipboard.");
                    await vibrate("warning");
                  });
              }}
            >
              {isSharing ? "Sharing..." : "Share invite"}
            </Button>
            <Button
              variant="outlined"
              onClick={() => {
                void copy(joinUrl).then(async () => {
                  setStatusMessage("Invite link copied to clipboard.");
                  await vibrate("light");
                });
              }}
            >
              Copy invite link
            </Button>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}
