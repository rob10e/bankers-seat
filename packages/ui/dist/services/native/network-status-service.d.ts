import { type LoggerLike, type PluginListenerHandleLike } from "./native-platform.js";
export type NetworkStatusChangeCallback = (isOnline: boolean) => void;
export interface INetworkStatusService {
    isOnline(): boolean;
    onStatusChange(callback: NetworkStatusChangeCallback): () => void;
}
export interface NetworkPluginLike {
    getStatus: () => Promise<{
        readonly connected: boolean;
    }>;
    addListener: (eventName: "networkStatusChange", listener: (status: {
        readonly connected: boolean;
    }) => void) => Promise<PluginListenerHandleLike>;
}
export interface NetworkWindowLike {
    readonly navigator: {
        readonly onLine: boolean;
    };
    addEventListener: (eventName: "online" | "offline", listener: () => void) => void;
}
interface NetworkStatusServiceDependencies {
    readonly network: NetworkPluginLike;
    readonly isNativePlatform: () => boolean;
    readonly window: NetworkWindowLike | null;
    readonly logger: LoggerLike;
}
export declare class NetworkStatusService implements INetworkStatusService {
    private readonly callbacks;
    private readonly dependencies;
    private online;
    private nativeListenerStarted;
    private webListenersStarted;
    constructor(dependencies?: Partial<NetworkStatusServiceDependencies>);
    isOnline(): boolean;
    onStatusChange(callback: NetworkStatusChangeCallback): () => void;
    private startNativeListeners;
    private startWebListeners;
    private setOnline;
}
export declare const networkStatusService: NetworkStatusService;
export {};
//# sourceMappingURL=network-status-service.d.ts.map