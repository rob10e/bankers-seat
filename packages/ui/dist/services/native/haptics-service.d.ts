import { type VibrateOptions } from "@capacitor/haptics";
export type VibrationPattern = "success" | "error" | "warning" | "light";
export interface IHapticsService {
    vibrate(pattern: VibrationPattern): Promise<void>;
    isAvailable(): boolean;
}
export interface HapticsPluginLike {
    vibrate: (options: VibrateOptions) => Promise<void>;
}
interface HapticsServiceDependencies {
    readonly haptics: HapticsPluginLike;
    readonly isNativePlatform: () => boolean;
}
export declare class HapticsService implements IHapticsService {
    private readonly dependencies;
    constructor(dependencies?: Partial<HapticsServiceDependencies>);
    isAvailable(): boolean;
    vibrate(pattern: VibrationPattern): Promise<void>;
}
export declare const hapticsService: HapticsService;
export {};
//# sourceMappingURL=haptics-service.d.ts.map