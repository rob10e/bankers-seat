import { describe, expect, it, vi } from "vitest";
import { DeepLinkService } from "./deep-link-service.ts";
describe("DeepLinkService", () => {
    it("parses a valid join link", () => {
        const service = new DeepLinkService({
            isNativePlatform: () => false,
            window,
        });
        expect(service.parseJoinLink("https://example.com/join/ABCD12")).toEqual({
            roomCode: "ABCD12",
            url: "https://example.com/join/ABCD12",
        });
    });
    it("returns null for invalid join links", () => {
        const service = new DeepLinkService({
            isNativePlatform: () => false,
            window,
        });
        expect(service.parseJoinLink("https://example.com/game/ABCD12")).toBeNull();
    });
    it("notifies callbacks when handling a valid deep link", async () => {
        const callback = vi.fn();
        const service = new DeepLinkService({
            isNativePlatform: () => false,
            window,
        });
        service.onDeepLinkReceived(callback);
        await service.handleDeepLink("https://example.com/join/ABCD12");
        expect(callback).toHaveBeenCalledWith({
            roomCode: "ABCD12",
            url: "https://example.com/join/ABCD12",
        });
    });
    it("ignores invalid deep links", async () => {
        const callback = vi.fn();
        const service = new DeepLinkService({
            isNativePlatform: () => false,
            window,
        });
        service.onDeepLinkReceived(callback);
        await service.handleDeepLink("notaurl");
        expect(callback).not.toHaveBeenCalled();
    });
    it("handles the current web location and popstate events", async () => {
        const callback = vi.fn();
        const addEventListener = vi.fn((_event, listener) => {
            listener();
        });
        const service = new DeepLinkService({
            isNativePlatform: () => false,
            window: {
                location: new URL("https://example.com/join/ROOM42"),
                addEventListener,
            },
        });
        service.onDeepLinkReceived(callback);
        expect(addEventListener).toHaveBeenCalledWith("popstate", expect.any(Function));
        expect(callback).toHaveBeenCalledWith({
            roomCode: "ROOM42",
            url: "https://example.com/join/ROOM42",
        });
    });
    it("subscribes to native appUrlOpen events", async () => {
        const listenerSpy = vi.fn();
        const service = new DeepLinkService({
            isNativePlatform: () => true,
            app: {
                addListener: vi.fn().mockImplementation(async (_event, listener) => {
                    listenerSpy.mockImplementation(listener);
                    return { remove: vi.fn() };
                }),
                getLaunchUrl: vi.fn().mockResolvedValue(undefined),
            },
        });
        const callback = vi.fn();
        service.onDeepLinkReceived(callback);
        listenerSpy({ url: "https://example.com/join/ABCD12" });
        await Promise.resolve();
        expect(callback).toHaveBeenCalledWith({
            roomCode: "ABCD12",
            url: "https://example.com/join/ABCD12",
        });
    });
    it("processes native launch urls on registration", async () => {
        const callback = vi.fn();
        const service = new DeepLinkService({
            isNativePlatform: () => true,
            app: {
                addListener: vi.fn().mockResolvedValue({ remove: vi.fn() }),
                getLaunchUrl: vi
                    .fn()
                    .mockResolvedValue({ url: "https://example.com/join/ABCD12" }),
            },
        });
        service.onDeepLinkReceived(callback);
        await Promise.resolve();
        expect(callback).toHaveBeenCalledWith({
            roomCode: "ABCD12",
            url: "https://example.com/join/ABCD12",
        });
    });
    it("logs callback errors without aborting deep link handling", async () => {
        const logger = {
            error: vi.fn(),
        };
        const callback = vi.fn().mockImplementation(() => {
            throw new Error("boom");
        });
        const service = new DeepLinkService({
            isNativePlatform: () => false,
            window,
            logger,
        });
        service.onDeepLinkReceived(callback);
        await expect(service.handleDeepLink("https://example.com/join/ABCD12")).resolves.toBeUndefined();
        expect(logger.error).toHaveBeenCalled();
    });
});
