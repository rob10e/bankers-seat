export interface ISecureStorageService {
    setCredential(key: string, value: string): Promise<void>;
    getCredential(key: string): Promise<string | null>;
    clearCredential(key: string): Promise<void>;
    clearAllCredentials(): Promise<void>;
}
export interface PreferencesPluginLike {
    configure?: (options: {
        readonly group?: string;
        readonly secure?: boolean;
    }) => Promise<void>;
    set: (options: {
        readonly key: string;
        readonly value: string;
    }) => Promise<void>;
    get: (options: {
        readonly key: string;
    }) => Promise<{
        readonly value: string | null;
    }>;
    remove: (options: {
        readonly key: string;
    }) => Promise<void>;
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
export declare class SecureStorageService implements ISecureStorageService {
    private readonly dependencies;
    constructor(dependencies?: Partial<SecureStorageServiceDependencies>);
    setCredential(key: string, value: string): Promise<void>;
    getCredential(key: string): Promise<string | null>;
    clearCredential(key: string): Promise<void>;
    clearAllCredentials(): Promise<void>;
    private requireStorage;
    private readIndexSync;
    private persistIndexSync;
    private readIndexAsync;
    private persistIndexAsync;
}
export declare const secureStorageService: SecureStorageService;
export {};
//# sourceMappingURL=secure-storage-service.d.ts.map