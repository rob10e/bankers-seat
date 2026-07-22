import { useCallback, useMemo, useRef, useState } from "react";
import {
  createQrScannerService,
  type IQrScannerService,
  qrScannerService,
} from "@bankers-seat/ui";
import { QRScanner } from "../features/join/qr-scanner.tsx";

interface UseQrScannerOptions {
  readonly service?: IQrScannerService;
  readonly title?: string;
  readonly onScanned?: (value: string) => void;
  readonly onClose?: () => void;
}

interface UseQrScannerResult {
  readonly isAvailable: boolean;
  readonly isScanning: boolean;
  readonly lastScannedValue: string | null;
  readonly error: Error | null;
  readonly overlay: React.ReactNode;
  startScanning: () => Promise<string>;
  stopScanning: () => Promise<void>;
}

export function useQrScanner(options: UseQrScannerOptions = {}): UseQrScannerResult {
  const [isScanning, setIsScanning] = useState(false);
  const [lastScannedValue, setLastScannedValue] = useState<string | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const resolveRef = useRef<((value: string) => void) | null>(null);
  const rejectRef = useRef<((reason?: unknown) => void) | null>(null);

  const cleanupPendingScan = useCallback(() => {
    resolveRef.current = null;
    rejectRef.current = null;
    setIsScanning(false);
  }, []);

  const stopWebScan = useCallback(async () => {
    rejectRef.current?.(new Error("QR scan cancelled."));
    cleanupPendingScan();
    options.onClose?.();
  }, [cleanupPendingScan, options]);

  const startWebScan = useCallback(() => {
    setError(null);
    setIsScanning(true);
    return new Promise<string>((resolve, reject) => {
      resolveRef.current = resolve;
      rejectRef.current = reject;
    });
  }, []);

  const service = useMemo(() => {
    if (options.service) {
      return options.service;
    }

    return createQrScannerService({
      isNativePlatform: () => false,
      startWebScan,
      stopWebScan,
    });
  }, [options.service, startWebScan, stopWebScan]);

  const startScanning = useCallback(async () => {
    setError(null);
    try {
      const scannedValue = await service.startScanning();
      setLastScannedValue(scannedValue);
      options.onScanned?.(scannedValue);
      return scannedValue;
    } catch (unknownError) {
      const nextError =
        unknownError instanceof Error
          ? unknownError
          : new Error("Failed to scan QR code.");
      setError(nextError);
      throw nextError;
    }
  }, [options, service]);

  const stopScanning = useCallback(async () => {
    await service.stopScanning();
    cleanupPendingScan();
  }, [cleanupPendingScan, service]);

  const overlay = useMemo(() => {
    if (!isScanning || options.service || service === qrScannerService) {
      return null;
    }

    return (
      <QRScanner
        title={options.title}
        onScanned={(value) => {
          resolveRef.current?.(value);
          cleanupPendingScan();
        }}
        onClose={() => {
          void stopScanning();
        }}
      />
    );
  }, [cleanupPendingScan, isScanning, options.service, options.title, service, stopScanning]);

  return useMemo(
    () => ({
      isAvailable: service.isAvailable(),
      isScanning,
      lastScannedValue,
      error,
      overlay,
      startScanning,
      stopScanning,
    }),
    [error, isScanning, lastScannedValue, overlay, service, startScanning, stopScanning],
  );
}
