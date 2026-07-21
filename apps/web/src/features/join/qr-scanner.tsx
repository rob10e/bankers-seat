import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useRef, useState } from "react";
import jsQR from "jsqr";

interface QRScannerProps {
  onScanned: (roomCode: string) => void;
  onClose: () => void;
  title?: string;
}

export function QRScanner({
  onScanned,
  onClose,
  title = "Scan Room QR Code",
}: QRScannerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [permissionGranted, setPermissionGranted] = useState(false);
  const animationFrameRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    const startCamera = async () => {
      try {
        setError(null);

        const constraints: MediaStreamConstraints = {
          video: { facingMode: "environment" },
          audio: false,
        };

        const stream = await navigator.mediaDevices.getUserMedia(constraints);

        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          setPermissionGranted(true);
        }
      } catch (err) {
        const errorMsg =
          err instanceof Error ? err.message : "Failed to access camera";
        setError(errorMsg);
      }
    };

    void startCamera();

    return () => {
      // oxlint-disable-next-line react-hooks/exhaustive-deps
      const videoElement = videoRef.current;
      if (videoElement?.srcObject) {
        const stream = videoElement.srcObject as MediaStream;
        stream.getTracks().forEach((track) => track.stop());
      }
    };
  }, []);

  useEffect(() => {
    if (!permissionGranted || !videoRef.current || !canvasRef.current) {
      return;
    }

    const video = videoRef.current;
    const canvas = canvasRef.current;
    const ctx = canvas.getContext("2d");

    if (!ctx) {
      return;
    }

    const detectQR = () => {
      if (
        video.readyState === video.HAVE_ENOUGH_DATA &&
        video.videoWidth > 0 &&
        video.videoHeight > 0
      ) {
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;

        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        const imageData = ctx.getImageData(
          0,
          0,
          canvas.width,
          canvas.height
        );

        const qrCode = jsQR(imageData.data, canvas.width, canvas.height);

        if (qrCode) {
          const scannedValue = qrCode.data;
          onScanned(scannedValue);
          return;
        }
      }

      animationFrameRef.current = requestAnimationFrame(detectQR);
    };

    animationFrameRef.current = requestAnimationFrame(detectQR);

    return () => {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, [permissionGranted, onScanned]);

  return (
    <Card>
      <CardContent>
        <Stack spacing={2}>
          <Typography variant="h6">{title}</Typography>

          {error ? (
            <Alert severity="error">
              Camera error: {error}. Please allow camera permissions or enter the room code manually.
            </Alert>
          ) : null}

          {!permissionGranted && !error ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
              <CircularProgress />
            </Box>
          ) : null}

          {permissionGranted ? (
            <Box
              sx={{
                position: "relative",
                width: "100%",
                paddingBottom: "100%",
                backgroundColor: "#000",
                borderRadius: 1,
                overflow: "hidden",
              }}
            >
              <video
                ref={videoRef}
                autoPlay
                playsInline
                muted
                style={{
                  position: "absolute",
                  top: 0,
                  left: 0,
                  width: "100%",
                  height: "100%",
                }}
              />
            </Box>
          ) : null}

          <canvas
            ref={canvasRef}
            style={{ display: "none" }}
          />

          <Typography variant="body2" color="text.secondary" sx={{ textAlign: "center" }}>
            {permissionGranted
              ? "Point your camera at a QR code to scan it"
              : "Requesting camera access..."}
          </Typography>

          <Button variant="outlined" onClick={onClose}>
            Close scanner
          </Button>
        </Stack>
      </CardContent>
    </Card>
  );
}
