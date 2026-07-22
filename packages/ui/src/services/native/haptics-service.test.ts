import { describe, expect, it, vi } from "vitest";
import { HapticsService } from "./haptics-service.ts";

describe("HapticsService", () => {
  it("vibrates with the success pattern", async () => {
    const vibrate = vi.fn().mockResolvedValue(undefined);
    const service = new HapticsService({
      haptics: { vibrate },
      isNativePlatform: () => true,
    });

    await service.vibrate("success");

    expect(vibrate).toHaveBeenCalledTimes(1);
    expect(vibrate).toHaveBeenCalledWith({ duration: 50 });
  });

  it("vibrates with the error pattern", async () => {
    const vibrate = vi.fn().mockResolvedValue(undefined);
    const service = new HapticsService({
      haptics: { vibrate },
      isNativePlatform: () => true,
    });

    await service.vibrate("error");

    expect(vibrate).toHaveBeenCalledTimes(3);
    expect(vibrate).toHaveBeenNthCalledWith(1, { duration: 20 });
    expect(vibrate).toHaveBeenNthCalledWith(2, { duration: 20 });
    expect(vibrate).toHaveBeenNthCalledWith(3, { duration: 20 });
  });

  it("vibrates with the warning pattern", async () => {
    const vibrate = vi.fn().mockResolvedValue(undefined);
    const service = new HapticsService({
      haptics: { vibrate },
      isNativePlatform: () => true,
    });

    await service.vibrate("warning");

    expect(vibrate).toHaveBeenCalledWith({ duration: 100 });
  });

  it("vibrates with the light pattern", async () => {
    const vibrate = vi.fn().mockResolvedValue(undefined);
    const service = new HapticsService({
      haptics: { vibrate },
      isNativePlatform: () => true,
    });

    await service.vibrate("light");

    expect(vibrate).toHaveBeenCalledWith({ duration: 10 });
  });

  it("becomes a no-op when native haptics are unavailable", async () => {
    const vibrate = vi.fn().mockResolvedValue(undefined);
    const service = new HapticsService({
      haptics: { vibrate },
      isNativePlatform: () => false,
    });

    await service.vibrate("success");

    expect(service.isAvailable()).toBe(false);
    expect(vibrate).not.toHaveBeenCalled();
  });

  it("wraps native plugin failures", async () => {
    const service = new HapticsService({
      haptics: {
        vibrate: vi.fn().mockRejectedValue(new Error("boom")),
      },
      isNativePlatform: () => true,
    });

    await expect(service.vibrate("success")).rejects.toThrow("boom");
  });
});
