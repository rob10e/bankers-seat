import { CapacitorBarcodeScanner, } from "@capacitor/barcode-scanner";
import { isNativePlatform, toError } from "./native-platform.js";
export function createQrScannerService(dependencies) {
    return new QrScannerService(dependencies);
}
export class QrScannerService {
    dependencies;
    constructor(dependencies) {
        this.dependencies = {
            barcodeScanner: dependencies?.barcodeScanner ?? CapacitorBarcodeScanner,
            isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
            startWebScan: dependencies?.startWebScan,
            stopWebScan: dependencies?.stopWebScan,
        };
    }
    isAvailable() {
        return this.dependencies.isNativePlatform() || Boolean(this.dependencies.startWebScan);
    }
    async startScanning() {
        try {
            if (this.dependencies.isNativePlatform()) {
                const result = await this.dependencies.barcodeScanner.scanBarcode({
                    hint: "ALL",
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
        }
        catch (error) {
            throw toError(error, "Failed to scan QR code.");
        }
    }
    async stopScanning() {
        try {
            await this.dependencies.stopWebScan?.();
        }
        catch (error) {
            throw toError(error, "Failed to stop QR scanning.");
        }
    }
}
export const qrScannerService = new QrScannerService();
