import { type LoggerLike, type PluginListenerHandleLike } from "./native-platform.js";
export interface JoinLinkMatch {
    readonly roomCode: string;
    readonly url: string;
}
export type DeepLinkReceivedCallback = (link: JoinLinkMatch) => void;
export interface IDeepLinkService {
    handleDeepLink(url: string): Promise<void>;
    onDeepLinkReceived(callback: DeepLinkReceivedCallback): void;
    parseJoinLink(url: string): JoinLinkMatch | null;
}
export interface AppPluginLike {
    addListener: (eventName: "appUrlOpen", listener: (data: {
        readonly url: string;
    }) => void) => Promise<PluginListenerHandleLike>;
    getLaunchUrl: () => Promise<{
        readonly url?: string;
    } | undefined>;
}
export interface DeepLinkWindowLike {
    readonly location: Location;
    addEventListener: (event: "popstate", listener: () => void) => void;
}
interface DeepLinkServiceDependencies {
    readonly app: AppPluginLike;
    readonly isNativePlatform: () => boolean;
    readonly window: DeepLinkWindowLike | null;
    readonly logger: LoggerLike;
}
export declare class DeepLinkService implements IDeepLinkService {
    private readonly callbacks;
    private appListenerStarted;
    private webListenerStarted;
    private readonly dependencies;
    constructor(dependencies?: Partial<DeepLinkServiceDependencies>);
    parseJoinLink(url: string): JoinLinkMatch | null;
    handleDeepLink(url: string): Promise<void>;
    onDeepLinkReceived(callback: DeepLinkReceivedCallback): void;
    private startNativeListeners;
    private startWebListeners;
}
export declare const deepLinkService: DeepLinkService;
export {};
//# sourceMappingURL=deep-link-service.d.ts.map