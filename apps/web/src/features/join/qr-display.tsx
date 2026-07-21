import { Card, CardContent, Stack, Typography } from "@mui/material";
import { QRCodeCanvas } from "qrcode.react";
import { useRef } from "react";

interface QRDisplayProps {
  roomCode: string;
  title?: string;
}

export function QRDisplay({ roomCode, title = "Room QR Code" }: QRDisplayProps) {
  const qrRef = useRef<HTMLDivElement>(null);

  return (
    <Card>
      <CardContent>
        <Stack spacing={2} sx={{ alignItems: "center" }}>
          <Typography variant="h6">{title}</Typography>
          <div ref={qrRef} style={{ display: "flex", justifyContent: "center" }}>
            <QRCodeCanvas
              value={roomCode}
              size={256}
              level="H"
              includeMargin={true}
            />
          </div>
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: "center" }}>
            Room Code: <strong>{roomCode}</strong>
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ textAlign: "center" }}>
            Scan this QR code to join the room, or share the room code manually.
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}
