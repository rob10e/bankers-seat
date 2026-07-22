export interface LoggerLike {
    info?: (...args: readonly unknown[]) => void;
    warn?: (...args: readonly unknown[]) => void;
    error?: (...args: readonly unknown[]) => void;
}
export interface PluginListenerHandleLike {
    remove: () => Promise<void> | void;
}
export declare function isNativePlatform(): boolean;
export declare function toError(error: unknown, fallbackMessage: string): Error;
export declare function normalizeRoomCode(value: string): string;
//# sourceMappingURL=native-platform.d.ts.map