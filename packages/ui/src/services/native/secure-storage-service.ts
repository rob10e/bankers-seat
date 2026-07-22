import { Preferences } from "@capacitor/preferences";
import { isNativePlatform, toError } from "./native-platform.js";

const secureStoragePrefix = "bankers-seat:secure:";
const secureStorageIndexKey = `${secureStoragePrefix}index`;

export interface ISecureStorageService {
  setCredential(key: string, value: string): Promise<void>;
  getCredential(key: string): Promise<string | null>;
  clearCredential(key: string): Promise<void>;
  clearAllCredentials(): Promise<void>;
}

export interface PreferencesPluginLike {
  configure?: (options: { readonly group?: string; readonly secure?: boolean }) => Promise<void>;
  set: (options: { readonly key: string; readonly value: string }) => Promise<void>;
  get: (options: { readonly key: string }) => Promise<{ readonly value: string | null }>;
  remove: (options: { readonly key: string }) => Promise<void>;
}

export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

interface SecureStorageServiceDependencies {
  readonly preferences: PreferencesPluginLike;
  readonly storage: StorageLike | null;
  readonly isNativePlatform: () => boolean;
}

function getScopedKey(key: string): string {
  return `${secureStoragePrefix}${key}`;
}

function validateStorageKey(key: string): void {
  if (key.trim().length === 0) {
    throw new Error("Secure storage keys must not be empty.");
  }
}

function parseKeyIndex(value: string | null): string[] {
  if (!value) {
    return [];
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    return Array.isArray(parsed)
      ? parsed.filter((entry): entry is string => typeof entry === "string")
      : [];
  } catch {
    return [];
  }
}

export class SecureStorageService implements ISecureStorageService {
  private readonly dependencies: SecureStorageServiceDependencies;

  public constructor(dependencies?: Partial<SecureStorageServiceDependencies>) {
    this.dependencies = {
      preferences: dependencies?.preferences ?? Preferences,
      storage:
        dependencies?.storage ??
        (typeof window !== "undefined" ? window.localStorage : null),
      isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
    };
  }

  public async setCredential(key: string, value: string): Promise<void> {
    validateStorageKey(key);

    try {
      const scopedKey = getScopedKey(key);
      if (this.dependencies.isNativePlatform()) {
        await this.dependencies.preferences.configure?.({
          group: "bankers-seat-secure-storage",
          secure: true,
        });
        await this.dependencies.preferences.set({ key: scopedKey, value });
        await this.persistIndexAsync(scopedKey, true);
        return;
      }

      this.requireStorage().setItem(scopedKey, value);
      this.persistIndexSync(scopedKey, true);
    } catch (error) {
      throw toError(error, "Failed to persist secure credential.");
    }
  }

  public async getCredential(key: string): Promise<string | null> {
    validateStorageKey(key);

    try {
      const scopedKey = getScopedKey(key);
      if (this.dependencies.isNativePlatform()) {
        const result = await this.dependencies.preferences.get({ key: scopedKey });
        return result.value;
      }

      return this.requireStorage().getItem(scopedKey);
    } catch (error) {
      throw toError(error, "Failed to read secure credential.");
    }
  }

  public async clearCredential(key: string): Promise<void> {
    validateStorageKey(key);

    try {
      const scopedKey = getScopedKey(key);
      if (this.dependencies.isNativePlatform()) {
        await this.dependencies.preferences.remove({ key: scopedKey });
        await this.persistIndexAsync(scopedKey, false);
        return;
      }

      this.requireStorage().removeItem(scopedKey);
      this.persistIndexSync(scopedKey, false);
    } catch (error) {
      throw toError(error, "Failed to clear secure credential.");
    }
  }

  public async clearAllCredentials(): Promise<void> {
    try {
      if (this.dependencies.isNativePlatform()) {
        const keys = await this.readIndexAsync();
        await Promise.all(
          keys.map((key) => this.dependencies.preferences.remove({ key })),
        );
        await this.dependencies.preferences.remove({ key: secureStorageIndexKey });
        return;
      }

      const storage = this.requireStorage();
      for (const key of this.readIndexSync()) {
        storage.removeItem(key);
      }
      storage.removeItem(secureStorageIndexKey);
    } catch (error) {
      throw toError(error, "Failed to clear secure credentials.");
    }
  }

  private requireStorage(): StorageLike {
    if (!this.dependencies.storage) {
      throw new Error("Web storage is not available in the current environment.");
    }

    return this.dependencies.storage;
  }

  private readIndexSync(): string[] {
    return parseKeyIndex(this.requireStorage().getItem(secureStorageIndexKey));
  }

  private persistIndexSync(key: string, include: boolean): void {
    const currentKeys = new Set(this.readIndexSync());
    if (include) {
      currentKeys.add(key);
    } else {
      currentKeys.delete(key);
    }

    this.requireStorage().setItem(
      secureStorageIndexKey,
      JSON.stringify([...currentKeys]),
    );
  }

  private async readIndexAsync(): Promise<string[]> {
    const result = await this.dependencies.preferences.get({ key: secureStorageIndexKey });
    return parseKeyIndex(result.value);
  }

  private async persistIndexAsync(key: string, include: boolean): Promise<void> {
    const currentKeys = new Set(await this.readIndexAsync());
    if (include) {
      currentKeys.add(key);
    } else {
      currentKeys.delete(key);
    }

    await this.dependencies.preferences.set({
      key: secureStorageIndexKey,
      value: JSON.stringify([...currentKeys]),
    });
  }
}

export const secureStorageService = new SecureStorageService();
