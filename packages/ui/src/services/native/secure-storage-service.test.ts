import { beforeEach, describe, expect, it, vi } from "vitest";
import { SecureStorageService } from "./secure-storage-service.ts";

function createMemoryStorage() {
  const backingStore = new Map<string, string>();

  return {
    getItem(key: string) {
      return backingStore.get(key) ?? null;
    },
    setItem(key: string, value: string) {
      backingStore.set(key, value);
    },
    removeItem(key: string) {
      backingStore.delete(key);
    },
    clear() {
      backingStore.clear();
    },
  };
}

describe("SecureStorageService", () => {
  const storage = createMemoryStorage();

  beforeEach(() => {
    storage.clear();
  });

  it("stores and retrieves web credentials with localStorage", async () => {
    const service = new SecureStorageService({
      isNativePlatform: () => false,
      storage,
    });

    await service.setCredential("reconnect", "secret");

    await expect(service.getCredential("reconnect")).resolves.toBe("secret");
  });

  it("clears a single credential without touching others", async () => {
    const service = new SecureStorageService({
      isNativePlatform: () => false,
      storage,
    });

    await service.setCredential("a", "1");
    await service.setCredential("b", "2");
    await service.clearCredential("a");

    await expect(service.getCredential("a")).resolves.toBeNull();
    await expect(service.getCredential("b")).resolves.toBe("2");
  });

  it("clears only managed credentials from web storage", async () => {
    const service = new SecureStorageService({
      isNativePlatform: () => false,
      storage,
    });
    storage.setItem("untouched", "value");

    await service.setCredential("a", "1");
    await service.setCredential("b", "2");
    await service.clearAllCredentials();

    expect(storage.getItem("untouched")).toBe("value");
    await expect(service.getCredential("a")).resolves.toBeNull();
    await expect(service.getCredential("b")).resolves.toBeNull();
  });

  it("uses native preferences with secure configuration", async () => {
    const configure = vi.fn().mockResolvedValue(undefined);
    const set = vi.fn().mockResolvedValue(undefined);
    const get = vi.fn()
      .mockResolvedValueOnce({ value: null })
      .mockResolvedValueOnce({ value: "secret" });
    const remove = vi.fn().mockResolvedValue(undefined);
    const service = new SecureStorageService({
      isNativePlatform: () => true,
      preferences: {
        configure,
        set,
        get,
        remove,
      },
    });

    await service.setCredential("reconnect", "secret");
    const value = await service.getCredential("reconnect");

    expect(configure).toHaveBeenCalledWith({
      group: "bankers-seat-secure-storage",
      secure: true,
    });
    expect(set).toHaveBeenCalledWith({
      key: "bankers-seat:secure:reconnect",
      value: "secret",
    });
    expect(set).toHaveBeenCalledWith({
      key: "bankers-seat:secure:index",
      value: "[\"bankers-seat:secure:reconnect\"]",
    });
    expect(value).toBe("secret");
  });

  it("returns null when a credential is missing", async () => {
    const service = new SecureStorageService({
      isNativePlatform: () => false,
      storage,
    });

    await expect(service.getCredential("missing")).resolves.toBeNull();
  });

  it("wraps persistence errors with a stable error", async () => {
    const service = new SecureStorageService({
      isNativePlatform: () => false,
      storage: {
        getItem: vi.fn(() => null),
        setItem: vi.fn(() => {
          throw new Error("storage unavailable");
        }),
        removeItem: vi.fn(),
      },
    });

    await expect(service.setCredential("reconnect", "secret")).rejects.toThrow(
      "storage unavailable",
    );
  });
});
