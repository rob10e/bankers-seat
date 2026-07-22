import { Capacitor } from "@capacitor/core";
export function isNativePlatform() {
    return Capacitor.isNativePlatform();
}
export function toError(error, fallbackMessage) {
    return error instanceof Error ? error : new Error(fallbackMessage);
}
export function normalizeRoomCode(value) {
    return value.replace(/[^a-z0-9]/gi, "").toUpperCase();
}
