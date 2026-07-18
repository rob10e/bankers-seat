import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { CssBaseline, ThemeProvider, useMediaQuery } from "@mui/material";
import { useMemo } from "react";
import { BrowserRouter } from "react-router-dom";
import App from "../App.tsx";
import { useAppSettingsStore } from "./app-settings-store.ts";
import { createAppTheme } from "./app-theme.ts";

const queryClient = new QueryClient();

export function AppProviders() {
  const themeMode = useAppSettingsStore((state) => state.themeMode);
  const prefersDarkMode = useMediaQuery("(prefers-color-scheme: dark)", {
    noSsr: true,
  });
  const resolvedMode =
    themeMode === "system" ? (prefersDarkMode ? "dark" : "light") : themeMode;
  const appTheme = useMemo(() => createAppTheme(resolvedMode), [resolvedMode]);

  return (
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
