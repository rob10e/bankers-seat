import { Capacitor } from "@capacitor/core";

export interface LoggerLike {
  info?: (...args: readonly unknown[]) => void;
  warn?: (...args: readonly unknown[]) => void;
  error?: (...args: readonly unknown[]) => void;
}

export interface PluginListenerHandleLike {
  remove: () => Promise<void> | void;
}

export function isNativePlatform(): boolean {
  return Capacitor.isNativePlatform();
}

export function toError(error: unknown, fallbackMessage: string): Error {
  return error instanceof Error ? error : new Error(fallbackMessage);
}

export function normalizeRoomCode(value: string): string {
  return value.replace(/[^a-z0-9]/gi, "").toUpperCase();
}
