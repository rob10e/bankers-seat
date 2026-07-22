import { describe, expect, it, vi } from "vitest";
import { NetworkStatusService } from "./network-status-service.ts";

describe("NetworkStatusService", () => {
  it("reflects the current web online state", () => {
    const service = new NetworkStatusService({
      isNativePlatform: () => false,
      window: {
        navigator: { onLine: true },
        addEventListener: vi.fn(),
      },
    });

    expect(service.isOnline()).toBe(true);
  });

  it("subscribes to web online and offline events", () => {
    const listeners = new Map<string, () => void>();
    const callback = vi.fn();
    const service = new NetworkStatusService({
      isNativePlatform: () => false,
      window: {
        navigator: { onLine: false },
        addEventListener: vi.fn((eventName: "online" | "offline", listener: () => void) => {
          listeners.set(eventName, listener);
        }),
      },
    });

    const unsubscribe = service.onStatusChange(callback);
    listeners.get("online")?.();
    unsubscribe();
    listeners.get("offline")?.();

    expect(callback).toHaveBeenNthCalledWith(1, false);
    expect(callback).toHaveBeenNthCalledWith(2, true);
    expect(callback).toHaveBeenCalledTimes(2);
  });

  it("initializes from native network status", async () => {
    const service = new NetworkStatusService({
      isNativePlatform: () => true,
      network: {
        getStatus: vi.fn().mockResolvedValue({ connected: false }),
        addListener: vi.fn().mockResolvedValue({ remove: vi.fn() }),
      },
    });

    await Promise.resolve();

    expect(service.isOnline()).toBe(false);
  });

  it("updates subscribers when native network status changes", async () => {
    let nativeListener: ((status: { readonly connected: boolean }) => void) | undefined;
    const callback = vi.fn();
    const service = new NetworkStatusService({
      isNativePlatform: () => true,
      network: {
        getStatus: vi.fn().mockResolvedValue({ connected: true }),
        addListener: vi.fn().mockImplementation(async (_event, listener) => {
          nativeListener = listener;
          return { remove: vi.fn() };
        }),
      },
    });

    service.onStatusChange(callback);
    nativeListener?.({ connected: false });

    expect(callback).toHaveBeenLastCalledWith(false);
    expect(service.isOnline()).toBe(false);
  });

  it("logs subscriber failures and keeps notifying others", () => {
    const logger = { error: vi.fn() };
    const workingCallback = vi.fn();
    const service = new NetworkStatusService({
      isNativePlatform: () => false,
      logger,
      window: {
        navigator: { onLine: true },
        addEventListener: vi.fn((eventName: "online" | "offline", listener: () => void) => {
          if (eventName === "offline") {
            listener();
          }
        }),
      },
    });

    service.onStatusChange(() => {
      throw new Error("bad callback");
    });
    service.onStatusChange(workingCallback);

    expect(logger.error).toHaveBeenCalled();
    expect(workingCallback).toHaveBeenLastCalledWith(false);
  });
});
