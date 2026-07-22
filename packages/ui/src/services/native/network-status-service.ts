import { Network } from "@capacitor/network";
import {
  type LoggerLike,
  type PluginListenerHandleLike,
  isNativePlatform,
} from "./native-platform.js";

export type NetworkStatusChangeCallback = (isOnline: boolean) => void;

export interface INetworkStatusService {
  isOnline(): boolean;
  onStatusChange(callback: NetworkStatusChangeCallback): () => void;
}

export interface NetworkPluginLike {
  getStatus: () => Promise<{ readonly connected: boolean }>;
  addListener: (
    eventName: "networkStatusChange",
    listener: (status: { readonly connected: boolean }) => void,
  ) => Promise<PluginListenerHandleLike>;
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

export class NetworkStatusService implements INetworkStatusService {
  private readonly callbacks = new Set<NetworkStatusChangeCallback>();
  private readonly dependencies: NetworkStatusServiceDependencies;
  private online: boolean;
  private nativeListenerStarted = false;
  private webListenersStarted = false;

  public constructor(dependencies?: Partial<NetworkStatusServiceDependencies>) {
    this.dependencies = {
      network: dependencies?.network ?? Network,
      isNativePlatform: dependencies?.isNativePlatform ?? isNativePlatform,
      window: dependencies?.window ?? (typeof window !== "undefined" ? window : null),
      logger: dependencies?.logger ?? console,
    };
    this.online = this.dependencies.window?.navigator.onLine ?? true;

    if (this.dependencies.isNativePlatform()) {
      void this.dependencies.network
        .getStatus()
        .then((status) => {
          this.setOnline(status.connected);
        })
        .catch((error) => {
          this.dependencies.logger.error?.("Failed to read native network status.", error);
        });
    }
  }

  public isOnline(): boolean {
    return this.online;
  }

  public onStatusChange(callback: NetworkStatusChangeCallback): () => void {
    this.callbacks.add(callback);

    if (this.dependencies.isNativePlatform()) {
      this.startNativeListeners();
    } else {
      this.startWebListeners();
    }

    try {
      callback(this.online);
    } catch (error) {
      this.dependencies.logger.error?.("Network status callback failed.", error);
    }

    return () => {
      this.callbacks.delete(callback);
    };
  }

  private startNativeListeners(): void {
    if (this.nativeListenerStarted) {
      return;
    }

    this.nativeListenerStarted = true;
    void this.dependencies.network
      .addListener("networkStatusChange", (status) => {
        this.setOnline(status.connected);
      })
      .catch((error) => {
        this.dependencies.logger.error?.("Failed to subscribe to native network changes.", error);
      });
  }

  private startWebListeners(): void {
    if (this.webListenersStarted || !this.dependencies.window) {
      return;
    }

    this.webListenersStarted = true;
    this.dependencies.window.addEventListener("online", () => {
      this.setOnline(true);
    });
    this.dependencies.window.addEventListener("offline", () => {
      this.setOnline(false);
    });
  }

  private setOnline(isOnline: boolean): void {
    this.online = isOnline;

    for (const callback of this.callbacks) {
      try {
        callback(isOnline);
      } catch (error) {
        this.dependencies.logger.error?.("Network status callback failed.", error);
      }
    }
  }
}

export const networkStatusService = new NetworkStatusService();
