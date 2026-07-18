import { create } from "zustand";
import { persist } from "zustand/middleware";

export type ThemeModePreference = "system" | "light" | "dark";

interface AppSettingsState {
  readonly themeMode: ThemeModePreference;
  readonly snapshotRefreshMs: number;
  setThemeMode: (value: ThemeModePreference) => void;
  setSnapshotRefreshMs: (value: number) => void;
}

export const useAppSettingsStore = create<AppSettingsState>()(
  persist(
    (set) => ({
      themeMode: "system",
      snapshotRefreshMs: 3000,
      setThemeMode: (value) => set({ themeMode: value }),
      setSnapshotRefreshMs: (value) => set({ snapshotRefreshMs: value }),
    }),
    {
      name: "bankers-seat-app-settings",
      version: 1,
    },
  ),
);
