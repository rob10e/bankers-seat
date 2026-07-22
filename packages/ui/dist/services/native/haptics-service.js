import { Haptics } from "@capacitor/haptics";
import { isNativePlatform, toError } from "./native-platform.js";
const vibrationDurations = {
    success: [50],
    error: [20, 20, 20],
    warning: [100],
    light: [10],
};
export class HapticsService {
    dependencies;
    constructor(dependencies) {
        this.dependencies = {
            haptics: dependencies?.haptics ?? Haptics,
            isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
        };
    }
    isAvailable() {
        return this.dependencies.isNativePlatform();
    }
    async vibrate(pattern) {
        if (!this.isAvailable()) {
            return;
        }
        try {
            for (const duration of vibrationDurations[pattern]) {
                await this.dependencies.haptics.vibrate({ duration });
            }
        }
        catch (error) {
            throw toError(error, `Failed to trigger ${pattern} haptics feedback.`);
        }
    }
}
export const hapticsService = new HapticsService();
