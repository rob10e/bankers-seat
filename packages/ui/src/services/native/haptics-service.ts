import { Haptics, type VibrateOptions } from "@capacitor/haptics";
import { isNativePlatform, toError } from "./native-platform.js";

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

const vibrationDurations: Record<VibrationPattern, readonly number[]> = {
  success: [50],
  error: [20, 20, 20],
  warning: [100],
  light: [10],
};

export class HapticsService implements IHapticsService {
  private readonly dependencies: HapticsServiceDependencies;

  public constructor(dependencies?: Partial<HapticsServiceDependencies>) {
    this.dependencies = {
      haptics: dependencies?.haptics ?? Haptics,
      isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
    };
  }

  public isAvailable(): boolean {
    return this.dependencies.isNativePlatform();
  }

  public async vibrate(pattern: VibrationPattern): Promise<void> {
    if (!this.isAvailable()) {
      return;
    }

    try {
      for (const duration of vibrationDurations[pattern]) {
        await this.dependencies.haptics.vibrate({ duration });
      }
    } catch (error) {
      throw toError(error, `Failed to trigger ${pattern} haptics feedback.`);
    }
  }
}

export const hapticsService = new HapticsService();
