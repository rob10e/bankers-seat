import { describe, expect, it, vi } from "vitest";
import { ShareService } from "./share-service.ts";
describe("ShareService", () => {
    it("delegates native shares to the Capacitor Share plugin", async () => {
        const share = vi.fn().mockResolvedValue(undefined);
        const service = new ShareService({
            isNativePlatform: () => true,
            sharePlugin: { share },
            clipboardPlugin: { write: vi.fn().mockResolvedValue(undefined) },
        });
        await service.share("Invite", "Join", "https://example.com/join/ABCD12", ["file://a"]);
        expect(share).toHaveBeenCalledWith({
            title: "Invite",
            text: "Join",
            url: "https://example.com/join/ABCD12",
            files: ["file://a"],
        });
    });
    it("reports native sharing support", () => {
        const service = new ShareService({
            isNativePlatform: () => true,
            sharePlugin: { share: vi.fn().mockResolvedValue(undefined) },
            clipboardPlugin: { write: vi.fn().mockResolvedValue(undefined) },
        });
        expect(service.canShare()).toBe(true);
    });
    it("uses the Web Share API when available", async () => {
        const share = vi.fn().mockResolvedValue(undefined);
        const service = new ShareService({
            isNativePlatform: () => false,
            navigator: { share },
        });
        await service.share("Invite", "Join", "https://example.com/join/ABCD12");
        expect(share).toHaveBeenCalledWith({
            title: "Invite",
            text: "Join",
            url: "https://example.com/join/ABCD12",
        });
    });
    it("returns false when neither sharing nor clipboard is available", () => {
        const service = new ShareService({
            isNativePlatform: () => false,
            navigator: {},
        });
        expect(service.canShare()).toBe(false);
    });
    it("falls back to copying content when web sharing is unavailable", async () => {
        const writeText = vi.fn().mockResolvedValue(undefined);
        const service = new ShareService({
            isNativePlatform: () => false,
            navigator: { clipboard: { writeText } },
        });
        await service.share("Invite", "Join", "https://example.com/join/ABCD12");
        expect(writeText).toHaveBeenCalledWith("Invite\nJoin\nhttps://example.com/join/ABCD12");
    });
    it("copies text through the web clipboard API", async () => {
        const writeText = vi.fn().mockResolvedValue(undefined);
        const service = new ShareService({
            isNativePlatform: () => false,
            navigator: { clipboard: { writeText } },
        });
        await service.copy("copy me");
        expect(writeText).toHaveBeenCalledWith("copy me");
    });
    it("wraps sharing failures", async () => {
        const service = new ShareService({
            isNativePlatform: () => false,
            navigator: {
                share: vi.fn().mockRejectedValue(new Error("denied")),
            },
        });
        await expect(service.share("Invite")).rejects.toThrow("denied");
    });
});
