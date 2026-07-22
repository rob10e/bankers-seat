import { type CapacitorBarcodeScannerOptions, type CapacitorBarcodeScannerScanResult } from "@capacitor/barcode-scanner";
export interface IQrScannerService {
    startScanning(): Promise<string>;
    stopScanning(): Promise<void>;
    isAvailable(): boolean;
}
export interface BarcodeScannerPluginLike {
    scanBarcode: (options: CapacitorBarcodeScannerOptions) => Promise<CapacitorBarcodeScannerScanResult>;
}
interface QrScannerServiceDependencies {
    readonly barcodeScanner: BarcodeScannerPluginLike;
    readonly isNativePlatform: () => boolean;
    readonly startWebScan?: () => Promise<string>;
    readonly stopWebScan?: () => Promise<void>;
}
export declare function createQrScannerService(dependencies?: Partial<QrScannerServiceDependencies>): IQrScannerService;
export declare class QrScannerService implements IQrScannerService {
    private readonly dependencies;
    constructor(dependencies?: Partial<QrScannerServiceDependencies>);
    isAvailable(): boolean;
    startScanning(): Promise<string>;
    stopScanning(): Promise<void>;
}
export declare const qrScannerService: QrScannerService;
export {};
//# sourceMappingURL=qr-scanner-service.d.ts.map