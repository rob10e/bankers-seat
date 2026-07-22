import { Clipboard } from "@capacitor/clipboard";
import { Share } from "@capacitor/share";
import { isNativePlatform, toError } from "./native-platform.js";
function buildCopyPayload(title, text, url) {
    return [title, text, url].filter(Boolean).join("\n");
}
export class ShareService {
    dependencies;
    constructor(dependencies) {
        this.dependencies = {
            sharePlugin: dependencies?.sharePlugin ?? Share,
            clipboardPlugin: dependencies?.clipboardPlugin ?? Clipboard,
            navigator: dependencies?.navigator ?? (typeof window !== "undefined" ? window.navigator : null),
            isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
        };
    }
    canShare() {
        if (this.dependencies.isNativePlatform()) {
            return true;
        }
        return Boolean(this.dependencies.navigator?.share || this.dependencies.navigator?.clipboard);
    }
    async share(title, text, url, files) {
        try {
            if (this.dependencies.isNativePlatform()) {
                await this.dependencies.sharePlugin.share({
                    title,
                    text,
                    url,
                    files: files ? [...files] : undefined,
                });
                return;
            }
            if (this.dependencies.navigator?.share) {
                await this.dependencies.navigator.share({ title, text, url });
                return;
            }
            await this.copy(buildCopyPayload(title, text, url));
        }
        catch (error) {
            throw toError(error, "Failed to share content.");
        }
    }
    async copy(text) {
        try {
            if (this.dependencies.isNativePlatform()) {
                await this.dependencies.clipboardPlugin.write({ string: text });
                return;
            }
            if (!this.dependencies.navigator?.clipboard) {
                throw new Error("Clipboard access is unavailable.");
            }
            await this.dependencies.navigator.clipboard.writeText(text);
        }
        catch (error) {
            throw toError(error, "Failed to copy content.");
        }
    }
}
export const shareService = new ShareService();
