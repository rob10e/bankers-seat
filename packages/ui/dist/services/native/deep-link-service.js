import { App } from "@capacitor/app";
import { isNativePlatform, normalizeRoomCode, toError, } from "./native-platform.js";
export class DeepLinkService {
    callbacks = new Set();
    appListenerStarted = false;
    webListenerStarted = false;
    dependencies;
    constructor(dependencies) {
        this.dependencies = {
            app: dependencies?.app ?? App,
            isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
            window: dependencies?.window ?? (typeof window !== "undefined" ? window : null),
            logger: dependencies?.logger ?? console,
        };
    }
    parseJoinLink(url) {
        try {
            const baseUrl = this.dependencies.window?.location.origin ?? "https://bankers-seat.app";
            const parsedUrl = new URL(url, baseUrl);
            const roomCodeMatch = /^\/join\/([a-z0-9-]+)$/i.exec(parsedUrl.pathname);
            if (!roomCodeMatch) {
                return null;
            }
            const roomCode = normalizeRoomCode(roomCodeMatch[1]);
            return roomCode.length >= 4 ? { roomCode, url: parsedUrl.toString() } : null;
        }
        catch {
            return null;
        }
    }
    async handleDeepLink(url) {
        try {
            const parsedLink = this.parseJoinLink(url);
            if (!parsedLink) {
                return;
            }
            for (const callback of this.callbacks) {
                try {
                    callback(parsedLink);
                }
                catch (error) {
                    this.dependencies.logger.error?.("Deep link callback failed.", error);
                }
            }
        }
        catch (error) {
            throw toError(error, "Failed to handle deep link.");
        }
    }
    onDeepLinkReceived(callback) {
        this.callbacks.add(callback);
        if (this.dependencies.isNativePlatform()) {
            this.startNativeListeners();
            return;
        }
        this.startWebListeners();
    }
    startNativeListeners() {
        if (this.appListenerStarted) {
            return;
        }
        this.appListenerStarted = true;
        void this.dependencies.app
            .addListener("appUrlOpen", (data) => {
            void this.handleDeepLink(data.url);
        })
            .catch((error) => {
            this.dependencies.logger.error?.("Failed to subscribe to native deep links.", error);
        });
        void this.dependencies.app
            .getLaunchUrl()
            .then((launchUrl) => {
            if (launchUrl?.url) {
                return this.handleDeepLink(launchUrl.url);
            }
            return undefined;
        })
            .catch((error) => {
            this.dependencies.logger.error?.("Failed to process launch deep link.", error);
        });
    }
    startWebListeners() {
        if (this.webListenerStarted || !this.dependencies.window) {
            return;
        }
        this.webListenerStarted = true;
        void this.handleDeepLink(this.dependencies.window.location.href);
        this.dependencies.window.addEventListener("popstate", () => {
            void this.handleDeepLink(this.dependencies.window?.location.href ?? "");
        });
    }
}
export const deepLinkService = new DeepLinkService();
