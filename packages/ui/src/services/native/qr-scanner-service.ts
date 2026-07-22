import {
  CapacitorBarcodeScanner,
  type CapacitorBarcodeScannerOptions,
  type CapacitorBarcodeScannerScanResult,
} from "@capacitor/barcode-scanner";
import { isNativePlatform, toError } from "./native-platform.js";

export interface IQrScannerService {
  startScanning(): Promise<string>;
  stopScanning(): Promise<void>;
  isAvailable(): boolean;
}

export interface BarcodeScannerPluginLike {
  scanBarcode: (
    options: CapacitorBarcodeScannerOptions,
  ) => Promise<CapacitorBarcodeScannerScanResult>;
}

interface QrScannerServiceDependencies {
  readonly barcodeScanner: BarcodeScannerPluginLike;
  readonly isNativePlatform: () => boolean;
  readonly startWebScan?: () => Promise<string>;
  readonly stopWebScan?: () => Promise<void>;
}

export function createQrScannerService(
  dependencies?: Partial<QrScannerServiceDependencies>,
): IQrScannerService {
  return new QrScannerService(dependencies);
}

export class QrScannerService implements IQrScannerService {
  private readonly dependencies: QrScannerServiceDependencies;

  public constructor(dependencies?: Partial<QrScannerServiceDependencies>) {
    this.dependencies = {
      barcodeScanner: dependencies?.barcodeScanner ?? CapacitorBarcodeScanner,
      isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
      startWebScan: dependencies?.startWebScan,
      stopWebScan: dependencies?.stopWebScan,
    };
  }

  public isAvailable(): boolean {
    return this.dependencies.isNativePlatform() || Boolean(this.dependencies.startWebScan);
  }

  public async startScanning(): Promise<string> {
    try {
      if (this.dependencies.isNativePlatform()) {
        const result = await this.dependencies.barcodeScanner.scanBarcode({
          hint: "ALL" as never,
          scanInstructions: "Scan the room QR code",
          scanText: "Scan",
          web: {
            showCameraSelection: true,
            scannerFPS: 10,
          },
        });

        if (result.ScanResult.trim().length === 0) {
          throw new Error("No QR code value was detected.");
        }

        return result.ScanResult;
      }

      if (!this.dependencies.startWebScan) {
        throw new Error("QR scanning is unavailable on this platform.");
      }

      return await this.dependencies.startWebScan();
    } catch (error) {
      throw toError(error, "Failed to scan QR code.");
    }
  }

  public async stopScanning(): Promise<void> {
    try {
      await this.dependencies.stopWebScan?.();
    } catch (error) {
      throw toError(error, "Failed to stop QR scanning.");
    }
  }
}

export const qrScannerService = new QrScannerService();
