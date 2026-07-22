import { App } from "@capacitor/app";
import {
  type LoggerLike,
  type PluginListenerHandleLike,
  isNativePlatform,
  normalizeRoomCode,
  toError,
} from "./native-platform.js";

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
  addListener: (
    eventName: "appUrlOpen",
    listener: (data: { readonly url: string }) => void,
  ) => Promise<PluginListenerHandleLike>;
  getLaunchUrl: () => Promise<{ readonly url?: string } | undefined>;
}

export interface DeepLinkWindowLike {
  readonly location: Location;
  addEventListener: (
    event: "popstate",
    listener: () => void,
  ) => void;
}

interface DeepLinkServiceDependencies {
  readonly app: AppPluginLike;
  readonly isNativePlatform: () => boolean;
  readonly window: DeepLinkWindowLike | null;
  readonly logger: LoggerLike;
}

export class DeepLinkService implements IDeepLinkService {
  private readonly callbacks = new Set<DeepLinkReceivedCallback>();
  private appListenerStarted = false;
  private webListenerStarted = false;
  private readonly dependencies: DeepLinkServiceDependencies;

  public constructor(dependencies?: Partial<DeepLinkServiceDependencies>) {
    this.dependencies = {
      app: dependencies?.app ?? App,
      isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
      window: dependencies?.window ?? (typeof window !== "undefined" ? window : null),
      logger: dependencies?.logger ?? console,
    };
  }

  public parseJoinLink(url: string): JoinLinkMatch | null {
    try {
      const baseUrl = this.dependencies.window?.location.origin ?? "https://bankers-seat.app";
      const parsedUrl = new URL(url, baseUrl);
      const roomCodeMatch = /^\/join\/([a-z0-9-]+)$/i.exec(parsedUrl.pathname);
      if (!roomCodeMatch) {
        return null;
      }

      const roomCode = normalizeRoomCode(roomCodeMatch[1]);
      return roomCode.length >= 4 ? { roomCode, url: parsedUrl.toString() } : null;
    } catch {
      return null;
    }
  }

  public async handleDeepLink(url: string): Promise<void> {
    try {
      const parsedLink = this.parseJoinLink(url);
      if (!parsedLink) {
        return;
      }

      for (const callback of this.callbacks) {
        try {
          callback(parsedLink);
        } catch (error) {
          this.dependencies.logger.error?.("Deep link callback failed.", error);
        }
      }
    } catch (error) {
      throw toError(error, "Failed to handle deep link.");
    }
  }

  public onDeepLinkReceived(callback: DeepLinkReceivedCallback): void {
    this.callbacks.add(callback);

    if (this.dependencies.isNativePlatform()) {
      this.startNativeListeners();
      return;
    }

    this.startWebListeners();
  }

  private startNativeListeners(): void {
    if (this.appListenerStarted) {
      return;
    }

    this.appListenerStarted = true;
    void this.dependencies.app
      .addListener("appUrlOpen", (data) => {
        void this.handleDeepLink(data.url);
      })
      .catch((error) => {
        this.dependencies.logger.error?.("Failed to subscribe to native deep links.", error);
      });

    void this.dependencies.app
      .getLaunchUrl()
      .then((launchUrl) => {
        if (launchUrl?.url) {
          return this.handleDeepLink(launchUrl.url);
        }

        return undefined;
      })
      .catch((error) => {
        this.dependencies.logger.error?.("Failed to process launch deep link.", error);
      });
  }

  private startWebListeners(): void {
    if (this.webListenerStarted || !this.dependencies.window) {
      return;
    }

    this.webListenerStarted = true;
    void this.handleDeepLink(this.dependencies.window.location.href);
    this.dependencies.window.addEventListener("popstate", () => {
      void this.handleDeepLink(this.dependencies.window?.location.href ?? "");
    });
  }
}

export const deepLinkService = new DeepLinkService();
