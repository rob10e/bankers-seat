import { describe, expect, it, vi } from "vitest";
import { createQrScannerService } from "./qr-scanner-service.ts";

describe("QrScannerService", () => {
  it("returns native scan results", async () => {
    const service = createQrScannerService({
      isNativePlatform: () => true,
      barcodeScanner: {
        scanBarcode: vi.fn().mockResolvedValue({ ScanResult: "ABCD12" }),
      },
    });

    await expect(service.startScanning()).resolves.toBe("ABCD12");
  });

  it("rejects empty native scan results", async () => {
    const service = createQrScannerService({
      isNativePlatform: () => true,
      barcodeScanner: {
        scanBarcode: vi.fn().mockResolvedValue({ ScanResult: "" }),
      },
    });

    await expect(service.startScanning()).rejects.toThrow("No QR code value was detected.");
  });

  it("uses the injected web launcher when running on the web", async () => {
    const startWebScan = vi.fn().mockResolvedValue("ROOM42");
    const service = createQrScannerService({
      isNativePlatform: () => false,
      startWebScan,
    });

    await expect(service.startScanning()).resolves.toBe("ROOM42");
  });

  it("stops web scanning via the injected callback", async () => {
    const stopWebScan = vi.fn().mockResolvedValue(undefined);
    const service = createQrScannerService({
      isNativePlatform: () => false,
      startWebScan: vi.fn().mockResolvedValue("ROOM42"),
      stopWebScan,
    });

    await service.stopScanning();

    expect(stopWebScan).toHaveBeenCalledTimes(1);
  });

  it("reports unavailable scanning on the web without a launcher", () => {
    const service = createQrScannerService({
      isNativePlatform: () => false,
    });

    expect(service.isAvailable()).toBe(false);
  });
});
