import { Preferences } from "@capacitor/preferences";
import { isNativePlatform, toError } from "./native-platform.js";
const secureStoragePrefix = "bankers-seat:secure:";
const secureStorageIndexKey = `${secureStoragePrefix}index`;
function getScopedKey(key) {
    return `${secureStoragePrefix}${key}`;
}
function validateStorageKey(key) {
    if (key.trim().length === 0) {
        throw new Error("Secure storage keys must not be empty.");
    }
}
function parseKeyIndex(value) {
    if (!value) {
        return [];
    }
    try {
        const parsed = JSON.parse(value);
        return Array.isArray(parsed)
            ? parsed.filter((entry) => typeof entry === "string")
            : [];
    }
    catch {
        return [];
    }
}
export class SecureStorageService {
    dependencies;
    constructor(dependencies) {
        this.dependencies = {
            preferences: dependencies?.preferences ?? Preferences,
            storage: dependencies?.storage ??
                (typeof window !== "undefined" ? window.localStorage : null),
            isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
        };
    }
    async setCredential(key, value) {
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
        }
        catch (error) {
            throw toError(error, "Failed to persist secure credential.");
        }
    }
    async getCredential(key) {
        validateStorageKey(key);
        try {
            const scopedKey = getScopedKey(key);
            if (this.dependencies.isNativePlatform()) {
                const result = await this.dependencies.preferences.get({ key: scopedKey });
                return result.value;
            }
            return this.requireStorage().getItem(scopedKey);
        }
        catch (error) {
            throw toError(error, "Failed to read secure credential.");
        }
    }
    async clearCredential(key) {
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
        }
        catch (error) {
            throw toError(error, "Failed to clear secure credential.");
        }
    }
    async clearAllCredentials() {
        try {
            if (this.dependencies.isNativePlatform()) {
                const keys = await this.readIndexAsync();
                await Promise.all(keys.map((key) => this.dependencies.preferences.remove({ key })));
                await this.dependencies.preferences.remove({ key: secureStorageIndexKey });
                return;
            }
            const storage = this.requireStorage();
            for (const key of this.readIndexSync()) {
                storage.removeItem(key);
            }
            storage.removeItem(secureStorageIndexKey);
        }
        catch (error) {
            throw toError(error, "Failed to clear secure credentials.");
        }
    }
    requireStorage() {
        if (!this.dependencies.storage) {
            throw new Error("Web storage is not available in the current environment.");
        }
        return this.dependencies.storage;
    }
    readIndexSync() {
        return parseKeyIndex(this.requireStorage().getItem(secureStorageIndexKey));
    }
    persistIndexSync(key, include) {
        const currentKeys = new Set(this.readIndexSync());
        if (include) {
            currentKeys.add(key);
        }
        else {
            currentKeys.delete(key);
        }
        this.requireStorage().setItem(secureStorageIndexKey, JSON.stringify([...currentKeys]));
    }
    async readIndexAsync() {
        const result = await this.dependencies.preferences.get({ key: secureStorageIndexKey });
        return parseKeyIndex(result.value);
    }
    async persistIndexAsync(key, include) {
        const currentKeys = new Set(await this.readIndexAsync());
        if (include) {
            currentKeys.add(key);
        }
        else {
            currentKeys.delete(key);
        }
        await this.dependencies.preferences.set({
            key: secureStorageIndexKey,
            value: JSON.stringify([...currentKeys]),
        });
    }
}
export const secureStorageService = new SecureStorageService();
