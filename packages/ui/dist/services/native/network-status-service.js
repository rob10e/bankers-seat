import { Network } from "@capacitor/network";
import { isNativePlatform, } from "./native-platform.js";
export class NetworkStatusService {
    callbacks = new Set();
    dependencies;
    online;
    nativeListenerStarted = false;
    webListenersStarted = false;
    constructor(dependencies) {
        this.dependencies = {
            network: dependencies?.network ?? Network,
            isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
            window: dependencies?.window ?? (typeof window !== "undefined" ? window : null),
            logger: dependencies?.logger ?? console,
        };
        this.online = this.dependencies.window?.navigator.onLine ?? true;
        if (this.dependencies.isNativePlatform()) {
            void this.dependencies.network
                .getStatus()
                .then((status) => {
                this.setOnline(status.connected);
            })
                .catch((error) => {
                this.dependencies.logger.error?.("Failed to read native network status.", error);
            });
        }
    }
    isOnline() {
        return this.online;
    }
    onStatusChange(callback) {
        this.callbacks.add(callback);
        if (this.dependencies.isNativePlatform()) {
            this.startNativeListeners();
        }
        else {
            this.startWebListeners();
        }
        try {
            callback(this.online);
        }
        catch (error) {
            this.dependencies.logger.error?.("Network status callback failed.", error);
        }
        return () => {
            this.callbacks.delete(callback);
        };
    }
    startNativeListeners() {
        if (this.nativeListenerStarted) {
            return;
        }
        this.nativeListenerStarted = true;
        void this.dependencies.network
            .addListener("networkStatusChange", (status) => {
            this.setOnline(status.connected);
        })
            .catch((error) => {
            this.dependencies.logger.error?.("Failed to subscribe to native network changes.", error);
        });
    }
    startWebListeners() {
        if (this.webListenersStarted || !this.dependencies.window) {
            return;
        }
        this.webListenersStarted = true;
        this.dependencies.window.addEventListener("online", () => {
            this.setOnline(true);
        });
        this.dependencies.window.addEventListener("offline", () => {
            this.setOnline(false);
        });
    }
    setOnline(isOnline) {
        this.online = isOnline;
        for (const callback of this.callbacks) {
            try {
                callback(isOnline);
            }
            catch (error) {
                this.dependencies.logger.error?.("Network status callback failed.", error);
            }
        }
    }
}
export const networkStatusService = new NetworkStatusService();
