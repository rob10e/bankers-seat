import { Clipboard } from "@capacitor/clipboard";
import { Share, type ShareOptions } from "@capacitor/share";
import { isNativePlatform, toError } from "./native-platform.js";

export type ShareFileDescriptor = string;

export interface IShareService {
  share(
    title?: string,
    text?: string,
    url?: string,
    files?: readonly ShareFileDescriptor[],
  ): Promise<void>;
  canShare(): boolean;
  copy(text: string): Promise<void>;
}

export interface SharePluginLike {
  share: (options: ShareOptions) => Promise<unknown>;
  canShare?: () => Promise<{ readonly value: boolean }>;
}

export interface ClipboardPluginLike {
  write: (options: { readonly string: string }) => Promise<void>;
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

function buildCopyPayload(title?: string, text?: string, url?: string): string {
  return [title, text, url].filter(Boolean).join("\n");
}

export class ShareService implements IShareService {
  private readonly dependencies: ShareServiceDependencies;

  public constructor(dependencies?: Partial<ShareServiceDependencies>) {
    this.dependencies = {
      sharePlugin: dependencies?.sharePlugin ?? Share,
      clipboardPlugin: dependencies?.clipboardPlugin ?? Clipboard,
      navigator:
        dependencies?.navigator ?? (typeof window !== "undefined" ? window.navigator : null),
      isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
    };
  }

  public canShare(): boolean {
    if (this.dependencies.isNativePlatform()) {
      return true;
    }

    return Boolean(
      this.dependencies.navigator?.share || this.dependencies.navigator?.clipboard,
    );
  }

  public async share(
    title?: string,
    text?: string,
    url?: string,
    files?: readonly ShareFileDescriptor[],
  ): Promise<void> {
    try {
      if (this.dependencies.isNativePlatform()) {
        await this.dependencies.sharePlugin.share({
          title,
          text,
          url,
          files: files ? [...files] : undefined,
        });
        return;
      }

      if (this.dependencies.navigator?.share) {
        await this.dependencies.navigator.share({ title, text, url });
        return;
      }

      await this.copy(buildCopyPayload(title, text, url));
    } catch (error) {
      throw toError(error, "Failed to share content.");
    }
  }

  public async copy(text: string): Promise<void> {
    try {
      if (this.dependencies.isNativePlatform()) {
        await this.dependencies.clipboardPlugin.write({ string: text });
        return;
      }

      if (!this.dependencies.navigator?.clipboard) {
        throw new Error("Clipboard access is unavailable.");
      }

      await this.dependencies.navigator.clipboard.writeText(text);
    } catch (error) {
      throw toError(error, "Failed to copy content.");
    }
  }
}

export const shareService = new ShareService();
