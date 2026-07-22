import { type ShareOptions } from "@capacitor/share";
export type ShareFileDescriptor = string;
export interface IShareService {
    share(title?: string, text?: string, url?: string, files?: readonly ShareFileDescriptor[]): Promise<void>;
    canShare(): boolean;
    copy(text: string): Promise<void>;
}
export interface SharePluginLike {
    share: (options: ShareOptions) => Promise<unknown>;
    canShare?: () => Promise<{
        readonly value: boolean;
    }>;
}
export interface ClipboardPluginLike {
    write: (options: {
        readonly string: string;
    }) => Promise<void>;
}
export interface ClipboardApiLike {
    writeText: (value: string) => Promise<void>;
}
export interface NavigatorLike {
    share?: (data: {
        readonly title?: string;
        readonly text?: string;
        readonly url?: string;
    }) => Promise<void>;
    clipboard?: ClipboardApiLike;
}
interface ShareServiceDependencies {
    readonly sharePlugin: SharePluginLike;
    readonly clipboardPlugin: ClipboardPluginLike;
    readonly navigator: NavigatorLike | null;
    readonly isNativePlatform: () => boolean;
}
export declare class ShareService implements IShareService {
    private readonly dependencies;
    constructor(dependencies?: Partial<ShareServiceDependencies>);
    canShare(): boolean;
    share(title?: string, text?: string, url?: string, files?: readonly ShareFileDescriptor[]): Promise<void>;
    copy(text: string): Promise<void>;
}
export declare const shareService: ShareService;
export {};
//# sourceMappingURL=share-service.d.ts.map